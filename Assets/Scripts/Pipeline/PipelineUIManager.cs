using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace PipelineControl
{
    /// <summary>
    /// UI Manager for controlling Pump and Valve ON/OFF functionality, displaying flow metrics,
    /// and managing tank fluid transfer UI controls.
    /// HUD is a world-space uGUI canvas (HoloLens / XR compatible).
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

        [Header("World-Space HUD (HoloLens)")]
        [Tooltip("Build the Pump/Valve/Tank HUD as a world-space canvas.")]
        [SerializeField] private bool enableWorldSpaceHUD = true;
        [Tooltip("HUD position relative to the pipe object (world axes). Default sits above the Sim 1/Sim 2 panels.")]
        [SerializeField] private Vector3 hudOffsetFromPipe = new Vector3(2.6f, 1.11f, 0.4f);
        [Tooltip("Rotation around Y. 180 matches the Sim panel canvas.")]
        [SerializeField] private float hudYaw = 180f;
        [Tooltip("World size of one canvas unit. 0.001 = 1 unit is 1 mm.")]
        [SerializeField] private float hudScale = 0.001f;
        [Tooltip("Canvas size in canvas units (width, height).")]
        [SerializeField] private Vector2 hudPanelSize = new Vector2(450f, 340f);

        [Header("Pump UI Controls (Optional uGUI)")]
        [SerializeField] private Button pumpButton;
        [SerializeField] private Text pumpStatusText;

        [Header("Valve UI Controls (Optional uGUI)")]
        [SerializeField] private Button valveButton;
        [SerializeField] private Text valveStatusText;

        [Header("System UI Displays (Optional uGUI)")]
        [SerializeField] private Text flowRateText;
        [SerializeField] private Text systemStatusText;

        // Runtime-created HUD references
        private Text _tank1Text;
        private Text _tank2Text;
        private Canvas _hudCanvas;
        private FluidFlowSimulation _sim;

        private void Awake()
        {
            FindReferences();
        }

        private void Start()
        {
            FindReferences();
            EnsureEventSystem();

            if (enableWorldSpaceHUD && !HasAssignedUI())
            {
                CreateWorldSpaceHUD();
            }

            SetupUIListeners();
            UpdateUI();
        }

        private void Update()
        {
            SimulateTankTransfer();
            UpdateStatusDisplays();

            if (_hudCanvas != null && _hudCanvas.worldCamera == null)
                _hudCanvas.worldCamera = Camera.main;
        }

        private void SimulateTankTransfer()
        {
            if (_sim == null) _sim = FindAnyObjectByType<FluidFlowSimulation>();
            var sim = _sim;
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
            if (_sim == null) _sim = FindAnyObjectByType<FluidFlowSimulation>();
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
                es.AddComponent<XRUIInputModule>();
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

        private void ComputeStatus(out string statusStr, out Color statusColor)
        {
            bool isPumpOn = (pumpController != null && pumpController.IsOn);
            bool isValveOpen = (valveController != null && valveController.IsOpen);
            bool isSim1Active = _sim != null && _sim.IsFlowing && _sim.ActiveDirection == FluidFlowSimulation.FlowDirection.Forward_Cube1To2;
            bool isSim2Active = _sim != null && _sim.IsFlowing && _sim.ActiveDirection == FluidFlowSimulation.FlowDirection.Reverse_Cube2To1;

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
        }

        private void UpdateStatusDisplays()
        {
            if (pumpController == null || valveController == null) FindReferences();

            bool isPumpOn = (pumpController != null && pumpController.IsOn);
            bool isValveOpen = (valveController != null && valveController.IsOpen);

            // Pump status
            if (pumpStatusText != null)
            {
                pumpStatusText.text = isPumpOn ? "PUMP: [ ON ]" : "PUMP: [ OFF ]";
                pumpStatusText.color = isPumpOn ? Color.green : Color.red;
            }

            // Valve status
            if (valveStatusText != null)
            {
                valveStatusText.text = isValveOpen ? "VALVE: [ OPEN ]" : "VALVE: [ CLOSED ]";
                valveStatusText.color = isValveOpen ? Color.green : Color.red;
            }

            // Flow rate text & System Status
            float currentFlow = (flowController != null) ? flowController.CurrentFlowRate : 0f;
            if (flowRateText != null)
            {
                flowRateText.text = $"FLOW RATE: {currentFlow:F1} L/min";
            }

            if (systemStatusText != null)
            {
                ComputeStatus(out string statusStr, out Color statusColor);
                systemStatusText.text = statusStr;
                systemStatusText.color = statusColor;
            }

            if (_tank1Text != null)
                _tank1Text.text = $"TANK 1 (Source): {Tanker1FillPercentage:F0}% ({tank1Current:F0} L)";
            if (_tank2Text != null)
                _tank2Text.text = $"TANK 2 (Storage): {Tanker2FillPercentage:F0}% ({tank2Current:F0} L)";
        }

        #region World-Space uGUI HUD Builder
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

        private Text MkText(Transform parent, string name, Font font, int size, FontStyle style,
                            Color color, TextAnchor align, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = new Vector2(8f, 0f);
            rt.offsetMax = new Vector2(-8f, 0f);
            return t;
        }

        private Button MkButton(Transform parent, string name, string label, Font font,
                                Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.05f, 0.45f, 0.85f, 1f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor      = new Color(0.05f, 0.45f, 0.85f, 1f);
            cb.highlightedColor = new Color(0.10f, 0.65f, 1.00f, 1f);
            cb.pressedColor     = new Color(0.02f, 0.25f, 0.55f, 1f);
            cb.disabledColor    = new Color(0.22f, 0.22f, 0.22f, 0.90f);
            btn.colors = cb;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var lblGO = new GameObject("Label");
            lblGO.transform.SetParent(go.transform, false);
            var t = lblGO.AddComponent<Text>();
            t.font = font;
            t.text = label;
            t.fontSize = 16;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            var lrt = lblGO.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            return btn;
        }

        private void CreateWorldSpaceHUD()
        {
            var canvasGO = new GameObject("PipelineHUDCanvas");
            _hudCanvas = canvasGO.AddComponent<Canvas>();
            _hudCanvas.renderMode = RenderMode.WorldSpace;
            _hudCanvas.sortingOrder = 20;
            _hudCanvas.worldCamera = Camera.main;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            canvasGO.AddComponent<TrackedDeviceGraphicRaycaster>();

            Vector3 pipePos = (_sim != null) ? _sim.GetPipePosition() : transform.position;
            var cvRT = canvasGO.GetComponent<RectTransform>();
            cvRT.sizeDelta  = hudPanelSize;
            cvRT.localScale = Vector3.one * hudScale;
            cvRT.position   = pipePos + hudOffsetFromPipe;
            cvRT.rotation   = Quaternion.Euler(0f, hudYaw, 0f);

            Font f = GetValidFont();

            // Dark semi-transparent background panel
            var panel = new GameObject("HUDPanel");
            panel.transform.SetParent(canvasGO.transform, false);
            panel.AddComponent<Image>().color = new Color(0.07f, 0.09f, 0.14f, 0.95f);
            var pRT = panel.GetComponent<RectTransform>();
            pRT.anchorMin = Vector2.zero;
            pRT.anchorMax = Vector2.one;
            pRT.offsetMin = pRT.offsetMax = Vector2.zero;

            var left = TextAnchor.MiddleLeft;

            MkText(panel.transform, "Title", f, 22, FontStyle.Bold, Color.cyan, left,
                   new Vector2(0.03f, 0.86f), new Vector2(0.97f, 0.98f)).text = "PIPELINE FLOW CONTROL";

            systemStatusText = MkText(panel.transform, "StatusText", f, 15, FontStyle.Bold, Color.white, left,
                   new Vector2(0.03f, 0.76f), new Vector2(0.97f, 0.86f));

            flowRateText = MkText(panel.transform, "FlowRateText", f, 17, FontStyle.Bold, Color.yellow, left,
                   new Vector2(0.03f, 0.66f), new Vector2(0.97f, 0.76f));

            _tank1Text = MkText(panel.transform, "Tank1Text", f, 15, FontStyle.Bold, Color.white, left,
                   new Vector2(0.03f, 0.55f), new Vector2(0.97f, 0.65f));

            _tank2Text = MkText(panel.transform, "Tank2Text", f, 15, FontStyle.Bold, Color.white, left,
                   new Vector2(0.03f, 0.45f), new Vector2(0.97f, 0.55f));

            // Pump row
            pumpStatusText = MkText(panel.transform, "PumpStatusText", f, 16, FontStyle.Bold, Color.red, left,
                   new Vector2(0.03f, 0.24f), new Vector2(0.50f, 0.40f));
            pumpButton = MkButton(panel.transform, "PumpButton", "TOGGLE PUMP", f,
                   new Vector2(0.52f, 0.24f), new Vector2(0.96f, 0.40f));

            // Valve row
            valveStatusText = MkText(panel.transform, "ValveStatusText", f, 16, FontStyle.Bold, Color.red, left,
                   new Vector2(0.03f, 0.05f), new Vector2(0.50f, 0.21f));
            valveButton = MkButton(panel.transform, "ValveButton", "TOGGLE VALVE", f,
                   new Vector2(0.52f, 0.05f), new Vector2(0.96f, 0.21f));
        }
        #endregion
    }
}