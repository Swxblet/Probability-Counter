using CommunityToolkit.Mvvm.ComponentModel;

namespace ChanceCast.App.Services;

/// <summary>
/// Mensaje de estado compartido ("Datos guardados" o error visible).
/// Los ViewModels lo actualizan; MainWindow lo muestra.
/// </summary>
public sealed partial class AppStatus : ObservableObject
{
    [ObservableProperty]
    private string _message = "Datos guardados";

    [ObservableProperty]
    private bool _isError;

    public void SetOk(string message = "Datos guardados")
    {
        Message = message;
        IsError = false;
    }

    public void SetError(string message)
    {
        Message = message;
        IsError = true;
    }
}
