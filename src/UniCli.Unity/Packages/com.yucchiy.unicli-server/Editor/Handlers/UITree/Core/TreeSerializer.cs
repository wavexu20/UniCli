using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.UIElements;

namespace UniCli.Server.Editor.Handlers
{
    internal sealed class TreeSerializer
    {
        private readonly ElementResolver _elementResolver;

        internal sealed class TreeSerializationResult
        {
            public string[] lines;
            public int matchedCount;
        }

        public TreeSerializer(ElementResolver elementResolver)
        {
            _elementResolver = elementResolver;
        }

        public TreeSerializationResult Serialize(
            VisualElement root,
            int depth,
            string filter,
            bool includeUnityClasses,
            CancellationToken cancellationToken)
        {
            if (root == null)
            {
                return new TreeSerializationResult
                {
                    lines = Array.Empty<string>(),
                    matchedCount = 0
                };
            }

            if (string.IsNullOrWhiteSpace(filter))
            {
                return new TreeSerializationResult
                {
                    lines = SerializeAll(root, depth, includeUnityClasses, cancellationToken),
                    matchedCount = 0
                };
            }

            var matches = _elementResolver.Query(root, filter, cancellationToken);
            var lines = new List<string>(matches.Count);
            for (var i = 0; i < matches.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var element = matches[i];
                var path = ElementIntrospection.GetHierarchyPath(root, element);
                lines.Add($"{path} {ElementIntrospection.DescribeElement(element, includeUnityClasses)}");
            }

            return new TreeSerializationResult
            {
                lines = lines.ToArray(),
                matchedCount = matches.Count
            };
        }

        private static string[] SerializeAll(VisualElement root, int depth, bool includeUnityClasses, CancellationToken cancellationToken)
        {
            var lines = new List<string>();
            var stack = new Stack<(VisualElement element, int depth)>();
            stack.Push((root, 0));

            while (stack.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = stack.Pop();
                if (depth >= 0 && current.depth > depth)
                {
                    continue;
                }

                lines.Add($"{new string(' ', current.depth * 2)}{ElementIntrospection.DescribeElement(current.element, includeUnityClasses)}");

                if (depth >= 0 && current.depth == depth)
                {
                    continue;
                }

                for (var i = current.element.childCount - 1; i >= 0; i--)
                {
                    stack.Push((current.element.ElementAt(i), current.depth + 1));
                }
            }

            return lines.ToArray();
        }
    }
}
