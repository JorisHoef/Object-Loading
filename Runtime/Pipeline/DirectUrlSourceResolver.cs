using System;
using System.Collections;

namespace Deucarian.ObjectLoading
{
    public sealed class DirectUrlSourceResolver : IObjectSourceResolver
    {
        public IEnumerator ResolveAsync(ObjectLoadRequest request, Action<ObjectSourceResolveResult> onCompleted)
        {
            if (request == null)
            {
                onCompleted?.Invoke(ObjectSourceResolveResult.Failure(ObjectLoadError.Create(
                    ObjectLoadErrorCode.InvalidRequest,
                    "Object load request is missing.")));
                yield break;
            }

            ObjectSource source = request.Source;
            if (source == null)
            {
                onCompleted?.Invoke(ObjectSourceResolveResult.Failure(ObjectLoadError.Create(
                    ObjectLoadErrorCode.SourceResolutionFailed,
                    "Object source is missing.")));
                yield break;
            }

            if (source.Type == ObjectSourceType.LocalFile)
            {
                if (string.IsNullOrWhiteSpace(source.Path))
                {
                    onCompleted?.Invoke(ObjectSourceResolveResult.Failure(ObjectLoadError.Create(
                        ObjectLoadErrorCode.InvalidRequest,
                        "Object source file path is missing.")));
                    yield break;
                }

                onCompleted?.Invoke(ObjectSourceResolveResult.Success(ObjectSource.LocalFile(source.Path.Trim())));
                yield break;
            }

            if (source.Type == ObjectSourceType.RawBytes)
            {
                if (source.Bytes == null || source.Bytes.Length == 0)
                {
                    onCompleted?.Invoke(ObjectSourceResolveResult.Failure(ObjectLoadError.Create(
                        ObjectLoadErrorCode.EmptyDownload,
                        "Object source bytes are missing.")));
                    yield break;
                }

                onCompleted?.Invoke(ObjectSourceResolveResult.Success(ObjectSource.RawBytes(source.Bytes)));
                yield break;
            }

            if (source.Type != ObjectSourceType.DirectUrl)
            {
                onCompleted?.Invoke(ObjectSourceResolveResult.Failure(ObjectLoadError.Create(
                    ObjectLoadErrorCode.SourceResolutionFailed,
                    "Unsupported object source type: " + source.Type + ".")));
                yield break;
            }

            if (string.IsNullOrWhiteSpace(source.Url))
            {
                onCompleted?.Invoke(ObjectSourceResolveResult.Failure(ObjectLoadError.Create(
                    ObjectLoadErrorCode.InvalidRequest,
                    "Object source URL is missing.")));
                yield break;
            }

            string url = source.Url.Trim();
            if (request.AppendPlatformQuery)
            {
                string platform = string.IsNullOrWhiteSpace(request.PlatformOverride)
                    ? ObjectLoadingPlatform.GetCurrentPlatformName()
                    : request.PlatformOverride.Trim();
                url = PlatformQueryUtility.AppendPlatformQuery(url, platform, request.PlatformQueryParameter);
            }

            onCompleted?.Invoke(ObjectSourceResolveResult.Success(ObjectSource.DirectUrl(url)));
        }
    }
}
