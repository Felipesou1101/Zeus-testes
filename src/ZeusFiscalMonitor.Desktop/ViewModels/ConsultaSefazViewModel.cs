using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZeusFiscalMonitor.Domain.Interfaces;
using ZeusFiscalMonitor.Domain.ValueObjects;

namespace ZeusFiscalMonitor.Desktop.ViewModels;

/// <summary>Aba 1 — consulta assíncrona do status do serviço NF-e na SEFAZ (homologação).</summary>
public partial class ConsultaSefazViewModel(ISefazSoapService sefaz) : ObservableObject
{
    public IReadOnlyList<string> Ufs => Uf.Siglas;

    [ObservableProperty] private string _ufSelecionada = "SE";
    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool? _sucesso;
    [ObservableProperty] private string? _cStat;
    [ObservableProperty] private string? _xMotivo;
    [ObservableProperty] private string? _detalhes;
    [ObservableProperty] private string? _erro;

    // AsyncRelayCommand: bloqueia novas execuções enquanto a anterior está em andamento
    // e a UI permanece responsiva (await libera o Dispatcher).
    [RelayCommand(IncludeCancelCommand = true)]
    private async Task ConsultarAsync(CancellationToken cancellationToken)
    {
        Carregando = true;
        Sucesso = null;
        CStat = XMotivo = Detalhes = Erro = null;

        try
        {
            var retorno = await sefaz.ConsultarStatusServicoAsync(new Uf(UfSelecionada), cancellationToken);

            Sucesso = retorno.Sucesso && retorno.CStat == "107"; // 107 = Serviço em Operação
            CStat = retorno.CStat;
            XMotivo = retorno.XMotivo;
            Erro = retorno.Erro;
            Detalhes = $"UF {retorno.Uf} · resposta em {retorno.TempoRespostaMs} ms" +
                       (retorno.TempoMedioSegundos is { } tMed ? $" · tempo médio SEFAZ {tMed}s" : string.Empty) +
                       (retorno.DataHoraRecebimento is { } dh ? $" · {dh:dd/MM/yyyy HH:mm:ss}" : string.Empty);
        }
        catch (OperationCanceledException)
        {
            Erro = "Consulta cancelada pelo usuário.";
            Detalhes = $"UF {UfSelecionada}";
        }
        finally
        {
            Carregando = false;
        }
    }
}
