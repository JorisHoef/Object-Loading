using Newtonsoft.Json;
using UnityEngine;

namespace Deucarian.ObjectLoading
{
    public sealed class ObjectLoadingDiagnosticsOverlay : MonoBehaviour
    {
        private const string GameObjectName = "ObjectLoadingDiagnosticsOverlay";
        private const float PanelMargin = 24f;
        private const float PanelWidth = 460f;
        private const float PanelHeight = 330f;

        [SerializeField] private bool visible = true;
        [SerializeField] private bool logToConsole = true;

        private GUIStyle _panelStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _mutedStyle;
        private double _startedAt = -1d;
        private ObjectLoadPhase _phase = ObjectLoadPhase.None;
        private string _stage = "none";
        private string _message = "No object load has started.";
        private string _displayName = "object";
        private string _sourceType = "Unknown";
        private float _progress;
        private bool? _succeeded;
        private ObjectLoadTelemetry _telemetry;
        private ObjectDiagnosticsReport _diagnostics;
        private ObjectLoadError _error;
        private string _copyStatus;

        public static ObjectLoadingDiagnosticsOverlay Active { get; private set; }

        public static ObjectLoadingDiagnosticsOverlay CreateIfEnabled(bool enabled, bool logToConsole = true)
        {
            if (!enabled)
            {
                return null;
            }

            GameObject existing = GameObject.Find(GameObjectName);
            ObjectLoadingDiagnosticsOverlay overlay = existing != null
                ? existing.GetComponent<ObjectLoadingDiagnosticsOverlay>()
                : null;

            if (overlay == null)
            {
                GameObject go = existing != null ? existing : new GameObject(GameObjectName);
                overlay = go.AddComponent<ObjectLoadingDiagnosticsOverlay>();
            }

            overlay.visible = true;
            overlay.logToConsole = logToConsole;
            Active = overlay;
            return overlay;
        }

        public void Begin(ObjectLoadRequest request)
        {
            Active = this;
            _startedAt = Time.realtimeSinceStartupAsDouble;
            _phase = ObjectLoadPhase.ResolvingSource;
            _stage = "resolving_source";
            _message = "Starting object load.";
            _displayName = string.IsNullOrWhiteSpace(request?.DisplayName)
                ? "object"
                : request.DisplayName;
            _sourceType = request?.Source != null ? request.Source.Type.ToString() : "Unknown";
            _progress = 0f;
            _succeeded = null;
            _telemetry = null;
            _diagnostics = null;
            _error = null;
            _copyStatus = null;
        }

        public void RecordProgress(ObjectLoadProgress progress)
        {
            if (progress == null)
            {
                return;
            }

            _phase = progress.Phase;
            _stage = string.IsNullOrWhiteSpace(progress.Stage) ? _stage : progress.Stage;
            _message = string.IsNullOrWhiteSpace(progress.Message) ? _message : progress.Message;
            _progress = Mathf.Clamp01(progress.Normalized);

            if (progress.Telemetry != null)
            {
                _telemetry = progress.Telemetry;
            }

            if (progress.Phase == ObjectLoadPhase.Completed)
            {
                _succeeded = true;
            }
            else if (progress.Phase == ObjectLoadPhase.Failed)
            {
                _succeeded = false;
            }
        }

        public void RecordResult(ObjectLoadResult result)
        {
            if (result == null)
            {
                _succeeded = false;
                _phase = ObjectLoadPhase.Failed;
                _stage = "failed";
                _message = "Object load finished without a result.";
                return;
            }

            _succeeded = result.Succeeded;
            _phase = result.Succeeded ? ObjectLoadPhase.Completed : ObjectLoadPhase.Failed;
            _stage = result.Succeeded ? "completed" : "failed";
            _message = string.IsNullOrWhiteSpace(result.Message) ? _message : result.Message;
            _progress = result.Succeeded ? 1f : _progress;
            _telemetry = result.Telemetry ?? _telemetry;
            _diagnostics = result.Diagnostics ?? _diagnostics;
            _error = result.Error;

            if (logToConsole)
            {
                ObjectLoadingDiagnosticsLogger.LogSnapshot(CreateSnapshot());
            }
        }

        public void SetVisible(bool isVisible)
        {
            visible = isVisible;
        }

        public ObjectLoadingDiagnosticsSnapshot CreateSnapshot()
        {
            return new ObjectLoadingDiagnosticsSnapshot
            {
                DisplayName = _displayName,
                SourceType = _sourceType,
                Phase = _phase,
                Stage = _stage,
                Progress = _progress,
                Message = _message,
                ElapsedMs = ElapsedMilliseconds(),
                Succeeded = _succeeded,
                Telemetry = _telemetry,
                Diagnostics = _diagnostics,
                Error = _error
            };
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(CreateSnapshot(), Formatting.Indented);
        }

        private void OnDestroy()
        {
            if (Active == this)
            {
                Active = null;
            }
        }

        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            EnsureStyles();

            float width = Mathf.Min(PanelWidth, Mathf.Max(280f, Screen.width - (PanelMargin * 2f)));
            float height = Mathf.Min(PanelHeight, Mathf.Max(240f, Screen.height - (PanelMargin * 2f)));
            Rect panel = new Rect(Screen.width - width - PanelMargin, PanelMargin, width, height);

            Color previous = GUI.color;
            GUI.color = _succeeded == false
                ? new Color(0.20f, 0.06f, 0.06f, 0.92f)
                : new Color(0.04f, 0.11f, 0.08f, 0.92f);
            GUI.Box(panel, GUIContent.none, _panelStyle);
            GUI.color = previous;

            GUILayout.BeginArea(new Rect(panel.x + 16f, panel.y + 14f, panel.width - 32f, panel.height - 28f));
            GUILayout.Label("Object Loading Diagnostics", _titleStyle);
            GUILayout.Space(6f);
            GUILayout.Label("Object: " + FormatValue(_displayName), _mutedStyle);
            GUILayout.Label("Phase: " + FormatPhase(_phase, _stage), _labelStyle);
            GUILayout.Label(_message, _mutedStyle);
            DrawProgressBar(_progress);
            GUILayout.Space(4f);
            GUILayout.Label("Elapsed: " + FormatMilliseconds(ElapsedMilliseconds()), _labelStyle);
            GUILayout.Label("Download: " + FormatMilliseconds(_telemetry?.DownloadTimeMs ?? 0)
                            + "    Bundle: " + FormatMilliseconds(_telemetry?.BundleLoadTimeMs ?? 0)
                            + "    Instantiate: " + FormatMilliseconds(_telemetry?.InstantiateTimeMs ?? 0), _labelStyle);
            GUILayout.Label("Total: " + FormatMilliseconds(_telemetry?.TotalTimeMs ?? 0)
                            + "    Bytes: " + FormatBytes(_telemetry?.BytesReceived ?? 0), _labelStyle);
            GUILayout.Label("Assets: " + (_telemetry?.AssetCount ?? 0)
                            + "    Scenes: " + (_telemetry?.SceneCount ?? 0)
                            + "    Renderers: " + (_telemetry?.RendererCount ?? 0), _labelStyle);
            GUILayout.Label("Materials: " + (_telemetry?.MaterialCount ?? 0)
                            + "    Missing shaders: " + (_telemetry?.MissingShaderMaterialCount ?? 0)
                            + "    Pink: " + (_telemetry?.PinkMaterialCount ?? 0), _labelStyle);
            GUILayout.Label("Source: " + FormatValue(_sourceType)
                            + "    Strategy: " + FormatValue(_telemetry?.LoadStrategy)
                            + "    Cache: " + FormatValue(_telemetry?.CacheStatus), _mutedStyle);
            DrawCopyButton();
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_panelStyle != null)
            {
                return;
            }

            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = Texture2D.whiteTexture }
            };

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = Color.white },
                wordWrap = true
            };

            _mutedStyle = new GUIStyle(_labelStyle)
            {
                normal = { textColor = new Color(0.78f, 0.88f, 0.82f, 1f) }
            };
        }

        private void DrawCopyButton()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy Diagnostics JSON", GUILayout.Height(24f)))
            {
                GUIUtility.systemCopyBuffer = ToJson();
                _copyStatus = "Copied";
            }

            if (!string.IsNullOrWhiteSpace(_copyStatus))
            {
                GUILayout.Label(_copyStatus, _mutedStyle, GUILayout.Width(72f));
            }

            GUILayout.EndHorizontal();
        }

        private static void DrawProgressBar(float value)
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 18f, GUILayout.ExpandWidth(true));
            Color previous = GUI.color;
            GUI.color = new Color(0.08f, 0.1f, 0.12f, 1f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(0.18f, 0.74f, 0.45f, 1f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height), Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(rect, Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%");
        }

        private long ElapsedMilliseconds()
        {
            if (_startedAt < 0d)
            {
                return 0;
            }

            return Mathf.Max(0, Mathf.RoundToInt((float)((Time.realtimeSinceStartupAsDouble - _startedAt) * 1000d)));
        }

        private static string FormatPhase(ObjectLoadPhase phase, string fallback)
        {
            return phase == ObjectLoadPhase.None
                ? FormatValue(fallback)
                : phase.ToString();
        }

        private static string FormatMilliseconds(long milliseconds)
        {
            return milliseconds <= 0 ? "0 ms" : milliseconds + " ms";
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0)
            {
                return "0 B";
            }

            if (bytes >= 1024 * 1024)
            {
                return (bytes / (1024f * 1024f)).ToString("0.0") + " MB";
            }

            if (bytes >= 1024)
            {
                return (bytes / 1024f).ToString("0.0") + " KB";
            }

            return bytes + " B";
        }

        private static string FormatValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "none" : value;
        }
    }
}
