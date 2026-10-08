using UnityEngine;
using UnityEngine.Events;

namespace PipelineControl
{
    /// <summary>
    /// Controls Pump ON/OFF state, motor speed, rotation visual, and status indicators.
    /// </summary>
    public class PumpController : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private bool isOn = false;
        [Range(0f, 500f)]
        [SerializeField] private float pumpSpeed = 150f;

        [Header("Visual Feedback")]
        [Tooltip("Optional transform (impeller/shaft) to rotate when the pump is ON.")]
        [SerializeField] private Transform impellerTransform;
        [SerializeField] private Vector3 rotationAxis = Vector3.forward;

        [Tooltip("Optional Renderer to change color depending on ON/OFF state.")]
        [SerializeField] private Renderer statusIndicatorRenderer;
        [SerializeField] private Color onColor = Color.green;
        [SerializeField] private Color offColor = Color.red;

        [Header("Events")]
        public UnityEvent<bool> OnStateChanged;
        public UnityEvent<float> OnSpeedChanged;

        public bool IsOn => isOn;
        public float PumpSpeed => pumpSpeed;

        private void Start()
        {
            UpdateStatusIndicator();
        }

        private void Update()
        {
            if (isOn && impellerTransform != null)
            {
                impellerTransform.Rotate(rotationAxis * (pumpSpeed * Time.deltaTime), Space.Self);
            }
        }

        public void TogglePump()
        {
            SetPumpState(!isOn);
        }

        public void SetPumpState(bool state)
        {
            if (isOn == state) return;

            isOn = state;
            UpdateStatusIndicator();
            OnStateChanged?.Invoke(isOn);
        }

        public void SetPumpSpeed(float newSpeed)
        {
            pumpSpeed = Mathf.Clamp(newSpeed, 0f, 1000f);
            OnSpeedChanged?.Invoke(pumpSpeed);
        }

        private void UpdateStatusIndicator()
        {
            if (statusIndicatorRenderer != null && statusIndicatorRenderer.material != null)
            {
                statusIndicatorRenderer.material.color = isOn ? onColor : offColor;
            }
        }
    }
}
