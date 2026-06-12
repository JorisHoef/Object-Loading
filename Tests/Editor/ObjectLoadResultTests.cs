using NUnit.Framework;

namespace Deucarian.ObjectLoading.Tests
{
    public sealed class ObjectLoadResultTests
    {
        [Test]
        public void Failure_UsesProvidedErrorData()
        {
            ObjectLoadError error = ObjectLoadError.Create(
                ObjectLoadErrorCode.DownloadFailed,
                "Download failed.",
                "https://example.com/object.bundle",
                404);

            ObjectLoadResult result = ObjectLoadResult.Failure(error);

            Assert.False(result.Succeeded);
            Assert.AreEqual(ObjectLoadErrorCode.DownloadFailed, result.Error.Code);
            Assert.AreEqual(404, result.Error.HttpStatusCode);
            Assert.AreEqual("Download failed.", result.Message);
        }
    }
}
