using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-off scene fixup for the VR black fade (BlackFade GameObject).
// Run once from the menu below, then delete this file.
public static class FixBlackFadeOneOff
{
    [MenuItem("Tools/FDLS2026/Fix Black Fade (one-off)")]
    private static void Fix()
    {
        GameObject blackFade = GameObject.Find("BlackFade");
        GameObject centerEye = GameObject.Find("CenterEyeAnchor");

        if (blackFade == null)
        {
            Debug.LogError("FixBlackFadeOneOff: 'BlackFade' not found in the open scene. Open MainScene and try again.");
            return;
        }
        if (centerEye == null)
        {
            Debug.LogError("FixBlackFadeOneOff: 'CenterEyeAnchor' not found in the open scene. Is the OVRCameraRig in this scene?");
            return;
        }

        Undo.SetTransformParent(blackFade.transform, centerEye.transform, "Reparent BlackFade under CenterEyeAnchor");
        blackFade.transform.localPosition = new Vector3(0f, 0f, 0.3f);
        blackFade.transform.localRotation = Quaternion.identity;
        blackFade.transform.localScale = Vector3.one;

        RectTransform rect = blackFade.GetComponent<RectTransform>();
        if (rect != null)
        {
            Undo.RecordObject(rect, "Resize BlackFade");
            rect.sizeDelta = new Vector2(2f, 2f);
        }

        Canvas canvas = blackFade.GetComponent<Canvas>();
        if (canvas != null)
        {
            Undo.RecordObject(canvas, "Fix BlackFade Canvas");
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.enabled = true;
        }

        CanvasGroup group = blackFade.GetComponent<CanvasGroup>();
        if (group != null)
        {
            Undo.RecordObject(group, "Enable BlackFade CanvasGroup");
            group.enabled = true;
        }

        ScreenFader fader = blackFade.GetComponent<ScreenFader>();
        if (fader != null)
        {
            Undo.RecordObject(fader, "Enable ScreenFader");
            fader.enabled = true;
        }
        else
        {
            Debug.LogWarning("FixBlackFadeOneOff: no ScreenFader component found on BlackFade.");
        }

        OVRScreenFade ovrFade = blackFade.GetComponent<OVRScreenFade>();
        if (ovrFade != null)
        {
            Undo.RecordObject(ovrFade, "Disable OVRScreenFade");
            ovrFade.enabled = false;
        }

        int rewired = 0;
        if (fader != null)
        {
            rewired += RewireField<ExperimentFlow>("_screenFader", fader);
            rewired += RewireField<CalibrationRig>("_fader", fader);
        }

        EditorSceneManager.MarkSceneDirty(blackFade.scene);
        Debug.Log($"FixBlackFadeOneOff: done. BlackFade parented under CenterEyeAnchor, World Space canvas, ScreenFader enabled, {rewired} reference(s) rewired. Save the scene (Ctrl+S).");
    }

    private static int RewireField<T>(string fieldName, ScreenFader value) where T : MonoBehaviour
    {
        int count = 0;
        foreach (T behaviour in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
        {
            var so = new SerializedObject(behaviour);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null) continue;

            Undo.RecordObject(behaviour, $"Rewire {fieldName}");
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
            count++;
        }
        return count;
    }
}
