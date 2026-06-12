using System.Collections;
using NUnit.Framework;

namespace Deucarian.ObjectLoading.Tests
{
    public sealed class ObjectSourceResolverTests
    {
        [Test]
        public void Resolver_PassesLocalFileWithoutPlatformQuery()
        {
            ObjectLoadRequest request = new ObjectLoadRequest
            {
                Source = ObjectSource.LocalFile("C:/Bundles/model.bundle"),
                PlatformOverride = "webgl"
            };
            ObjectSourceResolveResult result = Resolve(request);

            Assert.NotNull(result);
            Assert.True(result.Succeeded);
            Assert.AreEqual(ObjectSourceType.LocalFile, result.Source.Type);
            Assert.AreEqual("C:/Bundles/model.bundle", result.Source.Path);
        }

        [Test]
        public void Resolver_PassesRawBytesWithoutPlatformQuery()
        {
            byte[] bytes = { 1, 2, 3 };
            ObjectLoadRequest request = new ObjectLoadRequest
            {
                Source = ObjectSource.RawBytes(bytes),
                PlatformOverride = "webgl"
            };
            ObjectSourceResolveResult result = Resolve(request);

            Assert.NotNull(result);
            Assert.True(result.Succeeded);
            Assert.AreEqual(ObjectSourceType.RawBytes, result.Source.Type);
            Assert.AreSame(bytes, result.Source.Bytes);
        }

        private static ObjectSourceResolveResult Resolve(ObjectLoadRequest request)
        {
            DirectUrlSourceResolver resolver = new DirectUrlSourceResolver();
            ObjectSourceResolveResult result = null;
            Run(resolver.ResolveAsync(request, value => result = value));
            return result;
        }

        private static void Run(IEnumerator enumerator)
        {
            while (enumerator.MoveNext())
            {
            }
        }
    }
}
