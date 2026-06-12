using Newtonsoft.Json;

namespace Deucarian.ObjectLoading
{
    public sealed class ObjectLoadingDiagnosticsSnapshot
    {
        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("source_type")]
        public string SourceType { get; set; }

        [JsonProperty("phase")]
        public ObjectLoadPhase Phase { get; set; }

        [JsonProperty("stage")]
        public string Stage { get; set; }

        [JsonProperty("progress")]
        public float Progress { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("elapsed_ms")]
        public long ElapsedMs { get; set; }

        [JsonProperty("succeeded")]
        public bool? Succeeded { get; set; }

        [JsonProperty("telemetry")]
        public ObjectLoadTelemetry Telemetry { get; set; }

        [JsonProperty("diagnostics")]
        public ObjectDiagnosticsReport Diagnostics { get; set; }

        [JsonProperty("error")]
        public ObjectLoadError Error { get; set; }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}
