using ZeusFiscalMonitor.Domain.Enums;
using ZeusFiscalMonitor.Domain.Exceptions;
using ZeusFiscalMonitor.Domain.ValueObjects;

namespace ZeusFiscalMonitor.Domain.Entities;

/// <summary>
/// Agregado raiz. O estado só muda através de métodos de transição,
/// que garantem o fluxo PENDENTE → VALIDADO → ASSINADO (ou ERRO).
/// </summary>
public sealed class DocumentoFiscal
{
    public Guid Id { get; private set; }
    public ChaveAcesso ChaveAcesso { get; private set; } = null!;
    public ModeloDocumento Modelo { get; private set; }
    public Uf UfEmitente { get; private set; } = null!;
    public string XmlOriginal { get; private set; } = string.Empty;
    public string? XmlAssinado { get; private set; }
    public StatusValidacao StatusValidacao { get; private set; }
    public string? MensagemErro { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }
    public DateTimeOffset AtualizadoEm { get; private set; }

    // Exigido pelo EF Core para materialização.
    private DocumentoFiscal() { }

    public DocumentoFiscal(ChaveAcesso chaveAcesso, ModeloDocumento modelo, Uf ufEmitente, string xmlOriginal)
    {
        ArgumentNullException.ThrowIfNull(chaveAcesso);
        ArgumentNullException.ThrowIfNull(ufEmitente);
        if (string.IsNullOrWhiteSpace(xmlOriginal))
            throw new DomainException("O XML original é obrigatório.");
        if (!Enum.IsDefined(modelo))
            throw new DomainException($"Modelo de documento não suportado: {modelo}.");

        Id = Guid.NewGuid();
        ChaveAcesso = chaveAcesso;
        Modelo = modelo;
        UfEmitente = ufEmitente;
        XmlOriginal = xmlOriginal;
        StatusValidacao = StatusValidacao.Pendente;
        CriadoEm = AtualizadoEm = DateTimeOffset.UtcNow;
    }

    /// <summary>Reprocessamento: um novo XML para a mesma chave reinicia o ciclo.</summary>
    public void SubstituirXml(string novoXml, ModeloDocumento modelo)
    {
        if (string.IsNullOrWhiteSpace(novoXml))
            throw new DomainException("O XML original é obrigatório.");

        XmlOriginal = novoXml;
        Modelo = modelo;
        XmlAssinado = null;
        MensagemErro = null;
        StatusValidacao = StatusValidacao.Pendente;
        Tocar();
    }

    public void MarcarComoValidado()
    {
        if (StatusValidacao != StatusValidacao.Pendente)
            throw new DomainException($"Só é possível validar um documento PENDENTE (atual: {StatusValidacao.ParaBanco()}).");

        StatusValidacao = StatusValidacao.Validado;
        MensagemErro = null;
        Tocar();
    }

    public void RegistrarAssinatura(string xmlAssinado)
    {
        if (StatusValidacao != StatusValidacao.Validado)
            throw new DomainException($"Só é possível assinar um documento VALIDADO (atual: {StatusValidacao.ParaBanco()}).");
        if (string.IsNullOrWhiteSpace(xmlAssinado))
            throw new DomainException("O XML assinado é obrigatório.");

        XmlAssinado = xmlAssinado;
        StatusValidacao = StatusValidacao.Assinado;
        Tocar();
    }

    public void RegistrarErro(string mensagem)
    {
        if (string.IsNullOrWhiteSpace(mensagem))
            throw new DomainException("A mensagem de erro é obrigatória.");

        MensagemErro = mensagem;
        StatusValidacao = StatusValidacao.Erro;
        Tocar();
    }

    private void Tocar() => AtualizadoEm = DateTimeOffset.UtcNow;
}
