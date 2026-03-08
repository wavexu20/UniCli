using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.UIElements;

namespace UniCli.Server.Editor.Handlers
{
    public sealed class UITreeClickHandler : CommandHandler<UITreeClickRequest, UITreeClickResponse>
    {
        private static readonly BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly PanelResolver _panelResolver = new();
        private readonly ElementResolver _elementResolver = new();
        private readonly SideEffectTracker _sideEffectTracker = new();

        public override string CommandName => "UITree.Click";
        public override string Description => "Click-like interaction on a UI Toolkit element";

        protected override bool TryWriteFormatted(UITreeClickResponse response, bool success, IFormatWriter writer)
        {
            if (!success)
            {
                writer.WriteLine("UITree click failed.");
                return true;
            }

            writer.WriteLine($"Clicked: {response.target.type} #{response.target.name}");
            writer.WriteLine($"Result: {response.result}");
            writer.WriteLine($"Side effects: {response.sideEffects?.Length ?? 0}");
            return true;
        }

        protected override ValueTask<UITreeClickResponse> ExecuteAsync(UITreeClickRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.panel))
            {
                throw new CommandFailedException("panel is required.", new UITreeErrorResponse
                {
                    error = "panel is required.",
                    candidates = Array.Empty<string>()
                });
            }

            if (string.IsNullOrWhiteSpace(request.selector))
            {
                throw new CommandFailedException("selector is required.", new UITreeErrorResponse
                {
                    error = "selector is required.",
                    candidates = Array.Empty<string>()
                });
            }

            var panel = _panelResolver.Resolve(request.panel, cancellationToken);
            var element = _elementResolver.ResolveSingle(panel.root, request.selector, cancellationToken);

            if (!ElementIntrospection.IsInteractable(element))
            {
                throw new CommandFailedException("Target element is not interactable.", new UITreeErrorResponse
                {
                    error = "Target element is not interactable.",
                    candidates = new[] { ElementIntrospection.DescribeElement(element, true) }
                });
            }

            var before = _sideEffectTracker.Capture(panel.root, 1024, cancellationToken);
            if (!TryClickElement(element))
            {
                throw new CommandFailedException("Element does not support click interaction.", new UITreeErrorResponse
                {
                    error = "Element does not support click interaction.",
                    candidates = new[] { ElementIntrospection.DescribeElement(element, true) }
                });
            }

            var after = _sideEffectTracker.Capture(panel.root, 1024, cancellationToken);
            var response = new UITreeClickResponse
            {
                target = UITreeResponseHelper.BuildTargetInfo(panel.root, element, request.selector),
                result = "success",
                sideEffects = _sideEffectTracker.Diff(before, after, 64)
            };

            return new ValueTask<UITreeClickResponse>(response);
        }

        private static bool TryClickElement(VisualElement element)
        {
            if (element is Toggle toggle)
            {
                toggle.value = !toggle.value;
                return true;
            }

            if (element is Button button && TryInvokeClickable(button.clickable))
            {
                return true;
            }

            var clickableProperty = element.GetType().GetProperty("clickable", InstanceMembers);
            if (clickableProperty != null)
            {
                var clickable = clickableProperty.GetValue(element, null);
                if (TryInvokeClickable(clickable))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryInvokeClickable(object clickable)
        {
            if (clickable == null)
            {
                return false;
            }

            var type = clickable.GetType();
            var methods = type.GetMethods(InstanceMembers)
                .Where(m => string.Equals(m.Name, "SimulateSingleClick", StringComparison.Ordinal)
                    || string.Equals(m.Name, "Invoke", StringComparison.Ordinal))
                .OrderBy(m => m.GetParameters().Length)
                .ToArray();

            for (var i = 0; i < methods.Length; i++)
            {
                if (TryInvokeMethod(clickable, methods[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryInvokeMethod(object target, MethodInfo method)
        {
            try
            {
                var parameters = method.GetParameters();
                var args = new object[parameters.Length];
                for (var i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].HasDefaultValue)
                    {
                        args[i] = parameters[i].DefaultValue;
                    }
                    else
                    {
                        args[i] = parameters[i].ParameterType.IsValueType
                            ? Activator.CreateInstance(parameters[i].ParameterType)
                            : null;
                    }
                }

                method.Invoke(target, args);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
