using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using MixedReality.Toolkit.SpatialManipulation;

/// <summary>
/// Stands the pipeline upright and drops it onto the surface below when the user lets go.
/// Put this on the same GameObject as ObjectManipulator (the pipeline root).
/// </summary>
[RequireComponent(typeof(ObjectManipulator))]
public class PipelineSnapToSurface : MonoBehaviour
{
    [Header("Surface search")]
    [Tooltip("Layers that count as a surface (VirtualTable in the Editor, spatial mesh on device).")]
    public LayerMask surfaceLayers = ~0;
    [Tooltip("How far below the pipeline's bottom to look for a surface (meters).")]
    public float maxSearchDistance = 2f;
    [Tooltip("Surface normal's Y must be at least this (1 = flat, 0 = wall).")]
    [Range(0f, 1f)] public float minUpNormal = 0.7f;

    [Header("Behaviour")]
    public bool snapOnRelease = true;
    [Tooltip("Remove tilt on release: keeps left-right turn (yaw), restores the starting pitch/roll.")]
    public bool keepUpright = true;
    [Tooltip("Seconds to glide onto the surface. 0 = instant.")]
    public float settleTime = 0.25f;
    [Tooltip("Manual fine-tune: positive lifts the pipeline, negative pushes it down (meters).")]
    public float bottomOffset = 0f;

    [Header("Which renderers define the bottom")]
    [Tooltip("Renderers on objects whose name (or any parent's name under this root) contains one of these are ignored.")]
    public string[] ignoreNameContains = { "rigRoot", "Handle" };
    [Tooltip("Extra objects whose renderers should be ignored.")]
    public Transform[] ignoreRoots;

    ObjectManipulator manipulator;
    Coroutine settleRoutine;
    float startPitch, startRoll;

    void Awake()
    {
        Vector3 e = transform.eulerAngles;
        startPitch = e.x;
        startRoll = e.z;
    }

    void OnEnable()
    {
        manipulator = GetComponent<ObjectManipulator>();
        if (manipulator != null)
            manipulator.selectExited.AddListener(OnSelectExited);
    }

    void OnDisable()
    {
        if (manipulator != null)
            manipulator.selectExited.RemoveListener(OnSelectExited);
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        if (snapOnRelease)
            SnapToSurface();
    }

    // Right-click the component header (or the three dots) > Snap Now.
    [ContextMenu("Snap Now")]
    public void SnapToSurface()
    {
        Quaternion startRot = transform.rotation;
        Vector3 startPos = transform.position;

        Quaternion targetRot = keepUpright
            ? Quaternion.Euler(startPitch, transform.eulerAngles.y, startRoll)
            : startRot;

        // Measure at the target rotation so the bottom is correct once it is upright.
        transform.rotation = targetRot;

        if (!TryGetBounds(out Bounds b, out Renderer lowest))
        {
            transform.rotation = startRot;
            Debug.LogWarning("PipelineSnapToSurface: no usable Renderers found under this object.");
            return;
        }

        Vector3 origin = b.center;
        float distance = b.extents.y + maxSearchDistance;

        var hits = Physics.RaycastAll(origin, Vector3.down, distance, surfaceLayers,
                                      QueryTriggerInteraction.Ignore)
                          .Where(h => !h.collider.transform.IsChildOf(transform))
                          .OrderBy(h => h.distance);

        foreach (var hit in hits)
        {
            if (hit.normal.y < minUpNormal) continue;

            float deltaY = hit.point.y - b.min.y + bottomOffset;
            Vector3 targetPos = startPos + Vector3.up * deltaY;

            transform.rotation = startRot; // restore, then glide to target
            if (settleRoutine != null) StopCoroutine(settleRoutine);
            settleRoutine = StartCoroutine(MoveTo(targetPos, targetRot));

            Debug.Log($"PipelineSnapToSurface: surface '{hit.collider.name}', bottom defined by '{lowest.name}', moving {deltaY:F3} m in Y.");
            return;
        }

        transform.rotation = startRot;
        Debug.Log("PipelineSnapToSurface: no valid surface found below. Check Surface Layers, Max Search Distance, and that the table has a collider.");
    }

    IEnumerator MoveTo(Vector3 targetPos, Quaternion targetRot)
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        if (settleTime <= 0f)
        {
            transform.SetPositionAndRotation(targetPos, targetRot);
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / settleTime;
            float s = Mathf.SmoothStep(0f, 1f, t);
            transform.SetPositionAndRotation(Vector3.Lerp(startPos, targetPos, s),
                                             Quaternion.Slerp(startRot, targetRot, s));
            yield return null;
        }
        transform.SetPositionAndRotation(targetPos, targetRot);
        settleRoutine = null;
    }

    bool IsIgnored(Renderer r)
    {
        if (!r.enabled || !r.gameObject.activeInHierarchy) return true;
        if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) return true;

        if (ignoreRoots != null)
            foreach (var root in ignoreRoots)
                if (root != null && r.transform.IsChildOf(root)) return true;

        if (ignoreNameContains != null)
        {
            for (Transform t = r.transform; t != null && t != transform; t = t.parent)
                foreach (var n in ignoreNameContains)
                    if (!string.IsNullOrEmpty(n) &&
                        t.name.IndexOf(n, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
        }
        return false;
    }

    bool TryGetBounds(out Bounds bounds, out Renderer lowest)
    {
        bounds = default;
        lowest = null;
        bool first = true;

        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (IsIgnored(r)) continue;
            if (first) { bounds = r.bounds; first = false; }
            else bounds.Encapsulate(r.bounds);

            if (lowest == null || r.bounds.min.y < lowest.bounds.min.y)
                lowest = r;
        }
        return !first;
    }
}