using System.Windows;
using ContadorCofres.App.ViewModels;

namespace ContadorCofres.Desktop.Views;

/// <summary>Code-behind solo para cerrar la ventana a pedido del ViewModel.</summary>
public partial class MatchSummaryWindow : Window
{
    public MatchSummaryWindow(MatchSummaryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        viewModel.RequestClose += (_, _) => Close();
    }
}
