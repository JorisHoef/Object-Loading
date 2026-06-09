# Direct URL AssetBundle Loader Sample

Open `DirectUrlAssetBundleLoaderSample.unity`, enter a direct AssetBundle URL, optionally enter a bearer token or one custom header, then press Load.

The sample can show the reusable Object Loading diagnostics overlay, including progress, timings, counts, and copied diagnostics JSON.

The sample does not use API Helper. It passes the final URL and auth data directly into `ObjectLoadRequest`, displays progress/status, prints diagnostics, and unloads the returned handle when requested.
