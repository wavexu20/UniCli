using System;
using System.Threading;
using System.Threading.Tasks;

namespace UniCli.Server.Editor.Handlers
{
    public sealed class UITreeFillHandler : CommandHandler<UITreeFillRequest, UITreeFillResponse>
    {
        private readonly PanelResolver _panelResolver = new();
        private readonly ElementResolver _elementResolver = new();
        private readonly SideEffectTracker _sideEffectTracker = new();

        public override string CommandName => "UITree.Fill";
        public override string Description => "Set value/text on an editable UI Toolkit field";

        protected override bool TryWriteFormatted(UITreeFillResponse response, bool success, IFormatWriter writer)
        {
            if (!success)
            {
                writer.WriteLine("UITree fill failed.");
                return true;
            }

            writer.WriteLine($"Filled: {response.target.type} #{response.target.name}");
            writer.WriteLine($"Old: {response.oldValue}");
            writer.WriteLine($"New: {response.newValue}");
            writer.WriteLine($"Side effects: {response.sideEffects?.Length ?? 0}");
            return true;
        }

        protected override ValueTask<UITreeFillResponse> ExecuteAsync(UITreeFillRequest request, CancellationToken cancellationToken)
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
            if (!ElementIntrospection.TrySetValueFromString(element, request.value ?? string.Empty, out var oldValue, out var newValue, out var error))
            {
                throw new CommandFailedException(error, new UITreeErrorResponse
                {
                    error = error,
                    candidates = new[] { ElementIntrospection.DescribeElement(element, true) }
                });
            }

            var after = _sideEffectTracker.Capture(panel.root, 1024, cancellationToken);
            var response = new UITreeFillResponse
            {
                target = UITreeResponseHelper.BuildTargetInfo(panel.root, element, request.selector),
                oldValue = oldValue,
                newValue = newValue,
                sideEffects = _sideEffectTracker.Diff(before, after, 64)
            };

            return new ValueTask<UITreeFillResponse>(response);
        }
    }
}
