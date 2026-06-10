using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Relay.Classes;

namespace Relay.UI
{
    internal sealed class InputDialog : Window
    {
        private readonly DynamoGraphInputs graphInputs;
        private readonly Dictionary<string, object> prefilledValues;
        private readonly Dictionary<string, FrameworkElement> inputControls =
            new Dictionary<string, FrameworkElement>();

        public bool WasCancelled { get; private set; } = true;
        public bool NeedsPick { get; private set; }
        public string PickInputId { get; private set; }
        public bool PickMultiple { get; private set; }
        public Dictionary<string, object> PartialValues { get; private set; } =
            new Dictionary<string, object>();
        public Dictionary<string, object> UserValues { get; private set; } =
            new Dictionary<string, object>();

        public InputDialog(DynamoGraphInputs graphInputs, Dictionary<string, object> prefilledValues = null)
        {
            this.graphInputs = graphInputs;
            this.prefilledValues = prefilledValues ?? new Dictionary<string, object>();
            BuildUi();
        }

        private void BuildUi()
        {
            Title = $"Relay - {graphInputs.GraphName}";
            Width = 460;
            MinWidth = 360;
            MaxHeight = 680;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;

            var root = new Grid { Margin = new Thickness(18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new TextBlock
            {
                Text = "Configure Graph Inputs",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 14)
            };
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var inputPanel = new StackPanel();
            BuildGroupedInputs(inputPanel);

            var scrollViewer = new ScrollViewer
            {
                Content = inputPanel,
                MaxHeight = 480,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            Grid.SetRow(scrollViewer, 1);
            root.Children.Add(scrollViewer);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0)
            };

            var runButton = new Button
            {
                Content = "Run Graph",
                Width = 104,
                Height = 30,
                Margin = new Thickness(0, 0, 8, 0)
            };
            runButton.Click += OnRunClicked;

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 92,
                Height = 30
            };
            cancelButton.Click += OnCancelClicked;

            buttons.Children.Add(runButton);
            buttons.Children.Add(cancelButton);
            Grid.SetRow(buttons, 2);
            root.Children.Add(buttons);

            Content = root;
        }

        private void BuildGroupedInputs(StackPanel panel)
        {
            var groupInputs = new List<DynamoInputDefinition>();
            string currentGroupName = null;

            void FlushGroup()
            {
                if (groupInputs.Count == 0)
                    return;

                if (!string.IsNullOrEmpty(currentGroupName))
                {
                    var innerPanel = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
                    foreach (var input in groupInputs)
                        innerPanel.Children.Add(BuildInputBlock(input));

                    panel.Children.Add(new GroupBox
                    {
                        Header = currentGroupName,
                        Content = innerPanel,
                        Padding = new Thickness(8, 8, 8, 2),
                        Margin = new Thickness(0, 0, 0, 14)
                    });
                }
                else
                {
                    foreach (var input in groupInputs)
                        panel.Children.Add(BuildInputBlock(input));
                }

                groupInputs.Clear();
            }

            foreach (var input in graphInputs.Inputs)
            {
                var groupName = input.GroupName ?? string.Empty;
                if (currentGroupName == null)
                {
                    currentGroupName = groupName;
                }
                else if (!string.Equals(currentGroupName, groupName, StringComparison.Ordinal))
                {
                    FlushGroup();
                    currentGroupName = groupName;
                }

                groupInputs.Add(input);
            }

            FlushGroup();
        }

        private UIElement BuildInputBlock(DynamoInputDefinition input)
        {
            var block = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };

            block.Children.Add(new TextBlock
            {
                Text = input.Name,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            });

            var control = BuildControl(input);
            inputControls[input.Id] = control;
            block.Children.Add(control);

            if (!string.IsNullOrWhiteSpace(input.Description))
            {
                block.Children.Add(new TextBlock
                {
                    Text = input.Description,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 3, 0, 0)
                });
            }

            return block;
        }

        private FrameworkElement BuildControl(DynamoInputDefinition input)
        {
            return input.DataType switch
            {
                DynamoInputType.Boolean => BuildCheckBox(input),
                DynamoInputType.Number => BuildNumericControl(input, false),
                DynamoInputType.Integer => BuildNumericControl(input, true),
                DynamoInputType.Dropdown => BuildDropdown(input),
                DynamoInputType.Selection => BuildSelection(input),
                _ => BuildTextBox(input)
            };
        }

        private CheckBox BuildCheckBox(DynamoInputDefinition input)
        {
            var isChecked = prefilledValues.TryGetValue(input.Id, out var value) && value is bool boolValue
                ? boolValue
                : input.DefaultValue is bool defaultBool && defaultBool;

            return new CheckBox
            {
                IsChecked = isChecked,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private TextBox BuildTextBox(DynamoInputDefinition input)
        {
            var text = prefilledValues.TryGetValue(input.Id, out var value)
                ? value?.ToString() ?? string.Empty
                : input.DefaultValue?.ToString() ?? string.Empty;

            return new TextBox
            {
                Text = text,
                Height = 28,
                Padding = new Thickness(4),
                VerticalContentAlignment = VerticalAlignment.Center
            };
        }

        private FrameworkElement BuildNumericControl(DynamoInputDefinition input, bool isInteger)
        {
            var currentValue = prefilledValues.TryGetValue(input.Id, out var value)
                ? ConvertToDouble(value)
                : ConvertToDouble(input.DefaultValue);

            if (!input.MinValue.HasValue || !input.MaxValue.HasValue)
            {
                return new TextBox
                {
                    Text = FormatNumber(currentValue, isInteger),
                    Height = 28,
                    Padding = new Thickness(4),
                    VerticalContentAlignment = VerticalAlignment.Center
                };
            }

            var min = input.MinValue.Value;
            var max = input.MaxValue.Value;
            var step = input.StepValue ?? (isInteger ? 1.0 : 0.1);
            if (isInteger)
                step = Math.Max(1.0, Math.Round(step));

            currentValue = Math.Max(min, Math.Min(max, currentValue));
            if (isInteger)
                currentValue = Math.Round(currentValue);

            var slider = new Slider
            {
                Minimum = min,
                Maximum = max,
                Value = currentValue,
                SmallChange = step,
                LargeChange = Math.Max(step * 10, (max - min) / 10.0),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };

            if (isInteger)
            {
                slider.TickFrequency = step;
                slider.IsSnapToTickEnabled = true;
            }

            var textBox = new TextBox
            {
                Text = FormatNumber(currentValue, isInteger),
                Width = 76,
                Height = 28,
                Padding = new Thickness(4),
                VerticalContentAlignment = VerticalAlignment.Center
            };

            slider.ValueChanged += (_, _) =>
            {
                textBox.Text = FormatNumber(slider.Value, isInteger);
            };

            textBox.LostFocus += (_, _) =>
            {
                if (!double.TryParse(textBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                {
                    textBox.Text = FormatNumber(slider.Value, isInteger);
                    return;
                }

                if (isInteger)
                    parsed = Math.Round(parsed);

                slider.Value = Math.Max(min, Math.Min(max, parsed));
                textBox.Text = FormatNumber(slider.Value, isInteger);
            };

            var grid = new Grid { Tag = slider };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(slider, 0);
            Grid.SetColumn(textBox, 1);
            grid.Children.Add(slider);
            grid.Children.Add(textBox);

            return grid;
        }

        private ComboBox BuildDropdown(DynamoInputDefinition input)
        {
            var combo = new ComboBox
            {
                Height = 28,
                IsEditable = input.Items.Count == 0,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            foreach (var item in input.Items)
                combo.Items.Add(item);

            var selectedIndex = input.SelectedIndex;
            var selectedText = input.SelectedString ?? string.Empty;

            if (prefilledValues.TryGetValue(input.Id, out var value) && value is DropdownValue dropdownValue)
            {
                selectedIndex = dropdownValue.SelectedIndex;
                selectedText = dropdownValue.SelectedString ?? string.Empty;
            }

            if (selectedIndex >= 0 && selectedIndex < combo.Items.Count)
            {
                combo.SelectedIndex = selectedIndex;
            }
            else if (!string.IsNullOrEmpty(selectedText))
            {
                var displayIndex = input.ItemValues.FindIndex(value =>
                    string.Equals(value, selectedText, StringComparison.OrdinalIgnoreCase));

                if (displayIndex >= 0 && displayIndex < combo.Items.Count)
                    combo.SelectedIndex = displayIndex;
                else
                    combo.Text = selectedText;
            }

            if (input.Items.Count == 0 && !string.IsNullOrEmpty(combo.Text))
                combo.Items.Add(combo.Text);

            return combo;
        }

        private FrameworkElement BuildSelection(DynamoInputDefinition input)
        {
            var currentId = input.SelectionIdentifier ?? string.Empty;
            var displayText = string.IsNullOrEmpty(currentId)
                ? "No element selected"
                : $"Previously selected: {currentId}";

            if (prefilledValues.TryGetValue(input.Id, out var value))
            {
                if (value is SelectionValue selectionValue)
                {
                    currentId = selectionValue.Identifier;
                    displayText = string.IsNullOrEmpty(selectionValue.DisplayText)
                        ? selectionValue.Identifier
                        : selectionValue.DisplayText;
                }
                else if (value is string stringValue)
                {
                    currentId = stringValue;
                    displayText = string.IsNullOrEmpty(stringValue) ? "No element selected" : stringValue;
                }
            }

            var info = new TextBox
            {
                Text = displayText,
                IsReadOnly = true,
                Height = 28,
                Padding = new Thickness(4),
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                Margin = new Thickness(0, 0, 0, 4)
            };

            var pickButton = new Button
            {
                Content = input.IsMultipleSelection ? "Select Elements in Revit..." : "Select Element in Revit...",
                Height = 28,
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(8, 0, 8, 0)
            };

            var panel = new StackPanel { Tag = currentId };
            panel.Children.Add(info);
            panel.Children.Add(pickButton);

            pickButton.Click += (_, _) =>
            {
                PartialValues = CollectValues();
                NeedsPick = true;
                PickInputId = input.Id;
                PickMultiple = input.IsMultipleSelection;
                WasCancelled = false;
                DialogResult = false;
                Close();
            };

            return panel;
        }

        private Dictionary<string, object> CollectValues()
        {
            var values = new Dictionary<string, object>();
            foreach (var control in inputControls)
            {
                var definition = graphInputs.Inputs.FirstOrDefault(input => input.Id == control.Key);
                values[control.Key] = ExtractValue(control.Value, definition);
            }

            return values;
        }

        private static object ExtractValue(FrameworkElement control, DynamoInputDefinition definition)
        {
            if (control is CheckBox checkBox)
                return checkBox.IsChecked == true;

            if (control is TextBox textBox)
            {
                var text = textBox.Text ?? string.Empty;
                if (definition?.DataType == DynamoInputType.Number &&
                    double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var doubleValue))
                    return doubleValue;

                if (definition?.DataType == DynamoInputType.Integer)
                {
                    if (int.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var intValue))
                        return intValue;

                    if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedDouble))
                        return (int)Math.Round(parsedDouble);
                }

                return text;
            }

            if (control is Grid grid && grid.Tag is Slider slider)
            {
                return definition?.DataType == DynamoInputType.Integer
                    ? (object)(int)Math.Round(slider.Value)
                    : slider.Value;
            }

            if (control is ComboBox comboBox)
            {
                var selectedIndex = comboBox.SelectedIndex;
                var selectedString = comboBox.SelectedItem as string ?? comboBox.Text ?? string.Empty;

                if (definition != null &&
                    selectedIndex >= 0 &&
                    selectedIndex < definition.ItemValues.Count)
                    selectedString = definition.ItemValues[selectedIndex];

                return new DropdownValue
                {
                    SelectedIndex = selectedIndex,
                    SelectedString = selectedString
                };
            }

            if (control is StackPanel panel)
                return panel.Tag as string ?? string.Empty;

            return control?.ToString() ?? string.Empty;
        }

        private void OnRunClicked(object sender, RoutedEventArgs e)
        {
            UserValues = CollectValues();
            WasCancelled = false;
            DialogResult = true;
            Close();
        }

        private void OnCancelClicked(object sender, RoutedEventArgs e)
        {
            WasCancelled = true;
            DialogResult = false;
            Close();
        }

        private static double ConvertToDouble(object value)
        {
            if (value is double doubleValue)
                return doubleValue;

            if (value is int intValue)
                return intValue;

            return double.TryParse(
                value?.ToString() ?? "0",
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var parsed)
                    ? parsed
                    : 0.0;
        }

        private static string FormatNumber(double value, bool isInteger)
        {
            return isInteger
                ? ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture)
                : value.ToString("G", CultureInfo.InvariantCulture);
        }
    }
}
