using ChanceCast.App.Services;
using ChanceCast.App.ViewModels;
using ChanceCast.Core.Models;
using ChanceCast.Core.Services;

namespace ChanceCast.Desktop.Tests;

public class HistoryViewModelTests
{
    private static (HistoryViewModel Vm, MatchService Service, FakeDialogs Dialogs, AppStatus Status) Create(AppState? initial = null)
    {
        var (service, _, dialogs, _, status) = ViewModelFactory.Create(initial);
        var exporter = new CsvExportService(dialogs, Core.Abstractions.NullLogger.Instance);
        return (new HistoryViewModel(service, dialogs, exporter, status), service, dialogs, status);
    }

    private static AppState StateWith(params long[] attempts) => new()
    {
        Matches = attempts.Select((c, i) => new CompletedMatch(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(-3)).AddMinutes(30 * i),
            new DateTimeOffset(2026, 1, 1, 10, 20, 0, TimeSpan.FromHours(-3)).AddMinutes(30 * i),
            c)).ToList(),
    };

    [Fact]
    public void EmptyHistory_HasNoItemsAndDisabledActions()
    {
        var (vm, _, _, _) = Create();

        Assert.Empty(vm.Items);
        Assert.False(vm.EditSelectedCommand.CanExecute(null));
        Assert.False(vm.DeleteSelectedCommand.CanExecute(null));
    }

    [Fact]
    public void Items_AreOrderedMostRecentFirst()
    {
        var (vm, _, _, _) = Create(StateWith(5, 9, 3));

        Assert.Equal(3, vm.Items.Count);
        Assert.Equal(3, vm.Items[0].Attempts);
        Assert.Equal(9, vm.Items[1].Attempts);
        Assert.Equal(5, vm.Items[2].Attempts);
    }

    [Fact]
    public void Selection_EnablesEditAndDelete()
    {
        var (vm, _, _, _) = Create(StateWith(5));

        Assert.False(vm.EditSelectedCommand.CanExecute(null));
        vm.SelectedItem = vm.Items[0];

        Assert.True(vm.EditSelectedCommand.CanExecute(null));
        Assert.True(vm.DeleteSelectedCommand.CanExecute(null));
    }

    [Fact]
    public async Task Edit_Cancel_KeepsValue()
    {
        var (vm, service, dialogs, _) = Create(StateWith(5));
        vm.SelectedItem = vm.Items[0];
        dialogs.PromptResult = null;

        await vm.EditSelectedCommand.ExecuteAsync(null);

        Assert.Equal(5, service.Current.Matches[0].Attempts);
    }

    [Fact]
    public async Task Edit_AcceptsNewValue()
    {
        var (vm, service, dialogs, _) = Create(StateWith(5));
        vm.SelectedItem = vm.Items[0];
        dialogs.PromptResult = 42;

        await vm.EditSelectedCommand.ExecuteAsync(null);

        Assert.Equal(42, service.Current.Matches[0].Attempts);
        Assert.Equal(42, vm.Items[0].Attempts);
    }

    [Fact]
    public async Task Delete_Cancel_KeepsRecord()
    {
        var (vm, service, dialogs, _) = Create(StateWith(5));
        vm.SelectedItem = vm.Items[0];
        dialogs.ConfirmResult = false;

        await vm.DeleteSelectedCommand.ExecuteAsync(null);

        Assert.Single(service.Current.Matches);
    }

    [Fact]
    public async Task Delete_Confirm_RemovesRecord()
    {
        var (vm, service, _, _) = Create(StateWith(5, 9));
        vm.SelectedItem = vm.Items[0]; // el de 9 (más reciente)

        await vm.DeleteSelectedCommand.ExecuteAsync(null);

        Assert.Single(service.Current.Matches);
        Assert.Equal(5, service.Current.Matches[0].Attempts);
    }

    [Fact]
    public async Task Export_WithoutMatches_ShowsError()
    {
        var (vm, _, _, status) = Create();

        await vm.ExportCsvCommand.ExecuteAsync(null);

        Assert.True(status.IsError);
    }

    [Fact]
    public async Task Export_Cancelled_KeepsStatus()
    {
        var (vm, _, dialogs, status) = Create(StateWith(5));
        dialogs.SavePathResult = null;
        status.SetOk("Datos guardados");

        await vm.ExportCsvCommand.ExecuteAsync(null);

        Assert.False(status.IsError);
        Assert.Equal("Datos guardados", status.Message);
    }

    [Fact]
    public async Task Export_WritesFileWithBom()
    {
        var (vm, _, dialogs, _) = Create(StateWith(5));
        var path = Path.Combine(Path.GetTempPath(), "intentos-test-" + Guid.NewGuid().ToString("N") + ".csv");
        dialogs.SavePathResult = path;
        try
        {
            await vm.ExportCsvCommand.ExecuteAsync(null);

            var bytes = await File.ReadAllBytesAsync(path);
            Assert.Equal(0xEF, bytes[0]);
            Assert.Equal(0xBB, bytes[1]);
            Assert.Equal(0xBF, bytes[2]);
            Assert.Contains("Finalización", System.Text.Encoding.UTF8.GetString(bytes));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
