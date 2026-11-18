namespace ZeusFiscalMonitor.Domain.DTOs
{
    public sealed record HistoricoOperacaoDto(
        long EventoId,
        Guid DocumentoId,
        string ChaveAcesso,
        string OrigemEvento,
        string Etapa,
        string? StatusAnterior,
        string? StatusAtual,
        bool Sucesso,
        string? DetalheErro,
        string? AlteracoesCampos,
        DateTimeOffset OcorridoEm);
}
