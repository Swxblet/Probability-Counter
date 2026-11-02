using System.Windows;
using ContadorCofres.App.Abstractions;
using ContadorCofres.App.ViewModels;
using ContadorCofres.Core.Models;
using ContadorCofres.Core.Services;
using ContadorCofres.Desktop.Views;
using Microsoft.Win32;

namespace ContadorCofres.Desktop.Services;

/// <summary>Implementación WPF de los diálogos. Los ViewModels no dependen de WPF.</summary>
public sealed class DialogService : IDialogService
{
    private Window? _owner;

    public void Attach(Window owner) => _owner = owner;

    public Task<bool> ConfirmAsync(string title, string message, string confirmButtonText = "Aceptar")
    {
        // Por defecto se responde No en acciones destructivas.
        var defaultResult = confirmButtonText is "Descartar" or "Eliminar"
            ? MessageBoxResult.No
            : MessageBoxResult.Yes;

        var result = MessageBox.Show(
            _owner, message, title,
            MessageBoxButton.YesNo, MessageBoxImage.Question, defaultResult);
        return Task.FromResult(result == MessageBoxResult.Yes);
    }

    public void ShowError(string message)
        => MessageBox.Show(_owner, message, "Contador de Cofres", MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowInfo(string title, string message)
        => MessageBox.Show(_owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public Task<long?> PromptChestCountAsync(string title, string label, long current)
    {
        var window = new CountInputWindow(title, label, current) { Owner = _owner };
        var accepted = window.ShowDialog() == true;
        return Task.FromResult(accepted ? window.ResultValue : null);
    }

    public Task<string?> GetCsvSavePathAsync(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            FileName = suggestedFileName,
            DefaultExt = ".csv",
            AddExtension = true,
            Filter = "CSV (separado por comas)|*.csv",
        };
        var accepted = dialog.ShowDialog(_owner) == true;
        return Task.FromResult(accepted ? dialog.FileName : null);
    }

    public Task<bool> ShowSummaryAsync(MatchSummaryData data)
    {
        var averageText = ProbabilityFormatter.FormatAverage(data.AveragePerMatch);
        var viewModel = new MatchSummaryViewModel(data, averageText);
        var window = new MatchSummaryWindow(viewModel) { Owner = _owner };
        window.ShowDialog();
        return Task.FromResult(viewModel.StartNextRequested);
    }

    public Task<AppSettings?> ShowSettingsAsync(AppSettings current)
    {
        var viewModel = new SettingsViewModel(current);
        var window = new SettingsWindow(viewModel) { Owner = _owner };
        var accepted = window.ShowDialog() == true;
        return Task.FromResult(accepted ? viewModel.Result : null);
    }
}
