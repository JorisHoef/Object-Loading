# Deucarian Object Loading

`com.deucarian.object-loading` is a small Unity UPM package for loading AssetBundle-based object or scene content at runtime.

The package owns only the generic loading pipeline:

`ObjectLoadRequest -> source resolver -> source content loader -> instantiator -> diagnostics -> result/handle`

It is designed for callers that already know the final AssetBundle URL.

## What It Does

- Loads direct AssetBundle URLs, local AssetBundle files, or explicit raw AssetBundle bytes.
- Adds optional request headers and optional bearer token auth.
- Optionally appends a `platform` query, defaulting to `platform=webgl` unless overridden.
- Loads remote bundles with `UnityWebRequestAssetBundle.GetAssetBundle`.
- Loads local files with `AssetBundle.LoadFromFileAsync`.
- Keeps `AssetBundle.LoadFromMemoryAsync` available for explicit raw-byte workflows.
- Supports cache metadata for remote AssetBundle requests.
- Instantiates bundled scenes first by default, or prefab/GameObject assets when no scene is present.
- Applies optional parent, position, rotation, and scale to the loaded root container.
- Returns a cleanup handle that destroys instantiated GameObjects and unloads the AssetBundle.
- Reports diagnostics for assets, scenes, renderers, materials, shaders, and likely shader/material problems.
- Reports timing telemetry for download, bundle load, instantiation, total time, asset count, scene count, and cache status.
- Reports structured loading phases through `ObjectLoadRequest.Progress`.
- Provides an optional runtime diagnostics overlay and JSON copy/export for development builds.

## What It Does Not Do Yet

- No glTF loading.
- No Addressables integration.
- No backend project/object/version lookup.
- No API dependency in the core package.
- No material remapping, shader replacement, or render pipeline fixes.
- No ServiceLocator.
- No cache eviction policy or storage quota management.

Keep backend-specific source selection outside this core package. Pass the resolved URL, token, and headers into `ObjectLoadRequest`.

## Install

Add the package to a Unity project through Package Manager using this package folder or a Git URL, then import the optional sample from Package Manager.

The package depends on Unity's Newtonsoft Json package:

```json
"com.unity.nuget.newtonsoft-json": "3.2.1"
```

## Direct URL Loading

```csharp
using System.Collections;
using Deucarian.ObjectLoading;
using UnityEngine;

public sealed class ExampleLoader : MonoBehaviour
{
    private readonly ObjectLoadingPipeline _pipeline = new ObjectLoadingPipeline();
    private IObjectLoadHandle _handle;

    public IEnumerator Load(string assetBundleUrl, Transform parent)
    {
        ObjectLoadRequest request = ObjectLoadRequest.FromUrl(assetBundleUrl);
        request.Parent = parent;
        request.DisplayName = "Loaded object";
        request.LoadPreference = ObjectContentLoadPreference.Automatic;

        ObjectLoadResult result = null;
        yield return _pipeline.LoadAsync(request, value => result = value);

        if (result.Succeeded)
        {
            _handle = result.Handle;
            Debug.Log(result.Diagnostics.ToText());
        }
        else
        {
            Debug.LogError(result.Message);
        }
    }

    public void Unload()
    {
        _handle?.Unload();
        _handle = null;
    }
}
```

## Auth Headers

Use the bearer token convenience when the server expects `Authorization: Bearer ...`.

```csharp
ObjectLoadRequest request = ObjectLoadRequest.FromUrl(url);
request.BearerToken = accessToken;
```

Or pass explicit headers:

```csharp
request.AddHeader("Authorization", "Bearer " + accessToken);
request.AddHeader("X-Custom-Header", "value");
```

If both are supplied, the explicit `Authorization` header wins. `ToDebugSnapshotJson()` redacts bearer tokens and sensitive headers.

## Cache Metadata

Remote loads can request Unity AssetBundle caching when a stable version/hash is available:

```csharp
request.CacheMode = ObjectLoadCacheMode.UseUnityCache;
request.CacheKey = "project-832-model-497";
request.CacheHash = "0123456789abcdef0123456789abcdef";
request.Crc = 0;
```

If no cache metadata is supplied, remote URLs still use `UnityWebRequestAssetBundle` without forcing a managed `byte[]`.

## Platform Query

By default, direct URLs receive a `platform` query parameter when one is not already present:

```text
https://example.com/object.bundle -> https://example.com/object.bundle?platform=webgl
```

Override or disable this per request:

```csharp
request.PlatformOverride = "windows";
request.AppendPlatformQuery = false;
```

## Cleanup

The successful `ObjectLoadResult` includes an `IObjectLoadHandle`.

Calling `Unload()`:

- destroys instantiated root GameObjects,
- unloads the AssetBundle with `AssetBundle.Unload(false)`,
- is safe to call more than once.

`ObjectLoadingPipeline.UnloadLast()` is also available for simple callers that want the pipeline to track the latest successful handle.

## Diagnostics

`DefaultObjectDiagnostics` reports:

- loaded asset names,
- bundled scene paths,
- renderer count,
- material count,
- shader names,
- active render pipeline name,
- missing/error shader count,
- likely pink/magenta material count,
- warnings for common wrong platform or render pipeline symptoms.

Diagnostics report facts and warnings only. They do not change materials or shaders.

`ObjectLoadRequest.Progress` reports structured phases:

- `ResolvingSource`
- `Downloading`
- `LoadingBundle`
- `DiscoveringContent`
- `Instantiating`
- `Diagnostics`
- `Completed`
- `Failed`

Progress updates include normalized progress, elapsed milliseconds when available, bytes received, and the latest `ObjectLoadTelemetry` snapshot.

Use the optional runtime overlay when a project wants package-level developer visibility without building its own UI:

```csharp
ObjectLoadingDiagnosticsOverlay overlay = ObjectLoadingDiagnosticsOverlay.CreateIfEnabled(debugEnabled);
overlay?.Begin(request);
if (overlay != null)
{
    request.Progress = overlay.RecordProgress;
}
yield return pipeline.LoadAsync(request, result => overlay?.RecordResult(result));
```

The overlay shows only generic Object Loading state: phase, progress, elapsed time, download/bundle/instantiate timings, byte counts, asset/scene counts, renderer/material counts, and shader warning counts. It does not display request URLs, bearer tokens, backend IDs, or caller-specific API timings.

## Samples

Import `Direct URL AssetBundle Loader` from Package Manager. The sample scene provides:

- direct URL input,
- optional bearer token input,
- optional custom header input,
- load/unload buttons,
- status text,
- diagnostics output,
- reusable Object Loading diagnostics overlay with JSON copy.
