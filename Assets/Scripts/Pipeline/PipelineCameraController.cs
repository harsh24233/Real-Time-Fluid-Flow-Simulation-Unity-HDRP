using UnityEngine;

namespace PipelineControl
{
    /// <summary>
    /// Full 3D Free & Orbit Camera Navigation System:
    /// - WASD / Arrow Keys + Q/E: Move anywhere in 3D space (Forward, Back, Left, Right, Up, Down)
    /// - Left Shift: Fast Sprint Speed multiplier
    /// - Right-Click or Left-Click Drag: Look & Rotate camera freely in 3D
    /// - Middle-Click Drag: Pan camera plane
    /// - Mouse Scroll Wheel: Smooth Zoom / Move forward-backward
    /// Compatible with both Unity New Input System and Legacy Input.
    /// </summary>
    public class PipelineCameraController : MonoBehaviour
    {
        [Header("Movement Speeds")]
        [SerializeField] private float moveSpeed = 4.5f;
        [SerializeField] private float sprintMultiplier = 2.5f;
        [SerializeField] private float panSpeed = 3.5f;

        [Header("Rotation & Orbit Speeds")]
        [SerializeField] private float lookSensitivity = 2.0f;
        [SerializeField] private float zoomSpeed = 5.0f;

        [Header("Pitch Limits")]
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;

        // Current Euler angles
        private float yaw = 0f;
        private float pitch = 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttachToMainCamera()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) mainCam = FindAnyObjectByType<Camera>();
            if (mainCam != null && mainCam.GetComponent<PipelineCameraController>() == null)
            {
                mainCam.gameObject.AddComponent<PipelineCameraController>();
            }
        }

        private void Start()
        {
            Vector3 angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = angles.x;
            if (pitch > 180f) pitch -= 360f;
        }

        private void Update()
        {
            HandleRotationAndPan();
            HandleWASDMovement();
            HandleScrollZoom();
        }

        private void HandleWASDMovement()
        {
            Vector3 inputDir = Vector3.zero;
            bool sprint = false;

            // 1. Try New Input System
            try
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null)
                {
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed) inputDir += transform.forward;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed) inputDir -= transform.forward;
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) inputDir -= transform.right;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) inputDir += transform.right;
                    if (kb.eKey.isPressed || kb.spaceKey.isPressed) inputDir += Vector3.up;
                    if (kb.qKey.isPressed || kb.leftCtrlKey.isPressed) inputDir -= Vector3.up;
                    if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) sprint = true;
                }
            }
            catch { }

            // 2. Fallback to Legacy Input if inputDir is still zero
            if (inputDir.sqrMagnitude < 0.001f)
            {
                try
                {
                    if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) inputDir += transform.forward;
                    if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) inputDir -= transform.forward;
                    if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) inputDir -= transform.right;
                    if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) inputDir += transform.right;
                    if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space)) inputDir += Vector3.up;
                    if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftControl)) inputDir -= Vector3.up;
                    if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) sprint = true;
                }
                catch { }
            }

            if (inputDir.sqrMagnitude > 0.001f)
            {
                inputDir.Normalize();
                float speed = moveSpeed * (sprint ? sprintMultiplier : 1.0f);
                transform.position += inputDir * speed * Time.deltaTime;
            }
        }

        private void HandleRotationAndPan()
        {
            Vector2 mouseDelta = Vector2.zero;
            bool isRotating = false;
            bool isPanning = false;

            // 1. Try New Input System
            try
            {
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse != null)
                {
                    mouseDelta = mouse.delta.ReadValue();

                    if (mouse.rightButton.isPressed || mouse.leftButton.isPressed)
                    {
                        isRotating = true;
                    }
                    else if (mouse.middleButton.isPressed)
                    {
                        isPanning = true;
                    }
                }
            }
            catch { }

            // 2. Legacy Fallback
            if (!isRotating && !isPanning)
            {
                try
                {
                    mouseDelta = new Vector2(Input.GetAxis("Mouse X") * 10f, Input.GetAxis("Mouse Y") * 10f);
                    if (Input.GetMouseButton(1) || Input.GetMouseButton(0)) isRotating = true;
                    else if (Input.GetMouseButton(2)) isPanning = true;
                }
                catch { }
            }

            if (isRotating && mouseDelta.sqrMagnitude > 0.001f)
            {
                yaw += mouseDelta.x * lookSensitivity * 0.1f;
                pitch -= mouseDelta.y * lookSensitivity * 0.1f;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }
            else if (isPanning && mouseDelta.sqrMagnitude > 0.001f)
            {
                Vector3 pan = (-transform.right * mouseDelta.x - transform.up * mouseDelta.y) * panSpeed * 0.002f;
                transform.position += pan;
            }
        }

        private void HandleScrollZoom()
        {
            float scroll = 0f;

            try
            {
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse != null) scroll = mouse.scroll.ReadValue().y * 0.002f;
            }
            catch { }

            if (Mathf.Abs(scroll) < 0.001f)
            {
                try { scroll = Input.GetAxis("Mouse ScrollWheel"); } catch { }
            }

            if (Mathf.Abs(scroll) > 0.001f)
            {
                transform.position += transform.forward * scroll * zoomSpeed;
            }
        }
    }
}
