#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PipelineControl.Editor
{
    [InitializeOnLoad]
    public static class PipelineSetupEditor
    {
        static PipelineSetupEditor()
        {
            EditorApplication.delayCall += EnsurePipelineSystemInScene;
        }

        [MenuItem("Pipeline/Setup Pipe Control & Camera Angle")]
        public static void EnsurePipelineSystemInScene()
        {
            if (Application.isPlaying) return;

            // 1. Setup Camera Angle & Controller
            Camera mainCam = Camera.main ?? Object.FindAnyObjectByType<Camera>();
            GameObject pipelineObj = GameObject.Find("pipeline");

            if (mainCam != null && pipelineObj != null)
            {
                // Attach PipelineCameraController if missing
                PipelineCameraController camCtrl = mainCam.GetComponent<PipelineCameraController>();
                if (camCtrl == null)
                {
                    mainCam.gameObject.AddComponent<PipelineCameraController>();
                }

                // Align Main Camera to optimal FRONT view close-up
                Vector3 pipePos = pipelineObj.transform.position;
                mainCam.transform.position = pipePos + new Vector3(-0.85f, 0.45f, 3.2f);
                mainCam.transform.rotation = Quaternion.Euler(12f, 180f, 0f);
            }

            // 2. Check if PipelineSystem exists
            PipelineSystem existingSystem = Object.FindAnyObjectByType<PipelineSystem>();
            if (existingSystem != null)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                return;
            }

            GameObject targetObj = pipelineObj != null ? pipelineObj : new GameObject("PipelineSystem");

            // Attach PipelineSystem
            PipelineSystem sys = targetObj.GetComponent<PipelineSystem>();
            if (sys == null)
            {
                sys = targetObj.AddComponent<PipelineSystem>();
            }

            // Auto-link sub-objects by name from pipeline model
            Transform pumpChild = pipelineObj != null ? pipelineObj.transform.Find("S2_Pump_Motor_Unit") : null;
            Transform valveChild = pipelineObj != null ? pipelineObj.transform.Find("S2_ProportionalValve") : null;

            if (pumpChild != null && sys.Pump != null)
            {
                SerializedObject serializedPump = new SerializedObject(sys.Pump);
                SerializedProperty impellerProp = serializedPump.FindProperty("impellerTransform");
                if (impellerProp != null && impellerProp.objectReferenceValue == null)
                {
                    impellerProp.objectReferenceValue = pumpChild;
                    serializedPump.ApplyModifiedProperties();
                }
            }

            if (valveChild != null && sys.Valve != null)
            {
                SerializedObject serializedValve = new SerializedObject(sys.Valve);
                SerializedProperty handleProp = serializedValve.FindProperty("valveHandleTransform");
                if (handleProp != null && handleProp.objectReferenceValue == null)
                {
                    handleProp.objectReferenceValue = valveChild;
                    serializedValve.ApplyModifiedProperties();
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[PipelineSystem] Configured Pipe Control System & Camera Angle on OutdoorsScene!");
        }
    }
}
#endif
