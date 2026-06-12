using System;
using UnityEngine;

namespace Deucarian.ObjectLoading
{
    public sealed class AssetBundleContent : IDisposable
    {
        private AssetBundle _bundle;

        public AssetBundleContent(AssetBundle bundle, string[] assetNames, string[] scenePaths)
        {
            _bundle = bundle;
            AssetNames = assetNames ?? new string[0];
            ScenePaths = scenePaths ?? new string[0];
        }

        public AssetBundle Bundle
        {
            get { return _bundle; }
        }

        public string[] AssetNames { get; private set; }

        public string[] ScenePaths { get; private set; }

        public bool IsUnloaded { get; private set; }

        public void Dispose()
        {
            Unload(false);
        }

        public void Unload(bool unloadAllLoadedObjects)
        {
            if (IsUnloaded)
            {
                return;
            }

            if (_bundle != null)
            {
                _bundle.Unload(unloadAllLoadedObjects);
                _bundle = null;
            }

            IsUnloaded = true;
        }
    }
}
