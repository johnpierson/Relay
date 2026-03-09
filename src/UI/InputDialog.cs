using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Relay.Classes;

namespace Relay.UI
{
    /// <summary>
    /// A dynamically-generated WPF dialog that presents a form for every input node
    /// that is marked <c>IsSetAsInput = true</c> in a Dynamo graph.
    /// </summary>
    internal sealed class InputDialog : Window
    {
        private readonly DynamoGraphInputs _graphInputs;

        /// <summary>
        /// Pre-filled values to restore when the dialog is reopened after a Revit pick.
        /// Keyed by node ID.
        /// </summary>
        private readonly Dictionary<string, object> _prefilledValues;

        /// <summary>
        /// Control instances keyed by the corresponding node ID, used to collect
        /// values when the user clicks "Run Graph".
        /// </summary>
        private readonly Dictionary<string, FrameworkElement> _inputControls =
            new Dictionary<string, FrameworkElement>();

        // -----------------------------------------------------------------------
        // Public results
        // -----------------------------------------------------------------------

        /// <summary>
        /// <c>true</c> when the user dismissed the dialog without clicking "Run Graph".
        /// </summary>
        public bool WasCancelled { get; private set; } = true;

        /// <summary>
        /// <c>true</c> when the user clicked "Select in Revit" on a selection node,
        /// requesting the dialog to close so a Revit <c>PickObject</c> can be performed.
        /// </summary>
        public bool NeedsPick { get; private set; }

        /// <summary>
        /// Node ID of the selection input for which a pick is needed.
        /// Valid only when <see cref="NeedsPick"/> is <c>true</c>.
        /// </summary>
        public string PickInputId { get; private set; }

        /// <summary>
        /// <c>true</c> when the picker input allows selecting multiple elements.
        /// </summary>
        public bool PickMultiple { get; private set; }

        /// <summary>
        /// Snapshot of values entered so far, saved when the user clicks "Select in Revit"
        /// so that they can be restored when the dialog is shown again.
        /// </summary>
        public Dictionary<string, object> PartialValues { get; private set; } =
            new Dictionary<string, object>();

        /// <summary>
        /// Mapping from node ID to the value entered/selected by the user.
        /// Populated only when <see cref="WasCancelled"/> is <c>false</c>.
        /// </summary>
        public Dictionary<string, object> UserValues { get; private set; } =
            new Dictionary<string, object>();

        // -----------------------------------------------------------------------
        // Constructor
        // -----------------------------------------------------------------------

        /// <param name="graphInputs">The parsed input definitions from the graph.</param>
        /// <param name="prefilledValues">
        /// Optional values to pre-populate controls with (used when reopening after a pick).
        /// </param>
        public InputDialog(DynamoGraphInputs graphInputs,
                           Dictionary<string, object> prefilledValues = null)
        {
            _graphInputs = graphInputs;
            _prefilledValues = prefilledValues ?? new Dictionary<string, object>();
            BuildUI();
        }

        // -----------------------------------------------------------------------
        // UI construction
        // -----------------------------------------------------------------------

        private void BuildUI()
        {
            Title = $"Relay - {_graphInputs.GraphName}";
            Width = 420;
            MinWidth = 320;
            SizeToContent = SizeToContent.Height;
            MaxHeight = 640;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;

            var mainGrid = new Grid { Margin = new Thickness(20) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header label
            var header = new TextBlock
            {
                Text = "Configure Graph Inputs",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 15)
            };
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // Scrollable area for input controls
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 440
            };

            var inputPanel = new StackPanel();
            foreach (var input in _graphInputs.Inputs)
                inputPanel.Children.Add(BuildInputGroup(input));

            scrollViewer.Content = inputPanel;
            Grid.SetRow(scrollViewer, 1);
            mainGrid.Children.Add(scrollViewer);

            // Button row
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 15, 0, 0)
            };

            var runButton = new Button
            {
                Content = "Run Graph",
                Width = 100,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0)
            };
            runButton.Click += OnRunClicked;

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 100,
                Height = 30
            };
            cancelButton.Click += OnCancelClicked;

            buttonPanel.Children.Add(runButton);
            buttonPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonPanel, 2);
            mainGrid.Children.Add(buttonPanel);

            Content = mainGrid;
        }

        private UIElement BuildInputGroup(DynamoInputDefinition input)
        {
            var group = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };

            // Input label
            var label = new TextBlock
            {
                Text = input.Name,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            };
            group.Children.Add(label);

            // The actual input control
            var control = BuildControl(input);
            _inputControls[input.Id] = control;
            group.Children.Add(control);

            // Optional description text
            if (!string.IsNullOrWhiteSpace(input.Description))
            {
                group.Children.Add(new TextBlock
                {
                    Text = input.Description,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    Margin = new Thickness(0, 2, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            return group;
        }

        private FrameworkElement BuildControl(DynamoInputDefinition input)
        {
            switch (input.DataType)
            {
                case DynamoInputType.Boolean:
                    return BuildCheckBox(input);
                case DynamoInputType.Number:
                    return BuildNumericControl(input, isInteger: false);
                case DynamoInputType.Integer:
                    return BuildNumericControl(input, isInteger: true);
                case DynamoInputType.Dropdown:
                    return BuildDropdownControl(input);
                case DynamoInputType.Selection:
                    return BuildSelectionControl(input);
                case DynamoInputType.String:
                default:
                    return BuildTextBox(input);
            }
        }

        private CheckBox BuildCheckBox(DynamoInputDefinition input)
        {
            // Honour any pre-filled value from a previous dialog instance
            bool isChecked = _prefilledValues.TryGetValue(input.Id, out var pv) && pv is bool pvBool
                ? pvBool
                : input.DefaultValue is bool b && b;

            return new CheckBox
            {
                IsChecked = isChecked,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private TextBox BuildTextBox(DynamoInputDefinition input)
        {
            var text = _prefilledValues.TryGetValue(input.Id, out var pv)
                ? pv?.ToString() ?? string.Empty
                : input.DefaultValue?.ToString() ?? string.Empty;

            return new TextBox
            {
                Text = text,
                Padding = new Thickness(4),
                Height = 28,
                VerticalContentAlignment = VerticalAlignment.Center
            };
        }

        /// <summary>
        /// Builds a <see cref="ComboBox"/> for a <see cref="DynamoInputType.Dropdown"/> node.
        /// When the node's items list is empty (e.g. a dynamic Revit dropdown whose items
        /// could not be populated at parse time) the control degrades to an editable combo
        /// showing the currently stored selection string.
        /// </summary>
        private ComboBox BuildDropdownControl(DynamoInputDefinition input)
        {
            var combo = new ComboBox
            {
                Height = 28,
                VerticalContentAlignment = VerticalAlignment.Center,
                IsEditable = input.Items.Count == 0 // editable fallback when no item list
            };

            if (input.Items.Count > 0)
            {
                foreach (var item in input.Items)
                    combo.Items.Add(item);

                // Restore from pre-filled values or definition
                int selectedIndex = -1;
                if (_prefilledValues.TryGetValue(input.Id, out var pv) && pv is DropdownValue pvDv)
                    selectedIndex = pvDv.SelectedIndex;
                else
                    selectedIndex = input.SelectedIndex;

                if (selectedIndex >= 0 && selectedIndex < combo.Items.Count)
                    combo.SelectedIndex = selectedIndex;
                else if (!string.IsNullOrEmpty(input.SelectedString))
                    combo.SelectedItem = input.SelectedString;
            }
            else
            {
                // Editable ComboBox with just the current selection string as a hint
                var currentString = _prefilledValues.TryGetValue(input.Id, out var pv) && pv is DropdownValue pvDv
                    ? pvDv.SelectedString
                    : input.SelectedString ?? string.Empty;

                combo.Text = currentString;

                if (!string.IsNullOrEmpty(currentString))
                    combo.Items.Add(currentString);
            }

            return combo;
        }

        /// <summary>
        /// Builds a control for a <see cref="DynamoInputType.Selection"/> (element picker) node.
        /// Shows the currently selected element (if any) and a button that, when clicked,
        /// closes the dialog and signals <see cref="NeedsPick"/> so that the caller can
        /// invoke <c>UIDocument.Selection.PickObject</c> and then reopen the dialog.
        /// </summary>
        private FrameworkElement BuildSelectionControl(DynamoInputDefinition input)
        {
            // Current element identifier (either from an earlier pick or the stored identifier)
            var currentId = _prefilledValues.TryGetValue(input.Id, out var pv) && pv is string pvStr
                ? pvStr
                : input.SelectionIdentifier ?? string.Empty;

            var displayText = string.IsNullOrEmpty(currentId)
                ? "No element selected"
                : $"Element ID: {currentId}";

            var infoBox = new TextBox
            {
                Text = displayText,
                IsReadOnly = true,
                Padding = new Thickness(4),
                Height = 28,
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF5, 0xF5, 0xF5)),
                Margin = new Thickness(0, 0, 0, 4)
            };

            var buttonText = input.IsMultipleSelection
                ? "Select Elements in Revit…"
                : "Select Element in Revit…";

            var pickButton = new Button
            {
                Content = buttonText,
                Height = 28,
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(8, 0, 8, 0)
            };

            var panel = new StackPanel();
            panel.Children.Add(infoBox);
            panel.Children.Add(pickButton);

            // Tag stores the raw element ID for CollectValues
            panel.Tag = currentId;

            pickButton.Click += (_, e) =>
            {
                // Snapshot all current values before closing so they can be restored
                // when the dialog is reopened after the pick completes.
                PartialValues = CollectValues();
                NeedsPick     = true;
                PickInputId   = input.Id;
                PickMultiple  = input.IsMultipleSelection;
                // WasCancelled = false: the user has not abandoned the workflow — they want
                // to continue by selecting a Revit element.  The caller distinguishes a genuine
                // cancel (WasCancelled = true) from a pick request (NeedsPick = true).
                WasCancelled  = false;
                DialogResult  = false; // Close the dialog; caller checks NeedsPick
                Close();
            };

            return panel;
        }

        /// <summary>
        /// Returns a Slider + TextBox grid when min/max are known, or a plain TextBox otherwise.
        /// The <see cref="FrameworkElement.Tag"/> on the grid holds the Slider so that
        /// <see cref="CollectValues"/> can retrieve the current value.
        /// </summary>
        private FrameworkElement BuildNumericControl(DynamoInputDefinition input, bool isInteger)
        {
            // Honour any pre-filled value
            double currentValue = _prefilledValues.TryGetValue(input.Id, out var pv)
                ? ConvertToDouble(pv)
                : ConvertToDouble(input.DefaultValue);

            if (!input.MinValue.HasValue || !input.MaxValue.HasValue)
            {
                // No range info – fall back to a plain text box
                return new TextBox
                {
                    Text = FormatNumber(currentValue, isInteger),
                    Padding = new Thickness(4),
                    Height = 28,
                    VerticalContentAlignment = VerticalAlignment.Center
                };
            }

            double min  = input.MinValue.Value;
            double max  = input.MaxValue.Value;
            double step = input.StepValue ?? (isInteger ? 1.0 : 0.1);

            // Clamp the default value to the valid range
            currentValue = Math.Max(min, Math.Min(max, currentValue));

            var slider = new Slider
            {
                Minimum = min,
                Maximum = max,
                SmallChange = step,
                LargeChange = Math.Max(step * 10, (max - min) / 10.0),
                Value = currentValue,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var textBox = new TextBox
            {
                Text = FormatNumber(currentValue, isInteger),
                Width = 70,
                Padding = new Thickness(4),
                Height = 28,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            // Keep slider and text box in sync
            slider.ValueChanged += (_, e) =>
            {
                textBox.Text = FormatNumber(slider.Value, isInteger);
            };

            textBox.LostFocus += (sender, e) =>
            {
                if (double.TryParse(textBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                    slider.Value = Math.Max(min, Math.Min(max, parsed));
                else
                    textBox.Text = FormatNumber(slider.Value, isInteger);
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(slider, 0);
            Grid.SetColumn(textBox, 1);
            grid.Children.Add(slider);
            grid.Children.Add(textBox);

            // Store slider reference in Tag for value retrieval
            grid.Tag = slider;
            return grid;
        }

        // -----------------------------------------------------------------------
        // Value collection
        // -----------------------------------------------------------------------

        private Dictionary<string, object> CollectValues()
        {
            var values = new Dictionary<string, object>();

            foreach (var kvp in _inputControls)
            {
                var id = kvp.Key;
                var control = kvp.Value;
                var definition = _graphInputs.Inputs.Find(i => i.Id == id);
                values[id] = ExtractValue(control, definition);
            }

            return values;
        }

        private static object ExtractValue(FrameworkElement control, DynamoInputDefinition definition)
        {
            // CheckBox → bool
            if (control is CheckBox cb)
                return cb.IsChecked == true;

            // Plain TextBox → parse according to type
            if (control is TextBox tb)
            {
                var text = tb.Text ?? string.Empty;
                if (definition?.DataType == DynamoInputType.Number &&
                    double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                    return d;
                if (definition?.DataType == DynamoInputType.Integer &&
                    int.TryParse(text, out var i))
                    return i;
                return text;
            }

            // Slider+TextBox grid → read from the Slider stored in Tag
            if (control is Grid grid && grid.Tag is Slider slider)
            {
                return definition?.DataType == DynamoInputType.Integer
                    ? (object)(int)Math.Round(slider.Value)
                    : slider.Value;
            }

            // ComboBox → DropdownValue (index + string)
            if (control is ComboBox combo)
            {
                return new DropdownValue
                {
                    SelectedIndex = combo.SelectedIndex,
                    SelectedString = combo.SelectedItem as string ?? combo.Text ?? string.Empty
                };
            }

            // Selection panel (StackPanel with Tag = element ID string)
            if (control is StackPanel panel)
                return panel.Tag as string ?? string.Empty;

            return control?.ToString() ?? string.Empty;
        }

        // -----------------------------------------------------------------------
        // Button handlers
        // -----------------------------------------------------------------------

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

        // -----------------------------------------------------------------------
        // Utility helpers
        // -----------------------------------------------------------------------

        private static double ConvertToDouble(object value)
        {
            if (value is double d) return d;
            if (value is int i) return i;
            if (value is float f) return f;
            double.TryParse(value?.ToString() ?? "0",
                NumberStyles.Any, CultureInfo.InvariantCulture, out var result);
            return result;
        }

        private static string FormatNumber(double value, bool isInteger)
        {
            return isInteger
                ? ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture)
                : value.ToString("G", CultureInfo.InvariantCulture);
        }
    }
}
