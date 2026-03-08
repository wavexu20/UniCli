using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine.UIElements;

namespace UniCli.Server.Editor.Handlers
{
    internal sealed class SideEffectTracker
    {
        internal sealed class Snapshot
        {
            public Dictionary<string, ElementState> states;
        }

        internal sealed class ElementState
        {
            public string summary;
            public string value;
        }

        public Snapshot Capture(VisualElement root, int maxElements, CancellationToken cancellationToken)
        {
            var states = new Dictionary<string, ElementState>(StringComparer.Ordinal);
            if (root == null)
            {
                return new Snapshot { states = states };
            }

            var stack = new Stack<VisualElement>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (states.Count >= maxElements)
                {
                    break;
                }

                var element = stack.Pop();
                var path = ElementIntrospection.GetHierarchyPath(root, element);
                if (!string.IsNullOrEmpty(path))
                {
                    states[path] = new ElementState
                    {
                        summary = ElementIntrospection.DescribeElement(element, false),
                        value = BuildStateValue(element)
                    };
                }

                for (var i = element.childCount - 1; i >= 0; i--)
                {
                    stack.Push(element.ElementAt(i));
                }
            }

            return new Snapshot { states = states };
        }

        public UITreeSideEffectEntry[] Diff(Snapshot before, Snapshot after, int maxEntries)
        {
            var result = new List<UITreeSideEffectEntry>();
            var keys = new HashSet<string>(StringComparer.Ordinal);

            if (before != null && before.states != null)
            {
                foreach (var key in before.states.Keys)
                {
                    keys.Add(key);
                }
            }

            if (after != null && after.states != null)
            {
                foreach (var key in after.states.Keys)
                {
                    keys.Add(key);
                }
            }

            foreach (var key in keys.OrderBy(x => x, StringComparer.Ordinal))
            {
                if (result.Count >= maxEntries)
                {
                    break;
                }

                ElementState beforeState = null;
                ElementState afterState = null;
                if (before != null && before.states != null)
                {
                    before.states.TryGetValue(key, out beforeState);
                }

                if (after != null && after.states != null)
                {
                    after.states.TryGetValue(key, out afterState);
                }

                var beforeValue = beforeState != null ? beforeState.value : string.Empty;
                var afterValue = afterState != null ? afterState.value : string.Empty;
                if (string.Equals(beforeValue, afterValue, StringComparison.Ordinal))
                {
                    continue;
                }

                result.Add(new UITreeSideEffectEntry
                {
                    path = key,
                    summary = afterState != null ? afterState.summary : beforeState != null ? beforeState.summary : string.Empty,
                    before = beforeValue,
                    after = afterValue
                });
            }

            return result.ToArray();
        }

        private static string BuildStateValue(VisualElement element)
        {
            var classes = ElementIntrospection.GetClasses(element, true);
            var classValue = classes.Length == 0 ? string.Empty : string.Join(",", classes);
            var text = ElementIntrospection.ExtractText(element) ?? string.Empty;
            var value = ElementIntrospection.TryGetValueAsString(element, out var valueString)
                ? valueString
                : string.Empty;

            return $"enabled={element.enabledInHierarchy};visible={element.visible};display={element.resolvedStyle.display};classes={classValue};text={text};value={value}";
        }
    }
}
