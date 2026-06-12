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
            if (source == null || source.Type != ObjectSourceType.DirectUrl)
            {
                onCompleted?.Invoke(ObjectSourceResolveResult.Failure(ObjectLoadError.Create(
                    ObjectLoadErrorCode.SourceResolutionFailed,
                    "Only direct URL object sources are supported by DirectUrlSourceResolver.")));
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
