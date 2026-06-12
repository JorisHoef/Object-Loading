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

            request?.ReportProgress("content", 0f, "Loading AssetBundle from memory.");
            AssetBundleCreateRequest bundleRequest = AssetBundle.LoadFromMemoryAsync(bytes);
            yield return bundleRequest;

            AssetBundle bundle = bundleRequest.assetBundle;
            if (bundle == null)
            {
                onCompleted?.Invoke(ObjectContentLoadResult.Failure(ObjectLoadError.Create(
                    ObjectLoadErrorCode.ContentLoadFailed,
                    "Downloaded bytes could not be loaded as an AssetBundle. Check that the URL serves a Unity AssetBundle for the active platform.")));
                yield break;
            }

            string[] assetNames = bundle.GetAllAssetNames() ?? new string[0];
            string[] scenePaths = bundle.GetAllScenePaths() ?? new string[0];
            request?.ReportProgress("content", 1f, "AssetBundle loaded from memory.");
            onCompleted?.Invoke(ObjectContentLoadResult.Success(new AssetBundleContent(bundle, assetNames, scenePaths)));
        }
    }
}
