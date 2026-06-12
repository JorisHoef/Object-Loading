using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Deucarian.ObjectLoading
{
    public sealed class SourceAssetBundleContentLoader : IObjectSourceContentLoader
    {
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

            switch (source.Type)
            {
                case ObjectSourceType.DirectUrl:
                    yield return LoadRemoteUrl(source, request, onCompleted);
                    yield break;
                case ObjectSourceType.LocalFile:
                    yield return LoadLocalFile(source, request, onCompleted);
                    yield break;
                case ObjectSourceType.RawBytes:
                    yield return LoadRawBytes(source, request, onCompleted);
                    yield break;
                default:
                    onCompleted?.Invoke(ObjectContentLoadResult.Failure(ObjectLoadError.Create(
                        ObjectLoadErrorCode.SourceResolutionFailed,
                        "Unsupported object source type: " + source.Type + ".")));
                    yield break;
            }
        }

        private static IEnumerator LoadRemoteUrl(ObjectSource source,
                                                 ObjectLoadRequest request,
                                                 Action<ObjectContentLoadResult> onCompleted)
        {
            if (string.IsNullOrWhiteSpace(source.Url))
            {
                onCompleted?.Invoke(ObjectContentLoadResult.Failure(ObjectLoadError.Create(
                    ObjectLoadErrorCode.InvalidRequest,
                    "Object source URL is missing.")));
                yield break;
            }

            ObjectLoadTelemetry telemetry = CreateTelemetry(request, "remote-url");
            string cacheStatus;
            using (UnityWebRequest webRequest = CreateRemoteAssetBundleRequest(source.Url.Trim(), request, out cacheStatus))
            {
                telemetry.CacheStatus = cacheStatus;
                webRequest.timeout = request != null && request.TimeoutSeconds > 0 ? request.TimeoutSeconds : 120;

                if (!ApplyHeaders(webRequest, request, source.Url, onCompleted))
                {
                    yield break;
                }

                request?.ReportProgress(ObjectLoadPhase.Downloading, 0f, "Downloading AssetBundle.", 0, 0, telemetry);
                Stopwatch downloadTimer = Stopwatch.StartNew();
                UnityWebRequestAsyncOperation operation = webRequest.SendWebRequest();
                while (!operation.isDone)
                {
                    if (request != null && request.CancellationToken.IsCancellationRequested)
                    {
                        webRequest.Abort();
                        downloadTimer.Stop();
                        telemetry.DownloadTimeMs = downloadTimer.ElapsedMilliseconds;
                        telemetry.BytesReceived = ClampDownloadedBytes(webRequest.downloadedBytes);
                        request.ReportProgress(
                            ObjectLoadPhase.Failed,
                            1f,
                            "AssetBundle download was canceled.",
                            telemetry.BytesReceived,
                            0,
                            telemetry);
                        onCompleted?.Invoke(ObjectContentLoadResult.Failure(ObjectLoadError.Create(
                            ObjectLoadErrorCode.Canceled,
                            "AssetBundle download was canceled.",
                            source.Url,
                            webRequest.responseCode)));
                        yield break;
                    }

                    telemetry.BytesReceived = ClampDownloadedBytes(webRequest.downloadedBytes);
                    request?.Progress?.Invoke(ObjectLoadProgress.Create(
                        ObjectLoadPhase.Downloading,
                        webRequest.downloadProgress < 0f ? 0f : webRequest.downloadProgress,
                        "Downloading AssetBundle.",
                        telemetry.BytesReceived,
                        0,
                        telemetry));
                    yield return null;
                }

                downloadTimer.Stop();
                telemetry.DownloadTimeMs = downloadTimer.ElapsedMilliseconds;
                telemetry.BytesReceived = ClampDownloadedBytes(webRequest.downloadedBytes);

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    request?.ReportProgress(
                        ObjectLoadPhase.Failed,
                        1f,
                        string.IsNullOrWhiteSpace(webRequest.error)
                            ? "AssetBundle download failed."
                            : "AssetBundle download failed: " + webRequest.error,
                        telemetry.BytesReceived,
                        0,
                        telemetry);
                    onCompleted?.Invoke(ObjectContentLoadResult.Failure(ObjectLoadError.Create(
                        ObjectLoadErrorCode.DownloadFailed,
                        string.IsNullOrWhiteSpace(webRequest.error)
                            ? "AssetBundle download failed."
                            : "AssetBundle download failed: " + webRequest.error,
                        source.Url,
                        webRequest.responseCode)));
                    yield break;
                }

                request?.ReportProgress(ObjectLoadPhase.Downloading, 1f, "AssetBundle downloaded.", telemetry.BytesReceived, 0, telemetry);
                request?.ReportProgress(ObjectLoadPhase.LoadingBundle, 0f, "Reading downloaded AssetBundle.", telemetry.BytesReceived, 0, telemetry);
                Stopwatch bundleTimer = Stopwatch.StartNew();
                AssetBundle bundle = DownloadHandlerAssetBundle.GetContent(webRequest);
                bundleTimer.Stop();
                telemetry.BundleLoadTimeMs = bundleTimer.ElapsedMilliseconds;
                request?.ReportProgress(ObjectLoadPhase.LoadingBundle, 1f, "AssetBundle loaded.", telemetry.BytesReceived, 0, telemetry);

                CompleteBundleLoad(bundle, source.Url, telemetry, request, onCompleted);
            }
        }

        private static IEnumerator LoadLocalFile(ObjectSource source,
                                                 ObjectLoadRequest request,
                                                 Action<ObjectContentLoadResult> onCompleted)
        {
            if (string.IsNullOrWhiteSpace(source.Path))
            {
                onCompleted?.Invoke(ObjectContentLoadResult.Failure(ObjectLoadError.Create(
                    ObjectLoadErrorCode.InvalidRequest,
                    "Object source file path is missing.")));
                yield break;
            }

            string path = source.Path.Trim();
            if (!File.Exists(path))
            {
                onCompleted?.Invoke(ObjectContentLoadResult.Failure(ObjectLoadError.Create(
                    ObjectLoadErrorCode.ContentLoadFailed,
                    "AssetBundle file does not exist: " + path)));
                yield break;
            }

            ObjectLoadTelemetry telemetry = CreateTelemetry(request, "local-file");
            telemetry.CacheStatus = "local-file";
            telemetry.BytesReceived = new FileInfo(path).Length;

            request?.ReportProgress(ObjectLoadPhase.LoadingBundle, 0f, "Loading AssetBundle from file.", telemetry.BytesReceived, 0, telemetry);
            Stopwatch bundleTimer = Stopwatch.StartNew();
            AssetBundleCreateRequest bundleRequest = AssetBundle.LoadFromFileAsync(path, request != null ? request.Crc : 0);
            bool canceled = false;
            while (!bundleRequest.isDone)
            {
                canceled = canceled || (request != null && request.CancellationToken.IsCancellationRequested);
                request?.Progress?.Invoke(ObjectLoadProgress.Create(
                    ObjectLoadPhase.LoadingBundle,
                    bundleRequest.progress,
                    "Loading AssetBundle from file.",
                    telemetry.BytesReceived,
                    0,
                    telemetry));
                yield return null;
            }

            bundleTimer.Stop();
            telemetry.BundleLoadTimeMs = bundleTimer.ElapsedMilliseconds;

            AssetBundle bundle = bundleRequest.assetBundle;
            if (canceled)
            {
                if (bundle != null)
                {
                    bundle.Unload(false);
                }

                request?.ReportProgress(ObjectLoadPhase.Failed, 1f, "AssetBundle file load was canceled.", telemetry.BytesReceived, 0, telemetry);
                onCompleted?.Invoke(ObjectContentLoadResult.Failure(ObjectLoadError.Create(
                    ObjectLoadErrorCode.Canceled,
                    "AssetBundle file load was canceled.",
                    path)));
                yield break;
            }

            request?.ReportProgress(ObjectLoadPhase.LoadingBundle, 1f, "AssetBundle loaded from file.", telemetry.BytesReceived, 0, telemetry);
            CompleteBundleLoad(bundle, path, telemetry, request, onCompleted);
        }

        private static IEnumerator LoadRawBytes(ObjectSource source,
                                                ObjectLoadRequest request,
                                                Action<ObjectContentLoadResult> onCompleted)
        {
            if (source.Bytes == null || source.Bytes.Length == 0)
            {
                onCompleted?.Invoke(ObjectContentLoadResult.Failure(ObjectLoadError.Create(
                    ObjectLoadErrorCode.EmptyDownload,
                    "AssetBundle raw bytes are missing.")));
                yield break;
            }

            ObjectLoadTelemetry telemetry = CreateTelemetry(request, "raw-bytes");
            telemetry.CacheStatus = "not-cacheable";
            telemetry.BytesReceived = source.Bytes.Length;

            request?.ReportProgress(ObjectLoadPhase.LoadingBundle, 0f, "Loading AssetBundle from memory.", telemetry.BytesReceived, 0, telemetry);
            Stopwatch bundleTimer = Stopwatch.StartNew();
            AssetBundleCreateRequest bundleRequest = AssetBundle.LoadFromMemoryAsync(
                source.Bytes,
                request != null ? request.Crc : 0);
            bool canceled = false;
            while (!bundleRequest.isDone)
            {
                canceled = canceled || (request != null && request.CancellationToken.IsCancellationRequested);
                request?.Progress?.Invoke(ObjectLoadProgress.Create(
                    ObjectLoadPhase.LoadingBundle,
                    bundleRequest.progress,
                    "Loading AssetBundle from memory.",
                    telemetry.BytesReceived,
                    0,
                    telemetry));
                yield return null;
            }

            bundleTimer.Stop();
            telemetry.BundleLoadTimeMs = bundleTimer.ElapsedMilliseconds;

            AssetBundle bundle = bundleRequest.assetBundle;
            if (canceled)
            {
                if (bundle != null)
                {
                    bundle.Unload(false);
                }

                request?.ReportProgress(ObjectLoadPhase.Failed, 1f, "AssetBundle memory load was canceled.", telemetry.BytesReceived, 0, telemetry);
                onCompleted?.Invoke(ObjectContentLoadResult.Failure(ObjectLoadError.Create(
                    ObjectLoadErrorCode.Canceled,
                    "AssetBundle memory load was canceled.")));
                yield break;
            }

            request?.ReportProgress(ObjectLoadPhase.LoadingBundle, 1f, "AssetBundle loaded from memory.", telemetry.BytesReceived, 0, telemetry);
            CompleteBundleLoad(bundle, "raw-bytes", telemetry, request, onCompleted);
        }

        private static UnityWebRequest CreateRemoteAssetBundleRequest(string url,
                                                                      ObjectLoadRequest request,
                                                                      out string cacheStatus)
        {
            ObjectLoadCacheMode cacheMode = request != null ? request.CacheMode : ObjectLoadCacheMode.Default;
            uint crc = request != null ? request.Crc : 0;
            cacheStatus = "not-configured";

            if (cacheMode == ObjectLoadCacheMode.Disabled)
            {
                cacheStatus = "disabled";
                return UnityWebRequestAssetBundle.GetAssetBundle(url);
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            cacheStatus = "webgl-browser-cache";
            return UnityWebRequestAssetBundle.GetAssetBundle(url);
#else
            Hash128 hash;
            if (TryGetCacheHash(request, out hash))
            {
                if (!string.IsNullOrWhiteSpace(request.CacheKey))
                {
                    cacheStatus = "unity-cache-key-hash";
                    return UnityWebRequestAssetBundle.GetAssetBundle(
                        url,
                        new CachedAssetBundle(request.CacheKey.Trim(), hash),
                        crc);
                }

                cacheStatus = "unity-cache-hash";
                return UnityWebRequestAssetBundle.GetAssetBundle(url, hash, crc);
            }

            if (request != null && request.CacheVersion.HasValue)
            {
                cacheStatus = "unity-cache-version";
                return UnityWebRequestAssetBundle.GetAssetBundle(url, request.CacheVersion.Value, crc);
            }

            if (cacheMode == ObjectLoadCacheMode.UseUnityCache)
            {
                cacheStatus = "unity-cache-metadata-missing";
            }

            return UnityWebRequestAssetBundle.GetAssetBundle(url);
#endif
        }

        private static bool ApplyHeaders(UnityWebRequest webRequest,
                                         ObjectLoadRequest request,
                                         string requestUrl,
                                         Action<ObjectContentLoadResult> onCompleted)
        {
            Dictionary<string, string> headers = request != null
                ? request.CreateHeaders()
                : new Dictionary<string, string>();

            foreach (KeyValuePair<string, string> header in headers)
            {
                if (string.IsNullOrWhiteSpace(header.Key))
                {
                    continue;
                }

                try
                {
                    webRequest.SetRequestHeader(header.Key, header.Value ?? string.Empty);
                }
                catch (Exception exception)
                {
                    onCompleted?.Invoke(ObjectContentLoadResult.Failure(ObjectLoadError.Create(
                        ObjectLoadErrorCode.InvalidRequest,
                        "Invalid request header '" + header.Key + "': " + exception.Message,
                        requestUrl,
                        null,
                        exception.Message)));
                    return false;
                }
            }

            return true;
        }

        private static void CompleteBundleLoad(AssetBundle bundle,
                                               string sourceDescription,
                                               ObjectLoadTelemetry telemetry,
                                               ObjectLoadRequest request,
                                               Action<ObjectContentLoadResult> onCompleted)
        {
            if (bundle == null)
            {
                request?.ReportProgress(ObjectLoadPhase.Failed, 1f, "AssetBundle could not be loaded.", telemetry != null ? telemetry.BytesReceived : 0, 0, telemetry);
                onCompleted?.Invoke(ObjectContentLoadResult.Failure(ObjectLoadError.Create(
                    ObjectLoadErrorCode.ContentLoadFailed,
                    "AssetBundle could not be loaded. Check that the source serves a Unity AssetBundle for the active platform.",
                    sourceDescription)));
                return;
            }

            request?.ReportProgress(ObjectLoadPhase.DiscoveringContent, 0f, "Discovering AssetBundle content.", telemetry.BytesReceived, 0, telemetry);
            string[] assetNames = bundle.GetAllAssetNames() ?? new string[0];
            string[] scenePaths = bundle.GetAllScenePaths() ?? new string[0];
            telemetry.AssetCount = assetNames.Length;
            telemetry.SceneCount = scenePaths.Length;

            request?.ReportProgress(ObjectLoadPhase.DiscoveringContent, 1f, "AssetBundle content is ready.", telemetry.BytesReceived, 0, telemetry);
            onCompleted?.Invoke(ObjectContentLoadResult.Success(
                new AssetBundleContent(bundle, assetNames, scenePaths),
                telemetry));
        }

        private static ObjectLoadTelemetry CreateTelemetry(ObjectLoadRequest request, string loadStrategy)
        {
            return new ObjectLoadTelemetry
            {
                LoadStrategy = loadStrategy,
                CacheMode = request != null ? request.CacheMode : ObjectLoadCacheMode.Default,
                CacheKey = request != null ? request.CacheKey : null,
                CacheHash = request != null ? request.CacheHash : null,
                CacheVersion = request != null ? request.CacheVersion : null,
                Crc = request != null ? request.Crc : 0
            };
        }

        private static bool TryGetCacheHash(ObjectLoadRequest request, out Hash128 hash)
        {
            hash = default(Hash128);
            if (request == null || string.IsNullOrWhiteSpace(request.CacheHash))
            {
                return false;
            }

            try
            {
                hash = Hash128.Parse(request.CacheHash.Trim());
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static long ClampDownloadedBytes(ulong downloadedBytes)
        {
            return downloadedBytes > (ulong)long.MaxValue
                ? long.MaxValue
                : (long)downloadedBytes;
        }
    }
}
