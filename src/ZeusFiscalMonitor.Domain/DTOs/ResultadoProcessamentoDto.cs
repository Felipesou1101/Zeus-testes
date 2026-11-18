namespace ZeusFiscalMonitor.Domain.DTOs
{
    public sealed record ResultadoProcessamentoDto(
        bool Sucesso,
        string Mensagem,
        string? ChaveAcesso = null,
        string? Status = null,
        string? XmlAssinado = null,
        IReadOnlyList<string>? Erros = null);
}