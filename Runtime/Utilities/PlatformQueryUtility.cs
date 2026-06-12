using System;

namespace Deucarian.ObjectLoading
{
    public static class PlatformQueryUtility
    {
        public static string AppendPlatformQuery(string url, string platformName, string parameterName = "platform")
        {
            if (string.IsNullOrWhiteSpace(url)
                || string.IsNullOrWhiteSpace(platformName)
                || string.IsNullOrWhiteSpace(parameterName))
            {
                return url;
            }

            if (HasQueryParameter(url, parameterName))
            {
                return url;
            }

            int fragmentIndex = url.IndexOf('#');
            string fragment = fragmentIndex >= 0 ? url.Substring(fragmentIndex) : string.Empty;
            string urlWithoutFragment = fragmentIndex >= 0 ? url.Substring(0, fragmentIndex) : url;

            char separator = urlWithoutFragment.IndexOf('?') >= 0 ? '&' : '?';
            return urlWithoutFragment
                   + separator
                   + Uri.EscapeDataString(parameterName.Trim())
                   + "="
                   + Uri.EscapeDataString(platformName.Trim())
                   + fragment;
        }

        public static bool HasQueryParameter(string url, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            int queryStart = url.IndexOf('?');
            if (queryStart < 0)
            {
                return false;
            }

            int fragmentStart = url.IndexOf('#', queryStart + 1);
            string query = fragmentStart >= 0
                ? url.Substring(queryStart + 1, fragmentStart - queryStart - 1)
                : url.Substring(queryStart + 1);

            string[] parts = query.Split('&');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                int equalsIndex = part.IndexOf('=');
                string name = equalsIndex >= 0 ? part.Substring(0, equalsIndex) : part;
                name = Uri.UnescapeDataString(name);
                if (string.Equals(name, parameterName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
