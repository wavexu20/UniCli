using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine.UIElements;

namespace UniCli.Server.Editor.Handlers
{
    internal sealed class ElementResolver
    {
        private sealed class SelectorSegment
        {
            public string type;
            public string name;
            public List<string> classes;
            public bool wildcard;
        }

        public List<VisualElement> Query(VisualElement root, string selector, CancellationToken cancellationToken)
        {
            if (root == null)
            {
                return new List<VisualElement>();
            }

            var segments = ParseSelector(selector);
            var current = new List<VisualElement> { root };

            for (var i = 0; i < segments.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var segment = segments[i];
                var includeSelf = i == 0;
                var next = new List<VisualElement>();
                var seen = new HashSet<VisualElement>();

                for (var j = 0; j < current.Count; j++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    CollectMatches(current[j], segment, includeSelf, seen, next);
                }

                current = next;
                if (current.Count == 0)
                {
                    break;
                }
            }

            return current;
        }

        public VisualElement ResolveSingle(VisualElement root, string selector, CancellationToken cancellationToken)
        {
            var matches = Query(root, selector, cancellationToken);
            if (matches.Count == 1)
            {
                return matches[0];
            }

            if (matches.Count == 0)
            {
                throw new CommandFailedException($"No element matched selector '{selector}'.", new UITreeErrorResponse
                {
                    error = $"No element matched selector '{selector}'.",
                    matchCount = 0,
                    candidates = BuildNearestCandidates(root, cancellationToken)
                });
            }

            throw new CommandFailedException($"Selector '{selector}' matched multiple elements.", new UITreeErrorResponse
            {
                error = $"Selector '{selector}' matched multiple elements.",
                matchCount = matches.Count,
                candidates = matches.Take(10)
                    .Select(m => $"{ElementIntrospection.GetHierarchyPath(root, m)} {ElementIntrospection.DescribeElement(m, false)}")
                    .ToArray()
            });
        }

        private static string[] BuildNearestCandidates(VisualElement root, CancellationToken cancellationToken)
        {
            var result = new List<string>();
            if (root == null)
            {
                return result.ToArray();
            }

            var stack = new Stack<VisualElement>();
            stack.Push(root);
            while (stack.Count > 0 && result.Count < 10)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var element = stack.Pop();
                result.Add($"{ElementIntrospection.GetHierarchyPath(root, element)} {ElementIntrospection.DescribeElement(element, false)}");
                for (var i = element.childCount - 1; i >= 0; i--)
                {
                    stack.Push(element.ElementAt(i));
                }
            }

            return result.ToArray();
        }

        private static void CollectMatches(
            VisualElement seed,
            SelectorSegment segment,
            bool includeSelf,
            HashSet<VisualElement> seen,
            List<VisualElement> result)
        {
            if (seed == null)
            {
                return;
            }

            var stack = new Stack<VisualElement>();
            if (includeSelf)
            {
                stack.Push(seed);
            }
            else
            {
                for (var i = seed.childCount - 1; i >= 0; i--)
                {
                    stack.Push(seed.ElementAt(i));
                }
            }

            while (stack.Count > 0)
            {
                var element = stack.Pop();
                if (MatchSegment(element, segment) && seen.Add(element))
                {
                    result.Add(element);
                }

                for (var i = element.childCount - 1; i >= 0; i--)
                {
                    stack.Push(element.ElementAt(i));
                }
            }
        }

        private static bool MatchSegment(VisualElement element, SelectorSegment segment)
        {
            if (element == null)
            {
                return false;
            }

            if (!segment.wildcard)
            {
                if (!string.IsNullOrEmpty(segment.type))
                {
                    var type = element.GetType();
                    if (!string.Equals(type.Name, segment.type, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(type.FullName, segment.type, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                if (!string.IsNullOrEmpty(segment.name)
                    && !string.Equals(element.name, segment.name, StringComparison.Ordinal))
                {
                    return false;
                }

                if (segment.classes != null)
                {
                    for (var i = 0; i < segment.classes.Count; i++)
                    {
                        if (!element.ClassListContains(segment.classes[i]))
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private static List<SelectorSegment> ParseSelector(string selector)
        {
            if (string.IsNullOrWhiteSpace(selector))
            {
                throw new CommandFailedException("Selector is required.", new UITreeErrorResponse
                {
                    error = "Selector is required.",
                    candidates = Array.Empty<string>()
                });
            }

            var parts = selector
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            if (parts.Count == 0)
            {
                throw new CommandFailedException("Selector is required.", new UITreeErrorResponse
                {
                    error = "Selector is required.",
                    candidates = Array.Empty<string>()
                });
            }

            var parsed = new List<SelectorSegment>(parts.Count);
            for (var i = 0; i < parts.Count; i++)
            {
                parsed.Add(ParseSegment(parts[i]));
            }

            return parsed;
        }

        private static SelectorSegment ParseSegment(string segment)
        {
            if (segment == "*")
            {
                return new SelectorSegment
                {
                    wildcard = true,
                    classes = new List<string>()
                };
            }

            var parsed = new SelectorSegment
            {
                classes = new List<string>()
            };

            var index = 0;
            if (segment[0] != '#' && segment[0] != '.')
            {
                var start = index;
                while (index < segment.Length && segment[index] != '#' && segment[index] != '.')
                {
                    index++;
                }

                parsed.type = segment.Substring(start, index - start);
            }

            while (index < segment.Length)
            {
                var marker = segment[index];
                if (marker != '#' && marker != '.')
                {
                    throw new CommandFailedException($"Unsupported selector syntax: '{segment}'.", new UITreeErrorResponse
                    {
                        error = $"Unsupported selector syntax: '{segment}'.",
                        candidates = Array.Empty<string>()
                    });
                }

                index++;
                var start = index;
                while (index < segment.Length && segment[index] != '#' && segment[index] != '.')
                {
                    index++;
                }

                if (start == index)
                {
                    throw new CommandFailedException($"Unsupported selector syntax: '{segment}'.", new UITreeErrorResponse
                    {
                        error = $"Unsupported selector syntax: '{segment}'.",
                        candidates = Array.Empty<string>()
                    });
                }

                var token = segment.Substring(start, index - start);
                if (marker == '#')
                {
                    if (!string.IsNullOrEmpty(parsed.name))
                    {
                        throw new CommandFailedException($"Unsupported selector syntax: '{segment}'.", new UITreeErrorResponse
                        {
                            error = $"Unsupported selector syntax: '{segment}'.",
                            candidates = Array.Empty<string>()
                        });
                    }

                    parsed.name = token;
                }
                else
                {
                    parsed.classes.Add(token);
                }
            }

            if (string.IsNullOrEmpty(parsed.type)
                && string.IsNullOrEmpty(parsed.name)
                && (parsed.classes == null || parsed.classes.Count == 0))
            {
                throw new CommandFailedException($"Unsupported selector syntax: '{segment}'.", new UITreeErrorResponse
                {
                    error = $"Unsupported selector syntax: '{segment}'.",
                    candidates = Array.Empty<string>()
                });
            }

            return parsed;
        }
    }
}
