using System;
using System.Collections;

namespace JorisHoef.ObjectLoading
{
    public interface IObjectSourceResolver
    {
        IEnumerator ResolveAsync(ObjectLoadRequest request, Action<ObjectSourceResolveResult> onCompleted);
    }

    public interface IObjectDownloader
    {
        IEnumerator DownloadAsync(ObjectSource source,
                                  ObjectLoadRequest request,
                                  Action<ObjectDownloadResult> onCompleted);
    }

    public interface IObjectContentLoader
    {
        IEnumerator LoadAsync(byte[] bytes,
                              ObjectLoadRequest request,
                              Action<ObjectContentLoadResult> onCompleted);
    }

    public interface IObjectSourceContentLoader
    {
        IEnumerator LoadAsync(ObjectSource source,
                              ObjectLoadRequest request,
                              Action<ObjectContentLoadResult> onCompleted);
    }

    public interface IObjectInstantiator
    {
        IEnumerator InstantiateAsync(AssetBundleContent content,
                                     ObjectLoadRequest request,
                                     Action<ObjectInstantiationResult> onCompleted);
    }

    public interface IObjectDiagnostics
    {
        ObjectDiagnosticsReport CreateReport(IObjectLoadHandle handle, AssetBundleContent content);
    }

    public interface IObjectLoadingPipeline
    {
        IEnumerator LoadAsync(ObjectLoadRequest request, Action<ObjectLoadResult> onCompleted);
        void UnloadLast();
    }
}
