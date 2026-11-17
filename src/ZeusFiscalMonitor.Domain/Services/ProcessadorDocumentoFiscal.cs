using System.Text.Json;
using ZeusFiscalMonitor.Domain.DTOs;
using ZeusFiscalMonitor.Domain.Entities;
using ZeusFiscalMonitor.Domain.Enums;
using ZeusFiscalMonitor.Domain.Exceptions;
using ZeusFiscalMonitor.Domain.Interfaces;
using ZeusFiscalMonitor.Domain.ValueObjects;

namespace ZeusFiscalMonitor.Domain.Services;

/// <summary>
/// Orquestra o ciclo do documento: persistir → validar (XSD) → assinar, registrando
/// cada etapa na timeline. Depende apenas de abstrações, por isso é testável com Moq.
/// </summary>
public sealed class ProcessadorDocumentoFiscal : IProcessadorDocumentoFiscal
{
    private readonly IReadOnlyDictionary<string, IValidadorDocumento> _validadores;
    private readonly IAssinadorXmlService _assinador;
    private readonly IFiscalRepository _repositorio;

    public ProcessadorDocumentoFiscal(
        IEnumerable<IValidadorDocumento> validadores,
        IAssinadorXmlService assinador,
        IFiscalRepository repositorio)
    {
        // Strategy resolvida por dicionário: sem if/else/switch por tipo de documento.
        _validadores = validadores.ToDictionary(v => v.TipoDocumento, StringComparer.OrdinalIgnoreCase);
        _assinador = assinador;
        _repositorio = repositorio;
    }

    public IReadOnlyCollection<string> TiposSuportados => _validadores.Keys.ToArray();

    public async Task<ResultadoProcessamentoDto> ValidarAsync(string tipoDocumento, string xml, CancellationToken cancellationToken = default)
    {
        var (documento, resultado) = await ValidarInternoAsync(tipoDocumento, xml, cancellationToken);
        return resultado ?? Resumo(documento!, "Documento validado com sucesso.");
    }

    public async Task<ResultadoProcessamentoDto> ValidarEAssinarAsync(string tipoDocumento, RequisicaoAssinaturaDto requisicao, CancellationToken cancellationToken = default)
    {
        var (documento, falhaValidacao) = await ValidarInternoAsync(tipoDocumento, requisicao.Xml, cancellationToken);
        if (falhaValidacao is not null)
            return falhaValidacao;

        var assinatura = _assinador.Assinar(requisicao with { Xml = documento!.XmlOriginal });
        var anterior = documento.StatusValidacao;

        if (assinatura.Sucesso)
            documento.RegistrarAssinatura(assinatura.XmlAssinado!);
        else
            documento.RegistrarErro($"Falha na assinatura: {assinatura.Erro}");

        await _repositorio.AtualizarAsync(documento, cancellationToken);
        await RegistrarEtapaAsync(documento, TimelineEventoFiscal.Etapas.Assinatura, anterior, assinatura.Sucesso, cancellationToken);

        return assinatura.Sucesso
            ? Resumo(documento, $"Documento validado e assinado ({assinatura.TitularCertificado}).")
            : Resumo(documento, documento.MensagemErro!, sucesso: false, erros: [assinatura.Erro!]);
    }

    private async Task<(DocumentoFiscal? Documento, ResultadoProcessamentoDto? Falha)> ValidarInternoAsync(
        string tipoDocumento, string xml, CancellationToken cancellationToken)
    {
        if (!_validadores.TryGetValue(tipoDocumento, out var validador))
            return (null, new ResultadoProcessamentoDto(false, $"Tipo de documento não suportado: '{tipoDocumento}'."));

        ChaveAcesso chave;
        try
        {
            chave = LeitorChaveAcessoXml.Extrair(xml);
        }
        catch (DomainException ex)
        {
            // Sem chave válida não há como identificar/persistir o documento.
            return (null, new ResultadoProcessamentoDto(false, ex.Message, Erros: [ex.Message]));
        }

        var documento = await ObterOuCriarAsync(chave, validador.Modelo, xml, cancellationToken);
        var anterior = documento.StatusValidacao;
        var validacao = validador.Validar(xml);

        if (validacao.Valido)
            documento.MarcarComoValidado();
        else
            documento.RegistrarErro(string.Join(Environment.NewLine, validacao.Erros));

        await _repositorio.AtualizarAsync(documento, cancellationToken);
        await RegistrarEtapaAsync(documento, TimelineEventoFiscal.Etapas.ValidacaoXsd, anterior, validacao.Valido, cancellationToken);

        return validacao.Valido
            ? (documento, null)
            : (documento, Resumo(documento, "XML inválido segundo o schema.", sucesso: false, erros: validacao.Erros));
    }

    private async Task<DocumentoFiscal> ObterOuCriarAsync(ChaveAcesso chave, ModeloDocumento modelo, string xml, CancellationToken cancellationToken)
    {
        var existente = await _repositorio.ObterPorChaveAsync(chave, cancellationToken);
        if (existente is not null)
        {
            existente.SubstituirXml(xml, modelo);
            await _repositorio.AtualizarAsync(existente, cancellationToken);
            return existente;
        }

        var novo = new DocumentoFiscal(chave, modelo, Uf.DoCodigoIbge(chave.CodigoUf), xml);
        await _repositorio.AdicionarAsync(novo, cancellationToken);
        return novo;
    }

    private Task RegistrarEtapaAsync(DocumentoFiscal documento, string etapa, StatusValidacao anterior, bool sucesso, CancellationToken cancellationToken)
    {
        var diff = JsonSerializer.Serialize(new Dictionary<string, string[]>
        {
            ["status_validacao"] = [anterior.ParaBanco(), documento.StatusValidacao.ParaBanco()]
        });

        var evento = TimelineEventoFiscal.DaAplicacao(
            documento.Id, etapa, anterior, documento.StatusValidacao, sucesso,
            sucesso ? null : documento.MensagemErro, diff);

        return _repositorio.RegistrarEventoAsync(evento, cancellationToken);
    }

    private static ResultadoProcessamentoDto Resumo(DocumentoFiscal documento, string mensagem, bool sucesso = true, IReadOnlyList<string>? erros = null) =>
        new(sucesso, mensagem, documento.ChaveAcesso.Valor, documento.StatusValidacao.ParaBanco(), documento.XmlAssinado, erros);
}
