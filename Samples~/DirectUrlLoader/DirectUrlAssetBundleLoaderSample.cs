using System.Collections;
using Deucarian.ObjectLoading;
using UnityEngine;

public sealed class DirectUrlAssetBundleLoaderSample : MonoBehaviour
{
    [SerializeField] private string assetBundleUrl;
    [SerializeField] private string bearerToken;
    [SerializeField] private string headerName;
    [SerializeField] private string headerValue;
    [SerializeField] private ObjectContentLoadPreference loadPreference = ObjectContentLoadPreference.Automatic;

    private ObjectLoadingPipeline _pipeline;
    private IObjectLoadHandle _handle;
    private Coroutine _loadRoutine;
    private Transform _objectRoot;
    private Vector2 _diagnosticsScroll;
    private string _status = "Idle";
    private string _diagnostics = string.Empty;

    private void Awake()
    {
        _pipeline = new ObjectLoadingPipeline();
        EnsureSceneObjects();
    }

    private void OnDestroy()
    {
        if (_loadRoutine != null)
        {
            StopCoroutine(_loadRoutine);
            _loadRoutine = null;
        }

        Unload();
    }

    private void OnGUI()
    {
        float width = Mathf.Min(560f, Screen.width - 32f);
        GUILayout.BeginArea(new Rect(16f, 16f, width, Screen.height - 32f), GUI.skin.box);

        GUILayout.Label("Direct AssetBundle URL");
        assetBundleUrl = GUILayout.TextField(assetBundleUrl ?? string.Empty);

        GUILayout.Label("Bearer token");
        bearerToken = GUILayout.PasswordField(bearerToken ?? string.Empty, '*');

        GUILayout.Label("Optional header");
        GUILayout.BeginHorizontal();
        headerName = GUILayout.TextField(headerName ?? string.Empty, GUILayout.Width(width * 0.35f));
        headerValue = GUILayout.TextField(headerValue ?? string.Empty);
        GUILayout.EndHorizontal();

        GUILayout.Label("Load preference");
        loadPreference = (ObjectContentLoadPreference)GUILayout.SelectionGrid(
            (int)loadPreference,
            new[] { "Automatic", "Scene first", "Prefab first" },
            3);

        GUILayout.BeginHorizontal();
        GUI.enabled = _loadRoutine == null;
        if (GUILayout.Button("Load", GUILayout.Height(32f)))
        {
            StartLoad();
        }

        GUI.enabled = _handle != null || _loadRoutine != null;
        if (GUILayout.Button("Unload", GUILayout.Height(32f)))
        {
            if (_loadRoutine != null)
            {
                StopCoroutine(_loadRoutine);
                _loadRoutine = null;
                _status = "Load stopped.";
            }

            Unload();
        }

        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        GUILayout.Label("Status");
        GUILayout.TextArea(_status ?? string.Empty, GUILayout.Height(56f));

        GUILayout.Label("Diagnostics");
        _diagnosticsScroll = GUILayout.BeginScrollView(_diagnosticsScroll);
        GUILayout.TextArea(_diagnostics ?? string.Empty, GUILayout.ExpandHeight(true));
        GUILayout.EndScrollView();

        GUILayout.EndArea();
    }

    private void StartLoad()
    {
        if (_loadRoutine != null)
        {
            StopCoroutine(_loadRoutine);
        }

        _loadRoutine = StartCoroutine(LoadRoutine());
    }

    private IEnumerator LoadRoutine()
    {
        Unload();
        _status = "Starting load.";
        _diagnostics = string.Empty;

        if (string.IsNullOrWhiteSpace(assetBundleUrl))
        {
            _status = "Enter a direct AssetBundle URL.";
            _loadRoutine = null;
            yield break;
        }

        ObjectLoadRequest request = ObjectLoadRequest.FromUrl(assetBundleUrl);
        request.Parent = _objectRoot;
        request.DisplayName = "Sample Loaded Object";
        request.BearerToken = bearerToken;
        request.LoadPreference = loadPreference;
        request.Progress = progress =>
        {
            int percent = Mathf.RoundToInt(progress.Normalized * 100f);
            _status = progress.Stage + " " + percent + "% - " + progress.Message;
        };

        if (!string.IsNullOrWhiteSpace(headerName))
        {
            request.AddHeader(headerName, headerValue);
        }

        ObjectLoadResult result = null;
        yield return _pipeline.LoadAsync(request, value => result = value);

        if (result != null && result.Succeeded)
        {
            _handle = result.Handle;
            _status = result.Message;
            _diagnostics = result.Diagnostics != null ? result.Diagnostics.ToText() : string.Empty;
        }
        else
        {
            _status = result != null ? result.Message : "Object load finished without a result.";
            _diagnostics = result?.Error != null
                ? Newtonsoft.Json.JsonConvert.SerializeObject(result.Error, Newtonsoft.Json.Formatting.Indented)
                : string.Empty;
        }

        _loadRoutine = null;
    }

    private void Unload()
    {
        if (_handle != null)
        {
            _handle.Unload();
            _handle = null;
        }

        if (_pipeline != null)
        {
            _pipeline.UnloadLast();
        }

        _diagnostics = string.Empty;
    }

    private void EnsureSceneObjects()
    {
        if (_objectRoot == null)
        {
            GameObject root = new GameObject("Loaded Object Root");
            _objectRoot = root.transform;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.1f, 0.12f, 1f);
        }

        camera.transform.position = new Vector3(4f, 3f, -6f);
        camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1f, 0f) - camera.transform.position);

        if (Object.FindObjectOfType<Light>() == null)
        {
            GameObject lightGo = new GameObject("Sample Key Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
        }
    }
}
