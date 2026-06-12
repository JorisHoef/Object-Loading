using System;
using System.Collections;
using System.Diagnostics;

namespace Deucarian.ObjectLoading
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

            request.ReportProgress(ObjectLoadPhase.ResolvingSource, 0f, "Resolving object source.", 0, totalTimer.ElapsedMilliseconds);
            ObjectSourceResolveResult sourceResult = null;
            yield return _sourceResolver.ResolveAsync(request, value => sourceResult = value);
            if (sourceResult == null || !sourceResult.Succeeded)
            {
                totalTimer.Stop();
                ObjectLoadError error = sourceResult?.Error ?? ObjectLoadError.Create(
                    ObjectLoadErrorCode.SourceResolutionFailed,
                    "Could not resolve object source.");
                ReportFailed(request, totalTimer, null, error.Message);
                onCompleted?.Invoke(ObjectLoadResult.Failure(error));
                yield break;
            }

            request.ReportProgress(ObjectLoadPhase.ResolvingSource, 1f, "Object source resolved.", 0, totalTimer.ElapsedMilliseconds);
            AssetBundleContent content = null;
            ObjectContentLoadResult contentResult = null;
            yield return _contentLoader.LoadAsync(sourceResult.Source, request, value => contentResult = value);
            if (contentResult == null || !contentResult.Succeeded)
            {
                totalTimer.Stop();
                ObjectLoadTelemetry contentFailureTelemetry = contentResult != null ? contentResult.Telemetry : null;
                ObjectLoadError error = contentResult?.Error ?? ObjectLoadError.Create(
                    ObjectLoadErrorCode.ContentLoadFailed,
                    "Could not load object content.");
                ReportFailed(request, totalTimer, contentFailureTelemetry, error.Message);
                onCompleted?.Invoke(ObjectLoadResult.Failure(error));
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
                ObjectLoadError error = instantiationResult?.Error ?? ObjectLoadError.Create(
                    ObjectLoadErrorCode.InstantiationFailed,
                    "Could not instantiate object content.");
                ReportFailed(request, totalTimer, telemetry, error.Message);
                onCompleted?.Invoke(ObjectLoadResult.Failure(error));
                yield break;
            }

            _lastHandle = instantiationResult.Handle;
            request.ReportProgress(ObjectLoadPhase.Diagnostics, 0f, "Collecting object diagnostics.", telemetry.BytesReceived, totalTimer.ElapsedMilliseconds, telemetry);
            ObjectDiagnosticsReport report = _diagnostics.CreateReport(instantiationResult.Handle, content);
            CopyDiagnosticsToTelemetry(report, telemetry);
            request.ReportProgress(ObjectLoadPhase.Diagnostics, 1f, "Object diagnostics collected.", telemetry.BytesReceived, totalTimer.ElapsedMilliseconds, telemetry);
            request.ReportProgress(ObjectLoadPhase.Completed, 1f, instantiationResult.Message, telemetry.BytesReceived, totalTimer.ElapsedMilliseconds, telemetry);
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
                    request?.ReportProgress(ObjectLoadPhase.Downloading, 0f, "Downloading AssetBundle bytes.", 0, 0, telemetry);
                    ObjectDownloadResult downloadResult = null;
                    yield return _downloader.DownloadAsync(source, request, value => downloadResult = value);
                    downloadTimer.Stop();

                    if (downloadResult == null || !downloadResult.Succeeded)
                    {
                        ObjectLoadError error = downloadResult?.Error ?? ObjectLoadError.Create(
                            ObjectLoadErrorCode.DownloadFailed,
                            "Could not download object content.");
                        request?.ReportProgress(ObjectLoadPhase.Failed, 1f, error.Message, telemetry.BytesReceived, 0, telemetry);
                        onCompleted?.Invoke(ObjectContentLoadResult.Failure(error));
                        yield break;
                    }

                    bytes = downloadResult.Bytes;
                    telemetry.DownloadTimeMs = downloadTimer.ElapsedMilliseconds;
                    telemetry.BytesReceived = bytes != null ? bytes.Length : 0;
                    request?.ReportProgress(ObjectLoadPhase.Downloading, 1f, "AssetBundle bytes downloaded.", telemetry.BytesReceived, 0, telemetry);
                }
                else
                {
                    downloadTimer.Stop();
                }

                telemetry.BytesReceived = bytes != null ? bytes.Length : 0;

                Stopwatch bundleTimer = Stopwatch.StartNew();
                request?.ReportProgress(ObjectLoadPhase.LoadingBundle, 0f, "Loading AssetBundle from bytes.", telemetry.BytesReceived, 0, telemetry);
                ObjectContentLoadResult contentResult = null;
                yield return _contentLoader.LoadAsync(bytes, request, value => contentResult = value);
                bundleTimer.Stop();

                if (contentResult == null || !contentResult.Succeeded)
                {
                    ObjectLoadError error = contentResult != null
                        ? contentResult.Error
                        : ObjectLoadError.Create(
                        ObjectLoadErrorCode.ContentLoadFailed,
                        "Could not load object content.");
                    request?.ReportProgress(ObjectLoadPhase.Failed, 1f, error.Message, telemetry.BytesReceived, 0, telemetry);
                    onCompleted?.Invoke(contentResult ?? ObjectContentLoadResult.Failure(error));
                    yield break;
                }

                telemetry.BundleLoadTimeMs = bundleTimer.ElapsedMilliseconds;
                telemetry.AssetCount = contentResult.Content != null && contentResult.Content.AssetNames != null
                    ? contentResult.Content.AssetNames.Length
                    : 0;
                telemetry.SceneCount = contentResult.Content != null && contentResult.Content.ScenePaths != null
                    ? contentResult.Content.ScenePaths.Length
                    : 0;
                request?.ReportProgress(ObjectLoadPhase.DiscoveringContent, 1f, "AssetBundle content is ready.", telemetry.BytesReceived, 0, telemetry);

                onCompleted?.Invoke(ObjectContentLoadResult.Success(contentResult.Content, telemetry));
            }
        }

        private static void ReportFailed(ObjectLoadRequest request,
                                         Stopwatch totalTimer,
                                         ObjectLoadTelemetry telemetry,
                                         string message)
        {
            if (request == null)
            {
                return;
            }

            if (telemetry != null)
            {
                telemetry.TotalTimeMs = totalTimer.ElapsedMilliseconds;
            }

            request.ReportProgress(
                ObjectLoadPhase.Failed,
                1f,
                string.IsNullOrWhiteSpace(message) ? "Object loading failed." : message,
                telemetry != null ? telemetry.BytesReceived : 0,
                totalTimer.ElapsedMilliseconds,
                telemetry);
        }

        private static void CopyDiagnosticsToTelemetry(ObjectDiagnosticsReport report, ObjectLoadTelemetry telemetry)
        {
            if (report == null || telemetry == null)
            {
                return;
            }

            telemetry.RendererCount = report.RendererCount;
            telemetry.MaterialCount = report.MaterialCount;
            telemetry.MissingShaderMaterialCount = report.MissingShaderMaterialCount;
            telemetry.PinkMaterialCount = report.PinkMaterialCount;
        }
    }
}
