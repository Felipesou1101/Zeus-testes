namespace ZeusFiscalMonitor.Domain.Exceptions;

/// <summary>Violação de regra de negócio/invariante do domínio.</summary>
public sealed class DomainException(string mensagem) : Exception(mensagem);
