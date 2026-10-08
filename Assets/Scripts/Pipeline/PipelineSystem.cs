using UnityEngine;

namespace PipelineControl
{
    /// <summary>
    /// Master entry point for the Pipeline System. Automatically connects Pump, Valve,
    /// Flow Controller, and UI Manager on startup.
    /// Also includes automatic scene initialization on Play mode so no manual setup is required.
    /// </summary>
    [DisallowMultipleComponent]
    public class PipelineSystem : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private PumpController pump;
        [SerializeField] private ValveController valve;
        [SerializeField] private PipeFlowController flowController;
        [SerializeField] private PipelineUIManager uiManager;

        public PumpController Pump => pump;
        public ValveController Valve => valve;
        public PipeFlowController FlowController => flowController;
        public PipelineUIManager UIManager => uiManager;

        private void Awake()
        {
            EnsureComponentsExist();
        }

        private void EnsureComponentsExist()
        {
            if (pump == null)
            {
                pump = GetComponent<PumpController>() ?? gameObject.AddComponent<PumpController>();
            }

            if (valve == null)
            {
                valve = GetComponent<ValveController>() ?? gameObject.AddComponent<ValveController>();
            }

            if (flowController == null)
            {
                flowController = GetComponent<PipeFlowController>() ?? gameObject.AddComponent<PipeFlowController>();
            }

            if (uiManager == null)
            {
                uiManager = GetComponent<PipelineUIManager>() ?? gameObject.AddComponent<PipelineUIManager>();
            }
        }

        public void TogglePump()
        {
            if (pump != null) pump.TogglePump();
        }

        public void ToggleValve()
        {
            if (valve != null) valve.ToggleValve();
        }

        public void SetPumpState(bool isOn)
        {
            if (pump != null) pump.SetPumpState(isOn);
        }

        public void SetValveState(bool isOpen)
        {
            if (valve != null) valve.SetValveState(isOpen);
        }

        /// <summary>
        /// Automatically instantiates the PipelineSystem when Play mode starts if missing from active scene.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitializeOnPlay()
        {
            if (FindAnyObjectByType<PipelineSystem>() == null)
            {
                GameObject autoGO = new GameObject("[PipelineSystem_AutoCreated]");
                autoGO.AddComponent<PipelineSystem>();
                Debug.Log("[PipelineSystem] Automatically initialized Pipe Control System and UI HUD for Play Mode.");
            }
        }
    }
}
