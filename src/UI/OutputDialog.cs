using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Relay.Classes;

namespace Relay.UI
{
    /// <summary>
    /// Shows the values produced by the nodes a graph author marked as outputs, after the
    /// graph has run. Nested Dynamo lists are rendered as an expandable tree, and values
    /// that resolve to Revit elements can be selected in the model.
    /// </summary>
    internal sealed class OutputDialog : Window
    {
        private readonly IReadOnlyList<GraphOutputValue> _outputs;
        private readonly string _graphName;

        /// <summary>
        /// Element ids the user asked to select, or empty when they just closed the window.
        /// The caller performs the selection, because that needs the Revit UI document.
        /// </summary>
        public IReadOnlyList<long> ElementsToSelect { get; private set; } = Array.Empty<long>();

        private const string AccentHex     = "#0696D7";
        private const string WindowBgHex   = "#F9FAFB";
        private const string CardBgHex     = "#FFFFFF";
        private const string CardBorderHex = "#E5E7EB";
        private const string TextHex       = "#1F2937";
        private const string LabelHex      = "#374151";
        private const string MutedHex      = "#6B7280";
        private const string ValueHex      = "#0F766E";
        private const string WarningHex    = "#B45309";
        private const string InputBorderHex = "#D1D5DB";

        private static readonly Brush AccentBrush  = Freeze(AccentHex);
        private static readonly Brush TextBrush    = Freeze(TextHex);
        private static readonly Brush LabelBrush   = Freeze(LabelHex);
        private static readonly Brush MutedBrush   = Freeze(MutedHex);
        private static readonly Brush ValueBrush   = Freeze(ValueHex);
        private static readonly Brush WarningBrush = Freeze(WarningHex);

        private static Brush Freeze(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        public OutputDialog(string graphName, IReadOnlyList<GraphOutputValue> outputs)
        {
            _graphName = graphName;
            _outputs = outputs ?? Array.Empty<GraphOutputValue>();
            Resources.MergedDictionaries.Add(BuildStyles());
            BuildUI();
        }

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
    <Setter Property=""BorderThickness"" Value=""0""/>
    <Setter Property=""Height"" Value=""32""/>
    <Setter Property=""Padding"" Value=""16,0""/>
    <Setter Property=""Cursor"" Value=""Hand""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""Button"">
          <Border x:Name=""bd"" Background=""{TemplateBinding Background}""
                  BorderBrush=""{TemplateBinding BorderBrush}""
                  BorderThickness=""{TemplateBinding BorderThickness}"" CornerRadius=""6"">
            <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""
                              Margin=""{TemplateBinding Padding}""/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsMouseOver"" Value=""True"">
              <Setter TargetName=""bd"" Property=""Opacity"" Value=""0.88""/>
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
</ResourceDictionary>";

            xaml = xaml.Replace("{ACCENT}", AccentHex)
                       .Replace("{LABEL}", LabelHex)
                       .Replace("{INPUTBORDER}", InputBorderHex);
            return (ResourceDictionary)XamlReader.Parse(xaml);
        }

        private Style PrimaryButtonStyle => (Style)Resources["RelayPrimaryButton"];
        private Style SecondaryButtonStyle => (Style)Resources["RelaySecondaryButton"];

        private void BuildUI()
        {
            Title = $"Relay — {_graphName} results";
            Width = 520;
            MinWidth = 400;
            SizeToContent = SizeToContent.Height;
            MaxHeight = 760;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = false;
            Background = Freeze(WindowBgHex);
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            Foreground = TextBrush;

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new StackPanel { Margin = new Thickness(24, 18, 24, 12) };
            header.Children.Add(new TextBlock
            {
                Text = _graphName,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            });
            header.Children.Add(new TextBlock
            {
                Text = _outputs.Count == 1 ? "The graph ran and returned 1 output." : $"The graph ran and returned {_outputs.Count} outputs.",
                FontSize = 12,
                Foreground = MutedBrush,
                Margin = new Thickness(0, 3, 0, 0)
            });
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 540,
                Margin = new Thickness(24, 0, 24, 4),
                Focusable = false
            };
            // Right inset keeps the cards clear of the scroll bar when one appears
            var panel = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            BuildGroupedOutputs(panel);
            scroll.Content = panel;
            Grid.SetRow(scroll, 1);
            grid.Children.Add(scroll);

            var footer = new Border
            {
                Background = Freeze(CardBgHex),
                BorderBrush = Freeze(CardBorderHex),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(24, 12, 24, 12),
                Margin = new Thickness(0, 8, 0, 0)
            };

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var copyButton = new Button
            {
                Content = "Copy",
                MinWidth = 90,
                Style = SecondaryButtonStyle,
                Margin = new Thickness(0, 0, 8, 0),
                ToolTip = "Copy all results to the clipboard"
            };
            copyButton.Click += OnCopyClicked;
            buttons.Children.Add(copyButton);

            var selectableIds = AllElementIds();
            if (selectableIds.Count > 0)
            {
                var selectButton = new Button
                {
                    Content = selectableIds.Count == 1 ? "Select element" : $"Select {selectableIds.Count} elements",
                    MinWidth = 120,
                    Style = SecondaryButtonStyle,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                selectButton.Click += (_, _) =>
                {
                    ElementsToSelect = selectableIds;
                    DialogResult = true;
                    Close();
                };
                buttons.Children.Add(selectButton);
            }

            var closeButton = new Button
            {
                Content = "Close",
                MinWidth = 90,
                Style = PrimaryButtonStyle,
                IsDefault = true,
                IsCancel = true
            };
            closeButton.Click += (_, _) => { DialogResult = true; Close(); };
            buttons.Children.Add(closeButton);

            footer.Child = buttons;
            Grid.SetRow(footer, 2);
            grid.Children.Add(footer);

            Content = grid;
        }

        private void BuildGroupedOutputs(StackPanel panel)
        {
            var groupOrder = new List<string>();
            var groups = new Dictionary<string, List<GraphOutputValue>>(StringComparer.OrdinalIgnoreCase);

            foreach (var output in _outputs)
            {
                var key = output.GroupName ?? string.Empty;
                if (!groups.ContainsKey(key))
                {
                    groupOrder.Add(key);
                    groups[key] = new List<GraphOutputValue>();
                }
                groups[key].Add(output);
            }

            foreach (var key in groupOrder)
            {
                var content = new StackPanel();

                if (!string.IsNullOrEmpty(key))
                {
                    content.Children.Add(new TextBlock
                    {
                        Text = key,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = AccentBrush,
                        Margin = new Thickness(0, 0, 0, 4)
                    });
                }

                foreach (var output in groups[key])
                    content.Children.Add(BuildOutputRow(output));

                panel.Children.Add(new Border
                {
                    Background = Freeze(CardBgHex),
                    BorderBrush = Freeze(CardBorderHex),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16, 10, 16, 14),
                    Margin = new Thickness(0, 0, 0, 12),
                    Child = content
                });
            }
        }

        private UIElement BuildOutputRow(GraphOutputValue output)
        {
            var row = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };

            row.Children.Add(new TextBlock
            {
                Text = output.Name,
                FontWeight = FontWeights.SemiBold,
                Foreground = LabelBrush,
                Margin = new Thickness(0, 0, 0, 4),
                TextWrapping = TextWrapping.Wrap
            });

            if (output.Unavailable != null)
            {
                row.Children.Add(new TextBlock
                {
                    Text = output.Unavailable,
                    FontSize = 12,
                    FontStyle = FontStyles.Italic,
                    Foreground = WarningBrush,
                    TextWrapping = TextWrapping.Wrap
                });
                return row;
            }

            row.Children.Add(BuildValueElement(output.Value));

            if (!string.IsNullOrWhiteSpace(output.Description))
            {
                row.Children.Add(new TextBlock
                {
                    Text = output.Description,
                    FontSize = 11,
                    Foreground = MutedBrush,
                    Margin = new Thickness(0, 4, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            return row;
        }

        /// <summary>
        /// Renders a scalar as selectable text and a list as an expander, so a large result
        /// stays glanceable but can be drilled into.
        /// </summary>
        private UIElement BuildValueElement(OutputValueNode value)
        {
            if (!value.IsCollection)
                return BuildScalarElement(value);

            var items = new StackPanel { Margin = new Thickness(12, 4, 0, 0) };
            foreach (var child in value.Children)
                items.Children.Add(BuildValueElement(child));

            return new Expander
            {
                Header = value.Display,
                Content = items,
                // Short lists are more useful open; long ones would swamp the window.
                IsExpanded = value.Children.Count > 0 && value.Children.Count <= 12,
                Foreground = LabelBrush,
                Margin = new Thickness(0, 0, 0, 2)
            };
        }

        private UIElement BuildScalarElement(OutputValueNode value)
        {
            var text = new TextBox
            {
                Text = value.Display,
                IsReadOnly = true,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = ValueBrush,
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                TextWrapping = TextWrapping.Wrap,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 0, 2)
            };

            if (string.IsNullOrEmpty(value.TypeName) && !value.ElementId.HasValue)
                return text;

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(text);

            var hint = value.ElementId.HasValue
                ? $"  id {value.ElementId.Value}"
                : $"  {value.TypeName}";
            row.Children.Add(new TextBlock
            {
                Text = hint,
                FontSize = 11,
                Foreground = MutedBrush,
                VerticalAlignment = VerticalAlignment.Center
            });

            return row;
        }

        private List<long> AllElementIds() =>
            _outputs
                .Where(output => output.Unavailable is null)
                .SelectMany(output => output.Value.AllElementIds())
                .Distinct()
                .ToList();

        private void OnCopyClicked(object sender, RoutedEventArgs e)
        {
            var builder = new StringBuilder();
            foreach (var output in _outputs)
            {
                builder.AppendLine(output.Name);
                if (output.Unavailable != null)
                    builder.AppendLine($"  {output.Unavailable}");
                else
                    AppendValue(builder, output.Value, 1);
                builder.AppendLine();
            }

            try
            {
                Clipboard.SetText(builder.ToString());
            }
            catch (Exception ex)
            {
                // Another process can hold the clipboard open; not worth failing the run over
                System.Diagnostics.Trace.WriteLine($"[Relay] Could not copy results to the clipboard: {ex.Message}");
            }
        }

        private static void AppendValue(StringBuilder builder, OutputValueNode value, int depth)
        {
            var indent = new string(' ', depth * 2);
            if (value.IsCollection)
            {
                builder.AppendLine($"{indent}{value.Display}");
                foreach (var child in value.Children)
                    AppendValue(builder, child, depth + 1);
                return;
            }

            builder.AppendLine($"{indent}{value.Display}");
        }
    }
}
