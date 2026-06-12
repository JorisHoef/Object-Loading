using System;
using System.Collections;
using UnityEngine;

namespace Deucarian.ObjectLoading
{
    public sealed class AssetBundleContentLoader : IObjectContentLoader
    {
        public IEnumerator LoadAsync(byte[] bytes,
                                     ObjectLoadRequest request,
                                     Action<ObjectContentLoadResult> onCompleted)
        {
            if (bytes == null || bytes.Length == 0)
            {
                onCompleted?.Invoke(ObjectContentLoadResult.Failure(ObjectLoadError.Create(
                    ObjectLoadErrorCode.EmptyDownload,
                    "AssetBundle load received no bytes.")));
                yield break;
            }

            request?.ReportProgress(ObjectLoadPhase.LoadingBundle, 0f, "Loading AssetBundle from memory.");
            AssetBundleCreateRequest bundleRequest = AssetBundle.LoadFromMemoryAsync(bytes);
            yield return bundleRequest;

            AssetBundle bundle = bundleRequest.assetBundle;
            if (bundle == null)
            {
                request?.ReportProgress(ObjectLoadPhase.Failed, 1f, "Downloaded bytes could not be loaded as an AssetBundle.");
                onCompleted?.Invoke(ObjectContentLoadResult.Failure(ObjectLoadError.Create(
                    ObjectLoadErrorCode.ContentLoadFailed,
                    "Downloaded bytes could not be loaded as an AssetBundle. Check that the URL serves a Unity AssetBundle for the active platform.")));
                yield break;
            }

            request?.ReportProgress(ObjectLoadPhase.LoadingBundle, 1f, "AssetBundle loaded from memory.", bytes.Length);
            request?.ReportProgress(ObjectLoadPhase.DiscoveringContent, 0f, "Discovering AssetBundle content.", bytes.Length);
            string[] assetNames = bundle.GetAllAssetNames() ?? new string[0];
            string[] scenePaths = bundle.GetAllScenePaths() ?? new string[0];
            request?.ReportProgress(ObjectLoadPhase.DiscoveringContent, 1f, "AssetBundle content is ready.", bytes.Length);
            onCompleted?.Invoke(ObjectContentLoadResult.Success(new AssetBundleContent(bundle, assetNames, scenePaths)));
        }
    }
}
