using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace UniCli.Server.Editor.Handlers
{
    public sealed class UITreeDumpHandler : CommandHandler<UITreeDumpRequest, UITreeDumpResponse>
    {
        private readonly PanelResolver _panelResolver = new();
        private readonly ElementResolver _elementResolver = new();

        public override string CommandName => "UITree.Dump";
        public override string Description => "Dump UI Toolkit hierarchy for an Editor panel";

        protected override bool TryWriteFormatted(UITreeDumpResponse response, bool success, IFormatWriter writer)
        {
            if (!success)
            {
                writer.WriteLine("UITree dump failed.");
                return true;
            }

            if (response.panelInfo == null)
            {
                writer.WriteLine($"Opened panels ({response.lines?.Length ?? 0}):");
                if (response.lines != null)
                {
                    foreach (var line in response.lines)
                    {
                        writer.WriteLine($"  {line}");
                    }
                }

                return true;
            }

            writer.WriteLine($"Panel: {response.panelInfo.name} [{response.panelInfo.title}]");
            writer.WriteLine($"Size: {response.panelInfo.size.width}x{response.panelInfo.size.height}, Elements: {response.panelInfo.elementCount}");
            writer.WriteLine($"Lines: {response.lines?.Length ?? 0}");

            var previewCount = Math.Min(20, response.lines?.Length ?? 0);
            for (var i = 0; i < previewCount; i++)
            {
                writer.WriteLine(response.lines[i]);
            }

            if ((response.lines?.Length ?? 0) > previewCount)
            {
                writer.WriteLine("...");
            }

            return true;
        }

        protected override ValueTask<UITreeDumpResponse> ExecuteAsync(UITreeDumpRequest request, CancellationToken cancellationToken)
        {
            var depth = request.depth;
            if (depth < -1)
            {
                throw new CommandFailedException("Depth must be -1 or greater.", new UITreeErrorResponse
                {
                    error = "Depth must be -1 or greater.",
                    candidates = Array.Empty<string>()
                });
            }

            if (string.IsNullOrWhiteSpace(request.panel))
            {
                var openedPanels = _panelResolver.ListOpenPanels(cancellationToken);
                var lines = openedPanels
                    .Select(p => $"{p.panelInfo.name} [{p.panelInfo.title}] size={p.panelInfo.size.width}x{p.panelInfo.size.height} elements={p.panelInfo.elementCount}")
                    .ToArray();

                return new ValueTask<UITreeDumpResponse>(new UITreeDumpResponse
                {
                    panelInfo = null,
                    lines = lines,
                    matchedCount = 0
                });
            }

            var panel = _panelResolver.Resolve(request.panel, cancellationToken);
            var serializer = new TreeSerializer(_elementResolver);
            var serialized = serializer.Serialize(panel.root, depth, request.filter, request.includeUnityClasses, cancellationToken);

            return new ValueTask<UITreeDumpResponse>(new UITreeDumpResponse
            {
                panelInfo = panel.panelInfo,
                lines = serialized.lines,
                matchedCount = serialized.matchedCount
            });
        }
    }
}
