using NUnit.Framework;

namespace JorisHoef.ObjectLoading.Tests
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
    }
}
