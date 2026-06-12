using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Deucarian.ObjectLoading
{
    public enum ObjectSourceType
    {
        DirectUrl = 0,
        LocalFile = 1,
        RawBytes = 2
    }

    public enum ObjectContentLoadPreference
    {
        Automatic = 0,
        SceneFirst = 1,
        PrefabFirst = 2
    }

    public enum ObjectLoadCacheMode
    {
        Default = 0,
        Disabled = 1,
        UseUnityCache = 2
    }

    public enum ObjectLoadPhase
    {
        None = 0,
        ResolvingSource = 1,
        Downloading = 2,
        LoadingBundle = 3,
        DiscoveringContent = 4,
        Instantiating = 5,
        Diagnostics = 6,
        Completed = 7,
        Failed = 8
    }

    public enum ObjectLoadErrorCode
    {
        None = 0,
        InvalidRequest = 1,
        SourceResolutionFailed = 2,
        DownloadFailed = 3,
        EmptyDownload = 4,
        ContentLoadFailed = 5,
        InstantiationFailed = 6,
        Canceled = 7,
        Unknown = 100
    }

    public sealed class ObjectSource
    {
        public ObjectSource()
        {
            Type = ObjectSourceType.DirectUrl;
        }

        public ObjectSource(ObjectSourceType type, string url)
        {
            Type = type;
            Url = url;
        }

        [JsonProperty("type")]
        public ObjectSourceType Type { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonIgnore]
        public byte[] Bytes { get; set; }

        public static ObjectSource DirectUrl(string url)
        {
            return new ObjectSource(ObjectSourceType.DirectUrl, url);
        }

        public static ObjectSource LocalFile(string path)
        {
            return new ObjectSource(ObjectSourceType.LocalFile, null)
            {
                Path = path
            };
        }

        public static ObjectSource RawBytes(byte[] bytes)
        {
            return new ObjectSource(ObjectSourceType.RawBytes, null)
            {
                Bytes = bytes
            };
        }
    }

    public sealed class ObjectLoadProgress
    {
        [JsonProperty("phase")]
        public ObjectLoadPhase Phase { get; private set; }

        [JsonProperty("stage")]
        public string Stage { get; private set; }

        [JsonProperty("normalized")]
        public float Normalized { get; private set; }

        [JsonProperty("message")]
        public string Message { get; private set; }

        [JsonProperty("bytes_received")]
        public long BytesReceived { get; private set; }

        [JsonProperty("elapsed_ms")]
        public long ElapsedMs { get; private set; }

        [JsonProperty("telemetry")]
        public ObjectLoadTelemetry Telemetry { get; private set; }

        public static ObjectLoadProgress Create(ObjectLoadPhase phase,
                                                float normalized,
                                                string message,
                                                long bytesReceived = 0,
                                                long elapsedMs = 0,
                                                ObjectLoadTelemetry telemetry = null)
        {
            return new ObjectLoadProgress
            {
                Phase = phase,
                Stage = ToStageName(phase),
                Normalized = Mathf.Clamp01(normalized),
                Message = message,
                BytesReceived = bytesReceived,
                ElapsedMs = elapsedMs,
                Telemetry = telemetry
            };
        }

        public static ObjectLoadProgress Create(string stage,
                                                float normalized,
                                                string message,
                                                long bytesReceived = 0,
                                                long elapsedMs = 0,
                                                ObjectLoadTelemetry telemetry = null)
        {
            ObjectLoadPhase phase = ToPhase(stage);
            return new ObjectLoadProgress
            {
                Phase = phase,
                Stage = string.IsNullOrWhiteSpace(stage) ? ToStageName(phase) : stage,
                Normalized = Mathf.Clamp01(normalized),
                Message = message,
                BytesReceived = bytesReceived,
                ElapsedMs = elapsedMs,
                Telemetry = telemetry
            };
        }

        private static ObjectLoadPhase ToPhase(string stage)
        {
            if (string.IsNullOrWhiteSpace(stage))
            {
                return ObjectLoadPhase.None;
            }

            string normalized = stage.Trim().Replace("-", "_");
            if (normalized.Equals("resolve", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("resolving_source", StringComparison.OrdinalIgnoreCase))
            {
                return ObjectLoadPhase.ResolvingSource;
            }

            if (normalized.Equals("download", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("downloading", StringComparison.OrdinalIgnoreCase))
            {
                return ObjectLoadPhase.Downloading;
            }

            if (normalized.Equals("content", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("loading_bundle", StringComparison.OrdinalIgnoreCase))
            {
                return ObjectLoadPhase.LoadingBundle;
            }

            if (normalized.Equals("discovering_content", StringComparison.OrdinalIgnoreCase))
            {
                return ObjectLoadPhase.DiscoveringContent;
            }

            if (normalized.Equals("instantiate", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("instantiating", StringComparison.OrdinalIgnoreCase))
            {
                return ObjectLoadPhase.Instantiating;
            }

            if (normalized.Equals("diagnostics", StringComparison.OrdinalIgnoreCase))
            {
                return ObjectLoadPhase.Diagnostics;
            }

            if (normalized.Equals("completed", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("complete", StringComparison.OrdinalIgnoreCase))
            {
                return ObjectLoadPhase.Completed;
            }

            if (normalized.Equals("failed", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("failure", StringComparison.OrdinalIgnoreCase))
            {
                return ObjectLoadPhase.Failed;
            }

            return ObjectLoadPhase.None;
        }

        private static string ToStageName(ObjectLoadPhase phase)
        {
            switch (phase)
            {
                case ObjectLoadPhase.ResolvingSource:
                    return "resolving_source";
                case ObjectLoadPhase.Downloading:
                    return "downloading";
                case ObjectLoadPhase.LoadingBundle:
                    return "loading_bundle";
                case ObjectLoadPhase.DiscoveringContent:
                    return "discovering_content";
                case ObjectLoadPhase.Instantiating:
                    return "instantiating";
                case ObjectLoadPhase.Diagnostics:
                    return "diagnostics";
                case ObjectLoadPhase.Completed:
                    return "completed";
                case ObjectLoadPhase.Failed:
                    return "failed";
                default:
                    return "none";
            }
        }
    }

    public sealed class ObjectLoadError
    {
        [JsonProperty("code")]
        public ObjectLoadErrorCode Code { get; private set; }

        [JsonProperty("message")]
        public string Message { get; private set; }

        [JsonProperty("request_url")]
        public string RequestUrl { get; private set; }

        [JsonProperty("http_status_code")]
        public long? HttpStatusCode { get; private set; }

        [JsonProperty("exception")]
        public string ExceptionMessage { get; private set; }

        public static ObjectLoadError Create(ObjectLoadErrorCode code,
                                            string message,
                                            string requestUrl = null,
                                            long? httpStatusCode = null,
                                            string exceptionMessage = null)
        {
            return new ObjectLoadError
            {
                Code = code,
                Message = string.IsNullOrWhiteSpace(message) ? "Object loading failed." : message,
                RequestUrl = requestUrl,
                HttpStatusCode = httpStatusCode,
                ExceptionMessage = exceptionMessage
            };
        }
    }

    public sealed class ObjectLoadResult
    {
        [JsonProperty("succeeded")]
        public bool Succeeded { get; private set; }

        [JsonProperty("message")]
        public string Message { get; private set; }

        [JsonProperty("error")]
        public ObjectLoadError Error { get; private set; }

        [JsonProperty("diagnostics")]
        public ObjectDiagnosticsReport Diagnostics { get; private set; }

        [JsonProperty("telemetry")]
        public ObjectLoadTelemetry Telemetry { get; private set; }

        [JsonIgnore]
        public IObjectLoadHandle Handle { get; private set; }

        public static ObjectLoadResult Success(string message,
                                              IObjectLoadHandle handle,
                                              ObjectDiagnosticsReport diagnostics = null,
                                              ObjectLoadTelemetry telemetry = null)
        {
            return new ObjectLoadResult
            {
                Succeeded = true,
                Message = string.IsNullOrWhiteSpace(message) ? "Object loaded." : message,
                Handle = handle,
                Diagnostics = diagnostics ?? ObjectDiagnosticsReport.Empty(),
                Telemetry = telemetry ?? ObjectLoadTelemetry.Empty()
            };
        }

        public static ObjectLoadResult Failure(string message)
        {
            return Failure(ObjectLoadError.Create(ObjectLoadErrorCode.Unknown, message));
        }

        public static ObjectLoadResult Failure(ObjectLoadError error)
        {
            return new ObjectLoadResult
            {
                Succeeded = false,
                Message = error != null ? error.Message : "Object loading failed.",
                Error = error ?? ObjectLoadError.Create(ObjectLoadErrorCode.Unknown, "Object loading failed."),
                Diagnostics = ObjectDiagnosticsReport.Empty(),
                Telemetry = ObjectLoadTelemetry.Empty()
            };
        }
    }

    public sealed class ObjectLoadTelemetry
    {
        [JsonProperty("load_strategy")]
        public string LoadStrategy { get; set; }

        [JsonProperty("download_time_ms")]
        public long DownloadTimeMs { get; set; }

        [JsonProperty("bundle_load_time_ms")]
        public long BundleLoadTimeMs { get; set; }

        [JsonProperty("instantiate_time_ms")]
        public long InstantiateTimeMs { get; set; }

        [JsonProperty("total_time_ms")]
        public long TotalTimeMs { get; set; }

        [JsonProperty("bytes_received")]
        public long BytesReceived { get; set; }

        [JsonProperty("asset_count")]
        public int AssetCount { get; set; }

        [JsonProperty("scene_count")]
        public int SceneCount { get; set; }

        [JsonProperty("renderer_count")]
        public int RendererCount { get; set; }

        [JsonProperty("material_count")]
        public int MaterialCount { get; set; }

        [JsonProperty("missing_shader_material_count")]
        public int MissingShaderMaterialCount { get; set; }

        [JsonProperty("pink_material_count")]
        public int PinkMaterialCount { get; set; }

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

        [JsonProperty("cache_status")]
        public string CacheStatus { get; set; }

        public static ObjectLoadTelemetry Empty()
        {
            return new ObjectLoadTelemetry();
        }
    }

    public sealed class ObjectSourceResolveResult
    {
        public bool Succeeded { get; private set; }
        public ObjectSource Source { get; private set; }
        public ObjectLoadError Error { get; private set; }

        public static ObjectSourceResolveResult Success(ObjectSource source)
        {
            return new ObjectSourceResolveResult
            {
                Succeeded = true,
                Source = source
            };
        }

        public static ObjectSourceResolveResult Failure(ObjectLoadError error)
        {
            return new ObjectSourceResolveResult
            {
                Succeeded = false,
                Error = error
            };
        }
    }

    public sealed class ObjectDownloadResult
    {
        public bool Succeeded { get; private set; }
        public byte[] Bytes { get; private set; }
        public long HttpStatusCode { get; private set; }
        public Dictionary<string, string> ResponseHeaders { get; private set; }
        public ObjectLoadError Error { get; private set; }

        public static ObjectDownloadResult Success(byte[] bytes,
                                                  long httpStatusCode,
                                                  Dictionary<string, string> responseHeaders)
        {
            return new ObjectDownloadResult
            {
                Succeeded = true,
                Bytes = bytes,
                HttpStatusCode = httpStatusCode,
                ResponseHeaders = responseHeaders ?? new Dictionary<string, string>()
            };
        }

        public static ObjectDownloadResult Failure(ObjectLoadError error)
        {
            return new ObjectDownloadResult
            {
                Succeeded = false,
                Error = error
            };
        }
    }

    public sealed class ObjectContentLoadResult
    {
        public bool Succeeded { get; private set; }
        public AssetBundleContent Content { get; private set; }
        public ObjectLoadTelemetry Telemetry { get; private set; }
        public ObjectLoadError Error { get; private set; }

        public static ObjectContentLoadResult Success(AssetBundleContent content,
                                                      ObjectLoadTelemetry telemetry = null)
        {
            return new ObjectContentLoadResult
            {
                Succeeded = true,
                Content = content,
                Telemetry = telemetry ?? ObjectLoadTelemetry.Empty()
            };
        }

        public static ObjectContentLoadResult Failure(ObjectLoadError error)
        {
            return new ObjectContentLoadResult
            {
                Succeeded = false,
                Error = error
            };
        }
    }

    public sealed class ObjectInstantiationResult
    {
        public bool Succeeded { get; private set; }
        public IObjectLoadHandle Handle { get; private set; }
        public string Message { get; private set; }
        public ObjectLoadError Error { get; private set; }

        public static ObjectInstantiationResult Success(IObjectLoadHandle handle, string message)
        {
            return new ObjectInstantiationResult
            {
                Succeeded = true,
                Handle = handle,
                Message = message
            };
        }

        public static ObjectInstantiationResult Failure(ObjectLoadError error)
        {
            return new ObjectInstantiationResult
            {
                Succeeded = false,
                Error = error
            };
        }
    }
}
