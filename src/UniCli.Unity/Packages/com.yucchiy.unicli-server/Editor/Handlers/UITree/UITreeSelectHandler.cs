using System;
using System.Threading;
using System.Threading.Tasks;

namespace UniCli.Server.Editor.Handlers
{
    public sealed class UITreeSelectHandler : CommandHandler<UITreeSelectRequest, UITreeSelectResponse>
    {
        private readonly PanelResolver _panelResolver = new();
        private readonly ElementResolver _elementResolver = new();
        private readonly SideEffectTracker _sideEffectTracker = new();

        public override string CommandName => "UITree.Select";
        public override string Description => "Select an option in a UI Toolkit selection control";

        protected override bool TryWriteFormatted(UITreeSelectResponse response, bool success, IFormatWriter writer)
        {
            if (!success)
            {
                writer.WriteLine("UITree select failed.");
                return true;
            }

            writer.WriteLine($"Selected: {response.target.type} #{response.target.name}");
            writer.WriteLine($"Old: {response.oldValue}");
            writer.WriteLine($"New: {response.newValue}");
            writer.WriteLine($"Choices: {response.choices?.Length ?? 0}");
            writer.WriteLine($"Side effects: {response.sideEffects?.Length ?? 0}");
            return true;
        }

        protected override ValueTask<UITreeSelectResponse> ExecuteAsync(UITreeSelectRequest request, CancellationToken cancellationToken)
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

            if (string.IsNullOrEmpty(request.choice))
            {
                throw new CommandFailedException("choice is required.", new UITreeErrorResponse
                {
                    error = "choice is required.",
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
            if (!ElementIntrospection.TrySetChoiceFromString(element, request.choice, out var oldValue, out var newValue, out var choices, out var error))
            {
                throw new CommandFailedException(error, new UITreeErrorResponse
                {
                    error = error,
                    candidates = new[] { ElementIntrospection.DescribeElement(element, true) }
                });
            }

            var after = _sideEffectTracker.Capture(panel.root, 1024, cancellationToken);
            var response = new UITreeSelectResponse
            {
                target = UITreeResponseHelper.BuildTargetInfo(panel.root, element, request.selector),
                oldValue = oldValue,
                newValue = newValue,
                choices = choices,
                sideEffects = _sideEffectTracker.Diff(before, after, 64)
            };

            return new ValueTask<UITreeSelectResponse>(response);
        }
    }
}
