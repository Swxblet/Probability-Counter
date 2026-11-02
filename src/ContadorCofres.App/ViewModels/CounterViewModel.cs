using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContadorCofres.App.Abstractions;
using ContadorCofres.App.Services;
using ContadorCofres.Core.Services;

namespace ContadorCofres.App.ViewModels;

/// <summary>
/// Contador de la partida activa. Toda regla se valida también en
/// <see cref="MatchService"/>; los CanExecute solo mejoran la interfaz.
/// </summary>
public sealed partial class CounterViewModel : ObservableObject
{
    private readonly MatchService _service;
    private readonly IDialogService _dialogs;
    private readonly AppStatus _status;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartMatchCommand))]
    [NotifyCanExecuteChangedFor(nameof(IncrementCommand))]
    [NotifyCanExecuteChangedFor(nameof(DecrementCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditCountCommand))]
    [NotifyCanExecuteChangedFor(nameof(FinishMatchCommand))]
    [NotifyCanExecuteChangedFor(nameof(DiscardMatchCommand))]
    private bool _hasActiveMatch;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DecrementCommand))]
    private long _chestCount;

    [ObservableProperty]
    private string _matchProbabilityText = "0 %";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartMatchCommand))]
    [NotifyCanExecuteChangedFor(nameof(IncrementCommand))]
    [NotifyCanExecuteChangedFor(nameof(DecrementCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditCountCommand))]
    [NotifyCanExecuteChangedFor(nameof(FinishMatchCommand))]
    [NotifyCanExecuteChangedFor(nameof(DiscardMatchCommand))]
    private bool _isBusy;

    public CounterViewModel(MatchService service, IDialogService dialogs, AppStatus status)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _status = status ?? throw new ArgumentNullException(nameof(status));
        _service.StateChanged += (_, _) => Refresh();
        Refresh();
    }

    private bool CanStartMatch() => !HasActiveMatch && !IsBusy;

    private bool CanCount() => HasActiveMatch && !IsBusy;

    private bool CanDecrement() => HasActiveMatch && ChestCount > 0 && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanStartMatch))]
    private async Task StartMatchAsync()
    {
        await RunAsync(async () =>
        {
            var result = await _service.StartMatchAsync();
            if (!result.Success)
                _status.SetError(result.ErrorMessage!);
        });
    }

    [RelayCommand(CanExecute = nameof(CanCount))]
    private async Task IncrementAsync()
    {
        await RunAsync(async () =>
        {
            var result = await _service.IncrementAsync();
            if (!result.Success)
                _status.SetError(result.ErrorMessage!);
        });
    }

    [RelayCommand(CanExecute = nameof(CanDecrement))]
    private async Task DecrementAsync()
    {
        await RunAsync(async () =>
        {
            var result = await _service.DecrementAsync();
            if (!result.Success)
                _status.SetError(result.ErrorMessage!);
        });
    }

    [RelayCommand(CanExecute = nameof(CanCount))]
    private async Task EditCountAsync()
    {
        var edited = await _dialogs.PromptChestCountAsync("Editar total", "Cofres de la partida:", ChestCount);
        if (edited is null)
            return; // cancelar no modifica

        await RunAsync(async () =>
        {
            var result = await _service.SetCountAsync(edited.Value);
            if (!result.Success)
                _status.SetError(result.ErrorMessage!);
        });
    }

    [RelayCommand(CanExecute = nameof(CanCount))]
    private async Task FinishMatchAsync()
    {
        var settings = _service.Current.Settings;

        if (ChestCount == 0)
        {
            var confirmZero = await _dialogs.ConfirmAsync(
                "Terminar partida",
                "La partida tiene cero cofres. Se guardará igual y contará para el promedio. ¿Terminar?",
                "Terminar");
            if (!confirmZero)
                return;
        }
        else if (settings.ConfirmFinish)
        {
            var confirm = await _dialogs.ConfirmAsync(
                "Terminar partida",
                $"Se guardará la partida con {ChestCount} {(ChestCount == 1 ? "cofre" : "cofres")} en el historial. ¿Continuar?",
                "Terminar");
            if (!confirm)
                return;
        }

        CompletedMatchSnapshot? finished = null;
        await RunAsync(async () =>
        {
            var result = await _service.FinishMatchAsync();
            if (!result.Success)
            {
                _status.SetError(result.ErrorMessage!);
                return;
            }

            finished = new CompletedMatchSnapshot(
                result.FinishedMatch!.ChestCount,
                ProbabilityCalculator.AtLeastOneSuccess(result.FinishedMatch.ChestCount, settings.DropDenominator));
        });

        if (finished is null)
            return;

        var snapshot = StatisticsCalculator.Calculate(_service.Current);
        var startNext = await _dialogs.ShowSummaryAsync(new MatchSummaryData(
            finished.ChestCount,
            finished.Probability,
            snapshot.TotalChests,
            snapshot.AverageChestsPerMatch,
            settings.ItemName,
            settings.DropDenominator));

        if (startNext)
            await StartMatchAsync();
    }

    [RelayCommand(CanExecute = nameof(CanCount))]
    private async Task DiscardMatchAsync()
    {
        var confirm = await _dialogs.ConfirmAsync(
            "Descartar partida",
            "Se eliminará la partida activa sin crear historial. Esta acción no se puede deshacer. ¿Descartar?",
            "Descartar");
        if (!confirm)
            return;

        await RunAsync(async () =>
        {
            var result = await _service.DiscardMatchAsync();
            if (!result.Success)
                _status.SetError(result.ErrorMessage!);
        });
    }

    private async Task RunAsync(Func<Task> operation)
    {
        if (IsBusy)
            return;
        IsBusy = true;
        try
        {
            await operation();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Refresh()
    {
        var state = _service.Current;
        HasActiveMatch = state.ActiveMatch is not null;
        ChestCount = state.ActiveMatch?.ChestCount ?? 0;
        MatchProbabilityText = ProbabilityFormatter.FormatPercent(
            ProbabilityCalculator.AtLeastOneSuccess(ChestCount, state.Settings.DropDenominator),
            CultureInfo.CurrentCulture);
    }

    private sealed record CompletedMatchSnapshot(long ChestCount, double Probability);
}
