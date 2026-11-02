using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChanceCast.App.Abstractions;
using ChanceCast.App.Services;
using ChanceCast.Core.Services;

namespace ChanceCast.App.ViewModels;

/// <summary>Fila del historial para el DataGrid (solo lectura; se edita por diálogo).</summary>
public sealed class HistoryItemViewModel
{
    public required Guid Id { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required DateTimeOffset FinishedAt { get; init; }
    public required long Attempts { get; init; }

    public string LocalStartText => StartedAt.ToLocalTime().ToString("g");
    public string LocalDateText => StartedAt.ToLocalTime().ToString("d");
    public string LocalTimeText => StartedAt.ToLocalTime().ToString("t");
}

/// <summary>
/// Historial de sesiones: orden inicial por fecha descendente, edición y
/// eliminación por diálogo explícito, exportación a CSV.
/// </summary>
public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly MatchService _service;
    private readonly IDialogService _dialogs;
    private readonly CsvExportService _exporter;
    private readonly AppStatus _status;

    [ObservableProperty]
    private List<HistoryItemViewModel> _items = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
    private HistoryItemViewModel? _selectedItem;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportCsvCommand))]
    private bool _isBusy;

    public HistoryViewModel(MatchService service, IDialogService dialogs, CsvExportService exporter, AppStatus status)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        _status = status ?? throw new ArgumentNullException(nameof(status));
        _service.StateChanged += (_, _) => Refresh();
        Refresh();
    }

    private bool CanEditOrDelete() => SelectedItem is not null && !IsBusy;

    private bool CanExport() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    private async Task EditSelectedAsync()
    {
        if (SelectedItem is null)
            return;

        var edited = await _dialogs.PromptAttemptsAsync(
            "Editar registro", "Intentos de la sesión:", SelectedItem.Attempts);
        if (edited is null)
            return; // cancelar no modifica

        IsBusy = true;
        try
        {
            var result = await _service.EditHistoryAsync(SelectedItem.Id, edited.Value);
            if (!result.Success)
                _status.SetError(result.ErrorMessage!);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedItem is null)
            return;

        var confirm = await _dialogs.ConfirmAsync(
            "Eliminar registro",
            $"Se eliminará del historial la sesión con {SelectedItem.Attempts} {(SelectedItem.Attempts == 1 ? "intento" : "intentos")}. Esta acción no se puede deshacer. ¿Eliminar?",
            "Eliminar");
        if (!confirm)
            return;

        IsBusy = true;
        try
        {
            var result = await _service.DeleteHistoryAsync(SelectedItem.Id);
            if (!result.Success)
                _status.SetError(result.ErrorMessage!);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportCsvAsync()
    {
        IsBusy = true;
        try
        {
            var outcome = await _exporter.ExportAsync(_service.Current.Matches);
            if (outcome.Cancelled)
                return; // cancelar no es un error
            if (!outcome.Success)
                _status.SetError(outcome.ErrorMessage!);
            else
                _status.SetOk("Historial exportado correctamente.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Refresh()
    {
        var selectedId = SelectedItem?.Id;
        Items = _service.Current.Matches
            .OrderByDescending(m => m.StartedAt)
            .Select(m => new HistoryItemViewModel
            {
                Id = m.Id,
                StartedAt = m.StartedAt,
                FinishedAt = m.FinishedAt,
                Attempts = m.Attempts,
            })
            .ToList();
        SelectedItem = selectedId is null ? null : Items.FirstOrDefault(i => i.Id == selectedId);
    }
}
