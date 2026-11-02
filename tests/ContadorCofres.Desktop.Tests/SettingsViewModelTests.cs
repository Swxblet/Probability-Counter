using ContadorCofres.App.ViewModels;
using ContadorCofres.Core.Models;

namespace ContadorCofres.Desktop.Tests;

public class SettingsViewModelTests
{
    [Fact]
    public void Constructor_CopiesCurrentSettings()
    {
        var settings = AppSettings.CreateDefault() with { ItemName = "Paraguas", DropDenominator = 500 };

        var vm = new SettingsViewModel(settings);

        Assert.Equal("Paraguas", vm.ItemName);
        Assert.Equal("500", vm.DenominatorText);
        Assert.Null(vm.Result);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("")]
    [InlineData("   ")]
    public void TrySave_InvalidDenominator_Fails(string text)
    {
        var vm = new SettingsViewModel(AppSettings.CreateDefault()) { DenominatorText = text };

        Assert.False(vm.TrySave());
        Assert.NotNull(vm.ValidationError);
        Assert.Null(vm.Result);
    }

    [Fact]
    public void TrySave_ValidDenominator_Succeeds()
    {
        var vm = new SettingsViewModel(AppSettings.CreateDefault()) { DenominatorText = "1000000" };

        Assert.True(vm.TrySave());
        Assert.Null(vm.ValidationError);
        Assert.Equal(1_000_000, vm.Result!.DropDenominator);
    }

    [Fact]
    public void TrySave_DuplicateHotkeys_Fails()
    {
        var vm = new SettingsViewModel(AppSettings.CreateDefault())
        {
            IncrementHotkey = new HotkeyBinding("F5", HotkeyModifiers.None),
            DecrementHotkey = new HotkeyBinding("F5", HotkeyModifiers.None),
        };

        Assert.False(vm.TrySave());
        Assert.Contains("mismo atajo", vm.ValidationError);
    }

    [Fact]
    public void TrySave_SameKeyDifferentModifiers_IsAllowed()
    {
        var vm = new SettingsViewModel(AppSettings.CreateDefault())
        {
            IncrementHotkey = new HotkeyBinding("F5", HotkeyModifiers.None),
            DecrementHotkey = new HotkeyBinding("F5", HotkeyModifiers.Shift),
        };

        Assert.True(vm.TrySave());
    }

    [Fact]
    public void TrySave_NullHotkey_IsAllowed()
    {
        var vm = new SettingsViewModel(AppSettings.CreateDefault()) { DecrementHotkey = null };

        Assert.True(vm.TrySave());
        Assert.Null(vm.Result!.DecrementHotkey);
    }

    [Fact]
    public void DisplayNames_UpdateOnChange()
    {
        var vm = new SettingsViewModel(AppSettings.CreateDefault());

        Assert.Equal("OemPlus", vm.IncrementDisplay);
        vm.IncrementHotkey = null;

        Assert.Equal("Sin asignar", vm.IncrementDisplay);
        vm.IncrementHotkey = new HotkeyBinding("F5", HotkeyModifiers.Control | HotkeyModifiers.Shift);

        Assert.Equal("Ctrl + Shift + F5", vm.IncrementDisplay);
    }

    [Fact]
    public void Warning_MentionsRetroactiveRecalculation()
    {
        Assert.Contains("todos los cofres", SettingsViewModel.ProbabilityChangeWarning);
        Assert.Contains("no ser comparable", SettingsViewModel.ProbabilityChangeWarning);
    }
}
