using System;
using System.Collections;
using System.Diagnostics;

namespace JorisHoef.ObjectLoading
{
    public sealed class ObjectLoadingPipeline : IObjectLoadingPipeline
    {
        private readonly IObjectSourceResolver _sourceResolver;
        private readonly IObjectSourceContentLoader _contentLoader;
        private readonly IObjectInstantiator _instantiator;
        private readonly IObjectDiagnostics _diagnostics;
        private IObjectLoadHandle _lastHandle;

        public ObjectLoadingPipeline()
            : this(new DirectUrlSourceResolver(),
                   new SourceAssetBundleContentLoader(),
                   new AssetBundleObjectInstantiator(),
                   new DefaultObjectDiagnostics())
        {
        }

        public ObjectLoadingPipeline(IObjectSourceResolver sourceResolver,
                                    IObjectSourceContentLoader contentLoader,
                                    IObjectInstantiator instantiator,
                                    IObjectDiagnostics diagnostics)
        {
            _sourceResolver = sourceResolver ?? throw new ArgumentNullException(nameof(sourceResolver));
            _contentLoader = contentLoader ?? throw new ArgumentNullException(nameof(contentLoader));
            _instantiator = instantiator ?? throw new ArgumentNullException(nameof(instantiator));
            _diagnostics = diagnostics ?? new DefaultObjectDiagnostics();
        }

        public ObjectLoadingPipeline(IObjectSourceResolver sourceResolver,
                                    IObjectDownloader downloader,
                                    IObjectContentLoader contentLoader,
                                    IObjectInstantiator instantiator,
                                    IObjectDiagnostics diagnostics)
            : this(sourceResolver,
                   new ByteArrayObjectSourceContentLoader(downloader, contentLoader),
                   instantiator,
                   diagnostics)
        {
        }

        public IEnumerator LoadAsync(ObjectLoadRequest request, Action<ObjectLoadResult> onCompleted)
        {
            Stopwatch totalTimer = Stopwatch.StartNew();

            if (request == null)
            {
                onCompleted?.Invoke(ObjectLoadResult.Failure(ObjectLoadError.Create(
                    ObjectLoadErrorCode.InvalidRequest,
                    "Object load request is missing.")));
                yield break;
            }

            if (request.CancellationToken.IsCancellationRequested)
            {
                onCompleted?.Invoke(ObjectLoadResult.Failure(ObjectLoadError.Create(
                    ObjectLoadErrorCode.Canceled,
                    "Object load was canceled before it started.")));
                yield break;
            }

            request.ReportProgress("resolve", 0f, "Resolving object source.");
            ObjectSourceResolveResult sourceResult = null;
            yield return _sourceResolver.ResolveAsync(request, value => sourceResult = value);
            if (sourceResult == null || !sourceResult.Succeeded)
            {
                onCompleted?.Invoke(ObjectLoadResult.Failure(sourceResult?.Error ?? ObjectLoadError.Create(
                    ObjectLoadErrorCode.SourceResolutionFailed,
                    "Could not resolve object source.")));
                yield break;
            }

            request.ReportProgress("resolve", 1f, "Object source resolved.");
            AssetBundleContent content = null;
            ObjectContentLoadResult contentResult = null;
            yield return _contentLoader.LoadAsync(sourceResult.Source, request, value => contentResult = value);
            if (contentResult == null || !contentResult.Succeeded)
            {
                onCompleted?.Invoke(ObjectLoadResult.Failure(contentResult?.Error ?? ObjectLoadError.Create(
                    ObjectLoadErrorCode.ContentLoadFailed,
                    "Could not load object content.")));
                yield break;
            }

            content = contentResult.Content;
            ObjectInstantiationResult instantiationResult = null;
            Stopwatch instantiateTimer = Stopwatch.StartNew();
            yield return _instantiator.InstantiateAsync(content, request, value => instantiationResult = value);
            instantiateTimer.Stop();

            ObjectLoadTelemetry telemetry = contentResult.Telemetry ?? ObjectLoadTelemetry.Empty();
            telemetry.InstantiateTimeMs = instantiateTimer.ElapsedMilliseconds;
            telemetry.AssetCount = content != null && content.AssetNames != null ? content.AssetNames.Length : telemetry.AssetCount;
            telemetry.SceneCount = content != null && content.ScenePaths != null ? content.ScenePaths.Length : telemetry.SceneCount;
            totalTimer.Stop();
            telemetry.TotalTimeMs = totalTimer.ElapsedMilliseconds;

            if (instantiationResult == null || !instantiationResult.Succeeded)
            {
                content?.Unload(false);
                onCompleted?.Invoke(ObjectLoadResult.Failure(instantiationResult?.Error ?? ObjectLoadError.Create(
                    ObjectLoadErrorCode.InstantiationFailed,
                    "Could not instantiate object content.")));
                yield break;
            }

            _lastHandle = instantiationResult.Handle;
            ObjectDiagnosticsReport report = _diagnostics.CreateReport(instantiationResult.Handle, content);
            onCompleted?.Invoke(ObjectLoadResult.Success(instantiationResult.Message, instantiationResult.Handle, report, telemetry));
        }

        public void UnloadLast()
        {
            if (_lastHandle == null)
            {
                return;
            }

            _lastHandle.Unload();
            _lastHandle = null;
        }

        private sealed class ByteArrayObjectSourceContentLoader : IObjectSourceContentLoader
        {
            private readonly IObjectDownloader _downloader;
            private readonly IObjectContentLoader _contentLoader;

            public ByteArrayObjectSourceContentLoader(IObjectDownloader downloader, IObjectContentLoader contentLoader)
            {
                _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
                _contentLoader = contentLoader ?? throw new ArgumentNullException(nameof(contentLoader));
            }

            public IEnumerator LoadAsync(ObjectSource source,
                                         ObjectLoadRequest request,
                                         Action<ObjectContentLoadResult> onCompleted)
            {
                if (source == null)
                {
                    onCompleted?.Invoke(ObjectContentLoadResult.Failure(ObjectLoadError.Create(
                        ObjectLoadErrorCode.InvalidRequest,
                        "Object source is missing.")));
                    yield break;
                }

                Stopwatch downloadTimer = Stopwatch.StartNew();
                ObjectLoadTelemetry telemetry = new ObjectLoadTelemetry
                {
                    LoadStrategy = source.Type == ObjectSourceType.RawBytes ? "raw-bytes-legacy" : "byte-array-legacy",
                    CacheMode = request != null ? request.CacheMode : ObjectLoadCacheMode.Default,
                    CacheKey = request != null ? request.CacheKey : null,
                    CacheHash = request != null ? request.CacheHash : null,
                    CacheVersion = request != null ? request.CacheVersion : null,
                    Crc = request != null ? request.Crc : 0,
                    CacheStatus = "not-cacheable"
                };

                byte[] bytes = source.Bytes;
                if (source.Type != ObjectSourceType.RawBytes)
                {
                    ObjectDownloadResult downloadResult = null;
                    yield return _downloader.DownloadAsync(source, request, value => downloadResult = value);
                    downloadTimer.Stop();

                    if (downloadResult == null || !downloadResult.Succeeded)
                    {
                        onCompleted?.Invoke(ObjectContentLoadResult.Failure(downloadResult?.Error ?? ObjectLoadError.Create(
                            ObjectLoadErrorCode.DownloadFailed,
                            "Could not download object content.")));
                        yield break;
                    }

                    bytes = downloadResult.Bytes;
                    telemetry.DownloadTimeMs = downloadTimer.ElapsedMilliseconds;
                }
                else
                {
                    downloadTimer.Stop();
                }

                telemetry.BytesReceived = bytes != null ? bytes.Length : 0;

                Stopwatch bundleTimer = Stopwatch.StartNew();
                ObjectContentLoadResult contentResult = null;
                yield return _contentLoader.LoadAsync(bytes, request, value => contentResult = value);
                bundleTimer.Stop();

                if (contentResult == null || !contentResult.Succeeded)
                {
                    onCompleted?.Invoke(contentResult ?? ObjectContentLoadResult.Failure(ObjectLoadError.Create(
                        ObjectLoadErrorCode.ContentLoadFailed,
                        "Could not load object content.")));
                    yield break;
                }

                telemetry.BundleLoadTimeMs = bundleTimer.ElapsedMilliseconds;
                telemetry.AssetCount = contentResult.Content != null && contentResult.Content.AssetNames != null
                    ? contentResult.Content.AssetNames.Length
                    : 0;
                telemetry.SceneCount = contentResult.Content != null && contentResult.Content.ScenePaths != null
                    ? contentResult.Content.ScenePaths.Length
                    : 0;

                onCompleted?.Invoke(ObjectContentLoadResult.Success(contentResult.Content, telemetry));
            }
        }
    }
}
