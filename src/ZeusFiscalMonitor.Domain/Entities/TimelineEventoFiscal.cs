using ZeusFiscalMonitor.Domain.Enums;
using ZeusFiscalMonitor.Domain.Exceptions;

namespace ZeusFiscalMonitor.Domain.Entities;

/// <summary>
/// Evento imutável da linha do tempo de um documento. Pode ser gerado pela aplicação
/// (etapas de negócio) ou pela trigger do PostgreSQL (alterações de dados).
/// </summary>
public sealed class TimelineEventoFiscal
{
    public static class Origens
    {
        public const string Aplicacao = "APLICACAO_CSHARP";
        public const string Banco = "BANCO_TRIGGER";
    }

    public static class Etapas
    {
        public const string ValidacaoXsd = "VALIDACAO_XSD";
        public const string Assinatura = "ASSINATURA";
    }

    public long EventoId { get; private set; }
    public Guid DocumentoId { get; private set; }
    public string OrigemEvento { get; private set; } = string.Empty;
    public string Etapa { get; private set; } = string.Empty;
    public string? StatusAnterior { get; private set; }
    public string? StatusAtual { get; private set; }
    /// <summary>JSON (coluna JSONB) com apenas os campos alterados: {"campo": [antes, depois]}.</summary>
    public string? AlteracoesCampos { get; private set; }
    public string? DetalheErro { get; private set; }
    public bool Sucesso { get; private set; }
    public DateTimeOffset OcorridoEm { get; private set; }

    private TimelineEventoFiscal() { }

    public static TimelineEventoFiscal DaAplicacao(
        Guid documentoId,
        string etapa,
        StatusValidacao statusAnterior,
        StatusValidacao statusAtual,
        bool sucesso,
        string? detalheErro = null,
        string? alteracoesCamposJson = null)
    {
        if (documentoId == Guid.Empty)
            throw new DomainException("O evento precisa estar vinculado a um documento.");
        if (string.IsNullOrWhiteSpace(etapa))
            throw new DomainException("A etapa do evento é obrigatória.");

        return new TimelineEventoFiscal
        {
            DocumentoId = documentoId,
            OrigemEvento = Origens.Aplicacao,
            Etapa = etapa,
            StatusAnterior = statusAnterior.ParaBanco(),
            StatusAtual = statusAtual.ParaBanco(),
            Sucesso = sucesso,
            DetalheErro = detalheErro,
            AlteracoesCampos = alteracoesCamposJson,
            OcorridoEm = DateTimeOffset.UtcNow
        };
    }
}
