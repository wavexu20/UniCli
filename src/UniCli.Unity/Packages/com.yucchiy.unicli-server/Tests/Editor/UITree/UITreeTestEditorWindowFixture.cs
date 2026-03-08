using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniCli.Server.Editor.Tests
{
    internal enum UITreeFixtureMode
    {
        Alpha,
        Beta,
        Gamma
    }

    public sealed class UITreeTestEditorWindow : EditorWindow
    {
        public const string Title = "UniCli UITree Test";

        public int clickCount;
        public int fillChangeCount;
        public int selectChangeCount;
        public string lastTextValue;
        public string lastChoice;

        private VisualElement _statusBanner;
        private Label _statusLabel;
        private TextField _nameField;
        private IntegerField _countField;
        private DropdownField _modeDropdown;
        private Toggle _enabledToggle;
        private Label _previewTitle;
        private Label _modeBadge;
        private Label _summaryLabel;
        private VisualElement _previewCard;
        private VisualElement _previewSwatch;
        private VisualElement _meterFill;

        public string StatusText => _statusLabel != null ? _statusLabel.text : string.Empty;

        public static UITreeTestEditorWindow Open()
        {
            var window = GetWindow<UITreeTestEditorWindow>(true, Title, true);
            window.titleContent = new GUIContent(Title);
            window.position = new Rect(120f, 120f, 720f, 480f);
            window.Show();
            window.Focus();
            window.BuildUi();
            return window;
        }

        private void OnEnable()
        {
            BuildUi();
        }

        public void BuildUi()
        {
            rootVisualElement.Clear();

            clickCount = 0;
            fillChangeCount = 0;
            selectChangeCount = 0;
            lastTextValue = string.Empty;
            lastChoice = string.Empty;

            var container = new VisualElement
            {
                name = "fixture-root"
            };
            container.AddToClassList("test-container");
            container.style.flexDirection = FlexDirection.Column;
            container.style.paddingLeft = 8;
            container.style.paddingTop = 8;
            container.style.paddingRight = 8;
            container.style.paddingBottom = 8;
            rootVisualElement.Add(container);

            _statusBanner = new VisualElement
            {
                name = "status-banner"
            };
            _statusBanner.style.height = 54;
            _statusBanner.style.marginBottom = 10;
            _statusBanner.style.paddingLeft = 12;
            _statusBanner.style.paddingTop = 10;
            _statusBanner.style.paddingRight = 12;
            _statusBanner.style.paddingBottom = 10;
            _statusBanner.style.backgroundColor = new Color(0.15f, 0.19f, 0.24f);
            _statusBanner.style.borderBottomWidth = 2;
            _statusBanner.style.borderLeftWidth = 2;
            _statusBanner.style.borderRightWidth = 2;
            _statusBanner.style.borderTopWidth = 2;
            _statusBanner.style.borderBottomColor = new Color(0.38f, 0.46f, 0.58f);
            _statusBanner.style.borderLeftColor = new Color(0.38f, 0.46f, 0.58f);
            _statusBanner.style.borderRightColor = new Color(0.38f, 0.46f, 0.58f);
            _statusBanner.style.borderTopColor = new Color(0.38f, 0.46f, 0.58f);
            container.Add(_statusBanner);

            _statusLabel = new Label("idle")
            {
                name = "status-label"
            };
            _statusLabel.AddToClassList("status");
            _statusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _statusLabel.style.fontSize = 18;
            _statusBanner.Add(_statusLabel);

            _summaryLabel = new Label("preview pending")
            {
                name = "summary-label"
            };
            _summaryLabel.style.marginTop = 4;
            _summaryLabel.style.color = new Color(0.82f, 0.87f, 0.92f);
            _statusBanner.Add(_summaryLabel);

            var contentRow = new VisualElement
            {
                name = "content-row"
            };
            contentRow.style.flexDirection = FlexDirection.Row;
            contentRow.style.flexGrow = 1;
            container.Add(contentRow);

            var formColumn = new VisualElement
            {
                name = "form-column"
            };
            formColumn.style.flexGrow = 1;
            formColumn.style.marginRight = 12;
            contentRow.Add(formColumn);

            var button = new Button
            {
                name = "run-button",
                text = "Run"
            };
            button.AddToClassList("primary");
            button.clicked += () =>
            {
                clickCount++;
                _statusLabel.text = $"clicked:{clickCount}";
                UpdatePreviewVisuals();
            };
            button.style.marginBottom = 8;
            formColumn.Add(button);

            _nameField = new TextField("Name")
            {
                name = "name-field",
                value = "Initial"
            };
            _nameField.AddToClassList("editable");
            _nameField.bindingPath = "fixture.name";
            _nameField.userData = new FixtureData { name = "data-source" };
            _nameField.RegisterValueChangedCallback(evt =>
            {
                fillChangeCount++;
                lastTextValue = evt.newValue;
                _statusLabel.text = $"filled:{evt.newValue}";
                UpdatePreviewVisuals();
            });
            _nameField.style.marginBottom = 8;
            formColumn.Add(_nameField);

            _countField = new IntegerField("Count")
            {
                name = "count-field",
                value = 1
            };
            _countField.AddToClassList("editable");
            _countField.RegisterValueChangedCallback(evt =>
            {
                fillChangeCount++;
                _statusLabel.text = $"count:{evt.newValue}";
                UpdatePreviewVisuals();
            });
            _countField.style.marginBottom = 8;
            formColumn.Add(_countField);

            _modeDropdown = new DropdownField("Mode", new List<string> { "Alpha", "Beta", "Gamma" }, 0)
            {
                name = "mode-dropdown"
            };
            _modeDropdown.AddToClassList("selectable");
            _modeDropdown.RegisterValueChangedCallback(evt =>
            {
                selectChangeCount++;
                lastChoice = evt.newValue;
                _statusLabel.text = $"mode:{evt.newValue}";
                UpdatePreviewVisuals();
            });
            _modeDropdown.style.marginBottom = 8;
            formColumn.Add(_modeDropdown);

            _enabledToggle = new Toggle("Enabled")
            {
                name = "enabled-toggle",
                value = true
            };
            _enabledToggle.AddToClassList("toggle");
            _enabledToggle.style.marginBottom = 10;
            _enabledToggle.RegisterValueChangedCallback(_ => UpdatePreviewVisuals());
            formColumn.Add(_enabledToggle);

            var stackPreview = new VisualElement
            {
                name = "stack-preview"
            };
            stackPreview.style.flexDirection = FlexDirection.Column;
            stackPreview.style.marginTop = 4;
            formColumn.Add(stackPreview);

            for (var i = 0; i < 3; i++)
            {
                var row = new VisualElement
                {
                    name = $"activity-row-{i}"
                };
                row.style.height = 18;
                row.style.marginBottom = 4;
                row.style.backgroundColor = i % 2 == 0
                    ? new Color(0.20f, 0.24f, 0.29f)
                    : new Color(0.16f, 0.19f, 0.23f);
                stackPreview.Add(row);
            }

            _previewCard = new VisualElement
            {
                name = "preview-card"
            };
            _previewCard.style.width = 280;
            _previewCard.style.minHeight = 280;
            _previewCard.style.paddingLeft = 14;
            _previewCard.style.paddingTop = 14;
            _previewCard.style.paddingRight = 14;
            _previewCard.style.paddingBottom = 14;
            _previewCard.style.backgroundColor = new Color(0.16f, 0.19f, 0.24f);
            _previewCard.style.borderBottomWidth = 2;
            _previewCard.style.borderLeftWidth = 2;
            _previewCard.style.borderRightWidth = 2;
            _previewCard.style.borderTopWidth = 2;
            _previewCard.style.borderBottomColor = new Color(0.32f, 0.39f, 0.49f);
            _previewCard.style.borderLeftColor = new Color(0.32f, 0.39f, 0.49f);
            _previewCard.style.borderRightColor = new Color(0.32f, 0.39f, 0.49f);
            _previewCard.style.borderTopColor = new Color(0.32f, 0.39f, 0.49f);
            contentRow.Add(_previewCard);

            _previewTitle = new Label("Initial")
            {
                name = "preview-title"
            };
            _previewTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _previewTitle.style.fontSize = 19;
            _previewTitle.style.marginBottom = 6;
            _previewCard.Add(_previewTitle);

            _modeBadge = new Label("Alpha / 0 clicks")
            {
                name = "mode-badge"
            };
            _modeBadge.style.marginBottom = 10;
            _modeBadge.style.color = new Color(0.89f, 0.92f, 0.95f);
            _previewCard.Add(_modeBadge);

            _previewSwatch = new VisualElement
            {
                name = "preview-swatch"
            };
            _previewSwatch.style.height = 120;
            _previewSwatch.style.marginBottom = 10;
            _previewCard.Add(_previewSwatch);

            var meterTrack = new VisualElement
            {
                name = "preview-meter-track"
            };
            meterTrack.style.height = 14;
            meterTrack.style.backgroundColor = new Color(0.10f, 0.12f, 0.15f);
            meterTrack.style.marginBottom = 10;
            _previewCard.Add(meterTrack);

            _meterFill = new VisualElement
            {
                name = "preview-meter-fill"
            };
            _meterFill.style.height = 14;
            meterTrack.Add(_meterFill);

            var chipRow = new VisualElement
            {
                name = "chip-row"
            };
            chipRow.style.flexDirection = FlexDirection.Row;
            chipRow.style.marginBottom = 10;
            _previewCard.Add(chipRow);

            chipRow.Add(CreateChip("chip-alpha", new Color(0.22f, 0.56f, 0.93f)));
            chipRow.Add(CreateChip("chip-beta", new Color(0.93f, 0.55f, 0.19f)));
            chipRow.Add(CreateChip("chip-gamma", new Color(0.29f, 0.73f, 0.34f)));

            var footer = new Label("The preview panel changes visually when commands mutate the controls.")
            {
                name = "preview-footer"
            };
            footer.style.whiteSpace = WhiteSpace.Normal;
            footer.style.color = new Color(0.82f, 0.87f, 0.92f);
            _previewCard.Add(footer);

            UpdatePreviewVisuals();

            rootVisualElement.schedule.Execute(() => rootVisualElement.MarkDirtyRepaint());
        }

        private void UpdatePreviewVisuals()
        {
            var modeColor = ResolveModeColor(_modeDropdown != null ? _modeDropdown.value : "Alpha");
            var countValue = _countField != null ? Mathf.Clamp(_countField.value, 5, 100) : 5;
            var enabled = _enabledToggle == null || _enabledToggle.value;

            if (_previewTitle != null)
            {
                _previewTitle.text = string.IsNullOrEmpty(_nameField?.value) ? "Untitled" : _nameField.value;
                _previewTitle.style.color = enabled
                    ? new Color(0.97f, 0.98f, 0.99f)
                    : new Color(0.58f, 0.61f, 0.64f);
            }

            if (_modeBadge != null)
            {
                _modeBadge.text = $"{_modeDropdown?.value ?? "Alpha"} / {clickCount} clicks";
            }

            if (_summaryLabel != null)
            {
                _summaryLabel.text = $"fills:{fillChangeCount}  selects:{selectChangeCount}  count:{_countField?.value ?? 0}";
            }

            if (_statusBanner != null)
            {
                _statusBanner.style.backgroundColor = clickCount > 0
                    ? Tint(modeColor, 0.35f)
                    : new Color(0.15f, 0.19f, 0.24f);
            }

            if (_previewCard != null)
            {
                _previewCard.style.borderBottomColor = modeColor;
                _previewCard.style.borderLeftColor = modeColor;
                _previewCard.style.borderRightColor = modeColor;
                _previewCard.style.borderTopColor = modeColor;
                _previewCard.style.backgroundColor = enabled
                    ? new Color(0.16f, 0.19f, 0.24f)
                    : new Color(0.11f, 0.12f, 0.14f);
            }

            if (_previewSwatch != null)
            {
                _previewSwatch.style.backgroundColor = modeColor;
            }

            if (_meterFill != null)
            {
                _meterFill.style.width = new Length(countValue, LengthUnit.Percent);
                _meterFill.style.backgroundColor = clickCount % 2 == 0
                    ? Tint(modeColor, 0.85f)
                    : Tint(modeColor, 1.10f);
            }

            rootVisualElement.MarkDirtyRepaint();
        }

        private static VisualElement CreateChip(string name, Color color)
        {
            var chip = new VisualElement
            {
                name = name
            };
            chip.style.width = 36;
            chip.style.height = 18;
            chip.style.marginRight = 6;
            chip.style.backgroundColor = color;
            return chip;
        }

        private static Color ResolveModeColor(string mode)
        {
            return mode switch
            {
                "Beta" => new Color(0.93f, 0.55f, 0.19f),
                "Gamma" => new Color(0.29f, 0.73f, 0.34f),
                _ => new Color(0.22f, 0.56f, 0.93f)
            };
        }

        private static Color Tint(Color color, float multiplier)
        {
            return new Color(
                Mathf.Clamp01(color.r * multiplier),
                Mathf.Clamp01(color.g * multiplier),
                Mathf.Clamp01(color.b * multiplier),
                1f);
        }

        [Serializable]
        private sealed class FixtureData
        {
            public string name;
        }
    }
}
