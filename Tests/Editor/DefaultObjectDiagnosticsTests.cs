using NUnit.Framework;
using UnityEngine;

namespace JorisHoef.ObjectLoading.Tests
{
    public sealed class DefaultObjectDiagnosticsTests
    {
        [Test]
        public void CreateReport_CountsRenderersMaterialsAndAssets()
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ObjectLoadHandle handle = new ObjectLoadHandle(cube, null);
            AssetBundleContent content = new AssetBundleContent(
                null,
                new[] { "assets/object.prefab" },
                new string[0]);

            ObjectDiagnosticsReport report = new DefaultObjectDiagnostics().CreateReport(handle, content);

            Assert.AreEqual(1, report.AssetNames.Count);
            Assert.GreaterOrEqual(report.RendererCount, 1);
            Assert.GreaterOrEqual(report.MaterialCount, 1);
            Assert.GreaterOrEqual(report.ShaderNames.Count, 1);

            handle.Unload();
        }
    }
}
