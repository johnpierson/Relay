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
        /// Mapping from node ID to the value entered/selected by the user.
        /// Populated only when <see cref="WasCancelled"/> is <c>false</c>.
        /// </summary>
        public Dictionary<string, object> UserValues { get; private set; } =
            new Dictionary<string, object>();

        // -----------------------------------------------------------------------
        // Constructor
        // -----------------------------------------------------------------------

        public InputDialog(DynamoGraphInputs graphInputs)
        {
            _graphInputs = graphInputs;
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
                case DynamoInputType.String:
                default:
                    return BuildTextBox(input);
            }
        }

        private static CheckBox BuildCheckBox(DynamoInputDefinition input)
        {
            return new CheckBox
            {
                IsChecked = input.DefaultValue is bool b && b,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static TextBox BuildTextBox(DynamoInputDefinition input)
        {
            return new TextBox
            {
                Text = input.DefaultValue?.ToString() ?? string.Empty,
                Padding = new Thickness(4),
                Height = 28,
                VerticalContentAlignment = VerticalAlignment.Center
            };
        }

        /// <summary>
        /// Returns a Slider + TextBox grid when min/max are known, or a plain TextBox otherwise.
        /// The <see cref="FrameworkElement.Tag"/> on the grid holds the Slider so that
        /// <see cref="CollectValues"/> can retrieve the current value.
        /// </summary>
        private static FrameworkElement BuildNumericControl(DynamoInputDefinition input, bool isInteger)
        {
            double currentValue = ConvertToDouble(input.DefaultValue);

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

            textBox.LostFocus += (_, __) =>
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
