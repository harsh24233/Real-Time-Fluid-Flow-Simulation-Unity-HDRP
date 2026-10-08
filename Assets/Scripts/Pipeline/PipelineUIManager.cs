using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace PipelineControl
{
    /// <summary>
    /// UI Manager for controlling Pump and Valve ON/OFF functionality, displaying flow metrics,
    /// and managing tank fluid transfer UI controls.
    /// </summary>
    public class PipelineUIManager : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private PumpController pumpController;
        [SerializeField] private ValveController valveController;
        [SerializeField] private PipeFlowController flowController;

        [Header("Tank Fluid Simulation")]
        [Tooltip("Simulated fluid capacity for Tank 1 in Liters.")]
        [SerializeField] private float tank1Capacity = 1000f;
        [SerializeField] private float tank1Current = 1000f;
        [SerializeField] private float tank2Current = 0f;

        public float Tanker1FillPercentage => tank1Capacity > 0f ? Mathf.Clamp01(tank1Current / tank1Capacity) * 100f : 0f;
        public float Tanker2FillPercentage => tank1Capacity > 0f ? Mathf.Clamp01(tank2Current / tank1Capacity) * 100f : 0f;

        [Header("Display Options")]
        [Tooltip("Enable built-in IMGUI overlay HUD with resolution scaling.")]
        [SerializeField] private bool enableOnGUIHUD = true;

        [Header("Pump UI Controls (Optional uGUI)")]
        [SerializeField] private Button pumpButton;
        [SerializeField] private Text pumpStatusText;

        [Header("Valve UI Controls (Optional uGUI)")]
        [SerializeField] private Button valveButton;
        [SerializeField] private Text valveStatusText;

        [Header("System UI Displays (Optional uGUI)")]
        [SerializeField] private Text flowRateText;
        [SerializeField] private Text systemStatusText;

        [Header("Auto uGUI Settings")]
        [SerializeField] private bool autoGenerateHUDIfMissing = false;

        private void Awake()
        {
            FindReferences();
        }

        private void Start()
        {
            FindReferences();
            EnsureEventSystem();

            if (autoGenerateHUDIfMissing && !HasAssignedUI())
            {
                CreateRuntimeHUD();
            }

            SetupUIListeners();
            UpdateUI();
        }

        private void Update()
        {
            SimulateTankTransfer();
            UpdateStatusDisplays();
        }

        private void SimulateTankTransfer()
        {
            var sim = FindAnyObjectByType<FluidFlowSimulation>();
            if (sim != null)
            {
                tank1Current = (sim.sourceFillPercent / 100f) * tank1Capacity;
                tank2Current = (sim.destinationFillPercent / 100f) * tank1Capacity;
            }
            else if (flowController != null && flowController.IsFlowing && tank1Current > 0f)
            {
                float transferRate = flowController.CurrentFlowRate / 60f; // Liters per second
                float transferred = transferRate * Time.deltaTime;
                transferred = Mathf.Min(transferred, tank1Current);
                tank1Current -= transferred;
                tank2Current += transferred;
            }
        }

        private void FindReferences()
        {
            if (pumpController == null) pumpController = FindAnyObjectByType<PumpController>();
            if (valveController == null) valveController = FindAnyObjectByType<ValveController>();
            if (flowController == null) flowController = FindAnyObjectByType<PipeFlowController>();
        }

        private bool HasAssignedUI()
        {
            return (pumpButton != null || pumpStatusText != null);
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();

                var inputModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (inputModuleType != null)
                {
                    es.AddComponent(inputModuleType);
                }
                else
                {
                    es.AddComponent<StandaloneInputModule>();
                }
            }
        }

        private void SetupUIListeners()
        {
            if (pumpButton != null)
            {
                pumpButton.onClick.RemoveAllListeners();
                pumpButton.onClick.AddListener(() => {
                    if (pumpController == null) pumpController = FindAnyObjectByType<PumpController>();
                    if (pumpController != null) pumpController.TogglePump();
                });
            }

            if (valveButton != null)
            {
                valveButton.onClick.RemoveAllListeners();
                valveButton.onClick.AddListener(() => {
                    if (valveController == null) valveController = FindAnyObjectByType<ValveController>();
                    if (valveController != null) valveController.ToggleValve();
                });
            }
        }

        public void UpdateUI()
        {
            UpdateStatusDisplays();
        }

        private void UpdateStatusDisplays()
        {
            if (pumpController == null || valveController == null) FindReferences();

            // Pump status
            if (pumpStatusText != null && pumpController != null)
            {
                bool isOn = pumpController.IsOn;
                pumpStatusText.text = isOn ? "PUMP: [ ON ]" : "PUMP: [ OFF ]";
                pumpStatusText.color = isOn ? Color.green : Color.red;
            }

            // Valve status
            if (valveStatusText != null && valveController != null)
            {
                bool isOpen = valveController.IsOpen;
                float pct = valveController.OpenPercentage;
                valveStatusText.text = isOpen ? $"VALVE: [ OPEN ({pct:F0}%) ]" : "VALVE: [ CLOSED ]";
                valveStatusText.color = isOpen ? (pct >= 100f ? Color.green : Color.yellow) : Color.red;
            }

            // Flow rate text & System Status
            float currentFlow = (flowController != null) ? flowController.CurrentFlowRate : 0f;
            if (flowRateText != null)
            {
                flowRateText.text = $"FLOW RATE: {currentFlow:F1} L/min";
            }

            if (systemStatusText != null)
            {
                if (pumpController != null && !pumpController.IsOn)
                {
                    systemStatusText.text = "STATUS: IDLE (Pump Stopped)";
                    systemStatusText.color = new Color(1f, 0.7f, 0.2f);
                }
                else if (valveController != null && !valveController.IsOpen)
                {
                    systemStatusText.text = "STATUS: BLOCKED (Valve Closed)";
                    systemStatusText.color = Color.red;
                }
                else
                {
                    systemStatusText.text = "STATUS: ACTIVE (Fluid Flowing)";
                    systemStatusText.color = Color.green;
                }
            }
        }

        #region IMGUI Resolution-Scaled Overlay HUD
        private void OnGUI()
        {
            if (!enableOnGUIHUD || !Application.isPlaying) return;

            if (pumpController == null || valveController == null) FindReferences();

            // Dynamic scaling matrix based on screen height (baseline 1080p)
            float scale = Mathf.Max(1.0f, Screen.height / 1080f);
            Matrix4x4 origMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1.0f));

            int width = 450;
            int height = 340;
            Rect hudRect = new Rect(25, 25, width, height);

            // Dark semi-transparent container box
            GUI.color = new Color(0.07f, 0.09f, 0.14f, 0.95f);
            GUI.Box(hudRect, GUIContent.none);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(40, 25, width - 30, height - 30));

            // Title
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.cyan }
            };
            GUILayout.Label("PIPELINE FLOW CONTROL", titleStyle);

            // Flow Rate & System Status
            var sim = FindAnyObjectByType<FluidFlowSimulation>();
            float currentFlow = (flowController != null) ? flowController.CurrentFlowRate : 0f;
            bool isPumpOn = (pumpController != null && pumpController.IsOn);
            bool isValveOpen = (valveController != null && valveController.IsOpen);
            bool isSim1Active = sim != null && sim.IsFlowing && sim.ActiveDirection == FluidFlowSimulation.FlowDirection.Forward_Cube1To2;
            bool isSim2Active = sim != null && sim.IsFlowing && sim.ActiveDirection == FluidFlowSimulation.FlowDirection.Reverse_Cube2To1;

            string statusStr;
            Color statusColor;

            if (isSim1Active)
            {
                statusStr = "STATUS: ACTIVE (Sim 1: Forward Flow)";
                statusColor = Color.green;
            }
            else if (isSim2Active)
            {
                if (!isPumpOn)
                {
                    statusStr = "STATUS: IDLE (Pump Stopped)";
                    statusColor = new Color(1f, 0.7f, 0.2f);
                }
                else if (!isValveOpen)
                {
                    statusStr = "STATUS: BLOCKED (Valve Closed)";
                    statusColor = Color.red;
                }
                else
                {
                    statusStr = "STATUS: ACTIVE (Sim 2: Reverse 8-Point Flow)";
                    statusColor = new Color(0.3f, 0.85f, 1f);
                }
            }
            else if (!isPumpOn)
            {
                statusStr = "STATUS: IDLE (Pump Stopped)";
                statusColor = new Color(1f, 0.7f, 0.2f);
            }
            else if (!isValveOpen)
            {
                statusStr = "STATUS: BLOCKED (Valve Closed)";
                statusColor = Color.red;
            }
            else
            {
                statusStr = "STATUS: IDLE (Flow Stopped)";
                statusColor = new Color(1f, 0.7f, 0.2f);
            }

            GUIStyle statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = statusColor }
            };
            GUILayout.Label(statusStr, statusStyle);

            GUIStyle flowStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.yellow }
            };
            GUILayout.Label($"FLOW RATE: {currentFlow:F1} L/min", flowStyle);

            GUILayout.Space(6);

            // Tanker Fill Levels (Tank 1 & Tank 2)
            float tank1Fill = Tanker1FillPercentage;
            float tank2Fill = Tanker2FillPercentage;

            GUIStyle tankStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            GUILayout.Label($"TANK 1 (Source): {tank1Fill:F0}% ({tank1Current:F0} L)", tankStyle);
            GUILayout.Label($"TANK 2 (Storage): {tank2Fill:F0}% ({tank2Current:F0} L)", tankStyle);

            GUILayout.Space(10);

            // Pump Control Row
            GUILayout.BeginHorizontal();
            string pumpText = isPumpOn ? "PUMP: [ ON ]" : "PUMP: [ OFF ]";
            Color pumpColor = isPumpOn ? Color.green : Color.red;
            GUIStyle pLblStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = pumpColor }
            };
            GUILayout.Label(pumpText, pLblStyle, GUILayout.Width(170), GUILayout.Height(34));

            GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };

            if (GUILayout.Button("TOGGLE PUMP", btnStyle, GUILayout.Width(160), GUILayout.Height(34)))
            {
                if (pumpController != null) pumpController.TogglePump();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Valve Control Row
            GUILayout.BeginHorizontal();
            string valveText = isValveOpen ? "VALVE: [ OPEN ]" : "VALVE: [ CLOSED ]";
            Color valveColor = isValveOpen ? Color.green : Color.red;
            GUIStyle vLblStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = valveColor }
            };
            GUILayout.Label(valveText, vLblStyle, GUILayout.Width(170), GUILayout.Height(34));

            if (GUILayout.Button("TOGGLE VALVE", btnStyle, GUILayout.Width(160), GUILayout.Height(34)))
            {
                if (valveController != null) valveController.ToggleValve();
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();

            // Restore GUI matrix
            GUI.matrix = origMatrix;
        }
        #endregion

        #region Runtime uGUI Canvas Builder
        private Font GetValidFont()
        {
            Font f = null;
            try
            {
                f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch { }

            if (f == null)
            {
                try
                {
                    f = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                catch { }
            }

            if (f == null)
            {
                f = Font.CreateDynamicFontFromOSFont("Arial", 14);
            }

            return f;
        }

        private void CreateRuntimeHUD()
        {
            GameObject canvasGO = new GameObject("PipelineHUDCanvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            Font defaultFont = GetValidFont();

            // Panel Background
            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(canvasGO.transform, false);
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.02f, 0.65f);
            panelRect.anchorMax = new Vector2(0.35f, 0.98f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // Pump Status Text
            GameObject pumpTextGO = new GameObject("PumpStatusText");
            pumpTextGO.transform.SetParent(panel.transform, false);
            pumpStatusText = pumpTextGO.AddComponent<Text>();
            pumpStatusText.font = defaultFont;
            pumpStatusText.fontSize = 18;
            pumpStatusText.alignment = TextAnchor.MiddleLeft;
            RectTransform pumpTextRect = pumpTextGO.GetComponent<RectTransform>();
            pumpTextRect.anchorMin = new Vector2(0.05f, 0.7f);
            pumpTextRect.anchorMax = new Vector2(0.6f, 0.9f);
            pumpTextRect.offsetMin = Vector2.zero;
            pumpTextRect.offsetMax = Vector2.zero;

            // Pump Toggle Button
            GameObject pumpBtnGO = new GameObject("PumpButton");
            pumpBtnGO.transform.SetParent(panel.transform, false);
            Image pumpBtnImg = pumpBtnGO.AddComponent<Image>();
            pumpBtnImg.color = new Color(0.2f, 0.6f, 1f);
            pumpButton = pumpBtnGO.AddComponent<Button>();
            RectTransform pumpBtnRect = pumpBtnGO.GetComponent<RectTransform>();
            pumpBtnRect.anchorMin = new Vector2(0.65f, 0.72f);
            pumpBtnRect.anchorMax = new Vector2(0.95f, 0.88f);
            pumpBtnRect.offsetMin = Vector2.zero;
            pumpBtnRect.offsetMax = Vector2.zero;

            GameObject pumpBtnLabelGO = new GameObject("Label");
            pumpBtnLabelGO.transform.SetParent(pumpBtnGO.transform, false);
            Text pumpBtnLabel = pumpBtnLabelGO.AddComponent<Text>();
            pumpBtnLabel.font = defaultFont;
            pumpBtnLabel.text = "Toggle Pump";
            pumpBtnLabel.alignment = TextAnchor.MiddleCenter;
            pumpBtnLabel.color = Color.white;
            RectTransform pumpBtnLabelRect = pumpBtnLabelGO.GetComponent<RectTransform>();
            pumpBtnLabelRect.anchorMin = Vector2.zero;
            pumpBtnLabelRect.anchorMax = Vector2.one;
            pumpBtnLabelRect.offsetMin = Vector2.zero;
            pumpBtnLabelRect.offsetMax = Vector2.zero;

            // Valve Status Text
            GameObject valveTextGO = new GameObject("ValveStatusText");
            valveTextGO.transform.SetParent(panel.transform, false);
            valveStatusText = valveTextGO.AddComponent<Text>();
            valveStatusText.font = defaultFont;
            valveStatusText.fontSize = 18;
            valveStatusText.alignment = TextAnchor.MiddleLeft;
            RectTransform valveTextRect = valveTextGO.GetComponent<RectTransform>();
            valveTextRect.anchorMin = new Vector2(0.05f, 0.4f);
            valveTextRect.anchorMax = new Vector2(0.6f, 0.6f);
            valveTextRect.offsetMin = Vector2.zero;
            valveTextRect.offsetMax = Vector2.zero;

            // Valve Toggle Button
            GameObject valveBtnGO = new GameObject("ValveButton");
            valveBtnGO.transform.SetParent(panel.transform, false);
            Image valveBtnImg = valveBtnGO.AddComponent<Image>();
            valveBtnImg.color = new Color(0.2f, 0.6f, 1f);
            valveButton = valveBtnGO.AddComponent<Button>();
            RectTransform valveBtnRect = valveBtnGO.GetComponent<RectTransform>();
            valveBtnRect.anchorMin = new Vector2(0.65f, 0.42f);
            valveBtnRect.anchorMax = new Vector2(0.95f, 0.58f);
            valveBtnRect.offsetMin = Vector2.zero;
            valveBtnRect.offsetMax = Vector2.zero;

            GameObject valveBtnLabelGO = new GameObject("Label");
            valveBtnLabelGO.transform.SetParent(valveBtnGO.transform, false);
            Text valveBtnLabel = valveBtnLabelGO.AddComponent<Text>();
            valveBtnLabel.font = defaultFont;
            valveBtnLabel.text = "Toggle Valve";
            valveBtnLabel.alignment = TextAnchor.MiddleCenter;
            valveBtnLabel.color = Color.white;
            RectTransform valveBtnLabelRect = valveBtnLabelGO.GetComponent<RectTransform>();
            valveBtnLabelRect.anchorMin = Vector2.zero;
            valveBtnLabelRect.anchorMax = Vector2.one;
            valveBtnLabelRect.offsetMin = Vector2.zero;
            valveBtnLabelRect.offsetMax = Vector2.zero;

            // Flow Rate Text
            GameObject flowTextGO = new GameObject("FlowRateText");
            flowTextGO.transform.SetParent(panel.transform, false);
            flowRateText = flowTextGO.AddComponent<Text>();
            flowRateText.font = defaultFont;
            flowRateText.fontSize = 16;
            flowRateText.alignment = TextAnchor.MiddleLeft;
            RectTransform flowTextRect = flowTextGO.GetComponent<RectTransform>();
            flowTextRect.anchorMin = new Vector2(0.05f, 0.1f);
            flowTextRect.anchorMax = new Vector2(0.95f, 0.3f);
            flowTextRect.offsetMin = Vector2.zero;
            flowTextRect.offsetMax = Vector2.zero;
        }
        #endregion
    }
}
