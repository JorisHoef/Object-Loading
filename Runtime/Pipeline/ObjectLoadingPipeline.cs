using System;
using System.Collections;

namespace JorisHoef.ObjectLoading
{
    public sealed class ObjectLoadingPipeline : IObjectLoadingPipeline
    {
        private readonly IObjectSourceResolver _sourceResolver;
        private readonly IObjectDownloader _downloader;
        private readonly IObjectContentLoader _contentLoader;
        private readonly IObjectInstantiator _instantiator;
        private readonly IObjectDiagnostics _diagnostics;
        private IObjectLoadHandle _lastHandle;

        public ObjectLoadingPipeline()
            : this(new DirectUrlSourceResolver(),
                   new UnityWebRequestObjectDownloader(),
                   new AssetBundleContentLoader(),
                   new AssetBundleObjectInstantiator(),
                   new DefaultObjectDiagnostics())
        {
        }

        public ObjectLoadingPipeline(IObjectSourceResolver sourceResolver,
                                    IObjectDownloader downloader,
                                    IObjectContentLoader contentLoader,
                                    IObjectInstantiator instantiator,
                                    IObjectDiagnostics diagnostics)
        {
            _sourceResolver = sourceResolver ?? throw new ArgumentNullException(nameof(sourceResolver));
            _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
            _contentLoader = contentLoader ?? throw new ArgumentNullException(nameof(contentLoader));
            _instantiator = instantiator ?? throw new ArgumentNullException(nameof(instantiator));
            _diagnostics = diagnostics ?? new DefaultObjectDiagnostics();
        }

        public IEnumerator LoadAsync(ObjectLoadRequest request, Action<ObjectLoadResult> onCompleted)
        {
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
            ObjectDownloadResult downloadResult = null;
            yield return _downloader.DownloadAsync(sourceResult.Source, request, value => downloadResult = value);
            if (downloadResult == null || !downloadResult.Succeeded)
            {
                onCompleted?.Invoke(ObjectLoadResult.Failure(downloadResult?.Error ?? ObjectLoadError.Create(
                    ObjectLoadErrorCode.DownloadFailed,
                    "Could not download object content.")));
                yield break;
            }

            AssetBundleContent content = null;
            ObjectContentLoadResult contentResult = null;
            yield return _contentLoader.LoadAsync(downloadResult.Bytes, request, value => contentResult = value);
            if (contentResult == null || !contentResult.Succeeded)
            {
                onCompleted?.Invoke(ObjectLoadResult.Failure(contentResult?.Error ?? ObjectLoadError.Create(
                    ObjectLoadErrorCode.ContentLoadFailed,
                    "Could not load object content.")));
                yield break;
            }

            content = contentResult.Content;
            ObjectInstantiationResult instantiationResult = null;
            yield return _instantiator.InstantiateAsync(content, request, value => instantiationResult = value);
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
            onCompleted?.Invoke(ObjectLoadResult.Success(instantiationResult.Message, instantiationResult.Handle, report));
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
    }
}
