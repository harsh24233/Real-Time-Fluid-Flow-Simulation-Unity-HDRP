using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MixedReality.Toolkit;
using MixedReality.Toolkit.SpatialManipulation;

/// <summary>
/// Makes the world-space UI canvases grabbable (move + rotate) from any empty area of the panel.
/// Each canvas is placed under a holster that is moved by an MRTK ObjectManipulator.
/// A thin invisible BoxCollider sits just behind the panel. Buttons (Selectables) stay
/// raycast-able, so they are hit first and still click normally.
/// Remove this file to go back to fixed panels.
/// </summary>
public class UIMoveHandleAttacher : MonoBehaviour
{
    private static readonly string[] kCanvasNames = { "FluidFlowSimCanvas", "PipelineHUDCanvas" };
    private readonly HashSet<string> _done = new HashSet<string>();
    private readonly List<Canvas> _attached = new List<Canvas>();
    private float _t;
    private float _refresh;

    private const float kColliderDepth = 0.02f;   // metres, thickness of the grab collider
    private const float kColliderBehind = 0.015f; // metres, centre offset behind the panel

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        var go = new GameObject("UIMoveHandleAttacher");
        go.AddComponent<UIMoveHandleAttacher>();
    }

    private void Update()
    {
        _t += Time.unscaledDeltaTime;

        foreach (var n in kCanvasNames)
        {
            if (_done.Contains(n)) continue;
            var go = GameObject.Find(n);
            if (go == null) continue;
            var cv = go.GetComponent<Canvas>();
            if (cv == null || cv.renderMode != RenderMode.WorldSpace) continue;
            Attach(cv);
            _attached.Add(cv);
            _done.Add(n);
        }

        // UI children may be created a little after the canvas itself, so re-apply the
        // raycast rule a few times during the first seconds.
        _refresh += Time.unscaledDeltaTime;
        if (_refresh >= 0.5f)
        {
            _refresh = 0f;
            foreach (var cv in _attached)
                if (cv != null) LetBackgroundPassRays(cv);
        }

        if (_t > 10f) Destroy(gameObject);
    }

    private static void Attach(Canvas canvas)
    {
        var cvRT = canvas.GetComponent<RectTransform>();
        float sx = cvRT.localScale.x;   // world size of one canvas unit
        float sy = cvRT.localScale.y;

        // Which side is the viewer on? The collider must sit BEHIND the panel,
        // otherwise it would block the buttons.
        float behind = 1f; // +Z local = behind a standard uGUI world-space canvas
        var cam = Camera.main;
        if (cam != null && Vector3.Dot(cvRT.forward, cam.transform.position - cvRT.position) > 0f)
            behind = -1f;

        // Holster: same pose as the canvas, canvas becomes its child.
        var holster = new GameObject(canvas.name + "_Holster");
        holster.transform.SetPositionAndRotation(cvRT.position, cvRT.rotation);
        canvas.transform.SetParent(holster.transform, true);

        // Invisible collider covering the whole panel, just behind it.
        var col = new GameObject("GrabCollider");
        col.transform.SetParent(holster.transform, false);
        Rect r = cvRT.rect;
        col.transform.localPosition = new Vector3(
            r.center.x * sx,
            r.center.y * sy,
            behind * kColliderBehind);
        var box = col.AddComponent<BoxCollider>();
        box.size = new Vector3(r.width * sx, r.height * sy, kColliderDepth);

        LetBackgroundPassRays(canvas);

        // Collider must exist before the manipulator is added.
        var om = holster.AddComponent<ObjectManipulator>();
        om.AllowedManipulations = TransformFlags.Move | TransformFlags.Rotate; // no scale
    }

    /// <summary>
    /// Only buttons/selectables keep raycastTarget. Backgrounds and plain labels let the
    /// ray pass through to the grab collider behind the panel.
    /// </summary>
    private static void LetBackgroundPassRays(Canvas canvas)
    {
        var graphics = canvas.GetComponentsInChildren<Graphic>(true);
        foreach (var g in graphics)
        {
            if (g.GetComponentInParent<Selectable>(true) != null) continue; // part of a button
            g.raycastTarget = false;
        }
    }
}