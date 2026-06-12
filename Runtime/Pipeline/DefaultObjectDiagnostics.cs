using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Deucarian.ObjectLoading
{
    public sealed class DefaultObjectDiagnostics : IObjectDiagnostics
    {
        public ObjectDiagnosticsReport CreateReport(IObjectLoadHandle handle, AssetBundleContent content)
        {
            ObjectDiagnosticsReport report = new ObjectDiagnosticsReport();
            report.RenderPipeline = GetRenderPipelineName();

            if (content != null)
            {
                report.AssetNames.AddRange(content.AssetNames ?? new string[0]);
                report.SceneNames.AddRange(content.ScenePaths ?? new string[0]);
            }

            HashSet<string> shaderNames = new HashSet<string>();
            if (handle != null)
            {
                IReadOnlyList<GameObject> roots = handle.InstantiatedObjects;
                for (int i = 0; i < roots.Count; i++)
                {
                    AddRendererDiagnostics(roots[i], report, shaderNames);
                }
            }

            report.ShaderNames.AddRange(shaderNames);
            report.ShaderNames.Sort();
            AddWarnings(report);
            return report;
        }

        private static void AddRendererDiagnostics(GameObject root,
                                                   ObjectDiagnosticsReport report,
                                                   HashSet<string> shaderNames)
        {
            if (root == null)
            {
                return;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            report.RendererCount += renderers.Length;

            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = renderers[i].sharedMaterials;
                if (materials == null)
                {
                    continue;
                }

                for (int j = 0; j < materials.Length; j++)
                {
                    Material material = materials[j];
                    report.MaterialCount++;
                    if (material == null)
                    {
                        report.MissingShaderMaterialCount++;
                        continue;
                    }

                    Shader shader = material.shader;
                    if (shader == null)
                    {
                        report.MissingShaderMaterialCount++;
                    }
                    else
                    {
                        shaderNames.Add(shader.name);
                        if (IsMissingShader(shader))
                        {
                            report.MissingShaderMaterialCount++;
                        }
                    }

                    if (LooksPink(material))
                    {
                        report.PinkMaterialCount++;
                    }
                }
            }
        }

        private static void AddWarnings(ObjectDiagnosticsReport report)
        {
            if (report.AssetNames.Count == 0 && report.SceneNames.Count == 0)
            {
                report.Warnings.Add("AssetBundle did not report asset or scene names. It may be built for the wrong platform or stripped unexpectedly.");
            }

            if (report.RendererCount == 0)
            {
                report.Warnings.Add("No renderers were found under the instantiated content.");
            }

            if (report.MissingShaderMaterialCount > 0)
            {
                report.Warnings.Add("One or more materials use missing or error shaders. Check that the bundle was built for the active render pipeline and includes required shaders.");
            }

            if (report.PinkMaterialCount > 0)
            {
                report.Warnings.Add("One or more materials appear magenta/pink, which usually indicates missing shaders or incompatible render pipeline materials.");
            }

            bool builtIn = string.Equals(report.RenderPipeline, "Built-in Render Pipeline", System.StringComparison.OrdinalIgnoreCase);
            bool sawUrpShader = false;
            bool sawHdrpShader = false;
            bool sawStandardShader = false;
            for (int i = 0; i < report.ShaderNames.Count; i++)
            {
                string shader = report.ShaderNames[i];
                sawUrpShader |= shader.StartsWith("Universal Render Pipeline/", System.StringComparison.OrdinalIgnoreCase);
                sawHdrpShader |= shader.StartsWith("HDRP/", System.StringComparison.OrdinalIgnoreCase)
                                 || shader.StartsWith("High Definition Render Pipeline/", System.StringComparison.OrdinalIgnoreCase);
                sawStandardShader |= string.Equals(shader, "Standard", System.StringComparison.OrdinalIgnoreCase);
            }

            if (builtIn && (sawUrpShader || sawHdrpShader))
            {
                report.Warnings.Add("Bundle materials reference SRP shaders while the active project is using the built-in render pipeline.");
            }
            else if (!builtIn && sawStandardShader)
            {
                report.Warnings.Add("Bundle materials reference the built-in Standard shader while the active project is using a Scriptable Render Pipeline.");
            }
        }

        private static bool IsMissingShader(Shader shader)
        {
            return shader != null
                   && shader.name.IndexOf("InternalErrorShader", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool LooksPink(Material material)
        {
            Color color;
            if (TryGetMaterialColor(material, out color))
            {
                return color.r > 0.85f && color.b > 0.85f && color.g < 0.35f;
            }

            return false;
        }

        private static bool TryGetMaterialColor(Material material, out Color color)
        {
            color = default(Color);
            if (material == null)
            {
                return false;
            }

            if (material.HasProperty("_BaseColor"))
            {
                color = material.GetColor("_BaseColor");
                return true;
            }

            if (material.HasProperty("_Color"))
            {
                color = material.GetColor("_Color");
                return true;
            }

            return false;
        }

        private static string GetRenderPipelineName()
        {
            RenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline;
            return asset == null ? "Built-in Render Pipeline" : asset.GetType().Name;
        }
    }
}
