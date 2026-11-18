using System;
using System.Collections.Generic;
using System.Text;

namespace ZeusFiscalMonitor.Domain.DTOs
{
    public sealed record RetornoStatusSefazDto(
        bool Sucesso,
        string Uf,
        string? CStat,
        string? XMotivo,
        int? TempoMedioSegundos,
        DateTimeOffset? DataHoraRecebimento,
        long TempoRespostaMs,
        string? Erro = null
    );
}
