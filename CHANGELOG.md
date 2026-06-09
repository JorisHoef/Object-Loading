# Changelog

## 0.4.0

- Added reusable runtime Object Loading diagnostics overlay.
- Added diagnostics JSON snapshot/copy support.
- Added package-level diagnostics logging helpers.
- Updated the direct URL sample to demonstrate the reusable overlay.

## 0.3.0

- Added structured load phases to progress callbacks.
- Added elapsed time and telemetry snapshots to progress updates.
- Added renderer, material, missing shader, and pink material counts to load telemetry.
- Added completed and failed progress phases for caller diagnostics overlays.

## 0.2.0

- Replaced the default byte-array AssetBundle path with source-centric loading.
- Added remote URL loading through `UnityWebRequestAssetBundle.GetAssetBundle`.
- Added local file loading through `AssetBundle.LoadFromFileAsync`.
- Preserved raw-byte loading through `AssetBundle.LoadFromMemoryAsync`.
- Added cache metadata fields to `ObjectLoadRequest`.
- Added timing and cache telemetry to `ObjectLoadResult`.

## 0.1.0

- Added initial UPM package metadata.
- Added direct URL AssetBundle loading pipeline.
- Added runtime request, result, error, progress, source, diagnostics, and handle types.
- Added source resolver, UnityWebRequest downloader, AssetBundle content loader, AssetBundle instantiator, diagnostics, and pipeline implementations.
- Added EditMode tests for pure request, URL, result, cleanup, and diagnostics behavior.
- Added direct URL loading sample.
