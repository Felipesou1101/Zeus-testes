namespace ZeusFiscalMonitor.Desktop.ViewModels;

/// <summary>Agrega os ViewModels de cada aba (composição em vez de um "God ViewModel").</summary>
public sealed class MainViewModel(
    ConsultaSefazViewModel consultaSefaz,
    ValidadorAssinadorViewModel validador,
    HistoricoViewModel historico)
{
    public ConsultaSefazViewModel ConsultaSefaz { get; } = consultaSefaz;
    public ValidadorAssinadorViewModel Validador { get; } = validador;
    public HistoricoViewModel Historico { get; } = historico;
}
