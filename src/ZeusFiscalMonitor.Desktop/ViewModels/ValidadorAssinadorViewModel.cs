using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ZeusFiscalMonitor.Desktop.Services;
using ZeusFiscalMonitor.Domain.DTOs;
using ZeusFiscalMonitor.Domain.Interfaces;
using ZeusFiscalMonitor.Infraestrutura.Servicos;

namespace ZeusFiscalMonitor.Desktop.ViewModels;

/// <summary>Mensagem publicada quando um documento muda de estado (o histórico se atualiza sozinho).</summary>
public sealed record DocumentoProcessadoMensagem(string? ChaveAcesso);

/// <summary>Aba 2 — validação XSD (Strategy NFE/NFCE) e assinatura digital com certificado A1.</summary>
public partial class ValidadorAssinadorViewModel : ObservableObject
{
    private const string FiltroXml = "Arquivos XML (*.xml)|*.xml|Todos (*.*)|*.*";
    private const string SenhaCertificadoTeste = "zeus123";

    private readonly IProcessadorDocumentoFiscal _processador;
    private readonly IDialogService _dialogos;
    private readonly IMessenger _messenger;

    public ValidadorAssinadorViewModel(IProcessadorDocumentoFiscal processador, IDialogService dialogos, IMessenger messenger)
    {
        _processador = processador;
        _dialogos = dialogos;
        _messenger = messenger;
        TiposDocumento = processador.TiposSuportados.Order().ToArray();
        _tipoSelecionado = TiposDocumento.FirstOrDefault() ?? string.Empty;
    }

    public IReadOnlyList<string> TiposDocumento { get; }

    [ObservableProperty] private string _tipoSelecionado;
    [ObservableProperty] private string? _caminhoXml;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ValidarCommand), nameof(ValidarEAssinarCommand))]
    private string _conteudoXml = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ValidarEAssinarCommand))]
    private string _caminhoCertificado = string.Empty;

    [ObservableProperty] private string _senhaCertificado = string.Empty;

    [ObservableProperty] private bool _processando;
    [ObservableProperty] private bool? _sucesso;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private string? _status;
    [ObservableProperty] private string? _chaveAcesso;
    [ObservableProperty] private string? _erros;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SalvarXmlAssinadoCommand))]
    private string? _xmlAssinado;

    [RelayCommand]
    private void SelecionarXml()
    {
        var caminho = _dialogos.AbrirArquivo("Selecione o XML da NF-e/NFC-e", FiltroXml);
        if (caminho is null)
            return;

        CaminhoXml = caminho;
        ConteudoXml = File.ReadAllText(caminho);
        // Sugere o tipo pelo conteúdo para poupar um clique (pode ser trocado manualmente).
        TipoSelecionado = ConteudoXml.Contains("<mod>65</mod>") ? "NFCE" : "NFE";
    }

    [RelayCommand]
    private void SelecionarCertificado()
    {
        var caminho = _dialogos.AbrirArquivo("Selecione o certificado A1", "Certificado A1 (*.pfx;*.p12)|*.pfx;*.p12");
        if (caminho is not null)
            CaminhoCertificado = caminho;
    }

    /// <summary>Atalho de demonstração: gera um .pfx autoassinado para testar a assinatura sem um e-CNPJ real.</summary>
    [RelayCommand]
    private void GerarCertificadoTeste()
    {
        var pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ZeusFiscalMonitor");
        Directory.CreateDirectory(pasta);
        var caminho = Path.Combine(pasta, "certificado_teste.pfx");

        using var certificado = CertificadoTesteFactory.Criar();
        CertificadoTesteFactory.ExportarPfx(certificado, caminho, SenhaCertificadoTeste);

        CaminhoCertificado = caminho;
        SenhaCertificado = SenhaCertificadoTeste;
        Sucesso = null;
        Mensagem = $"Certificado de teste gerado em {caminho}. Não é válido na SEFAZ.";
    }

    private bool PodeValidar() => !string.IsNullOrWhiteSpace(ConteudoXml);
    private bool PodeAssinar() => PodeValidar() && !string.IsNullOrWhiteSpace(CaminhoCertificado);

    [RelayCommand(CanExecute = nameof(PodeValidar))]
    private Task ValidarAsync(CancellationToken ct) =>
        ExecutarAsync(() => _processador.ValidarAsync(TipoSelecionado, ConteudoXml, ct));

    [RelayCommand(CanExecute = nameof(PodeAssinar))]
    private Task ValidarEAssinarAsync(CancellationToken ct) =>
        ExecutarAsync(() => _processador.ValidarEAssinarAsync(
            TipoSelecionado, new RequisicaoAssinaturaDto(ConteudoXml, CaminhoCertificado, SenhaCertificado), ct));

    [RelayCommand(CanExecute = nameof(PodeSalvar))]
    private void SalvarXmlAssinado()
    {
        var caminho = _dialogos.SalvarArquivo("Salvar XML assinado", FiltroXml, $"{ChaveAcesso}-assinado.xml");
        if (caminho is not null)
            File.WriteAllText(caminho, XmlAssinado);
    }

    private bool PodeSalvar() => !string.IsNullOrEmpty(XmlAssinado);

    private async Task ExecutarAsync(Func<Task<ResultadoProcessamentoDto>> operacao)
    {
        Processando = true;
        Sucesso = null;
        Mensagem = Status = ChaveAcesso = Erros = XmlAssinado = null;

        try
        {
            var resultado = await operacao();
            Sucesso = resultado.Sucesso;
            Mensagem = resultado.Mensagem;
            Status = resultado.Status;
            ChaveAcesso = resultado.ChaveAcesso;
            XmlAssinado = resultado.XmlAssinado;
            Erros = resultado.Erros is { Count: > 0 } erros ? string.Join(Environment.NewLine, erros) : null;

            _messenger.Send(new DocumentoProcessadoMensagem(resultado.ChaveAcesso));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Normalmente: PostgreSQL fora do ar (docker compose up -d).
            Sucesso = false;
            Mensagem = "Falha ao persistir o documento. O PostgreSQL está em execução?";
            Erros = ex.GetBaseException().Message;
        }
        finally
        {
            Processando = false;
        }
    }
}
