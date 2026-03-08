using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace UniCli.Server.Editor.Handlers
{
    internal sealed class InspectSerializer
    {
        public UITreeInspectResponse Serialize(VisualElement element, bool includeResolvedStyle, bool includeBindingInfo)
        {
            return new UITreeInspectResponse
            {
                element = new UITreeElementInfo
                {
                    type = element.GetType().FullName,
                    name = element.name,
                    classes = ElementIntrospection.GetClasses(element, true),
                    enabled = element.enabledInHierarchy,
                    visible = element.visible,
                    focusable = element.focusable
                },
                layout = new UITreeLayoutInfo
                {
                    worldBound = ElementIntrospection.ToRect(element.worldBound),
                    localBound = ElementIntrospection.ToRect(element.localBound)
                },
                content = new UITreeContentInfo
                {
                    text = ElementIntrospection.ExtractText(element),
                    value = ElementIntrospection.TryGetValueAsString(element, out var value) ? value : string.Empty
                },
                resolvedStyle = includeResolvedStyle
                    ? CollectResolvedStyle(element)
                    : Array.Empty<UITreeStyleEntry>(),
                binding = includeBindingInfo
                    ? CollectBindingInfo(element)
                    : null
            };
        }

        private static UITreeStyleEntry[] CollectResolvedStyle(VisualElement element)
        {
            var style = element.resolvedStyle;
            var entries = new List<UITreeStyleEntry>
            {
                CreateEntry("display", style.display),
                CreateEntry("visibility", style.visibility),
                CreateEntry("position", style.position),
                CreateEntry("left", style.left),
                CreateEntry("top", style.top),
                CreateEntry("right", style.right),
                CreateEntry("bottom", style.bottom),
                CreateEntry("width", style.width),
                CreateEntry("height", style.height),
                CreateEntry("minWidth", style.minWidth),
                CreateEntry("minHeight", style.minHeight),
                CreateEntry("maxWidth", style.maxWidth),
                CreateEntry("maxHeight", style.maxHeight),
                CreateEntry("opacity", style.opacity),
                CreateEntry("fontSize", style.fontSize),
                CreateEntry("flexDirection", style.flexDirection),
                CreateEntry("justifyContent", style.justifyContent),
                CreateEntry("alignItems", style.alignItems),
                CreateEntry("alignSelf", style.alignSelf),
                CreateEntry("color", style.color),
                CreateEntry("backgroundColor", style.backgroundColor)
            };

            return entries.ToArray();
        }

        private static UITreeBindingInfo CollectBindingInfo(VisualElement element)
        {
            var info = new UITreeBindingInfo
            {
                bindingPath = string.Empty,
                dataSourceType = string.Empty,
                hasDataSource = false
            };

            if (element is IBindable bindable)
            {
                info.bindingPath = bindable.bindingPath ?? string.Empty;
            }

            if (element.userData != null)
            {
                info.hasDataSource = true;
                info.dataSourceType = element.userData.GetType().FullName;
            }

            return info;
        }

        private static UITreeStyleEntry CreateEntry(string key, object value)
        {
            return new UITreeStyleEntry
            {
                key = key,
                value = ConvertToString(value)
            };
        }

        private static string ConvertToString(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value is IFormattable formattable)
            {
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            return value.ToString() ?? string.Empty;
        }
    }
}
