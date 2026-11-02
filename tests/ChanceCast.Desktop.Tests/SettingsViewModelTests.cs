using ChanceCast.App.ViewModels;
using ChanceCast.Core.Models;

namespace ChanceCast.Desktop.Tests;

public class SettingsViewModelTests
{
    [Fact]
    public void Constructor_CopiesCurrentSettings()
    {
        var settings = AppSettings.CreateDefault() with { EventName = "Paraguas", DropDenominator = 500 };

        var vm = new SettingsViewModel(settings);

        Assert.Equal("Paraguas", vm.EventName);
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
    public void DenominatorPercentText_UpdatesWithDenominator()
    {
        var vm = new SettingsViewModel(AppSettings.CreateDefault());

        Assert.StartsWith("≈", vm.DenominatorPercentText);

        vm.DenominatorText = "2";
        Assert.Equal("≈ 50 %", vm.DenominatorPercentText.Replace(",", "."));

        vm.DenominatorText = "abc";
        Assert.Equal("—", vm.DenominatorPercentText);
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
    public void Constructor_CopiesThemeAsDarkFlag()
    {
        var dark = new SettingsViewModel(AppSettings.CreateDefault() with { Theme = AppTheme.Dark });
        var light = new SettingsViewModel(AppSettings.CreateDefault() with { Theme = AppTheme.Light });

        Assert.True(dark.IsDarkTheme);
        Assert.False(light.IsDarkTheme);
    }

    [Fact]
    public void TrySave_PersistsTheme()
    {
        var toLight = new SettingsViewModel(AppSettings.CreateDefault()) { IsDarkTheme = false };
        var toDark = new SettingsViewModel(AppSettings.CreateDefault() with { Theme = AppTheme.Light }) { IsDarkTheme = true };

        Assert.True(toLight.TrySave());
        Assert.True(toDark.TrySave());
        Assert.Equal(AppTheme.Light, toLight.Result!.Theme);
        Assert.Equal(AppTheme.Dark, toDark.Result!.Theme);
    }

    [Fact]
    public void Warning_MentionsRetroactiveRecalculation()
    {
        Assert.Contains("todos los intentos", SettingsViewModel.ProbabilityChangeWarning);
        Assert.Contains("no ser comparable", SettingsViewModel.ProbabilityChangeWarning);
    }
}
