using System;
using System.Collections.Generic;
using System.Text;

namespace ZeusFiscalMonitor.Domain.DTOs
{
    public sealed record ResultadoValidacaoDto(bool Valido, IReadOnlyList<string> Erros)
    {
        public static ResultadoValidacaoDto Ok() => new(true, []);
        public static ResultadoValidacaoDto Falha(params string[] erros) => new(false, erros);
    }
}
