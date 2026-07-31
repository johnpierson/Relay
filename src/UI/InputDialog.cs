using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using Relay.Classes;
using Relay.Utilities;

namespace Relay.UI
{
    /// <summary>
    /// A dynamically-generated WPF dialog that presents a form for every input node
    /// that is marked <c>IsSetAsInput = true</c> in a Dynamo graph.
    /// Inputs that share a Dynamo group (annotation) are rendered together inside a
    /// titled card; ungrouped inputs are rendered in a card of their own.
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

        /// <summary>
        /// Inline validation message labels keyed by node ID, shown under the
        /// offending control when validation fails.
        /// </summary>
        private readonly Dictionary<string, TextBlock> _errorLabels =
            new Dictionary<string, TextBlock>();

        // -----------------------------------------------------------------------
        // Palette
        // -----------------------------------------------------------------------

        private const string AccentHex     = "#0696D7"; // Autodesk/Dynamo blue
        private const string WindowBgHex   = "#F9FAFB";
        private const string CardBgHex     = "#FFFFFF";
        private const string CardBorderHex = "#E5E7EB";
        private const string TextHex       = "#1F2937";
        private const string LabelHex      = "#374151";
        private const string MutedHex      = "#6B7280";
        private const string ErrorHex      = "#DC2626";
        private const string InputBorderHex = "#D1D5DB";

        private static readonly Brush AccentBrush     = Freeze(AccentHex);
        private static readonly Brush TextBrush       = Freeze(TextHex);
        private static readonly Brush LabelBrush      = Freeze(LabelHex);
        private static readonly Brush MutedBrush      = Freeze(MutedHex);
        private static readonly Brush ErrorBrush      = Freeze(ErrorHex);
        private static readonly Brush InputBorderBrush = Freeze(InputBorderHex);

        private static Brush Freeze(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

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
            Resources.MergedDictionaries.Add(BuildStyles());
            BuildUI();
        }

        // -----------------------------------------------------------------------
        // Styles (rounded text boxes and buttons, parsed once per dialog)
        // -----------------------------------------------------------------------

        private static ResourceDictionary BuildStyles()
        {
            var xaml = @"
<ResourceDictionary
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">

  <Style x:Key=""RelayPrimaryButton"" TargetType=""Button"">
    <Setter Property=""Background"" Value=""{ACCENT}""/>
    <Setter Property=""Foreground"" Value=""White""/>
    <Setter Property=""FontWeight"" Value=""SemiBold""/>
    <Setter Property=""BorderBrush"" Value=""Transparent""/>
    <Setter Property=""BorderThickness"" Value=""0""/>
    <Setter Property=""Height"" Value=""32""/>
    <Setter Property=""Padding"" Value=""16,0""/>
    <Setter Property=""Cursor"" Value=""Hand""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""Button"">
          <Border x:Name=""bd""
                  Background=""{TemplateBinding Background}""
                  BorderBrush=""{TemplateBinding BorderBrush}""
                  BorderThickness=""{TemplateBinding BorderThickness}""
                  CornerRadius=""6"">
            <ContentPresenter HorizontalAlignment=""Center""
                              VerticalAlignment=""Center""
                              Margin=""{TemplateBinding Padding}""/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsMouseOver"" Value=""True"">
              <Setter TargetName=""bd"" Property=""Opacity"" Value=""0.88""/>
            </Trigger>
            <Trigger Property=""IsPressed"" Value=""True"">
              <Setter TargetName=""bd"" Property=""Opacity"" Value=""0.75""/>
            </Trigger>
            <Trigger Property=""IsEnabled"" Value=""False"">
              <Setter TargetName=""bd"" Property=""Opacity"" Value=""0.45""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key=""RelaySecondaryButton"" TargetType=""Button"" BasedOn=""{StaticResource RelayPrimaryButton}"">
    <Setter Property=""Background"" Value=""#FFFFFF""/>
    <Setter Property=""Foreground"" Value=""{LABEL}""/>
    <Setter Property=""FontWeight"" Value=""Normal""/>
    <Setter Property=""BorderBrush"" Value=""{INPUTBORDER}""/>
    <Setter Property=""BorderThickness"" Value=""1""/>
  </Style>

  <Style x:Key=""RelayTextBox"" TargetType=""TextBox"">
    <Setter Property=""Height"" Value=""32""/>
    <Setter Property=""Padding"" Value=""8,0""/>
    <Setter Property=""VerticalContentAlignment"" Value=""Center""/>
    <Setter Property=""Background"" Value=""#FFFFFF""/>
    <Setter Property=""Foreground"" Value=""{TEXT}""/>
    <Setter Property=""BorderBrush"" Value=""{INPUTBORDER}""/>
    <Setter Property=""BorderThickness"" Value=""1""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""TextBox"">
          <Border x:Name=""bd""
                  Background=""{TemplateBinding Background}""
                  BorderBrush=""{TemplateBinding BorderBrush}""
                  BorderThickness=""{TemplateBinding BorderThickness}""
                  CornerRadius=""6"">
            <ScrollViewer x:Name=""PART_ContentHost""
                          Margin=""{TemplateBinding Padding}""
                          VerticalAlignment=""Center""/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsKeyboardFocusWithin"" Value=""True"">
              <Setter TargetName=""bd"" Property=""BorderBrush"" Value=""{ACCENT}""/>
            </Trigger>
            <Trigger Property=""IsReadOnly"" Value=""True"">
              <Setter TargetName=""bd"" Property=""Background"" Value=""#F3F4F6""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
</ResourceDictionary>";

            xaml = xaml.Replace("{ACCENT}", AccentHex)
                       .Replace("{TEXT}", TextHex)
                       .Replace("{LABEL}", LabelHex)
                       .Replace("{INPUTBORDER}", InputBorderHex);

            return (ResourceDictionary)XamlReader.Parse(xaml);
        }

        private Style PrimaryButtonStyle   => (Style)Resources["RelayPrimaryButton"];
        private Style SecondaryButtonStyle => (Style)Resources["RelaySecondaryButton"];
        private Style TextBoxStyle         => (Style)Resources["RelayTextBox"];

        // -----------------------------------------------------------------------
        // UI construction
        // -----------------------------------------------------------------------

        private void BuildUI()
        {
            Title = $"Relay — {_graphInputs.GraphName}";
            Width = 480;
            MinWidth = 380;
            SizeToContent = SizeToContent.Height;
            MaxHeight = 760;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Background = Freeze(WindowBgHex);
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            Foreground = TextBrush;

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // --- Header ---
            var header = new StackPanel { Margin = new Thickness(24, 18, 24, 12) };
            header.Children.Add(new TextBlock
            {
                Text = _graphInputs.GraphName,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            });
            header.Children.Add(new TextBlock
            {
                Text = "Review the inputs below, then run the graph.",
                FontSize = 12,
                Foreground = MutedBrush,
                Margin = new Thickness(0, 3, 0, 0)
            });
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // --- Scrollable input cards ---
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 540,
                Margin = new Thickness(24, 0, 24, 4),
                Focusable = false
            };

            // Right inset keeps the cards clear of the scroll bar when one appears
            var inputPanel = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            BuildGroupedInputs(inputPanel);

            scrollViewer.Content = inputPanel;
            Grid.SetRow(scrollViewer, 1);
            mainGrid.Children.Add(scrollViewer);

            // --- Footer with action buttons ---
            var footer = new Border
            {
                Background = Freeze(CardBgHex),
                BorderBrush = Freeze(CardBorderHex),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(24, 12, 24, 12),
                Margin = new Thickness(0, 8, 0, 0)
            };

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                MinWidth = 90,
                Style = SecondaryButtonStyle,
                Margin = new Thickness(0, 0, 8, 0),
                IsCancel = true
            };
            cancelButton.Click += OnCancelClicked;

            var runButton = new Button
            {
                Content = "Run Graph",
                MinWidth = 110,
                Style = PrimaryButtonStyle,
                IsDefault = true
            };
            runButton.Click += OnRunClicked;

            buttonPanel.Children.Add(cancelButton);
            buttonPanel.Children.Add(runButton);
            footer.Child = buttonPanel;
            Grid.SetRow(footer, 2);
            mainGrid.Children.Add(footer);

            Content = mainGrid;
        }

        /// <summary>
        /// Populates <paramref name="panel"/> with input cards.  Inputs that share a
        /// Dynamo group name are rendered together inside a card titled with the group
        /// name; consecutive ungrouped inputs share an untitled card.
        /// Ordering is preserved from <see cref="DynamoGraphInputs.Inputs"/>.
        /// </summary>
        private void BuildGroupedInputs(StackPanel panel)
        {
            // Build an ordered list of (groupKey, inputs) preserving NodeViews order
            var groupOrder = new List<string>();
            var groupMap   = new Dictionary<string, List<DynamoInputDefinition>>(StringComparer.OrdinalIgnoreCase);

            foreach (var input in _graphInputs.Inputs)
            {
                var key = input.GroupName ?? string.Empty;
                if (!groupMap.ContainsKey(key))
                {
                    groupOrder.Add(key);
                    groupMap[key] = new List<DynamoInputDefinition>();
                }
                groupMap[key].Add(input);
            }

            foreach (var key in groupOrder)
            {
                var inputsInGroup = groupMap[key];

                var cardContent = new StackPanel();

                if (!string.IsNullOrEmpty(key))
                {
                    cardContent.Children.Add(new TextBlock
                    {
                        Text = key,
                        FontWeight = FontWeights.SemiBold,
                        FontSize = 13,
                        Foreground = AccentBrush,
                        Margin = new Thickness(0, 0, 0, 4)
                    });
                }

                foreach (var input in inputsInGroup)
                    cardContent.Children.Add(BuildInputRow(input));

                panel.Children.Add(new Border
                {
                    Background = Freeze(CardBgHex),
                    BorderBrush = Freeze(CardBorderHex),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16, 10, 16, 14),
                    Margin = new Thickness(0, 0, 0, 12),
                    Child = cardContent
                });
            }
        }

        private UIElement BuildInputRow(DynamoInputDefinition input)
        {
            var row = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };

            // Input label
            row.Children.Add(new TextBlock
            {
                Text = input.Name,
                FontWeight = FontWeights.SemiBold,
                Foreground = LabelBrush,
                Margin = new Thickness(0, 0, 0, 4),
                TextWrapping = TextWrapping.Wrap
            });

            if (input.DataType == DynamoInputType.Unknown)
            {
                // Unsupported node kind: inform the user instead of guessing a control.
                // The value already saved in the graph is used unchanged at run time.
                row.Children.Add(new TextBlock
                {
                    Text = "This input type isn't supported yet — the value saved in the graph will be used.",
                    FontSize = 11,
                    FontStyle = FontStyles.Italic,
                    Foreground = MutedBrush,
                    TextWrapping = TextWrapping.Wrap
                });
                return row;
            }

            // The actual input control
            var control = BuildControl(input);
            _inputControls[input.Id] = control;
            row.Children.Add(control);

            // Inline validation message (hidden until validation fails)
            var errorLabel = new TextBlock
            {
                FontSize = 11,
                Foreground = ErrorBrush,
                Margin = new Thickness(0, 3, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed
            };
            _errorLabels[input.Id] = errorLabel;
            row.Children.Add(errorLabel);

            // Optional description text
            if (!string.IsNullOrWhiteSpace(input.Description))
            {
                row.Children.Add(new TextBlock
                {
                    Text = input.Description,
                    FontSize = 11,
                    Foreground = MutedBrush,
                    Margin = new Thickness(0, 3, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            return row;
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
                case DynamoInputType.FilePath:
                    return BuildPathControl(input, isDirectory: false);
                case DynamoInputType.DirectoryPath:
                    return BuildPathControl(input, isDirectory: true);
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

            var checkBox = new CheckBox
            {
                IsChecked = isChecked,
                Content = isChecked ? "True" : "False",
                Foreground = TextBrush,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 2)
            };

            checkBox.Checked   += (_, _) => checkBox.Content = "True";
            checkBox.Unchecked += (_, _) => checkBox.Content = "False";

            return checkBox;
        }

        private TextBox BuildTextBox(DynamoInputDefinition input)
        {
            var text = _prefilledValues.TryGetValue(input.Id, out var pv)
                ? pv?.ToString() ?? string.Empty
                : input.DefaultValue?.ToString() ?? string.Empty;

            return new TextBox
            {
                Text = text,
                Style = TextBoxStyle
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
                Height = 32,
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
            // Resolve current identifier and display text from prefilledValues or the
            // graph definition.  prefilledValues may hold a SelectionValue (set by
            // DynamoMethods after a successful pick) or a plain string (initial state).
            string currentId   = string.Empty;
            string displayText = string.Empty;

            if (_prefilledValues.TryGetValue(input.Id, out var pv))
            {
                if (pv is SelectionValue sv)
                {
                    currentId   = sv.Identifier;
                    displayText = sv.DisplayText;
                }
                else if (pv is string str)
                {
                    currentId   = str;
                    displayText = string.IsNullOrEmpty(str) ? string.Empty : $"ID: {str}";
                }
            }

            if (string.IsNullOrEmpty(currentId))
            {
                currentId   = input.SelectionIdentifier ?? string.Empty;
                displayText = string.IsNullOrEmpty(currentId)
                    ? "No element selected"
                    : $"Previously: {currentId}";
            }

            if (string.IsNullOrEmpty(displayText))
                displayText = "No element selected";

            var infoBox = new TextBox
            {
                Text = displayText,
                IsReadOnly = true,
                Style = TextBoxStyle,
                Margin = new Thickness(0, 0, 0, 6)
            };

            var buttonText = input.IsMultipleSelection
                ? "Select Elements in Revit…"
                : "Select Element in Revit…";

            var pickButton = new Button
            {
                Content = buttonText,
                Style = SecondaryButtonStyle,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var panel = new StackPanel();
            panel.Children.Add(infoBox);
            panel.Children.Add(pickButton);

            // Tag stores the UniqueId string for CollectValues / write-back
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
        /// Builds a text box + Browse… button for File Path / Directory Path nodes.
        /// The grid's <see cref="FrameworkElement.Tag"/> holds the TextBox so that
        /// <see cref="CollectValues"/> can retrieve the current value.
        /// </summary>
        private FrameworkElement BuildPathControl(DynamoInputDefinition input, bool isDirectory)
        {
            var text = _prefilledValues.TryGetValue(input.Id, out var pv)
                ? pv?.ToString() ?? string.Empty
                : input.DefaultValue?.ToString() ?? string.Empty;

            var pathBox = new TextBox
            {
                Text = text,
                Style = TextBoxStyle,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var browseButton = new Button
            {
                Content = "Browse…",
                Style = SecondaryButtonStyle,
                MinWidth = 84
            };

            browseButton.Click += (_, e) =>
            {
                if (isDirectory)
                {
                    var dlg = new Microsoft.Win32.OpenFolderDialog();
                    if (Directory.Exists(pathBox.Text))
                        dlg.InitialDirectory = pathBox.Text;
                    if (dlg.ShowDialog(this) == true)
                        pathBox.Text = dlg.FolderName;
                }
                else
                {
                    var dlg = new Microsoft.Win32.OpenFileDialog { CheckFileExists = false };
                    try
                    {
                        if (File.Exists(pathBox.Text))
                        {
                            dlg.InitialDirectory = Path.GetDirectoryName(pathBox.Text);
                            dlg.FileName = Path.GetFileName(pathBox.Text);
                        }
                    }
                    catch
                    {
                        // Invalid stored path — open the dialog at its default location
                    }
                    if (dlg.ShowDialog(this) == true)
                        pathBox.Text = dlg.FileName;
                }
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(pathBox, 0);
            Grid.SetColumn(browseButton, 1);
            grid.Children.Add(pathBox);
            grid.Children.Add(browseButton);

            // Store the TextBox reference in Tag for value retrieval
            grid.Tag = pathBox;
            return grid;
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
                    Style = TextBoxStyle
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
                Margin = new Thickness(0, 0, 10, 0)
            };

            if (isInteger)
            {
                slider.TickFrequency = step;
                slider.IsSnapToTickEnabled = true;
            }

            var textBox = new TextBox
            {
                Text = FormatNumber(currentValue, isInteger),
                Width = 74,
                Style = TextBoxStyle,
                TextAlignment = TextAlignment.Center
            };

            // Keep slider and text box in sync
            slider.ValueChanged += (_, e) =>
            {
                textBox.Text = FormatNumber(slider.Value, isInteger);
            };

            textBox.LostFocus += (sender, e) =>
            {
                if (TryParseDouble(textBox.Text, out var parsed))
                {
                    if (isInteger) parsed = Math.Round(parsed);
                    slider.Value = Math.Max(min, Math.Min(max, parsed));
                    textBox.Text = FormatNumber(slider.Value, isInteger);
                }
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
                    TryParseDouble(text, out var d))
                    return d;
                if (definition?.DataType == DynamoInputType.Integer &&
                    int.TryParse(text, out var i))
                    return i;
                return text;
            }

            if (control is Grid grid)
            {
                // Slider+TextBox grid → read from the Slider stored in Tag
                if (grid.Tag is Slider slider)
                {
                    return definition?.DataType == DynamoInputType.Integer
                        ? (object)(int)Math.Round(slider.Value)
                        : slider.Value;
                }

                // Path control grid → read from the TextBox stored in Tag
                if (grid.Tag is TextBox pathBox)
                    return pathBox.Text ?? string.Empty;
            }

            // ComboBox → DropdownValue (index + serialisation string)
            if (control is ComboBox combo)
            {
                var idx = combo.SelectedIndex;

                // If the definition has parallel serialisation values (e.g. OST names for
                // Categories), use those instead of the display string so that the correct
                // value is written back to Nodes[].SelectedString.
                string selectedStr;
                if (definition != null &&
                    definition.ItemValues.Count > 0 &&
                    idx >= 0 &&
                    idx < definition.ItemValues.Count)
                    selectedStr = definition.ItemValues[idx];
                else
                    selectedStr = combo.SelectedItem as string ?? combo.Text ?? string.Empty;

                return new DropdownValue
                {
                    SelectedIndex  = idx,
                    SelectedString = selectedStr
                };
            }

            // Selection panel (StackPanel with Tag = element ID string)
            if (control is StackPanel panel)
                return panel.Tag as string ?? string.Empty;

            return control?.ToString() ?? string.Empty;
        }

        // -----------------------------------------------------------------------
        // Validation
        // -----------------------------------------------------------------------

        /// <summary>
        /// Validates every supported input control, surfacing inline error messages.
        /// Returns <c>true</c> when all values are acceptable.
        /// </summary>
        private bool ValidateInputs()
        {
            bool allValid = true;
            FrameworkElement firstInvalid = null;

            foreach (var kvp in _inputControls)
            {
                var id = kvp.Key;
                var control = kvp.Value;
                var definition = _graphInputs.Inputs.Find(i => i.Id == id);
                var error = GetValidationError(control, definition);

                SetErrorState(id, control, error);

                if (error != null)
                {
                    allValid = false;
                    firstInvalid ??= control;
                }
            }

            firstInvalid?.Focus();
            return allValid;
        }

        private static string GetValidationError(FrameworkElement control, DynamoInputDefinition definition)
        {
            if (definition == null) return null;

            // Plain numeric text boxes (sliders are always in range by construction)
            if (control is TextBox tb &&
                (definition.DataType == DynamoInputType.Number ||
                 definition.DataType == DynamoInputType.Integer))
            {
                var text = (tb.Text ?? string.Empty).Trim();
                if (text.Length == 0)
                    return "Enter a value.";

                if (definition.DataType == DynamoInputType.Integer)
                {
                    if (!int.TryParse(text, out var intVal))
                        return "Enter a whole number.";
                    return RangeError(intVal, definition);
                }

                if (!TryParseDouble(text, out var dblVal))
                    return "Enter a valid number.";
                return RangeError(dblVal, definition);
            }

            // Path controls: a non-empty path must exist on disk
            if (control is Grid grid && grid.Tag is TextBox pathBox)
            {
                var path = (pathBox.Text ?? string.Empty).Trim();
                if (path.Length == 0)
                    return null; // empty keeps the graph's stored behavior

                if (definition.DataType == DynamoInputType.FilePath && !File.Exists(path))
                    return "File not found.";
                if (definition.DataType == DynamoInputType.DirectoryPath && !Directory.Exists(path))
                    return "Folder not found.";
            }

            // Dropdowns with a populated list require a selection
            if (control is ComboBox combo && !combo.IsEditable && combo.Items.Count > 0 &&
                combo.SelectedIndex < 0)
                return "Choose an option.";

            return null;
        }

        private static string RangeError(double value, DynamoInputDefinition definition)
        {
            if (definition.MinValue.HasValue && value < definition.MinValue.Value ||
                definition.MaxValue.HasValue && value > definition.MaxValue.Value)
            {
                var min = definition.MinValue?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-∞";
                var max = definition.MaxValue?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "∞";
                return $"Enter a value between {min} and {max}.";
            }
            return null;
        }

        private void SetErrorState(string id, FrameworkElement control, string error)
        {
            if (_errorLabels.TryGetValue(id, out var label))
            {
                label.Text = error ?? string.Empty;
                label.Visibility = error == null ? Visibility.Collapsed : Visibility.Visible;
            }

            // Highlight the offending text box, if there is one
            var box = control as TextBox ?? (control as Grid)?.Tag as TextBox;
            if (box != null)
                box.BorderBrush = error == null ? InputBorderBrush : ErrorBrush;
        }

        // -----------------------------------------------------------------------
        // Button handlers
        // -----------------------------------------------------------------------

        private void OnRunClicked(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs())
                return;

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

        private static bool TryParseDouble(string text, out double value) =>
            NumericInput.TryParseUserNumber(text, out value);

        private static double ConvertToDouble(object value) => NumericInput.FromGraphValue(value);

        private static string FormatNumber(double value, bool isInteger) =>
            NumericInput.Format(value, isInteger);
    }
}
