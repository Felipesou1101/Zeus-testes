using Microsoft.Win32;

namespace ZeusFiscalMonitor.Desktop.Services;

/// <summary>Abstrai diálogos de arquivo para que os ViewModels não dependam de WPF (testáveis).</summary>
public interface IDialogService
{
    string? AbrirArquivo(string titulo, string filtro);
    string? SalvarArquivo(string titulo, string filtro, string nomeSugerido);
}

public sealed class DialogService : IDialogService
{
    public string? AbrirArquivo(string titulo, string filtro)
    {
        var dialogo = new OpenFileDialog { Title = titulo, Filter = filtro, CheckFileExists = true };
        return dialogo.ShowDialog() == true ? dialogo.FileName : null;
    }

    public string? SalvarArquivo(string titulo, string filtro, string nomeSugerido)
    {
        var dialogo = new SaveFileDialog { Title = titulo, Filter = filtro, FileName = nomeSugerido };
        return dialogo.ShowDialog() == true ? dialogo.FileName : null;
    }
}
