using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniCli.Server.Editor.Handlers
{
    internal static class ElementIntrospection
    {
        private static readonly BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

        public static string[] GetClasses(VisualElement element, bool includeUnityClasses)
        {
            if (element == null)
            {
                return Array.Empty<string>();
            }

            var classes = element.GetClasses();
            if (classes == null)
            {
                return Array.Empty<string>();
            }

            return classes
                .Where(c => includeUnityClasses || !IsUnityClass(c))
                .OrderBy(c => c, StringComparer.Ordinal)
                .ToArray();
        }

        public static bool IsInteractable(VisualElement element)
        {
            return element != null
                && element.enabledInHierarchy
                && element.visible
                && element.resolvedStyle.display != DisplayStyle.None
                && element.resolvedStyle.visibility != Visibility.Hidden;
        }

        public static string DescribeElement(VisualElement element, bool includeUnityClasses)
        {
            if (element == null)
            {
                return "<null>";
            }

            var parts = new List<string> { element.GetType().Name };
            if (!string.IsNullOrEmpty(element.name))
            {
                parts.Add($"#{element.name}");
            }

            var classes = GetClasses(element, includeUnityClasses);
            for (var i = 0; i < classes.Length; i++)
            {
                parts.Add($".{classes[i]}");
            }

            var content = GetContentPreview(element);
            if (!string.IsNullOrEmpty(content))
            {
                parts.Add($" \"{content}\"");
            }

            parts.Add($" [enabled={element.enabledInHierarchy} visible={element.visible}]");

            return string.Concat(parts);
        }

        public static string GetContentPreview(VisualElement element)
        {
            if (element == null)
            {
                return string.Empty;
            }

            var text = ExtractText(element);
            if (!string.IsNullOrEmpty(text))
            {
                return TrimContent(text);
            }

            if (TryGetValueAsString(element, out var value) && !string.IsNullOrEmpty(value))
            {
                return TrimContent(value);
            }

            return string.Empty;
        }

        public static string ExtractText(VisualElement element)
        {
            if (element is TextElement textElement)
            {
                return textElement.text ?? string.Empty;
            }

            return string.Empty;
        }

        public static bool TryGetValueAsString(VisualElement element, out string value)
        {
            value = string.Empty;
            if (element == null)
            {
                return false;
            }

            var type = element.GetType();
            var property = type.GetProperty("value", PublicInstance);
            if (property == null || !property.CanRead)
            {
                return false;
            }

            try
            {
                var valueObj = property.GetValue(element, null);
                value = ConvertToInvariantString(valueObj);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TrySetValueFromString(VisualElement element, string input, out string oldValue, out string newValue, out string error)
        {
            oldValue = string.Empty;
            newValue = string.Empty;
            error = string.Empty;

            if (element == null)
            {
                error = "Element is null.";
                return false;
            }

            var type = element.GetType();
            var property = type.GetProperty("value", PublicInstance);
            if (property == null || !property.CanRead || !property.CanWrite)
            {
                error = "Element does not expose a writable value property.";
                return false;
            }

            object oldObj;
            try
            {
                oldObj = property.GetValue(element, null);
                oldValue = ConvertToInvariantString(oldObj);
            }
            catch (Exception ex)
            {
                error = $"Failed to read current value: {ex.Message}";
                return false;
            }

            if (!TryConvertString(input, property.PropertyType, oldObj, out var converted, out error))
            {
                return false;
            }

            try
            {
                property.SetValue(element, converted, null);
                var currentObj = property.GetValue(element, null);
                newValue = ConvertToInvariantString(currentObj);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed to set value: {ex.Message}";
                return false;
            }
        }

        public static bool TrySetChoiceFromString(VisualElement element, string choice, out string oldValue, out string newValue, out string[] choices, out string error)
        {
            oldValue = string.Empty;
            newValue = string.Empty;
            choices = Array.Empty<string>();
            error = string.Empty;

            if (element is DropdownField dropdown)
            {
                choices = dropdown.choices != null
                    ? dropdown.choices.Select(c => c ?? string.Empty).ToArray()
                    : Array.Empty<string>();

                if (!choices.Contains(choice, StringComparer.Ordinal))
                {
                    error = $"Choice '{choice}' does not exist.";
                    return false;
                }

                oldValue = dropdown.value ?? string.Empty;
                dropdown.value = choice;
                newValue = dropdown.value ?? string.Empty;
                return true;
            }

            if (element is EnumField enumField)
            {
                var enumType = enumField.value != null ? enumField.value.GetType() : null;
                if (enumType == null || !enumType.IsEnum)
                {
                    error = "EnumField has no enum type.";
                    return false;
                }

                choices = Enum.GetNames(enumType);
                if (!choices.Contains(choice, StringComparer.Ordinal))
                {
                    error = $"Choice '{choice}' does not exist.";
                    return false;
                }

                oldValue = enumField.value != null ? enumField.value.ToString() : string.Empty;
                enumField.value = (Enum)Enum.Parse(enumType, choice, false);
                newValue = enumField.value != null ? enumField.value.ToString() : string.Empty;
                return true;
            }

            var type = element.GetType();
            if (!type.IsGenericType)
            {
                error = "Element is not a supported selection control.";
                return false;
            }

            var typeDefinition = type.GetGenericTypeDefinition();
            if (!string.Equals(typeDefinition.Name, "PopupField`1", StringComparison.Ordinal))
            {
                error = "Element is not a supported selection control.";
                return false;
            }

            var choicesProperty = type.GetProperty("choices", PublicInstance);
            var valueProperty = type.GetProperty("value", PublicInstance);
            if (choicesProperty == null || valueProperty == null || !valueProperty.CanWrite)
            {
                error = "Element is not a supported selection control.";
                return false;
            }

            var valueType = valueProperty.PropertyType;
            var rawChoices = choicesProperty.GetValue(element, null) as System.Collections.IEnumerable;
            var list = new List<string>();
            object selected = null;
            if (rawChoices != null)
            {
                foreach (var item in rawChoices)
                {
                    var text = item != null ? item.ToString() : string.Empty;
                    list.Add(text);
                    if (string.Equals(text, choice, StringComparison.Ordinal))
                    {
                        selected = item;
                    }
                }
            }

            choices = list.ToArray();
            if (selected == null)
            {
                error = $"Choice '{choice}' does not exist.";
                return false;
            }

            oldValue = ConvertToInvariantString(valueProperty.GetValue(element, null));
            if (!valueType.IsAssignableFrom(selected.GetType()))
            {
                error = $"Choice '{choice}' is incompatible with target field type '{valueType.Name}'.";
                return false;
            }

            valueProperty.SetValue(element, selected, null);
            newValue = ConvertToInvariantString(valueProperty.GetValue(element, null));
            return true;
        }

        public static UITreeRect ToRect(Rect rect)
        {
            return new UITreeRect
            {
                x = SanitizeFloat(rect.x),
                y = SanitizeFloat(rect.y),
                width = SanitizeFloat(rect.width),
                height = SanitizeFloat(rect.height)
            };
        }

        public static string GetHierarchyPath(VisualElement root, VisualElement element)
        {
            if (root == null || element == null)
            {
                return string.Empty;
            }

            if (ReferenceEquals(root, element))
            {
                return "0";
            }

            var indexes = new List<int>();
            var current = element;
            while (current != null && !ReferenceEquals(current, root))
            {
                var parent = current.hierarchy.parent;
                if (parent == null)
                {
                    return string.Empty;
                }

                indexes.Add(parent.IndexOf(current));
                current = parent;
            }

            if (!ReferenceEquals(current, root))
            {
                return string.Empty;
            }

            indexes.Reverse();
            return "0/" + string.Join("/", indexes);
        }

        public static bool IsUnityClass(string className)
        {
            return !string.IsNullOrEmpty(className)
                && className.StartsWith("unity-", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryConvertString(string input, Type targetType, object currentValue, out object converted, out string error)
        {
            converted = null;
            error = string.Empty;

            var nullableType = Nullable.GetUnderlyingType(targetType);
            var effectiveType = nullableType ?? targetType;

            if (effectiveType == typeof(string))
            {
                converted = input;
                return true;
            }

            if (string.IsNullOrEmpty(input))
            {
                if (nullableType != null)
                {
                    converted = null;
                    return true;
                }

                if (!effectiveType.IsValueType)
                {
                    converted = null;
                    return true;
                }
            }

            try
            {
                if (effectiveType == typeof(bool))
                {
                    if (!bool.TryParse(input, out var boolValue))
                    {
                        error = $"Cannot convert '{input}' to bool.";
                        return false;
                    }

                    converted = boolValue;
                    return true;
                }

                if (effectiveType.IsEnum)
                {
                    converted = Enum.Parse(effectiveType, input, true);
                    return true;
                }

                if (effectiveType == typeof(int))
                {
                    if (!int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                    {
                        error = $"Cannot convert '{input}' to int.";
                        return false;
                    }

                    converted = intValue;
                    return true;
                }

                if (effectiveType == typeof(long))
                {
                    if (!long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
                    {
                        error = $"Cannot convert '{input}' to long.";
                        return false;
                    }

                    converted = longValue;
                    return true;
                }

                if (effectiveType == typeof(float))
                {
                    if (!float.TryParse(input, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var floatValue))
                    {
                        error = $"Cannot convert '{input}' to float.";
                        return false;
                    }

                    converted = floatValue;
                    return true;
                }

                if (effectiveType == typeof(double))
                {
                    if (!double.TryParse(input, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var doubleValue))
                    {
                        error = $"Cannot convert '{input}' to double.";
                        return false;
                    }

                    converted = doubleValue;
                    return true;
                }

                if (effectiveType == typeof(decimal))
                {
                    if (!decimal.TryParse(input, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var decimalValue))
                    {
                        error = $"Cannot convert '{input}' to decimal.";
                        return false;
                    }

                    converted = decimalValue;
                    return true;
                }

                if (effectiveType == typeof(uint))
                {
                    if (!uint.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var uintValue))
                    {
                        error = $"Cannot convert '{input}' to uint.";
                        return false;
                    }

                    converted = uintValue;
                    return true;
                }

                if (effectiveType == typeof(ulong))
                {
                    if (!ulong.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ulongValue))
                    {
                        error = $"Cannot convert '{input}' to ulong.";
                        return false;
                    }

                    converted = ulongValue;
                    return true;
                }

                if (effectiveType == typeof(short))
                {
                    if (!short.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var shortValue))
                    {
                        error = $"Cannot convert '{input}' to short.";
                        return false;
                    }

                    converted = shortValue;
                    return true;
                }

                if (effectiveType == typeof(byte))
                {
                    if (!byte.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var byteValue))
                    {
                        error = $"Cannot convert '{input}' to byte.";
                        return false;
                    }

                    converted = byteValue;
                    return true;
                }

                converted = Convert.ChangeType(input, effectiveType, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Cannot convert '{input}' to {effectiveType.Name}: {ex.Message}";
                return false;
            }
        }

        private static string ConvertToInvariantString(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return value switch
            {
                float f => f.ToString(CultureInfo.InvariantCulture),
                double d => d.ToString(CultureInfo.InvariantCulture),
                decimal m => m.ToString(CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty
            };
        }

        private static string TrimContent(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var compact = value.Replace("\r", " ").Replace("\n", " ").Trim();
            return compact.Length <= 80 ? compact : compact.Substring(0, 77) + "...";
        }

        private static float SanitizeFloat(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }
    }
}
