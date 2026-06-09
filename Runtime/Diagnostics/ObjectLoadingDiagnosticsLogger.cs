using System.Collections.Generic;
using UnityEngine;

namespace JorisHoef.ObjectLoading
{
    public static class ObjectLoadingDiagnosticsLogger
    {
        public static void LogSnapshot(ObjectLoadingDiagnosticsSnapshot snapshot)
        {
            if (snapshot == null)
            {
                Debug.LogWarning("[ObjectLoading] Diagnostics snapshot is missing.");
                return;
            }

            ObjectLoadTelemetry telemetry = snapshot.Telemetry;
            Debug.Log(
                "[ObjectLoading] Diagnostics snapshot: " +
                "display_name=" + FormatValue(snapshot.DisplayName) +
                ", source_type=" + FormatValue(snapshot.SourceType) +
                ", succeeded=" + (snapshot.Succeeded.HasValue ? snapshot.Succeeded.Value.ToString() : "pending") +
                ", phase=" + snapshot.Phase +
                ", stage=" + FormatValue(snapshot.Stage) +
                ", progress=" + snapshot.Progress.ToString("0.00") +
                ", message=" + FormatValue(snapshot.Message) +
                ", load_strategy=" + FormatValue(telemetry?.LoadStrategy) +
                ", download_ms=" + (telemetry?.DownloadTimeMs ?? 0) +
                ", bundle_load_ms=" + (telemetry?.BundleLoadTimeMs ?? 0) +
                ", instantiate_ms=" + (telemetry?.InstantiateTimeMs ?? 0) +
                ", total_ms=" + (telemetry?.TotalTimeMs ?? 0) +
                ", bytes_received=" + (telemetry?.BytesReceived ?? 0) +
                ", assets=" + (telemetry?.AssetCount ?? 0) +
                ", scenes=" + (telemetry?.SceneCount ?? 0) +
                ", renderers=" + (telemetry?.RendererCount ?? 0) +
                ", materials=" + (telemetry?.MaterialCount ?? 0) +
                ", missing_shader_materials=" + (telemetry?.MissingShaderMaterialCount ?? 0) +
                ", pink_materials=" + (telemetry?.PinkMaterialCount ?? 0) +
                ", cache_mode=" + (telemetry != null ? telemetry.CacheMode.ToString() : "Default") +
                ", cache_status=" + FormatValue(telemetry?.CacheStatus));

            if (snapshot.Diagnostics != null)
            {
                LogDiagnostics(snapshot.Diagnostics);
            }

            if (snapshot.Error != null)
            {
                Debug.LogWarning(
                    "[ObjectLoading] Load error: " +
                    "code=" + snapshot.Error.Code +
                    ", http_status_code=" + (snapshot.Error.HttpStatusCode.HasValue ? snapshot.Error.HttpStatusCode.Value.ToString() : "none") +
                    ", message=" + FormatValue(snapshot.Error.Message));
            }
        }

        public static void LogResult(ObjectLoadRequest request, ObjectLoadResult result)
        {
            if (result == null)
            {
                Debug.LogWarning("[ObjectLoading] Load finished without a result.");
                return;
            }

            ObjectLoadTelemetry telemetry = result.Telemetry;
            string sourceType = request?.Source != null ? request.Source.Type.ToString() : "Unknown";
            string displayName = string.IsNullOrWhiteSpace(request?.DisplayName)
                ? "object"
                : request.DisplayName;

            Debug.Log(
                "[ObjectLoading] Load result: " +
                "display_name=" + displayName +
                ", source_type=" + sourceType +
                ", succeeded=" + result.Succeeded +
                ", message=" + FormatValue(result.Message) +
                ", load_strategy=" + FormatValue(telemetry?.LoadStrategy) +
                ", download_ms=" + (telemetry?.DownloadTimeMs ?? 0) +
                ", bundle_load_ms=" + (telemetry?.BundleLoadTimeMs ?? 0) +
                ", instantiate_ms=" + (telemetry?.InstantiateTimeMs ?? 0) +
                ", total_ms=" + (telemetry?.TotalTimeMs ?? 0) +
                ", bytes_received=" + (telemetry?.BytesReceived ?? 0) +
                ", assets=" + (telemetry?.AssetCount ?? 0) +
                ", scenes=" + (telemetry?.SceneCount ?? 0) +
                ", renderers=" + (telemetry?.RendererCount ?? 0) +
                ", materials=" + (telemetry?.MaterialCount ?? 0) +
                ", missing_shader_materials=" + (telemetry?.MissingShaderMaterialCount ?? 0) +
                ", pink_materials=" + (telemetry?.PinkMaterialCount ?? 0) +
                ", cache_mode=" + (telemetry != null ? telemetry.CacheMode.ToString() : "Default") +
                ", cache_status=" + FormatValue(telemetry?.CacheStatus));

            if (result.Diagnostics != null)
            {
                LogDiagnostics(result.Diagnostics);
            }

            if (!result.Succeeded && result.Error != null)
            {
                Debug.LogWarning(
                    "[ObjectLoading] Load error: " +
                    "code=" + result.Error.Code +
                    ", http_status_code=" + (result.Error.HttpStatusCode.HasValue ? result.Error.HttpStatusCode.Value.ToString() : "none") +
                    ", message=" + FormatValue(result.Error.Message));
            }
        }

        public static void LogDiagnostics(ObjectDiagnosticsReport diagnostics)
        {
            if (diagnostics == null)
            {
                return;
            }

            Debug.Log(
                "[ObjectLoading] Diagnostics: " +
                "assets=" + diagnostics.AssetNames.Count +
                ", scenes=" + diagnostics.SceneNames.Count +
                ", renderers=" + diagnostics.RendererCount +
                ", materials=" + diagnostics.MaterialCount +
                ", missing_shader_materials=" + diagnostics.MissingShaderMaterialCount +
                ", pink_materials=" + diagnostics.PinkMaterialCount +
                ", render_pipeline=" + FormatValue(diagnostics.RenderPipeline) +
                ", shaders=" + FormatList(diagnostics.ShaderNames) +
                ", warnings=" + FormatList(diagnostics.Warnings));
        }

        private static string FormatList(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return "none";
            }

            return string.Join("; ", values);
        }

        private static string FormatValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "none" : value;
        }
    }
}
