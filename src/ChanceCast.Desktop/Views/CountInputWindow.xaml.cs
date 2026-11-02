using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace ChanceCast.Desktop.Views;

/// <summary>
/// Diálogo para ingresar el total de intentos. Solo acepta enteros no negativos;
/// cancelar no modifica nada.
/// </summary>
public partial class CountInputWindow : Window
{
    public CountInputWindow(string title, string label, long current)
    {
        InitializeComponent();
        Title = title;
        LabelText.Text = label;
        ValueBox.Text = current.ToString();
        ValueBox.SelectAll();
        Loaded += (_, _) => ValueBox.Focus();
    }

    /// <summary>Valor validado tras aceptar; null si se canceló.</summary>
    public long? ResultValue { get; private set; }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        var text = ValueBox.Text.Trim();
        if (!long.TryParse(text, out var value) || value < 0)
        {
            ErrorText.Text = "Ingresá un número entero mayor o igual a 0.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        ResultValue = value;
        DialogResult = true;
    }

    private void ValueBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // Solo dígitos en la edición directa; la validación final está en Accept_Click.
        e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
    }
}
