using NUnit.Framework;

namespace Deucarian.ObjectLoading.Tests
{
    public sealed class ObjectLoadRequestTests
    {
        [Test]
        public void CreateHeaders_AddsBearerTokenWithoutDuplicatingPrefix()
        {
            ObjectLoadRequest request = ObjectLoadRequest.FromUrl("https://example.com/object.bundle");
            request.BearerToken = "Bearer secret-token";
            request.AddHeader("X-Test", "1");

            System.Collections.Generic.Dictionary<string, string> headers = request.CreateHeaders();

            Assert.AreEqual("1", headers["X-Test"]);
            Assert.AreEqual("Bearer secret-token", headers["Authorization"]);
        }

        [Test]
        public void CreateHeaders_ExplicitAuthorizationWinsOverBearerConvenience()
        {
            ObjectLoadRequest request = ObjectLoadRequest.FromUrl("https://example.com/object.bundle");
            request.BearerToken = "secret-token";
            request.AddHeader("Authorization", "Bearer caller-token");

            System.Collections.Generic.Dictionary<string, string> headers = request.CreateHeaders();

            Assert.AreEqual("Bearer caller-token", headers["Authorization"]);
        }

        [Test]
        public void DebugSnapshot_RedactsBearerTokenAndSensitiveHeaders()
        {
            ObjectLoadRequest request = ObjectLoadRequest.FromUrl("https://example.com/object.bundle");
            request.BearerToken = "super-secret";
            request.AddHeader("X-Access-Token", "another-secret");
            request.AddHeader("X-Visible", "safe");

            string json = request.ToDebugSnapshotJson();

            Assert.False(json.Contains("super-secret"));
            Assert.False(json.Contains("another-secret"));
            Assert.True(json.Contains("[redacted]"));
            Assert.True(json.Contains("safe"));
        }

        [Test]
        public void DebugSnapshot_IncludesCacheMetadata()
        {
            ObjectLoadRequest request = ObjectLoadRequest.FromUrl("https://example.com/object.bundle");
            request.CacheMode = ObjectLoadCacheMode.UseUnityCache;
            request.CacheKey = "example-key";
            request.CacheHash = "0123456789abcdef0123456789abcdef";
            request.CacheVersion = 7;
            request.Crc = 42;

            ObjectLoadRequestDebugSnapshot snapshot = request.CreateDebugSnapshot();

            Assert.AreEqual(ObjectLoadCacheMode.UseUnityCache, snapshot.CacheMode);
            Assert.AreEqual("example-key", snapshot.CacheKey);
            Assert.AreEqual("0123456789abcdef0123456789abcdef", snapshot.CacheHash);
            Assert.AreEqual(7, snapshot.CacheVersion);
            Assert.AreEqual(42, snapshot.Crc);
        }

        [Test]
        public void ReportProgress_IncludesStructuredPhaseTelemetryAndElapsedTime()
        {
            ObjectLoadTelemetry telemetry = new ObjectLoadTelemetry
            {
                DownloadTimeMs = 12,
                BytesReceived = 345
            };
            ObjectLoadRequest request = ObjectLoadRequest.FromUrl("https://example.com/object.bundle");

            ObjectLoadProgress progress = null;
            request.Progress = value => progress = value;

            request.ReportProgress(ObjectLoadPhase.Downloading, 0.5f, "Downloading.", 345, 67, telemetry);

            Assert.NotNull(progress);
            Assert.AreEqual(ObjectLoadPhase.Downloading, progress.Phase);
            Assert.AreEqual("downloading", progress.Stage);
            Assert.AreEqual(0.5f, progress.Normalized);
            Assert.AreEqual(345, progress.BytesReceived);
            Assert.AreEqual(67, progress.ElapsedMs);
            Assert.AreSame(telemetry, progress.Telemetry);
        }

        [Test]
        public void ProgressCreate_MapsLegacyStageNamesToStructuredPhases()
        {
            ObjectLoadProgress progress = ObjectLoadProgress.Create("instantiate", 1f, "Done.");

            Assert.AreEqual(ObjectLoadPhase.Instantiating, progress.Phase);
            Assert.AreEqual("instantiate", progress.Stage);
            Assert.AreEqual(1f, progress.Normalized);
        }
    }
}
