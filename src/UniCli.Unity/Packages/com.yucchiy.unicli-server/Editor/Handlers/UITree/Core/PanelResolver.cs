using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniCli.Server.Editor.Handlers
{
    internal sealed class PanelResolver
    {
        internal sealed class ResolvedPanel
        {
            public EditorWindow window;
            public VisualElement root;
            public UITreePanelInfo panelInfo;
        }

        private sealed class PanelCandidate
        {
            public EditorWindow window;
            public string typeName;
            public string shortTypeName;
            public string title;
        }

        public ResolvedPanel[] ListOpenPanels(CancellationToken cancellationToken)
        {
            var candidates = EnumeratePanelCandidates(cancellationToken);
            var result = new ResolvedPanel[candidates.Count];
            for (var i = 0; i < candidates.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var candidate = candidates[i];
                result[i] = new ResolvedPanel
                {
                    window = candidate.window,
                    root = candidate.window.rootVisualElement,
                    panelInfo = BuildPanelInfo(candidate.window, cancellationToken)
                };
            }

            return result;
        }

        public ResolvedPanel Resolve(string panelHint, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(panelHint))
            {
                throw new CommandFailedException("Panel name is required.", new UITreeErrorResponse
                {
                    error = "Panel name is required.",
                    candidates = Array.Empty<string>()
                });
            }

            var candidates = EnumeratePanelCandidates(cancellationToken);
            if (candidates.Count == 0)
            {
                throw new CommandFailedException("No opened EditorWindow panels found.", new UITreeErrorResponse
                {
                    error = "No opened EditorWindow panels found.",
                    candidates = Array.Empty<string>()
                });
            }

            var bestScore = 0;
            var best = new List<PanelCandidate>();
            for (var i = 0; i < candidates.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var candidate = candidates[i];
                var score = CalculateScore(candidate, panelHint);
                if (score <= 0)
                {
                    continue;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best.Clear();
                    best.Add(candidate);
                }
                else if (score == bestScore)
                {
                    best.Add(candidate);
                }
            }

            if (best.Count == 0)
            {
                throw new CommandFailedException($"Panel '{panelHint}' not found.", new UITreeErrorResponse
                {
                    error = $"Panel '{panelHint}' not found.",
                    candidates = candidates.Select(DescribePanel).Take(20).ToArray()
                });
            }

            if (best.Count > 1)
            {
                throw new CommandFailedException($"Panel '{panelHint}' is ambiguous.", new UITreeErrorResponse
                {
                    error = $"Panel '{panelHint}' is ambiguous.",
                    matchCount = best.Count,
                    candidates = best.Select(DescribePanel).Take(20).ToArray()
                });
            }

            var resolved = best[0];
            return new ResolvedPanel
            {
                window = resolved.window,
                root = resolved.window.rootVisualElement,
                panelInfo = BuildPanelInfo(resolved.window, cancellationToken)
            };
        }

        private static List<PanelCandidate> EnumeratePanelCandidates(CancellationToken cancellationToken)
        {
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            var list = new List<PanelCandidate>(windows.Length);
            for (var i = 0; i < windows.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var window = windows[i];
                if (window == null)
                {
                    continue;
                }

                var root = window.rootVisualElement;
                if (root == null || root.panel == null)
                {
                    continue;
                }

                var type = window.GetType();
                list.Add(new PanelCandidate
                {
                    window = window,
                    typeName = type.FullName,
                    shortTypeName = type.Name,
                    title = window.titleContent != null ? window.titleContent.text ?? string.Empty : string.Empty
                });
            }

            list.Sort((a, b) =>
            {
                var cmp = string.Compare(a.typeName, b.typeName, StringComparison.Ordinal);
                if (cmp != 0)
                {
                    return cmp;
                }

                return string.Compare(a.title, b.title, StringComparison.Ordinal);
            });

            return list;
        }

        private static int CalculateScore(PanelCandidate candidate, string panelHint)
        {
            var hint = panelHint.Trim();

            if (string.Equals(candidate.typeName, hint, StringComparison.OrdinalIgnoreCase))
            {
                return 400;
            }

            if (string.Equals(candidate.shortTypeName, hint, StringComparison.OrdinalIgnoreCase))
            {
                return 350;
            }

            if (string.Equals(candidate.title, hint, StringComparison.OrdinalIgnoreCase))
            {
                return 300;
            }

            if (!string.IsNullOrEmpty(candidate.typeName) && candidate.typeName.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 200;
            }

            if (!string.IsNullOrEmpty(candidate.shortTypeName) && candidate.shortTypeName.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 180;
            }

            if (!string.IsNullOrEmpty(candidate.title) && candidate.title.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 160;
            }

            return 0;
        }

        private static string DescribePanel(PanelCandidate candidate)
        {
            if (string.IsNullOrEmpty(candidate.title))
            {
                return candidate.typeName;
            }

            return $"{candidate.typeName} [{candidate.title}]";
        }

        private static UITreePanelInfo BuildPanelInfo(EditorWindow window, CancellationToken cancellationToken)
        {
            var root = window.rootVisualElement;
            return new UITreePanelInfo
            {
                name = window.GetType().FullName,
                title = window.titleContent != null ? window.titleContent.text ?? string.Empty : string.Empty,
                size = new UITreeSize
                {
                    width = window.position.width,
                    height = window.position.height
                },
                elementCount = CountElements(root, cancellationToken)
            };
        }

        private static int CountElements(VisualElement root, CancellationToken cancellationToken)
        {
            if (root == null)
            {
                return 0;
            }

            var count = 0;
            var stack = new Stack<VisualElement>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var element = stack.Pop();
                count++;

                for (var i = element.childCount - 1; i >= 0; i--)
                {
                    stack.Push(element.ElementAt(i));
                }
            }

            return count;
        }
    }
}
