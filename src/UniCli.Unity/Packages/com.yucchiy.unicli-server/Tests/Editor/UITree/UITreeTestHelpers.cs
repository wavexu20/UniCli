using System.IO;
using System.Threading;
using UniCli.Protocol;
using UniCli.Server.Editor.Handlers;
using UnityEditor;
using UnityEngine;

namespace UniCli.Server.Editor.Tests
{
    internal static class UITreeTestHelpers
    {
        public static TResponse Execute<TRequest, TResponse>(CommandHandler<TRequest, TResponse> handler, TRequest request)
        {
            var data = request == null ? string.Empty : JsonUtility.ToJson(request);
            var commandRequest = new CommandRequest
            {
                command = handler.CommandName,
                data = data,
                cwd = Directory.GetCurrentDirectory()
            };

            var result = ((ICommandHandler)handler)
                .ExecuteAsync(commandRequest, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            return (TResponse)result;
        }

        public static UITreeTestEditorWindow OpenFixtureWindow()
        {
            var window = UITreeTestEditorWindow.Open();
            window.Repaint();
            EditorApplication.QueuePlayerLoopUpdate();
            return window;
        }

        public static void CloseFixtureWindows()
        {
            var windows = Resources.FindObjectsOfTypeAll<UITreeTestEditorWindow>();
            foreach (var window in windows)
            {
                if (window != null)
                {
                    window.Close();
                }
            }
        }
    }
}
