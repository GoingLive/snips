using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Snips.Core.Templates;

namespace Snips.App;

public partial class InteractivePromptWindow : Window
{
    private sealed record FieldControl(PromptField Field, FrameworkElement Input);

    private readonly List<FieldControl> _controls = [];

    public InteractivePromptWindow(IReadOnlyList<PromptField> fields)
    {
        InitializeComponent();

        foreach (var field in fields)
            _controls.Add(BuildRow(field));

        Loaded += (_, _) => _controls[0].Input.Focus();
    }

    public IReadOnlyDictionary<string, string> GetAnswers() =>
        _controls.ToDictionary(c => c.Field.Label, GetValue);

    private FieldControl BuildRow(PromptField field)
    {
        if (field is CheckPromptField check)
        {
            // The checkbox's own text serves as the label — no separate label line above it.
            var checkBox = new CheckBox { Content = check.Label, Margin = new Thickness(0, 0, 0, 12) };
            FieldsPanel.Children.Add(checkBox);
            return new FieldControl(field, checkBox);
        }

        var label = new TextBlock { Text = field.Label, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 4) };
        FieldsPanel.Children.Add(label);

        FrameworkElement input = field switch
        {
            TextPromptField text => new TextBox { Text = text.Default ?? string.Empty },
            MultilinePromptField => new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 80, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            ChoicePromptField choice => BuildComboBox(choice),
            DatePromptField => new DatePicker { SelectedDate = DateTime.Today },
            _ => new TextBox(),
        };
        input.Margin = new Thickness(0, 0, 0, 12);
        FieldsPanel.Children.Add(input);

        return new FieldControl(field, input);
    }

    private static ComboBox BuildComboBox(ChoicePromptField choice)
    {
        var comboBox = new ComboBox { ItemsSource = choice.Options };
        if (choice.Options.Count > 0)
            comboBox.SelectedIndex = 0;
        return comboBox;
    }

    private static string GetValue(FieldControl control) => control.Field switch
    {
        CheckPromptField check => ((CheckBox)control.Input).IsChecked == true ? check.CheckedValue : check.UncheckedValue,
        ChoicePromptField => (control.Input as ComboBox)?.SelectedItem as string ?? string.Empty,
        DatePromptField date => ((DatePicker)control.Input).SelectedDate is { } d ? d.ToString(date.Format) : string.Empty,
        _ => ((TextBox)control.Input).Text,
    };

    private void OkButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }
}
