using System;
using System.Threading;
using System.Threading.Tasks;

namespace UniCli.Server.Editor.Handlers
{
    public sealed class UITreeInspectHandler : CommandHandler<UITreeInspectRequest, UITreeInspectResponse>
    {
        private readonly PanelResolver _panelResolver = new();
        private readonly ElementResolver _elementResolver = new();
        private readonly InspectSerializer _serializer = new();

        public override string CommandName => "UITree.Inspect";
        public override string Description => "Inspect one UI Toolkit element in detail";

        protected override bool TryWriteFormatted(UITreeInspectResponse response, bool success, IFormatWriter writer)
        {
            if (!success)
            {
                writer.WriteLine("UITree inspect failed.");
                return true;
            }

            writer.WriteLine($"Type: {response.element.type}");
            writer.WriteLine($"Name: {response.element.name}");
            writer.WriteLine($"Enabled: {response.element.enabled}, Visible: {response.element.visible}, Focusable: {response.element.focusable}");
            writer.WriteLine($"WorldBound: ({response.layout.worldBound.x}, {response.layout.worldBound.y}, {response.layout.worldBound.width}, {response.layout.worldBound.height})");
            writer.WriteLine($"Text: {response.content.text}");
            writer.WriteLine($"Value: {response.content.value}");

            return true;
        }

        protected override ValueTask<UITreeInspectResponse> ExecuteAsync(UITreeInspectRequest request, CancellationToken cancellationToken)
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
            var response = _serializer.Serialize(element, request.includeResolvedStyle, request.includeBindingInfo);
            return new ValueTask<UITreeInspectResponse>(response);
        }
    }
}
