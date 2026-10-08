using UnityEngine;
using UnityEngine.Events;

namespace PipelineControl
{
    /// <summary>
    /// Controls Valve OPEN/CLOSED state, flow percentage, handle rotation animation, and status indicators.
    /// </summary>
    public class ValveController : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private bool isOpen = false;
        [Range(0f, 100f)]
        [SerializeField] private float openPercentage = 100f;

        [Header("Valve Handle Visuals")]
        [Tooltip("Optional transform (handwheel/lever) that rotates when opening/closing.")]
        [SerializeField] private Transform valveHandleTransform;
        [SerializeField] private Vector3 closedRotationEuler = Vector3.zero;
        [SerializeField] private Vector3 openRotationEuler = new Vector3(0, 90f, 0);
        [SerializeField] private float smoothSpeed = 5f;

        [Header("Status Indicator")]
        [SerializeField] private Renderer statusIndicatorRenderer;
        [SerializeField] private Color openColor = Color.green;
        [SerializeField] private Color closedColor = Color.red;
        [SerializeField] private Color partialColor = Color.yellow;

        [Header("Events")]
        public UnityEvent<bool> OnStateChanged;
        public UnityEvent<float> OnPercentageChanged;

        public bool IsOpen => isOpen;
        public float OpenPercentage => isOpen ? openPercentage : 0f;

        private void Start()
        {
            UpdateStatusIndicator();
        }

        private void Update()
        {
            if (valveHandleTransform != null)
            {
                float targetNormalized = OpenPercentage / 100f;
                Quaternion targetRotation = Quaternion.Euler(Vector3.Lerp(closedRotationEuler, openRotationEuler, targetNormalized));
                valveHandleTransform.localRotation = Quaternion.Slerp(valveHandleTransform.localRotation, targetRotation, Time.deltaTime * smoothSpeed);
            }
        }

        public void ToggleValve()
        {
            SetValveState(!isOpen);
        }

        public void SetValveState(bool state)
        {
            if (isOpen == state) return;

            isOpen = state;
            UpdateStatusIndicator();
            OnStateChanged?.Invoke(isOpen);
        }

        public void SetOpenPercentage(float percentage)
        {
            openPercentage = Mathf.Clamp(percentage, 0f, 100f);
            if (openPercentage > 0f && !isOpen)
            {
                isOpen = true;
                OnStateChanged?.Invoke(true);
            }
            else if (openPercentage <= 0f && isOpen)
            {
                isOpen = false;
                OnStateChanged?.Invoke(false);
            }

            UpdateStatusIndicator();
            OnPercentageChanged?.Invoke(openPercentage);
        }

        private void UpdateStatusIndicator()
        {
            if (statusIndicatorRenderer != null && statusIndicatorRenderer.material != null)
            {
                if (!isOpen || openPercentage <= 0f)
                {
                    statusIndicatorRenderer.material.color = closedColor;
                }
                else if (openPercentage >= 100f)
                {
                    statusIndicatorRenderer.material.color = openColor;
                }
                else
                {
                    statusIndicatorRenderer.material.color = partialColor;
                }
            }
        }
    }
}
