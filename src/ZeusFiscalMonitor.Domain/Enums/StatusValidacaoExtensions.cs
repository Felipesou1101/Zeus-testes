namespace ZeusFiscalMonitor.Domain.Enums
{
    public static class StatusValidacaoExtensions
    {
        /// <summary>Representação textual persistida ("PENDENTE", "VALIDADO"...).</summary>
        public static string ParaBanco(this StatusValidacao status) => status.ToString().ToUpperInvariant();

        public static StatusValidacao DoBanco(string valor) => Enum.Parse<StatusValidacao>(valor, ignoreCase: true);
    }
}