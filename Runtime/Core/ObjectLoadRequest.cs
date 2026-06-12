using System;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

namespace Deucarian.ObjectLoading
{
    public sealed class ObjectLoadRequest
    {
        private const string RedactedValue = "[redacted]";

        public ObjectLoadRequest()
        {
            Source = new ObjectSource();
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            AppendPlatformQuery = true;
            PlatformQueryParameter = "platform";
            LoadPreference = ObjectContentLoadPreference.Automatic;
            TimeoutSeconds = 120;
            CacheMode = ObjectLoadCacheMode.Default;
        }

        [JsonProperty("source")]
        public ObjectSource Source { get; set; }

        [JsonProperty("headers")]
        public Dictionary<string, string> Headers { get; set; }

        [JsonProperty("bearer_token")]
        public string BearerToken { get; set; }

        [JsonProperty("append_platform_query")]
        public bool AppendPlatformQuery { get; set; }

        [JsonProperty("platform_query_parameter")]
        public string PlatformQueryParameter { get; set; }

        [JsonProperty("platform_override")]
        public string PlatformOverride { get; set; }

        [JsonProperty("load_preference")]
        public ObjectContentLoadPreference LoadPreference { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("timeout_seconds")]
        public int TimeoutSeconds { get; set; }

        [JsonProperty("cache_mode")]
        public ObjectLoadCacheMode CacheMode { get; set; }

        [JsonProperty("cache_key")]
        public string CacheKey { get; set; }

        [JsonProperty("cache_hash")]
        public string CacheHash { get; set; }

        [JsonProperty("cache_version")]
        public uint? CacheVersion { get; set; }

        [JsonProperty("crc")]
        public uint Crc { get; set; }

        [JsonIgnore]
        public Transform Parent { get; set; }

        [JsonIgnore]
        public Vector3? Position { get; set; }

        [JsonIgnore]
        public Quaternion? Rotation { get; set; }

        [JsonIgnore]
        public Vector3? Scale { get; set; }

        [JsonIgnore]
        public CancellationToken CancellationToken { get; set; }

        [JsonIgnore]
        public Action<ObjectLoadProgress> Progress { get; set; }

        [JsonIgnore]
        public string Url
        {
            get { return Source != null ? Source.Url : null; }
            set { Source = ObjectSource.DirectUrl(value); }
        }

        public static ObjectLoadRequest FromUrl(string url)
        {
            return new ObjectLoadRequest
            {
                Source = ObjectSource.DirectUrl(url)
            };
        }

        public void AddHeader(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            if (Headers == null)
            {
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            Headers[name.Trim()] = value;
        }

        public Dictionary<string, string> CreateHeaders()
        {
            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (Headers != null)
            {
                foreach (KeyValuePair<string, string> pair in Headers)
                {
                    if (!string.IsNullOrWhiteSpace(pair.Key))
                    {
                        headers[pair.Key.Trim()] = pair.Value;
                    }
                }
            }

            string token = StripBearerPrefix(BearerToken);
            if (!string.IsNullOrWhiteSpace(token) && !headers.ContainsKey("Authorization"))
            {
                headers["Authorization"] = "Bearer " + token;
            }

            return headers;
        }

        public ObjectLoadRequestDebugSnapshot CreateDebugSnapshot()
        {
            Dictionary<string, string> redactedHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> pair in CreateHeaders())
            {
                redactedHeaders[pair.Key] = IsSensitiveHeader(pair.Key) ? RedactedValue : pair.Value;
            }

            return new ObjectLoadRequestDebugSnapshot
            {
                Source = Source,
                Headers = redactedHeaders,
                BearerToken = string.IsNullOrWhiteSpace(BearerToken) ? null : RedactedValue,
                AppendPlatformQuery = AppendPlatformQuery,
                PlatformQueryParameter = PlatformQueryParameter,
                PlatformOverride = PlatformOverride,
                LoadPreference = LoadPreference,
                DisplayName = DisplayName,
                TimeoutSeconds = TimeoutSeconds,
                CacheMode = CacheMode,
                CacheKey = CacheKey,
                CacheHash = CacheHash,
                CacheVersion = CacheVersion,
                Crc = Crc
            };
        }

        public string ToDebugSnapshotJson()
        {
            return JsonConvert.SerializeObject(CreateDebugSnapshot(), Formatting.Indented);
        }

        public void ReportProgress(ObjectLoadPhase phase,
                                   float normalized,
                                   string message,
                                   long bytesReceived = 0,
                                   long elapsedMs = 0,
                                   ObjectLoadTelemetry telemetry = null)
        {
            if (Progress == null)
            {
                return;
            }

            Progress.Invoke(ObjectLoadProgress.Create(phase, normalized, message, bytesReceived, elapsedMs, telemetry));
        }

        public void ReportProgress(string stage,
                                   float normalized,
                                   string message,
                                   long bytesReceived = 0,
                                   long elapsedMs = 0,
                                   ObjectLoadTelemetry telemetry = null)
        {
            if (Progress == null)
            {
                return;
            }

            Progress.Invoke(ObjectLoadProgress.Create(stage, normalized, message, bytesReceived, elapsedMs, telemetry));
        }

        public static string StripBearerPrefix(string bearerToken)
        {
            if (string.IsNullOrWhiteSpace(bearerToken))
            {
                return null;
            }

            string trimmed = bearerToken.Trim();
            const string prefix = "Bearer ";
            return trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? trimmed.Substring(prefix.Length).Trim()
                : trimmed;
        }

        private static bool IsSensitiveHeader(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            string normalized = name.Trim();
            return normalized.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
                   || normalized.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase)
                   || normalized.Equals("X-Api-Key", StringComparison.OrdinalIgnoreCase)
                   || normalized.Equals("Api-Key", StringComparison.OrdinalIgnoreCase)
                   || normalized.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    public sealed class ObjectLoadRequestDebugSnapshot
    {
        [JsonProperty("source")]
        public ObjectSource Source { get; set; }

        [JsonProperty("headers")]
        public Dictionary<string, string> Headers { get; set; }

        [JsonProperty("bearer_token")]
        public string BearerToken { get; set; }

        [JsonProperty("append_platform_query")]
        public bool AppendPlatformQuery { get; set; }

        [JsonProperty("platform_query_parameter")]
        public string PlatformQueryParameter { get; set; }

        [JsonProperty("platform_override")]
        public string PlatformOverride { get; set; }

        [JsonProperty("load_preference")]
        public ObjectContentLoadPreference LoadPreference { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("timeout_seconds")]
        public int TimeoutSeconds { get; set; }

        [JsonProperty("cache_mode")]
        public ObjectLoadCacheMode CacheMode { get; set; }

        [JsonProperty("cache_key")]
        public string CacheKey { get; set; }

        [JsonProperty("cache_hash")]
        public string CacheHash { get; set; }

        [JsonProperty("cache_version")]
        public uint? CacheVersion { get; set; }

        [JsonProperty("crc")]
        public uint Crc { get; set; }
    }
}
