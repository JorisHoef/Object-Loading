using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Deucarian.ObjectLoading
{
    public sealed class AssetBundleObjectInstantiator : IObjectInstantiator
    {
        public IEnumerator InstantiateAsync(AssetBundleContent content,
                                            ObjectLoadRequest request,
                                            Action<ObjectInstantiationResult> onCompleted)
        {
            if (content == null || content.Bundle == null)
            {
                onCompleted?.Invoke(ObjectInstantiationResult.Failure(ObjectLoadError.Create(
                    ObjectLoadErrorCode.ContentLoadFailed,
                    "AssetBundle content is missing.")));
                yield break;
            }

            ObjectContentLoadPreference preference = request != null
                ? request.LoadPreference
                : ObjectContentLoadPreference.Automatic;

            bool prefabFirst = preference == ObjectContentLoadPreference.PrefabFirst;
            if (prefabFirst)
            {
                ObjectInstantiationResult prefabResult = null;
                yield return InstantiatePrefabs(content, request, value => prefabResult = value);
                if (prefabResult != null && prefabResult.Succeeded)
                {
                    onCompleted?.Invoke(prefabResult);
                    yield break;
                }

                if (content.ScenePaths.Length > 0)
                {
                    yield return InstantiateScene(content, request, onCompleted);
                    yield break;
                }

                onCompleted?.Invoke(prefabResult ?? CreateNoContentResult());
                yield break;
            }

            if (content.ScenePaths.Length > 0)
            {
                yield return InstantiateScene(content, request, onCompleted);
                yield break;
            }

            yield return InstantiatePrefabs(content, request, onCompleted);
        }

        private static IEnumerator InstantiatePrefabs(AssetBundleContent content,
                                                      ObjectLoadRequest request,
                                                      Action<ObjectInstantiationResult> onCompleted)
        {
            request?.ReportProgress("instantiate", 0f, "Loading GameObject assets from AssetBundle.");
            AssetBundleRequest assetRequest = content.Bundle.LoadAllAssetsAsync<GameObject>();
            yield return assetRequest;

            List<GameObject> prefabs = new List<GameObject>();
            UnityEngine.Object[] assets = assetRequest.allAssets ?? new UnityEngine.Object[0];
            for (int i = 0; i < assets.Length; i++)
            {
                GameObject prefab = assets[i] as GameObject;
                if (prefab != null)
                {
                    prefabs.Add(prefab);
                }
            }

            if (prefabs.Count == 0)
            {
                onCompleted?.Invoke(CreateNoContentResult());
                yield break;
            }

            GameObject root = CreateContainer(request);
            for (int i = 0; i < prefabs.Count; i++)
            {
                GameObject instance = UnityEngine.Object.Instantiate(prefabs[i], root.transform);
                instance.name = prefabs[i].name;
            }

            request?.ReportProgress("instantiate", 1f, "Instantiated AssetBundle GameObject assets.");
            onCompleted?.Invoke(ObjectInstantiationResult.Success(
                new ObjectLoadHandle(root, content.Bundle),
                "Ready with AssetBundle object from " + prefabs.Count + " GameObject asset(s)."));
        }

        private static IEnumerator InstantiateScene(AssetBundleContent content,
                                                    ObjectLoadRequest request,
                                                    Action<ObjectInstantiationResult> onCompleted)
        {
            string scenePath = content.ScenePaths[0];
            request?.ReportProgress("instantiate", 0f, "Loading bundled scene.");

            AsyncOperation operation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            if (operation == null)
            {
                onCompleted?.Invoke(ObjectInstantiationResult.Failure(ObjectLoadError.Create(
                    ObjectLoadErrorCode.InstantiationFailed,
                    "Could not start loading bundled scene: " + scenePath)));
                yield break;
            }

            yield return operation;

            Scene loadedScene = SceneManager.GetSceneByPath(scenePath);
            if (!loadedScene.IsValid())
            {
                loadedScene = SceneManager.GetSceneByName(Path.GetFileNameWithoutExtension(scenePath));
            }

            if (!loadedScene.IsValid() || !loadedScene.isLoaded)
            {
                onCompleted?.Invoke(ObjectInstantiationResult.Failure(ObjectLoadError.Create(
                    ObjectLoadErrorCode.InstantiationFailed,
                    "Bundled scene loaded but could not be found: " + scenePath)));
                yield break;
            }

            GameObject[] sceneRoots = loadedScene.GetRootGameObjects();
            if (sceneRoots == null || sceneRoots.Length == 0)
            {
                AsyncOperation unloadEmptyScene = SceneManager.UnloadSceneAsync(loadedScene);
                if (unloadEmptyScene != null)
                {
                    yield return unloadEmptyScene;
                }

                onCompleted?.Invoke(ObjectInstantiationResult.Failure(ObjectLoadError.Create(
                    ObjectLoadErrorCode.InstantiationFailed,
                    "Bundled scene contained no root GameObjects.")));
                yield break;
            }

            GameObject root = CreateContainer(request);
            Scene targetScene = root.scene;
            for (int i = 0; i < sceneRoots.Length; i++)
            {
                SceneManager.MoveGameObjectToScene(sceneRoots[i], targetScene);
                sceneRoots[i].transform.SetParent(root.transform, true);
            }

            AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(loadedScene);
            if (unloadOperation != null)
            {
                yield return unloadOperation;
            }

            request?.ReportProgress("instantiate", 1f, "Instantiated bundled scene content.");
            onCompleted?.Invoke(ObjectInstantiationResult.Success(
                new ObjectLoadHandle(root, content.Bundle),
                "Ready with AssetBundle scene: " + Path.GetFileNameWithoutExtension(scenePath) + "."));
        }

        private static GameObject CreateContainer(ObjectLoadRequest request)
        {
            string name = !string.IsNullOrWhiteSpace(request?.DisplayName)
                ? request.DisplayName
                : "Loaded object";
            GameObject root = new GameObject(name);

            if (request?.Parent != null)
            {
                root.transform.SetParent(request.Parent, false);
                root.transform.localPosition = request.Position ?? Vector3.zero;
                root.transform.localRotation = request.Rotation ?? Quaternion.identity;
                root.transform.localScale = request.Scale ?? Vector3.one;
            }
            else
            {
                root.transform.position = request != null && request.Position.HasValue
                    ? request.Position.Value
                    : Vector3.zero;
                root.transform.rotation = request != null && request.Rotation.HasValue
                    ? request.Rotation.Value
                    : Quaternion.identity;
                root.transform.localScale = request != null && request.Scale.HasValue
                    ? request.Scale.Value
                    : Vector3.one;
            }

            return root;
        }

        private static ObjectInstantiationResult CreateNoContentResult()
        {
            return ObjectInstantiationResult.Failure(ObjectLoadError.Create(
                ObjectLoadErrorCode.InstantiationFailed,
                "AssetBundle did not contain scenes or GameObject assets that the loader can instantiate."));
        }
    }
}
