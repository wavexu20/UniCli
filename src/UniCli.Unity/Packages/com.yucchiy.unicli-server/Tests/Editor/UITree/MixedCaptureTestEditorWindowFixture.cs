using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniCli.Server.Editor.Tests
{
    internal sealed class MixedCaptureTestEditorWindow : EditorWindow
    {
        public const string Title = "UniCli Mixed Capture Test";

        public static MixedCaptureTestEditorWindow Open()
        {
            var window = GetWindow<MixedCaptureTestEditorWindow>(true, Title, true);
            window.titleContent = new GUIContent(Title);
            window.position = new Rect(180f, 180f, 420f, 260f);
            window.Show();
            window.Focus();
            window.BuildUi();
            return window;
        }

        private void OnEnable()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            rootVisualElement.Clear();

            var greenBox = new VisualElement
            {
                name = "uitk-green-box"
            };
            greenBox.style.width = 120;
            greenBox.style.height = 80;
            greenBox.style.marginLeft = 220;
            greenBox.style.marginTop = 40;
            greenBox.style.backgroundColor = new Color(0.18f, 0.73f, 0.31f);
            rootVisualElement.Add(greenBox);

            rootVisualElement.MarkDirtyRepaint();
        }

        private void OnGUI()
        {
            EditorGUI.DrawRect(new Rect(20f, 30f, 140f, 80f), new Color(0.86f, 0.24f, 0.18f));
            GUI.Label(new Rect(32f, 62f, 120f, 20f), "IMGUI Layer");
        }
    }
}
