using NUnit.Framework;
using UnityEngine;

namespace Deucarian.ObjectLoading.Tests
{
    public sealed class ObjectLoadHandleTests
    {
        [Test]
        public void Unload_IsIdempotentAndDestroysInstantiatedObject()
        {
            GameObject loaded = new GameObject("Loaded");
            ObjectLoadHandle handle = new ObjectLoadHandle(loaded, null);

            handle.Unload();
            handle.Unload();

            Assert.True(handle.IsUnloaded);
            Assert.AreEqual(0, handle.InstantiatedObjects.Count);
            Assert.True(loaded == null);
        }
    }
}
