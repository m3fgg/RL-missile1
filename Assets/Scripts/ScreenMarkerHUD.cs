#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SceneViewMarkers
{
    private static bool markersEnabled = false;

    static SceneViewMarkers()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    // Adds a clickable button under "Tools -> Toggle Scene Markers" AND binds the 'M' key
    [MenuItem("Tools/Toggle Scene Markers _m")]
    public static void ToggleMarkers()
    {
        markersEnabled = !markersEnabled;
        SceneView.RepaintAll();
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        if (!markersEnabled) return;

        var agents = Object.FindObjectsByType<MissileAgent>();
        var targets = Object.FindObjectsByType<TargetController>();

        Handles.BeginGUI();

        Color prevColor = GUI.backgroundColor;

        // Draw green 2D UI markers for all on-screen missiles
        GUI.backgroundColor = new Color(0.2f, 1f, 0.4f, 0.9f);
        foreach (var agent in agents)
        {
            if (agent != null && agent.gameObject.activeInHierarchy)
            {
                DrawMarker(agent.transform.position, sceneView, "M");
            }
        }

        // Draw red 2D UI markers for all on-screen targets
        GUI.backgroundColor = new Color(1f, 0.25f, 0.25f, 0.9f);
        foreach (var target in targets)
        {
            if (target != null && target.gameObject.activeInHierarchy)
            {
                DrawMarker(target.transform.position, sceneView, "T");
            }
        }

        GUI.backgroundColor = prevColor;
        Handles.EndGUI();
    }

    private static void DrawMarker(Vector3 worldPos, SceneView sceneView, string label)
    {
        // Converts 3D world position to 2D Scene-view GUI coordinates (z > 0 means in front of camera)
        Vector3 guiPos = HandleUtility.WorldToGUIPointWithDepth(worldPos);

        if (guiPos.z <= 0f) return;
        if (guiPos.x < 0f || guiPos.x > sceneView.position.width ||
            guiPos.y < 0f || guiPos.y > sceneView.position.height)
        {
            return;
        }

        const float size = 22f;
        Rect rect = new Rect(guiPos.x - size * 0.5f, guiPos.y - size * 0.5f, size, size);
        GUI.Box(rect, label);
    }
}
#endif
