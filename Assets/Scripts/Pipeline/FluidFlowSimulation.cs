using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Dual Fluid Flow Simulation Engine:
/// Simulation 1: Forward Flow (Cube.001 Source -> Cube.002 Destination)
/// Simulation 2: Reverse Flow (Cube.002 Source -> Cube.001 Destination - 8 Points Path)
/// Both simulations feature independent customizable Scene View waypoints and UI controls.
///
/// Rig support: the pipeline root (parent of the pipe object) can be moved, rotated and scaled.
/// Waypoint offsets stay stored as world-axis offsets of the rest pose; at runtime they follow
/// the rig. Tank fluid is parented to the rig, particle size follows the rig scale.
/// </summary>
public class FluidFlowSimulation : MonoBehaviour
{
    public enum FlowDirection
    {
        None,
        Forward_Cube1To2,
        Reverse_Cube2To1
    }

    [Header("Tank & Pipe References")]
    public GameObject sourceTank;       // Cube.001
    public GameObject pipe;             // Cube (physical pipe object)
    public GameObject destinationTank;  // Cube.002

    [Header("Simulation 1: Forward Flow (Cube.001 -> Cube.002)")]
    public bool useCustomWaypointsSim1 = true;
    public List<Vector3> customWaypointsSim1 = new List<Vector3>();

    [Header("Simulation 2: Reverse Flow (Cube.002 -> Cube.001 - 8 Points)")]
    public bool useCustomWaypointsSim2 = true;
    public List<Vector3> customWaypointsSim2 = new List<Vector3>();

    [Header("Default Pipe Offsets")]
    public float upperPipeHeight = 0.58f;
    public float upperRightXOffset = 2.1f;
    public float bendStartXOffset = 0.8f;
    public float bendEndXOffset = -0.2f;
    public float lowerLeftXOffset = -2.5f;

    [Header("Particle Appearance")]
    public float particleSize = 0.055f;
    public float emissionRate = 180f;

    [Header("Flow Parameters")]
    public float flowRate = 5f;

    [Header("Pipe Opening Threshold Limits")]
    [Tooltip("Percentage height limit for Simulation 1 (Cube.001 -> Cube.002). Fluid stops draining at 28.7%.")]
    [Range(5f, 60f)]
    public float minDrainSim1 = 28.7f;

    [Tooltip("Percentage height limit for Simulation 2 (Cube.002 -> Cube.001). Fluid stops draining at 37.8%.")]
    [Range(5f, 60f)]
    public float minDrainSim2 = 37.8f;

    [Header("Live Tank Fill (0\u2013100%)")]
    [Range(0f, 100f)] public float sourceFillPercent      = 100f;
    [Range(0f, 100f)] public float destinationFillPercent = 0f;

    [Header("UI Placement (World Space - HoloLens)")]
    [Tooltip("Where the Sim 1/Sim 2 panel sits, relative to the pipe object (world axes).")]
    public Vector3 uiOffsetFromPipe = new Vector3(2.6f, 0.7f, 0.4f);
    [Tooltip("Rotation around Y. 180 = panel faces toward +Z (viewer standing at +Z).")]
    public float uiYaw = 180f;
    [Tooltip("World size of one canvas unit. 0.001 = 1 unit is 1 mm.")]
    public float uiScale = 0.001f;
    [Tooltip("Canvas size in canvas units (width, height).")]
    public Vector2 uiPanelSize = new Vector2(400f, 420f);

    // Private Fields
    private FlowDirection _activeDirection = FlowDirection.None;
    public FlowDirection ActiveDirection => _activeDirection;
    public bool IsFlowing => _activeDirection != FlowDirection.None;

    private Transform _srcFluid;
    private Transform _dstFluid;

    private ParticleSystem _pipePS;
    private ParticleSystem.Particle[] _particleBuffer;

    private Canvas _canvas;
    private bool   _xrModuleReady;

    // Rig (pipeline root) tracking
    private Transform  _rig;
    private bool       _restCaptured;
    private Quaternion _restRot   = Quaternion.identity;
    private Vector3    _restScale = Vector3.one;
    private Bounds     _srcLocalBounds;
    private Bounds     _dstLocalBounds;

    // UI elements - Sim 1
    private Button _startBtnSim1;
    private Text   _startBtnTxtSim1;
    private Text   _srcLblSim1, _dstLblSim1;

    // UI elements - Sim 2
    private Button _startBtnSim2;
    private Text   _startBtnTxtSim2;
    private Text   _srcLblSim2, _dstLblSim2;

    static readonly Color kFluidBase  = new Color(0.08f, 0.52f, 0.88f, 0.75f); // Realistic Aqua Water
    static readonly Color kFluidFlow  = new Color(0.12f, 0.65f, 1.00f, 0.85f); // Dynamic Flow Water
    static readonly Color kParticle1  = new Color(0.12f, 0.72f, 1.00f, 0.82f); // Vibrant Water Liquid
    static readonly Color kParticle2  = new Color(0.00f, 0.78f, 1.00f, 0.82f); // Bright Aqua Liquid

    const float kInset = 0.04f;

    private void Awake()
    {
        Resolve();
        CaptureRigRest();

        if (sourceTank != null)      _srcFluid = BuildInternalFluid(sourceTank, "Src");
        if (destinationTank != null) _dstFluid = BuildInternalFluid(destinationTank, "Dst");

        if (customWaypointsSim1 == null || customWaypointsSim1.Count < 9) ResetWaypointsSim1ToDefault();
        if (customWaypointsSim2 == null || customWaypointsSim2.Count < 8) ResetWaypointsSim2ToDefault();

        BuildPipeParticles();

        sourceFillPercent      = 100f;
        destinationFillPercent = 0f;
        RefreshFluids();

        BuildUI();
    }

    private void OnValidate()
    {
        if (customWaypointsSim1 == null || customWaypointsSim1.Count < 9)
        {
            ResetWaypointsSim1ToDefault();
        }
        if (customWaypointsSim2 == null || customWaypointsSim2.Count < 8)
        {
            ResetWaypointsSim2ToDefault();
        }
    }

    [Header("Pump & Valve Dependencies")]
    public PipelineControl.PumpController pumpController;
    public PipelineControl.ValveController valveController;

    private void Start()
    {
        EnsureEventSystem();
        if (pumpController == null) pumpController = FindAnyObjectByType<PipelineControl.PumpController>();
        if (valveController == null) valveController = FindAnyObjectByType<PipelineControl.ValveController>();
        SyncUI();
    }

    private void Update()
    {
        // Make sure the scene's EventSystem uses the XR UI Input Module (needed for hand/gaze clicks).
        if (!_xrModuleReady) EnsureXRInputModule();

        RefreshFluids();

        if (!IsFlowing) return;

        if (pumpController == null) pumpController = FindAnyObjectByType<PipelineControl.PumpController>();
        if (valveController == null) valveController = FindAnyObjectByType<PipelineControl.ValveController>();

        bool canFlow = true;
        if (_activeDirection == FlowDirection.Reverse_Cube2To1)
        {
            canFlow = (pumpController == null || pumpController.IsOn) && (valveController == null || valveController.IsOpen);
        }
        else if (_activeDirection == FlowDirection.Forward_Cube1To2)
        {
            canFlow = (valveController == null || valveController.IsOpen);
        }

        if (_pipePS != null)
        {
            var em = _pipePS.emission;
            em.enabled = canFlow;

            if (!_pipePS.isPlaying) _pipePS.Play();
            UpdateParticlesAlongPath();
        }

        if (!canFlow) return; // Stop tank fill math when Pump is OFF or Valve is CLOSED

        float delta = flowRate * Time.deltaTime;

        if (_activeDirection == FlowDirection.Forward_Cube1To2)
        {
            float maxDrainable = Mathf.Max(0f, sourceFillPercent - minDrainSim1);
            float maxFillable  = Mathf.Max(0f, (100f - minDrainSim1) - destinationFillPercent);
            delta = Mathf.Min(delta, maxDrainable);
            delta = Mathf.Min(delta, maxFillable);

            if (delta > 0f)
            {
                sourceFillPercent      -= delta;
                destinationFillPercent += delta;
            }

            if (sourceFillPercent <= minDrainSim1 + 0.05f || destinationFillPercent >= (100f - minDrainSim1) - 0.05f)
            {
                sourceFillPercent      = minDrainSim1;
                destinationFillPercent = 100f - minDrainSim1;
                StopFlow();
            }
        }
        else if (_activeDirection == FlowDirection.Reverse_Cube2To1)
        {
            float maxDrainable = Mathf.Max(0f, destinationFillPercent - minDrainSim2);
            float maxFillable  = Mathf.Max(0f, (100f - minDrainSim2) - sourceFillPercent);
            delta = Mathf.Min(delta, maxDrainable);
            delta = Mathf.Min(delta, maxFillable);

            if (delta > 0f)
            {
                destinationFillPercent -= delta;
                sourceFillPercent      += delta;
            }

            if (destinationFillPercent <= minDrainSim2 + 0.05f || sourceFillPercent >= (100f - minDrainSim2) - 0.05f)
            {
                destinationFillPercent = minDrainSim2;
                sourceFillPercent      = 100f - minDrainSim2;
                StopFlow();
            }
        }

        SyncUI();
    }

    private void OnDrawGizmos()
    {
        // Path 1 (Forward: Cyan Blue)
        Vector3[] way1 = GetWaypointsSim1();
        if (way1 != null && way1.Length >= 2)
        {
            Gizmos.color = new Color(0f, 0.85f, 1f, 0.95f);
            for (int i = 0; i < way1.Length - 1; i++)
            {
                Gizmos.DrawLine(way1[i], way1[i + 1]);
                Gizmos.DrawSphere(way1[i], 0.04f);
            }
            Gizmos.DrawSphere(way1[way1.Length - 1], 0.04f);
        }

        // Path 2 (Reverse: Deep Electric Blue - 8 Points)
        Vector3[] way2 = GetWaypointsSim2();
        if (way2 != null && way2.Length >= 2)
        {
            Gizmos.color = new Color(0.1f, 0.65f, 1f, 0.95f);
            for (int i = 0; i < way2.Length - 1; i++)
            {
                Gizmos.DrawLine(way2[i], way2[i + 1]);
                Gizmos.DrawSphere(way2[i], 0.04f);
            }
            Gizmos.DrawSphere(way2[way2.Length - 1], 0.04f);
        }
    }

    // ------------------------------------------------------------------
    // Rig support
    // ------------------------------------------------------------------

    // Remembers the rig's rest pose (rotation + scale) at startup.
    private void CaptureRigRest()
    {
        _rig = (pipe != null) ? pipe.transform.parent : null;
        if (_rig == null)
        {
            _restCaptured = false;
            return;
        }
        _restRot      = _rig.rotation;
        _restScale    = _rig.lossyScale;
        _restCaptured = true;
    }

    private static float SafeDiv(float a, float b)
    {
        return Mathf.Abs(b) > 1e-6f ? a / b : 1f;
    }

    // Uniform scale of the rig relative to its rest pose (1 = unchanged).
    private float RigScaleRatio
    {
        get
        {
            if (!_restCaptured || _rig == null) return 1f;
            return SafeDiv(_rig.lossyScale.x, _restScale.x);
        }
    }

    // Stored offsets are world-axis offsets of the rest pose. This converts one
    // to a world offset that follows the rig's current rotation and scale.
    private Vector3 OffsetToWorld(Vector3 offset)
    {
        if (!_restCaptured || _rig == null) return offset;

        Vector3 cur   = _rig.lossyScale;
        Vector3 ratio = new Vector3(
            SafeDiv(cur.x, _restScale.x),
            SafeDiv(cur.y, _restScale.y),
            SafeDiv(cur.z, _restScale.z));

        Vector3 local = Quaternion.Inverse(_restRot) * offset;
        return _rig.rotation * Vector3.Scale(local, ratio);
    }

    // Bounds of a tank in the rig's local space (world space if there is no rig).
    private Bounds LocalBounds(GameObject go)
    {
        Matrix4x4 toSpace = (_rig != null) ? _rig.worldToLocalMatrix : Matrix4x4.identity;
        bool first = true;
        Bounds b = default;

        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            Bounds lb;
            Matrix4x4 m;
            var mf = r.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && !(r is SkinnedMeshRenderer))
            {
                lb = mf.sharedMesh.bounds;
                m  = toSpace * r.localToWorldMatrix;
            }
            else
            {
                lb = r.bounds;
                m  = toSpace;
            }

            Vector3 c = lb.center;
            Vector3 e = lb.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + new Vector3(
                    (i & 1) == 0 ? -e.x : e.x,
                    (i & 2) == 0 ? -e.y : e.y,
                    (i & 4) == 0 ? -e.z : e.z);
                Vector3 p = m.MultiplyPoint3x4(corner);
                if (first) { b = new Bounds(p, Vector3.zero); first = false; }
                else b.Encapsulate(p);
            }
        }

        if (first) b = new Bounds(toSpace.MultiplyPoint3x4(go.transform.position), Vector3.one);
        return b;
    }

    // Public API
    public void StartFlowSim1()
    {
        if (IsFlowing)
        {
            if (_activeDirection == FlowDirection.Forward_Cube1To2) StopFlow();
            return;
        }

        // Cannot start Sim 1 if Tank 1 is already drained down to pipe height limit
        if (sourceFillPercent <= minDrainSim1 + 0.05f) return;

        if (valveController == null) valveController = FindAnyObjectByType<PipelineControl.ValveController>();
        if (valveController != null) valveController.SetValveState(true);

        _activeDirection = FlowDirection.Forward_Cube1To2;
        if (_pipePS != null)
        {
            _pipePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _pipePS.Clear();
            var main = _pipePS.main;
            main.startColor = kParticle1;
            var em = _pipePS.emission;
            em.enabled = true;
            var psr = _pipePS.GetComponent<ParticleSystemRenderer>();
            if (psr != null) psr.material = MakeParticleMat(kParticle1);
            _pipePS.Play();
        }
        SyncUI();
    }

    public void StartFlowSim2()
    {
        if (IsFlowing)
        {
            if (_activeDirection == FlowDirection.Reverse_Cube2To1) StopFlow();
            return;
        }

        // Cannot start Sim 2 if Tank 2 is already drained down to pipe height limit
        if (destinationFillPercent <= minDrainSim2 + 0.05f) return;

        if (pumpController == null) pumpController = FindAnyObjectByType<PipelineControl.PumpController>();
        if (valveController == null) valveController = FindAnyObjectByType<PipelineControl.ValveController>();

        if (pumpController != null) pumpController.SetPumpState(true);
        if (valveController != null) valveController.SetValveState(true);

        _activeDirection = FlowDirection.Reverse_Cube2To1;
        if (_pipePS != null)
        {
            _pipePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _pipePS.Clear();
            var main = _pipePS.main;
            main.startColor = kParticle2;
            var em = _pipePS.emission;
            em.enabled = true;
            var psr = _pipePS.GetComponent<ParticleSystemRenderer>();
            if (psr != null) psr.material = MakeParticleMat(kParticle2);
            _pipePS.Play();
        }
        SyncUI();
    }

    public void StopFlow()
    {
        _activeDirection = FlowDirection.None;

        if (_pipePS != null)
        {
            _pipePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _pipePS.Clear();
        }

        RefreshFluids();
        SyncUI();
    }

    public Vector3 GetPipePosition()
    {
        if (pipe != null) return pipe.transform.position;
        var pObj = GameObject.Find("Cube");
        if (pObj != null) return pObj.transform.position;
        return new Vector3(0f, 1.25f, 0f);
    }

    public void ResetWaypointsSim1ToDefault()
    {
        // 9 Default Points for Forward Flow through LOWER PIPE (Cube.001 -> Cube.002)
        customWaypointsSim1 = new List<Vector3>
        {
            new Vector3(-1.85f, 0.18f, 0f), // P0: Lower pipe mouth at Cube.001
            new Vector3(-1.30f, 0.18f, 0f), // P1: Lower horizontal pipe
            new Vector3(-0.70f, 0.18f, 0f), // P2: Lower horizontal pipe
            new Vector3(-0.10f, 0.18f, 0f), // P3: Lower horizontal pipe
            new Vector3(0.50f,  0.18f, 0f), // P4: Lower horizontal pipe
            new Vector3(1.00f,  0.18f, 0f), // P5: Lower horizontal pipe
            new Vector3(1.45f,  0.18f, 0f), // P6: Lower horizontal pipe
            new Vector3(1.70f,  0.18f, 0f), // P7: Lower pipe near outlet
            new Vector3(1.85f,  0.18f, 0f)  // P8: Point after P7 at Cube.002 outlet
        };
    }

    public Vector3[] GetWaypointsSim1()
    {
        Vector3 pipePos = GetPipePosition();
        if (useCustomWaypointsSim1 && customWaypointsSim1 != null && customWaypointsSim1.Count >= 2)
        {
            Vector3[] res = new Vector3[customWaypointsSim1.Count];
            for (int i = 0; i < customWaypointsSim1.Count; i++) res[i] = pipePos + OffsetToWorld(customWaypointsSim1[i]);
            return res;
        }
        return new Vector3[] {
            pipePos + OffsetToWorld(new Vector3(-1.85f, 0.18f, 0f)),
            pipePos + OffsetToWorld(new Vector3(-1.30f, 0.18f, 0f)),
            pipePos + OffsetToWorld(new Vector3(-0.70f, 0.18f, 0f)),
            pipePos + OffsetToWorld(new Vector3(-0.10f, 0.18f, 0f)),
            pipePos + OffsetToWorld(new Vector3(0.50f,  0.18f, 0f)),
            pipePos + OffsetToWorld(new Vector3(1.00f,  0.18f, 0f)),
            pipePos + OffsetToWorld(new Vector3(1.45f,  0.18f, 0f)),
            pipePos + OffsetToWorld(new Vector3(1.70f,  0.18f, 0f)),
            pipePos + OffsetToWorld(new Vector3(1.85f,  0.18f, 0f))
        };
    }

    public void ResetWaypointsSim2ToDefault()
    {
        // 8 Default Points for Reverse Flow through UPPER PIPE (Cube.002 -> Cube.001)
        customWaypointsSim2 = new List<Vector3>
        {
            new Vector3(1.85f,  0.18f, 0f), // P0: Lower inlet mouth at Cube.002
            new Vector3(1.85f,  0.35f, 0f), // P1: Vertical pipe rise
            new Vector3(1.80f,  0.52f, 0f), // P2: Upper-right elbow entry
            new Vector3(1.50f,  0.58f, 0f), // P3: Upper-right elbow exit
            new Vector3(0.70f,  0.58f, 0f), // P4: Upper horizontal pipe right (through pump/valve)
            new Vector3(-0.30f, 0.58f, 0f), // P5: Upper horizontal pipe left
            new Vector3(-1.10f, 0.58f, 0f), // P6: Top-left bend
            new Vector3(-1.85f, 0.58f, 0f)  // P7: Upper pipe mouth at Cube.001 white fitting
        };
    }

    public Vector3[] GetWaypointsSim2()
    {
        Vector3 pipePos = GetPipePosition();
        if (useCustomWaypointsSim2 && customWaypointsSim2 != null && customWaypointsSim2.Count >= 2)
        {
            Vector3[] res = new Vector3[customWaypointsSim2.Count];
            for (int i = 0; i < customWaypointsSim2.Count; i++) res[i] = pipePos + OffsetToWorld(customWaypointsSim2[i]);
            return res;
        }
        return new Vector3[] {
            pipePos + OffsetToWorld(new Vector3(1.85f,  0.18f, 0f)),
            pipePos + OffsetToWorld(new Vector3(1.85f,  0.35f, 0f)),
            pipePos + OffsetToWorld(new Vector3(1.80f,  0.52f, 0f)),
            pipePos + OffsetToWorld(new Vector3(1.50f,  0.58f, 0f)),
            pipePos + OffsetToWorld(new Vector3(0.70f,  0.58f, 0f)),
            pipePos + OffsetToWorld(new Vector3(-0.30f, 0.58f, 0f)),
            pipePos + OffsetToWorld(new Vector3(-1.10f, 0.58f, 0f)),
            pipePos + OffsetToWorld(new Vector3(-1.85f, 0.58f, 0f))
        };
    }

    private void BuildPipeParticles()
    {
        var psGO = new GameObject("PipeFluidParticles_Waypointed");
        psGO.transform.SetParent(this.transform, false);
        psGO.transform.position = Vector3.zero;
        psGO.transform.rotation = Quaternion.identity;

        _pipePS = psGO.AddComponent<ParticleSystem>();

        var main              = _pipePS.main;
        main.loop             = true;
        main.playOnAwake      = false;
        main.startLifetime    = 2.0f;
        main.startSpeed       = 0f;
        main.startSize        = particleSize;
        main.startColor       = kParticle1;
        main.maxParticles     = 500;
        main.simulationSpace  = ParticleSystemSimulationSpace.World;

        var em          = _pipePS.emission;
        em.enabled      = true;
        em.rateOverTime = emissionRate;

        var sh       = _pipePS.shape;
        sh.enabled   = false;

        var sizeOL     = _pipePS.sizeOverLifetime;
        sizeOL.enabled = true;
        sizeOL.size    = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.7f), new Keyframe(0.1f, 1f), new Keyframe(0.9f, 1f), new Keyframe(1f, 0.95f)));

        var col     = _pipePS.colorOverLifetime;
        col.enabled = true;
        var cg      = new Gradient();
        cg.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1.0f) },
            new[] { new GradientAlphaKey(0.95f, 0.0f),
                    new GradientAlphaKey(0.95f, 0.96f),
                    new GradientAlphaKey(0.85f, 1.00f) });
        col.color = cg;

        var psr           = psGO.GetComponent<ParticleSystemRenderer>();
        psr.material      = MakeParticleMat(kParticle1);
        psr.renderMode    = ParticleSystemRenderMode.Billboard;

        _pipePS.Stop();
    }

    private void UpdateParticlesAlongPath()
    {
        if (_pipePS == null || !IsFlowing) return;

        int maxP = _pipePS.main.maxParticles;
        if (_particleBuffer == null || _particleBuffer.Length < maxP)
            _particleBuffer = new ParticleSystem.Particle[maxP];

        int count = _pipePS.GetParticles(_particleBuffer);
        Vector3[] waypoints = (_activeDirection == FlowDirection.Reverse_Cube2To1) ? GetWaypointsSim2() : GetWaypointsSim1();
        if (waypoints == null || waypoints.Length < 2) return;

        int numSegments = waypoints.Length - 1;
        float[] segmentLengths = new float[numSegments];
        float totalL = 0f;

        for (int i = 0; i < numSegments; i++)
        {
            segmentLengths[i] = Vector3.Distance(waypoints[i], waypoints[i + 1]);
            totalL += segmentLengths[i];
        }
        totalL = Mathf.Max(totalL, 0.001f);

        float scaleRatio = RigScaleRatio;
        float pSize      = particleSize * scaleRatio;
        float jitterAmt  = 0.006f * scaleRatio;

        for (int i = 0; i < count; i++)
        {
            float totalLife = _particleBuffer[i].startLifetime;
            float remaining = _particleBuffer[i].remainingLifetime;
            float age = totalLife - remaining;
            float t = Mathf.Clamp01(age / Mathf.Max(totalLife, 0.01f));

            float targetDist = t * totalL;
            float accumulated = 0f;
            Vector3 pos = waypoints[0];
            Vector3 dir = Vector3.forward;

            for (int s = 0; s < numSegments; s++)
            {
                float segLen = segmentLengths[s];
                if (targetDist <= accumulated + segLen || s == numSegments - 1)
                {
                    float localDist = targetDist - accumulated;
                    float localT = segLen > 0.0001f ? Mathf.Clamp01(localDist / segLen) : 0f;
                    pos = Vector3.Lerp(waypoints[s], waypoints[s + 1], localT);
                    Vector3 segmentDir = (waypoints[s + 1] - waypoints[s]);
                    if (segmentDir.sqrMagnitude > 0.0001f) dir = segmentDir.normalized;
                    break;
                }
                accumulated += segLen;
            }

            uint id = _particleBuffer[i].randomSeed;
            float rAngle = (id % 360) * Mathf.Deg2Rad;
            Vector3 perp = Vector3.Cross(dir, Vector3.up);
            if (perp.sqrMagnitude < 0.01f) perp = Vector3.Cross(dir, Vector3.forward);
            perp.Normalize();
            Vector3 perp2 = Vector3.Cross(dir, perp).normalized;
            Vector3 jitter = (perp * Mathf.Cos(rAngle) + perp2 * Mathf.Sin(rAngle)) * jitterAmt;

            _particleBuffer[i].position  = pos + jitter;
            _particleBuffer[i].velocity  = Vector3.zero;
            _particleBuffer[i].startSize = pSize;
        }

        _pipePS.SetParticles(_particleBuffer, count);
    }

    private void Resolve()
    {
        if (sourceTank == null) sourceTank = GameObject.Find("Cube.001");
        if (pipe == null) pipe = GameObject.Find("Cube");
        if (destinationTank == null) destinationTank = GameObject.Find("Cube.002");
    }

    private Material _srcMat;
    private Material _dstMat;

    private Transform BuildInternalFluid(GameObject tank, string label)
    {
        Bounds lb = LocalBounds(tank);
        if (label == "Src") _srcLocalBounds = lb;
        else if (label == "Dst") _dstLocalBounds = lb;

        float insetX = lb.size.x * kInset;
        float insetZ = lb.size.z * kInset;
        float fluidW = lb.size.x - insetX * 2f;
        float fluidD = lb.size.z - insetZ * 2f;

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "InternalFluid_" + label;
        Object.DestroyImmediate(go.GetComponent<Collider>());

        // Parent to the rig so the fluid moves, rotates and scales with it.
        go.transform.SetParent(_rig != null ? _rig : this.transform, false);

        Vector3 pos = new Vector3(lb.center.x, lb.min.y + 0.0005f, lb.center.z);
        if (_rig != null)
        {
            go.transform.localRotation = Quaternion.identity;
            go.transform.localPosition = pos;
        }
        else
        {
            go.transform.rotation = Quaternion.identity;
            go.transform.position = pos;
        }
        go.transform.localScale = new Vector3(fluidW, 0.001f, fluidD);

        var mat = MakeFluidMaterial(kFluidBase);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (label == "Src") _srcMat = mat;
        else if (label == "Dst") _dstMat = mat;

        return go.transform;
    }

    private void RefreshFluids()
    {
        SetFluidLevel(_srcFluid, _srcLocalBounds, sourceFillPercent);
        SetFluidLevel(_dstFluid, _dstLocalBounds, destinationFillPercent);

        Color col = IsFlowing ? kFluidFlow : kFluidBase;
        TintFluid(_srcFluid, _srcMat, col);
        TintFluid(_dstFluid, _dstMat, col);
    }

    private void SetFluidLevel(Transform fluid, Bounds lb, float pct)
    {
        if (fluid == null) return;

        float insetX = lb.size.x * kInset;
        float insetZ = lb.size.z * kInset;
        float fluidW = Mathf.Max(lb.size.x - insetX * 2f, 0.001f);
        float fluidD = Mathf.Max(lb.size.z - insetZ * 2f, 0.001f);

        float fill  = Mathf.Clamp01(pct / 100f);
        float fillH = Mathf.Max(lb.size.y * fill, 0.001f);

        Vector3 pos = new Vector3(lb.center.x, lb.min.y + fillH * 0.5f, lb.center.z);
        if (_rig != null) fluid.localPosition = pos;
        else              fluid.position      = pos;
        fluid.localScale = new Vector3(fluidW, fillH, fluidD);
    }

    private static void TintFluid(Transform fluid, Material mat, Color col)
    {
        if (fluid == null || mat == null) return;
        ConfigureWaterMaterial(mat, col);
    }

    private void BuildUI()
    {
        // ---- World Space canvas (HoloLens) ----
        var cvGO = new GameObject("FluidFlowSimCanvas");
        var cv   = cvGO.AddComponent<Canvas>();
        cv.renderMode   = RenderMode.WorldSpace;
        cv.sortingOrder = 20;
        cv.worldCamera  = Camera.main;
        _canvas = cv;
        cvGO.AddComponent<CanvasScaler>();
        cvGO.AddComponent<GraphicRaycaster>();
        cvGO.AddComponent<TrackedDeviceGraphicRaycaster>();

        var cvRT = cvGO.GetComponent<RectTransform>();
        cvRT.sizeDelta  = uiPanelSize;
        cvRT.localScale = Vector3.one * uiScale;
        cvRT.position   = GetPipePosition() + uiOffsetFromPipe;
        cvRT.rotation   = Quaternion.Euler(0f, uiYaw, 0f);

        Font f = GetFont();

        // PANEL 1: SIMULATION 1 (Forward: Cube.001 -> Cube.002) - All Blue Styling
        var pan1GO = new GameObject("FluidFlowPanel_Sim1");
        pan1GO.transform.SetParent(cvGO.transform, false);
        pan1GO.AddComponent<Image>().color = new Color(0.03f, 0.08f, 0.18f, 0.95f);
        var pan1RT       = pan1GO.GetComponent<RectTransform>();
        pan1RT.anchorMin = new Vector2(0f, 0f);
        pan1RT.anchorMax = new Vector2(1f, 0.48f);
        pan1RT.offsetMin = pan1RT.offsetMax = Vector2.zero;

        MkLbl(pan1GO.transform, "Title1", "SIMULATION 1: FORWARD FLOW",
              f, 17, FontStyle.Bold, new Color(0.3f, 0.9f, 1f),
              new Vector2(0.03f, 0.79f), new Vector2(0.97f, 0.97f));

        var div1 = new GameObject("Div1");
        div1.transform.SetParent(pan1GO.transform, false);
        div1.AddComponent<Image>().color = new Color(0.3f, 0.9f, 1f, 0.35f);
        var d1RT       = div1.GetComponent<RectTransform>();
        d1RT.anchorMin = new Vector2(0.03f, 0.775f);
        d1RT.anchorMax = new Vector2(0.97f, 0.785f);
        d1RT.offsetMin = d1RT.offsetMax = Vector2.zero;

        _srcLblSim1 = MkLbl(pan1GO.transform, "SrcLbl1", "Cube.001 (Source): 100.0%",
                            f, 15, FontStyle.Normal, new Color(0.75f, 0.95f, 1f),
                            new Vector2(0.03f, 0.57f), new Vector2(0.97f, 0.76f));

        _dstLblSim1 = MkLbl(pan1GO.transform, "DstLbl1", "Cube.002 (Dest):   0.0%",
                            f, 15, FontStyle.Normal, new Color(0.75f, 0.95f, 1f),
                            new Vector2(0.03f, 0.37f), new Vector2(0.97f, 0.56f));

        var btn1GO    = new GameObject("StartBtn_Sim1");
        btn1GO.transform.SetParent(pan1GO.transform, false);
        var btn1Img   = btn1GO.AddComponent<Image>();
        btn1Img.color = new Color(0.05f, 0.45f, 0.85f, 1f);
        _startBtnSim1 = btn1GO.AddComponent<Button>();
        _startBtnSim1.targetGraphic = btn1Img;
        var cb1              = _startBtnSim1.colors;
        cb1.normalColor      = new Color(0.05f, 0.45f, 0.85f, 1f);
        cb1.highlightedColor = new Color(0.10f, 0.65f, 1.00f, 1f);
        cb1.pressedColor     = new Color(0.02f, 0.25f, 0.55f, 1f);
        cb1.disabledColor    = new Color(0.22f, 0.22f, 0.22f, 0.90f);
        _startBtnSim1.colors = cb1;
        var b1RT       = btn1GO.GetComponent<RectTransform>();
        b1RT.anchorMin = new Vector2(0.05f, 0.05f);
        b1RT.anchorMax = new Vector2(0.95f, 0.34f);
        b1RT.offsetMin = b1RT.offsetMax = Vector2.zero;

        var lbl1GO     = new GameObject("BtnLbl1");
        lbl1GO.transform.SetParent(btn1GO.transform, false);
        _startBtnTxtSim1 = lbl1GO.AddComponent<Text>();
        _startBtnTxtSim1.font      = f;
        _startBtnTxtSim1.text      = "\u25B6 Start Sim 1 (Cube.001 \u2794 Cube.002)";
        _startBtnTxtSim1.fontSize  = 16;
        _startBtnTxtSim1.fontStyle = FontStyle.Bold;
        _startBtnTxtSim1.alignment = TextAnchor.MiddleCenter;
        _startBtnTxtSim1.color     = Color.white;
        var l1RT       = lbl1GO.GetComponent<RectTransform>();
        l1RT.anchorMin = Vector2.zero;
        l1RT.anchorMax = Vector2.one;
        l1RT.offsetMin = l1RT.offsetMax = Vector2.zero;
        _startBtnSim1.onClick.AddListener(StartFlowSim1);

        // PANEL 2: SIMULATION 2 (Reverse: Cube.002 -> Cube.001 - 8 Points) - All Blue Styling
        var pan2GO = new GameObject("FluidFlowPanel_Sim2");
        pan2GO.transform.SetParent(cvGO.transform, false);
        pan2GO.AddComponent<Image>().color = new Color(0.02f, 0.10f, 0.22f, 0.95f);
        var pan2RT       = pan2GO.GetComponent<RectTransform>();
        pan2RT.anchorMin = new Vector2(0f, 0.52f);
        pan2RT.anchorMax = new Vector2(1f, 1f);
        pan2RT.offsetMin = pan2RT.offsetMax = Vector2.zero;

        MkLbl(pan2GO.transform, "Title2", "SIMULATION 2: REVERSE (8-POINT)",
              f, 17, FontStyle.Bold, new Color(0.2f, 0.85f, 1f),
              new Vector2(0.03f, 0.79f), new Vector2(0.97f, 0.97f));

        var div2 = new GameObject("Div2");
        div2.transform.SetParent(pan2GO.transform, false);
        div2.AddComponent<Image>().color = new Color(0.2f, 0.85f, 1f, 0.35f);
        var d2RT       = div2.GetComponent<RectTransform>();
        d2RT.anchorMin = new Vector2(0.03f, 0.775f);
        d2RT.anchorMax = new Vector2(0.97f, 0.785f);
        d2RT.offsetMin = d2RT.offsetMax = Vector2.zero;

        _dstLblSim2 = MkLbl(pan2GO.transform, "SrcLbl2", "Cube.002 (Source):   0.0%",
                            f, 15, FontStyle.Normal, new Color(0.75f, 0.95f, 1f),
                            new Vector2(0.03f, 0.57f), new Vector2(0.97f, 0.76f));

        _srcLblSim2 = MkLbl(pan2GO.transform, "DstLbl2", "Cube.001 (Dest): 100.0%",
                            f, 15, FontStyle.Normal, new Color(0.75f, 0.95f, 1f),
                            new Vector2(0.03f, 0.37f), new Vector2(0.97f, 0.56f));

        var btn2GO    = new GameObject("StartBtn_Sim2");
        btn2GO.transform.SetParent(pan2GO.transform, false);
        var btn2Img   = btn2GO.AddComponent<Image>();
        btn2Img.color = new Color(0.00f, 0.50f, 0.90f, 1f);
        _startBtnSim2 = btn2GO.AddComponent<Button>();
        _startBtnSim2.targetGraphic = btn2Img;
        var cb2              = _startBtnSim2.colors;
        cb2.normalColor      = new Color(0.00f, 0.50f, 0.90f, 1f);
        cb2.highlightedColor = new Color(0.10f, 0.70f, 1.00f, 1f);
        cb2.pressedColor     = new Color(0.00f, 0.30f, 0.60f, 1f);
        cb2.disabledColor    = new Color(0.22f, 0.22f, 0.22f, 0.90f);
        _startBtnSim2.colors = cb2;
        var b2RT       = btn2GO.GetComponent<RectTransform>();
        b2RT.anchorMin = new Vector2(0.05f, 0.05f);
        b2RT.anchorMax = new Vector2(0.95f, 0.34f);
        b2RT.offsetMin = b2RT.offsetMax = Vector2.zero;

        var lbl2GO     = new GameObject("BtnLbl2");
        lbl2GO.transform.SetParent(btn2GO.transform, false);
        _startBtnTxtSim2 = lbl2GO.AddComponent<Text>();
        _startBtnTxtSim2.font      = f;
        _startBtnTxtSim2.text      = "\u25B6 Start Sim 2 (Cube.002 \u2794 Cube.001)";
        _startBtnTxtSim2.fontSize  = 16;
        _startBtnTxtSim2.fontStyle = FontStyle.Bold;
        _startBtnTxtSim2.alignment = TextAnchor.MiddleCenter;
        _startBtnTxtSim2.color     = Color.white;
        var l2RT       = lbl2GO.GetComponent<RectTransform>();
        l2RT.anchorMin = Vector2.zero;
        l2RT.anchorMax = Vector2.one;
        l2RT.offsetMin = l2RT.offsetMax = Vector2.zero;
        _startBtnSim2.onClick.AddListener(StartFlowSim2);
    }

    private Text MkLbl(Transform parent, string n, string txt, Font f,
                       int sz, FontStyle fs, Color col, Vector2 aMin, Vector2 aMax)
    {
        var go       = new GameObject(n);
        go.transform.SetParent(parent, false);
        var t        = go.AddComponent<Text>();
        t.font       = f; t.text = txt; t.fontSize = sz;
        t.fontStyle  = fs; t.color = col;
        t.alignment  = TextAnchor.MiddleLeft;
        var rt       = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.offsetMin = new Vector2(8f, 0f);
        rt.offsetMax = new Vector2(-8f, 0f);
        return t;
    }

    private void SyncUI()
    {
        if (_srcLblSim1 != null) _srcLblSim1.text = $"Cube.001 (Source):   {sourceFillPercent:F1}%";
        if (_dstLblSim1 != null) _dstLblSim1.text = $"Cube.002 (Dest):     {destinationFillPercent:F1}%";

        if (_srcLblSim2 != null) _srcLblSim2.text = $"Cube.001 (Dest):     {sourceFillPercent:F1}%";
        if (_dstLblSim2 != null) _dstLblSim2.text = $"Cube.002 (Source):   {destinationFillPercent:F1}%";

        if (_startBtnSim1 != null)
        {
            bool active1 = (_activeDirection == FlowDirection.Forward_Cube1To2);
            bool canStart1 = (sourceFillPercent > minDrainSim1 + 0.05f);
            _startBtnSim1.interactable = active1 || (!IsFlowing && canStart1);
            if (_startBtnTxtSim1 != null)
                _startBtnTxtSim1.text = active1 ? "\u23F9 Stop Sim 1" : (canStart1 ? "\u25B6 Start Sim 1 (Cube.001 \u2794 Cube.002)" : "\u25B6 Sim 1 Drained (Cube.001 Empty)");
        }

        if (_startBtnSim2 != null)
        {
            bool active2 = (_activeDirection == FlowDirection.Reverse_Cube2To1);
            bool canStart2 = (destinationFillPercent > minDrainSim2 + 0.05f);
            _startBtnSim2.interactable = active2 || (!IsFlowing && canStart2);
            if (_startBtnTxtSim2 != null)
                _startBtnTxtSim2.text = active2 ? "\u23F9 Stop Sim 2" : (canStart2 ? "\u25B6 Start Sim 2 (Cube.002 \u2794 Cube.001)" : "\u25B6 Sim 2 Drained (Cube.002 Empty)");
        }
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem_FluidSim");
        es.AddComponent<EventSystem>();
        es.AddComponent<XRUIInputModule>();
    }

    // Replaces any non-XR input module on the scene's EventSystem with XRUIInputModule,
    // so hand/gaze interaction can click the world-space canvas.
    private void EnsureXRInputModule()
    {
        var es = FindAnyObjectByType<EventSystem>();
        if (es == null) return;

        foreach (var m in es.GetComponents<BaseInputModule>())
        {
            if (!(m is XRUIInputModule)) Destroy(m);
        }
        if (es.GetComponent<XRUIInputModule>() == null)
            es.gameObject.AddComponent<XRUIInputModule>();

        if (_canvas != null && _canvas.worldCamera == null)
            _canvas.worldCamera = Camera.main;

        _xrModuleReady = true;
    }

    private static Font GetFont()
    {
        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (f == null) try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 14);
        return f;
    }

    private static Material MakeFluidMaterial(Color baseColor)
    {
        var sh  = Shader.Find("HDRP/Lit")
               ?? Shader.Find("Universal Render Pipeline/Lit")
               ?? Shader.Find("Standard");
        var mat = new Material(sh);
        ConfigureWaterMaterial(mat, baseColor);
        return mat;
    }

    private static void ConfigureWaterMaterial(Material mat, Color baseColor)
    {
        if (mat == null) return;

        Color waterColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.75f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", waterColor);
        if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     waterColor);

        // High Specular & Smoothness (Realistic Water Reflections)
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.96f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.96f);
        if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic",   0.05f);

        // HDRP / Transparency properties
        if (mat.HasProperty("_SurfaceType")) mat.SetFloat("_SurfaceType", 1f); // Transparent
        if (mat.HasProperty("_BlendMode"))   mat.SetFloat("_BlendMode",   0f); // Alpha Blend
        if (mat.HasProperty("_EnableRefraction")) mat.SetFloat("_EnableRefraction", 1f);
        if (mat.HasProperty("_Ior"))         mat.SetFloat("_Ior",         1.333f); // Water Index of Refraction

        Color em = new Color(0.05f, 0.35f, 0.65f, 0.4f);
        if (mat.HasProperty("_EmissiveColor"))
        {
            mat.SetColor("_EmissiveColor", em);
            if (mat.HasProperty("_UseEmissiveIntensity")) mat.SetFloat("_UseEmissiveIntensity", 1f);
            if (mat.HasProperty("_EmissiveIntensity"))    mat.SetFloat("_EmissiveIntensity",    0.35f);
        }
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.SetColor("_EmissionColor", em);
            mat.EnableKeyword("_EMISSION");
        }

        // Transparency Blend Setup
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private static Material MakeParticleMat(Color col)
    {
        var sh  = Shader.Find("HDRP/Lit")
               ?? Shader.Find("Universal Render Pipeline/Particles/Lit")
               ?? Shader.Find("Particles/Standard Unlit")
               ?? Shader.Find("Standard");
        var mat = new Material(sh);

        Color pCol = new Color(0.12f, 0.72f, 1.00f, 0.82f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", pCol);
        if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     pCol);

        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.96f);
        if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic",   0.05f);

        Color em = pCol * 0.7f;
        if (mat.HasProperty("_EmissiveColor"))
        {
            mat.SetColor("_EmissiveColor", em);
            if (mat.HasProperty("_UseEmissiveIntensity")) mat.SetFloat("_UseEmissiveIntensity", 1f);
            if (mat.HasProperty("_EmissiveIntensity"))    mat.SetFloat("_EmissiveIntensity",    0.4f);
        }
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.SetColor("_EmissionColor", em);
            mat.EnableKeyword("_EMISSION");
        }

        return mat;
    }

    private static void SetMatColor(Material mat, Color col, float emissive)
    {
        if (mat == null) return;
        ConfigureWaterMaterial(mat, col);
    }
}