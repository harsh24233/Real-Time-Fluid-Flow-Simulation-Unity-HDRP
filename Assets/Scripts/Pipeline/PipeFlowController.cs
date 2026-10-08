using UnityEngine;
using UnityEngine.Events;

namespace PipelineControl
{
    /// <summary>
    /// Calculates net fluid flow rate in the pipe based on Pump and Valve states,
    /// and controls pipe UV texture scrolling and output particle effects.
    /// </summary>
    public class PipeFlowController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private PumpController pumpController;
        [SerializeField] private ValveController valveController;

        [Header("Flow Parameters")]
        [SerializeField] private float maxFlowCapacity = 200f; // Liter/min
        [SerializeField] private float currentFlowRate = 0f;

        [Header("Pipe Fluid Animation")]
        [Tooltip("Optional Pipe Renderer whose material texture will scroll to visually show fluid movement.")]
        [SerializeField] private Renderer pipeRenderer;
        [SerializeField] private string texturePropertyName = "_BaseMap";
        [SerializeField] private Vector2 scrollDirection = new Vector2(0f, -0.5f);

        [Header("Particle System")]
        [Tooltip("Optional Particle System for fluid stream or discharge.")]
        [SerializeField] private ParticleSystem fluidParticles;

        [Header("Events")]
        public UnityEvent<float> OnFlowRateChanged;

        public float CurrentFlowRate => currentFlowRate;
        public float MaxFlowCapacity => maxFlowCapacity;
        public bool IsFlowing => currentFlowRate > 0.01f;

        private Vector2 currentUVOffset = Vector2.zero;

        private void Start()
        {
            if (pumpController == null) pumpController = GetComponent<PumpController>() ?? FindAnyObjectByType<PumpController>();
            if (valveController == null) valveController = GetComponent<ValveController>() ?? FindAnyObjectByType<ValveController>();
        }

        private void Update()
        {
            CalculateFlow();
            AnimatePipeMaterial();
            UpdateParticles();
        }

        private void CalculateFlow()
        {
            float pumpFactor = (pumpController != null && pumpController.IsOn) ? (pumpController.PumpSpeed / 100f) : 0f;
            float valveFactor = (valveController != null) ? (valveController.OpenPercentage / 100f) : 0f;

            float targetFlowRate = pumpFactor * valveFactor * maxFlowCapacity;
            
            // Smooth flow rate transition
            float prevFlow = currentFlowRate;
            currentFlowRate = Mathf.Lerp(currentFlowRate, targetFlowRate, Time.deltaTime * 5f);

            if (Mathf.Abs(currentFlowRate - prevFlow) > 0.1f)
            {
                OnFlowRateChanged?.Invoke(currentFlowRate);
            }
        }

        private void AnimatePipeMaterial()
        {
            if (pipeRenderer != null && pipeRenderer.material != null && IsFlowing)
            {
                float speedMultiplier = currentFlowRate / maxFlowCapacity;
                currentUVOffset += scrollDirection * (speedMultiplier * Time.deltaTime);

                if (pipeRenderer.material.HasProperty(texturePropertyName))
                {
                    pipeRenderer.material.SetTextureOffset(texturePropertyName, currentUVOffset);
                }
                else if (pipeRenderer.material.HasProperty("_MainTex"))
                {
                    pipeRenderer.material.SetTextureOffset("_MainTex", currentUVOffset);
                }
            }
        }

        private void UpdateParticles()
        {
            if (fluidParticles != null)
            {
                var main = fluidParticles.main;
                var emission = fluidParticles.emission;

                if (IsFlowing)
                {
                    if (!fluidParticles.isPlaying) fluidParticles.Play();
                    emission.rateOverTime = (currentFlowRate / maxFlowCapacity) * 50f;
                }
                else
                {
                    if (fluidParticles.isPlaying) fluidParticles.Stop();
                }
            }
        }
    }
}
