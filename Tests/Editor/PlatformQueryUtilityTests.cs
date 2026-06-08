using System.Collections;
using NUnit.Framework;

namespace JorisHoef.ObjectLoading.Tests
{
    public sealed class PlatformQueryUtilityTests
    {
        [Test]
        public void AppendPlatformQuery_AppendsBeforeFragment()
        {
            string url = PlatformQueryUtility.AppendPlatformQuery(
                "https://example.com/object.bundle?foo=1#section",
                "webgl");

            Assert.AreEqual("https://example.com/object.bundle?foo=1&platform=webgl#section", url);
        }

        [Test]
        public void AppendPlatformQuery_DoesNotDuplicateExistingPlatformQuery()
        {
            string url = PlatformQueryUtility.AppendPlatformQuery(
                "https://example.com/object.bundle?platform=windows",
                "webgl");

            Assert.AreEqual("https://example.com/object.bundle?platform=windows", url);
        }

        [Test]
        public void DirectUrlResolver_AppendsRequestedPlatform()
        {
            ObjectLoadRequest request = ObjectLoadRequest.FromUrl("https://example.com/object.bundle");
            request.PlatformOverride = "webgl";
            DirectUrlSourceResolver resolver = new DirectUrlSourceResolver();
            ObjectSourceResolveResult result = null;

            Run(resolver.ResolveAsync(request, value => result = value));

            Assert.NotNull(result);
            Assert.True(result.Succeeded);
            Assert.AreEqual("https://example.com/object.bundle?platform=webgl", result.Source.Url);
        }

        private static void Run(IEnumerator enumerator)
        {
            while (enumerator.MoveNext())
            {
            }
        }
    }
}
