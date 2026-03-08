using UnityEditor;
using UnityEngine;

namespace UniCli.Server.Editor.Tests
{
    internal sealed class PureImguiCaptureTestEditorWindow : EditorWindow
    {
        public const string Title = "UniCli Pure IMGUI Capture Test";

        public static PureImguiCaptureTestEditorWindow Open()
        {
            var window = GetWindow<PureImguiCaptureTestEditorWindow>(true, Title, true);
            window.titleContent = new GUIContent(Title);
            window.position = new Rect(220f, 220f, 420f, 260f);
            window.Show();
            window.Focus();
            return window;
        }

        private void OnGUI()
        {
            EditorGUI.DrawRect(new Rect(28f, 34f, 150f, 96f), new Color(0.86f, 0.24f, 0.18f));
            GUI.Label(new Rect(44f, 76f, 120f, 20f), "Pure IMGUI");

            EditorGUI.DrawRect(new Rect(224f, 52f, 120f, 84f), new Color(0.14f, 0.52f, 0.87f));
            GUI.Label(new Rect(236f, 86f, 90f, 20f), "Legacy");
        }
    }
}
