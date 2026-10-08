#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FluidFlowSimulation))]
public class FluidFlowSimulationEditor : Editor
{
    private FluidFlowSimulation sim;
    private int selectedPathTab = 0; // 0 = Sim 1 (Forward 9-Point), 1 = Sim 2 (Reverse 8-Point)

    private void OnEnable()
    {
        sim = (FluidFlowSimulation)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawDefaultInspector();

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Dual Path Interactive Controls", EditorStyles.boldLabel);

        selectedPathTab = GUILayout.Toolbar(selectedPathTab, new string[] { "Sim 1 Path (Forward 9-Pt)", "Sim 2 Path (Reverse 8-Pt)" }, GUILayout.Height(28));

        EditorGUILayout.Space(6);

        if (selectedPathTab == 0)
        {
            EditorGUILayout.HelpBox(
                "SIMULATION 1 PATH (Cyan Blue Line - 9 Points):\n" +
                "Fluid flows from Cube.001 (Source) ➔ Cube.002 (Destination).\n" +
                "Drag cyan handles labeled Sim1_P0, Sim1_P1... Sim1_P8 in Scene View to adjust.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("➕ Add Point to Sim 1 Path", GUILayout.Height(28)))
            {
                Undo.RecordObject(sim, "Add Point Sim 1");
                EnsureWaypointsSim1();
                Vector3 last = sim.customWaypointsSim1[sim.customWaypointsSim1.Count - 1];
                sim.customWaypointsSim1.Add(last + Vector3.left * 0.4f);
                sim.useCustomWaypointsSim1 = true;
                EditorUtility.SetDirty(sim);
            }

            if (GUILayout.Button("🔄 Reset Sim 1 (9-Point) Path", GUILayout.Height(28)))
            {
                Undo.RecordObject(sim, "Reset Sim 1 Path");
                sim.ResetWaypointsSim1ToDefault();
                sim.useCustomWaypointsSim1 = true;
                EditorUtility.SetDirty(sim);
            }
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.HelpBox(
                "SIMULATION 2 REVERSE PATH (Electric Blue Line - 8 Points):\n" +
                "Fluid flows from Cube.002 (Source) ➔ Cube.001 (Destination).\n" +
                "Drag blue handles labeled Sim2_P0, Sim2_P1... Sim2_P7 in Scene View to adjust.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("➕ Add Point to Sim 2 Path", GUILayout.Height(28)))
            {
                Undo.RecordObject(sim, "Add Point Sim 2");
                EnsureWaypointsSim2();
                Vector3 last = sim.customWaypointsSim2[sim.customWaypointsSim2.Count - 1];
                sim.customWaypointsSim2.Add(last + Vector3.left * 0.4f);
                sim.useCustomWaypointsSim2 = true;
                EditorUtility.SetDirty(sim);
            }

            if (GUILayout.Button("🔄 Reset Sim 2 (8-Point) Path", GUILayout.Height(28)))
            {
                Undo.RecordObject(sim, "Reset Sim 2 Path");
                sim.ResetWaypointsSim2ToDefault();
                sim.useCustomWaypointsSim2 = true;
                EditorUtility.SetDirty(sim);
            }
            EditorGUILayout.EndHorizontal();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void EnsureWaypointsSim1()
    {
        if (sim.customWaypointsSim1 == null || sim.customWaypointsSim1.Count < 9)
        {
            sim.ResetWaypointsSim1ToDefault();
            sim.useCustomWaypointsSim1 = true;
            EditorUtility.SetDirty(sim);
        }
    }

    private void EnsureWaypointsSim2()
    {
        if (sim.customWaypointsSim2 == null || sim.customWaypointsSim2.Count < 8)
        {
            sim.ResetWaypointsSim2ToDefault();
            sim.useCustomWaypointsSim2 = true;
            EditorUtility.SetDirty(sim);
        }
    }

    private void OnSceneGUI()
    {
        sim = (FluidFlowSimulation)target;
        if (sim == null) return;

        EnsureWaypointsSim1();
        EnsureWaypointsSim2();

        Vector3 pipePos = sim.GetPipePosition();

        // 1. Draw On-Screen GUI Overlay in Scene View
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(10, 10, 340, 120), "Dual Path Visual Tracer", GUI.skin.window);
        selectedPathTab = GUILayout.Toolbar(selectedPathTab, new string[] { "Sim 1 (Forward 9-Pt)", "Sim 2 (Reverse 8-Pt)" });
        
        if (selectedPathTab == 0)
        {
            GUILayout.Label("• Editing Cyan Line (Cube.001 ➔ Cube.002)", EditorStyles.boldLabel);
            if (GUILayout.Button("🔄 Reset Sim 1 Path"))
            {
                Undo.RecordObject(sim, "Reset Sim 1 Path");
                sim.ResetWaypointsSim1ToDefault();
                EditorUtility.SetDirty(sim);
            }
        }
        else
        {
            GUILayout.Label("• Editing Blue Line (Cube.002 ➔ Cube.001)", EditorStyles.boldLabel);
            if (GUILayout.Button("🔄 Reset Sim 2 (8-Point) Path"))
            {
                Undo.RecordObject(sim, "Reset Sim 2 Path");
                sim.ResetWaypointsSim2ToDefault();
                EditorUtility.SetDirty(sim);
            }
        }
        GUILayout.Label("• Drag 3D Handles in Scene to shape path.", EditorStyles.miniLabel);
        GUILayout.EndArea();
        Handles.EndGUI();

        // 2. Draw Simulation 1 Path (Cyan Blue)
        Color cyanCol = new Color(0f, 0.85f, 1f, 0.95f);
        Handles.color = cyanCol;
        for (int i = 0; i < sim.customWaypointsSim1.Count - 1; i++)
        {
            Vector3 p1 = pipePos + sim.customWaypointsSim1[i];
            Vector3 p2 = pipePos + sim.customWaypointsSim1[i + 1];
            Handles.DrawAAPolyLine(4f, p1, p2);
        }

        GUIStyle labelCyan = new GUIStyle();
        labelCyan.normal.textColor = cyanCol;
        labelCyan.fontStyle = FontStyle.Bold;
        labelCyan.fontSize = 12;

        for (int i = 0; i < sim.customWaypointsSim1.Count; i++)
        {
            Vector3 worldPos = pipePos + sim.customWaypointsSim1[i];
            Handles.Label(worldPos + Vector3.up * 0.08f, $"Sim1_P{i}", labelCyan);

            EditorGUI.BeginChangeCheck();
            Vector3 newWorldPos = Handles.PositionHandle(worldPos, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(sim, $"Move Sim 1 Point {i}");
                sim.useCustomWaypointsSim1 = true;
                sim.customWaypointsSim1[i] = newWorldPos - pipePos;
                EditorUtility.SetDirty(sim);
            }
        }

        // 3. Draw Simulation 2 Path (Electric Blue - 8 Points)
        Color blueCol = new Color(0.1f, 0.65f, 1f, 0.95f);
        Handles.color = blueCol;
        for (int i = 0; i < sim.customWaypointsSim2.Count - 1; i++)
        {
            Vector3 p1 = pipePos + sim.customWaypointsSim2[i];
            Vector3 p2 = pipePos + sim.customWaypointsSim2[i + 1];
            Handles.DrawAAPolyLine(4f, p1, p2);
        }

        GUIStyle labelBlue = new GUIStyle();
        labelBlue.normal.textColor = blueCol;
        labelBlue.fontStyle = FontStyle.Bold;
        labelBlue.fontSize = 12;

        for (int i = 0; i < sim.customWaypointsSim2.Count; i++)
        {
            Vector3 worldPos = pipePos + sim.customWaypointsSim2[i];
            Handles.Label(worldPos + Vector3.up * 0.08f, $"Sim2_P{i}", labelBlue);

            EditorGUI.BeginChangeCheck();
            Vector3 newWorldPos = Handles.PositionHandle(worldPos, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(sim, $"Move Sim 2 Point {i}");
                sim.useCustomWaypointsSim2 = true;
                sim.customWaypointsSim2[i] = newWorldPos - pipePos;
                EditorUtility.SetDirty(sim);
            }
        }

        // 4. Shift + Left Click to add point to active selected path tab
        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && e.shift)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Vector3 clickPoint;
            if (Physics.Raycast(ray, out RaycastHit hit)) clickPoint = hit.point;
            else
            {
                Plane plane = new Plane(Vector3.forward, pipePos);
                if (plane.Raycast(ray, out float enter)) clickPoint = ray.GetPoint(enter);
                else clickPoint = ray.GetPoint(2f);
            }

            if (selectedPathTab == 0)
            {
                Undo.RecordObject(sim, "Add Sim 1 Point by Click");
                sim.useCustomWaypointsSim1 = true;
                sim.customWaypointsSim1.Add(clickPoint - pipePos);
            }
            else
            {
                Undo.RecordObject(sim, "Add Sim 2 Point by Click");
                sim.useCustomWaypointsSim2 = true;
                sim.customWaypointsSim2.Add(clickPoint - pipePos);
            }
            EditorUtility.SetDirty(sim);
            e.Use();
        }
    }
}
#endif
