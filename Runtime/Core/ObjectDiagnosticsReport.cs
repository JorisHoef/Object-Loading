using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Deucarian.ObjectLoading
{
    public sealed class ObjectDiagnosticsReport
    {
        public ObjectDiagnosticsReport()
        {
            AssetNames = new List<string>();
            SceneNames = new List<string>();
            ShaderNames = new List<string>();
            Warnings = new List<string>();
        }

        [JsonProperty("asset_names")]
        public List<string> AssetNames { get; set; }

        [JsonProperty("scene_names")]
        public List<string> SceneNames { get; set; }

        [JsonProperty("renderer_count")]
        public int RendererCount { get; set; }

        [JsonProperty("material_count")]
        public int MaterialCount { get; set; }

        [JsonProperty("missing_shader_material_count")]
        public int MissingShaderMaterialCount { get; set; }

        [JsonProperty("pink_material_count")]
        public int PinkMaterialCount { get; set; }

        [JsonProperty("shader_names")]
        public List<string> ShaderNames { get; set; }

        [JsonProperty("render_pipeline")]
        public string RenderPipeline { get; set; }

        [JsonProperty("warnings")]
        public List<string> Warnings { get; set; }

        public static ObjectDiagnosticsReport Empty()
        {
            return new ObjectDiagnosticsReport();
        }

        public string ToText()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Assets: " + AssetNames.Count);
            for (int i = 0; i < AssetNames.Count; i++)
            {
                builder.AppendLine("- " + AssetNames[i]);
            }

            builder.AppendLine("Scenes: " + SceneNames.Count);
            for (int i = 0; i < SceneNames.Count; i++)
            {
                builder.AppendLine("- " + SceneNames[i]);
            }

            builder.AppendLine("Renderers: " + RendererCount);
            builder.AppendLine("Materials: " + MaterialCount);
            builder.AppendLine("Render pipeline: " + (string.IsNullOrWhiteSpace(RenderPipeline) ? "Unknown" : RenderPipeline));

            builder.AppendLine("Shaders: " + ShaderNames.Count);
            for (int i = 0; i < ShaderNames.Count; i++)
            {
                builder.AppendLine("- " + ShaderNames[i]);
            }

            if (Warnings.Count > 0)
            {
                builder.AppendLine("Warnings:");
                for (int i = 0; i < Warnings.Count; i++)
                {
                    builder.AppendLine("- " + Warnings[i]);
                }
            }

            return builder.ToString();
        }
    }
}
