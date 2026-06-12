using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;

namespace Deucarian.ObjectLoading
{
    public sealed class UnityWebRequestObjectDownloader : IObjectDownloader
    {
        public IEnumerator DownloadAsync(ObjectSource source,
                                         ObjectLoadRequest request,
                                         Action<ObjectDownloadResult> onCompleted)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.Url))
            {
                onCompleted?.Invoke(ObjectDownloadResult.Failure(ObjectLoadError.Create(
                    ObjectLoadErrorCode.InvalidRequest,
                    "Download URL is missing.")));
                yield break;
            }

            using (UnityWebRequest webRequest = UnityWebRequest.Get(source.Url))
            {
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.timeout = request != null && request.TimeoutSeconds > 0 ? request.TimeoutSeconds : 120;

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
                        onCompleted?.Invoke(ObjectDownloadResult.Failure(ObjectLoadError.Create(
                            ObjectLoadErrorCode.InvalidRequest,
                            "Invalid request header '" + header.Key + "': " + exception.Message,
                            source.Url,
                            null,
                            exception.Message)));
                        yield break;
                    }
                }

                request?.ReportProgress(ObjectLoadPhase.Downloading, 0f, "Downloading AssetBundle bytes.");
                UnityWebRequestAsyncOperation operation = webRequest.SendWebRequest();
                while (!operation.isDone)
                {
                    if (request != null && request.CancellationToken.IsCancellationRequested)
                    {
                        webRequest.Abort();
                        request.ReportProgress(ObjectLoadPhase.Failed, 1f, "AssetBundle download was canceled.");
                        onCompleted?.Invoke(ObjectDownloadResult.Failure(ObjectLoadError.Create(
                            ObjectLoadErrorCode.Canceled,
                            "AssetBundle download was canceled.",
                            source.Url,
                            webRequest.responseCode)));
                        yield break;
                    }

                    ulong downloadedBytes = webRequest.downloadedBytes;
                    long bytesReceived = downloadedBytes > (ulong)long.MaxValue
                        ? long.MaxValue
                        : (long)downloadedBytes;
                    request?.Progress?.Invoke(ObjectLoadProgress.Create(
                        ObjectLoadPhase.Downloading,
                        webRequest.downloadProgress < 0f ? 0f : webRequest.downloadProgress,
                        "Downloading AssetBundle bytes.",
                        bytesReceived));
                    yield return null;
                }

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    request?.ReportProgress(
                        ObjectLoadPhase.Failed,
                        1f,
                        string.IsNullOrWhiteSpace(webRequest.error)
                            ? "AssetBundle download failed."
                            : "AssetBundle download failed: " + webRequest.error);
                    onCompleted?.Invoke(ObjectDownloadResult.Failure(ObjectLoadError.Create(
                        ObjectLoadErrorCode.DownloadFailed,
                        string.IsNullOrWhiteSpace(webRequest.error)
                            ? "AssetBundle download failed."
                            : "AssetBundle download failed: " + webRequest.error,
                        source.Url,
                        webRequest.responseCode)));
                    yield break;
                }

                byte[] bytes = webRequest.downloadHandler != null ? webRequest.downloadHandler.data : null;
                if (bytes == null || bytes.Length == 0)
                {
                    request?.ReportProgress(ObjectLoadPhase.Failed, 1f, "AssetBundle download returned no bytes.");
                    onCompleted?.Invoke(ObjectDownloadResult.Failure(ObjectLoadError.Create(
                        ObjectLoadErrorCode.EmptyDownload,
                        "AssetBundle download returned no bytes.",
                        source.Url,
                        webRequest.responseCode)));
                    yield break;
                }

                request?.ReportProgress(ObjectLoadPhase.Downloading, 1f, "AssetBundle bytes downloaded.", bytes.Length);
                onCompleted?.Invoke(ObjectDownloadResult.Success(
                    bytes,
                    webRequest.responseCode,
                    webRequest.GetResponseHeaders()));
            }
        }
    }
}
