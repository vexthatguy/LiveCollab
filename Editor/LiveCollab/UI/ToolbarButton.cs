using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace LiveCollab.UI
{
    /// <summary>
    /// Injects a LiveCollab button into the top-left of the editor toolbar.
    /// Uses the well-known m_OnGUIHandler hook. If it fails on some Unity
    /// version, the Tools > LiveCollab menu item still works.
    /// </summary>
    [InitializeOnLoad]
    public static class ToolbarButton
    {
        static ToolbarButton()
        {
            EditorApplication.delayCall += Install;
        }

        static void Install()
        {
            try
            {
                var asm = typeof(Editor).Assembly;
                var toolbarType = asm.GetType("UnityEditor.Toolbar");
                if (toolbarType == null) return;
                var field = toolbarType.GetField("m_OnGUIHandler", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null) return;

                EditorWindow window = null;
                foreach (var w in Resources.FindObjectsOfTypeAll(toolbarType))
                {
                    window = (EditorWindow)w;
                    break;
                }
                if (window == null) return;

                var current = (Action)field.GetValue(window);
                field.SetValue(window, new Action(() =>
                {
                    try
                    {
                        DrawButton();
                    }
                    catch (Exception) { }
                    if (current != null) current();
                }));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LiveCollab] Toolbar button unavailable (menu item still works): " + e.Message);
            }
        }

        static void DrawButton()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent(IconFactory.Icon, "LiveCollab — host or join a live editing session"),
                GUILayout.Width(26), GUILayout.Height(22)))
            {
                var win = EditorWindow.GetWindow<LiveCollabWindow>(false, "LiveCollab", true);
                win.minSize = new Vector2(340, 380);
            }
            GUILayout.EndHorizontal();
        }
    }
}
