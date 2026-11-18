using System;
using System.Collections.Generic;
using System.Text;

namespace ZeusFiscalMonitor.Domain.DTOs
{
    public sealed record ResultadoAssinaturaDto(bool Sucesso, string? XmlAssinado, string? Erro, string? TitularCertificado = null)
    {
        public static ResultadoAssinaturaDto Ok(string xmlAssinado, string titular) => new(true, xmlAssinado, null, titular);
        public static ResultadoAssinaturaDto Falha(string erro) => new(false, null, erro);
    }
}
