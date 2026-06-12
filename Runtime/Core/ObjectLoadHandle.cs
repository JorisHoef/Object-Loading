using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deucarian.ObjectLoading
{
    public interface IObjectLoadHandle : IDisposable
    {
        IReadOnlyList<GameObject> InstantiatedObjects { get; }
        AssetBundle Bundle { get; }
        bool IsUnloaded { get; }
        void Unload();
    }

    public sealed class ObjectLoadHandle : IObjectLoadHandle
    {
        private readonly List<GameObject> _instantiatedObjects;
        private AssetBundle _bundle;

        public ObjectLoadHandle(GameObject instantiatedObject, AssetBundle bundle)
            : this(instantiatedObject == null
                ? new List<GameObject>()
                : new List<GameObject> { instantiatedObject }, bundle)
        {
        }

        public ObjectLoadHandle(IEnumerable<GameObject> instantiatedObjects, AssetBundle bundle)
        {
            _instantiatedObjects = new List<GameObject>();
            if (instantiatedObjects != null)
            {
                foreach (GameObject instance in instantiatedObjects)
                {
                    if (instance != null)
                    {
                        _instantiatedObjects.Add(instance);
                    }
                }
            }

            _bundle = bundle;
        }

        public IReadOnlyList<GameObject> InstantiatedObjects
        {
            get { return _instantiatedObjects; }
        }

        public AssetBundle Bundle
        {
            get { return _bundle; }
        }

        public bool IsUnloaded { get; private set; }

        public void Dispose()
        {
            Unload();
        }

        public void Unload()
        {
            if (IsUnloaded)
            {
                return;
            }

            for (int i = _instantiatedObjects.Count - 1; i >= 0; i--)
            {
                UnityObjectUtility.Destroy(_instantiatedObjects[i]);
            }

            _instantiatedObjects.Clear();

            if (_bundle != null)
            {
                _bundle.Unload(false);
                _bundle = null;
            }

            IsUnloaded = true;
        }
    }
}
