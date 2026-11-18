using ZeusFiscalMonitor.Domain.DTOs;
using ZeusFiscalMonitor.Domain.Entities;
using ZeusFiscalMonitor.Domain.Enums;
using ZeusFiscalMonitor.Domain.ValueObjects;

namespace ZeusFiscalMonitor.Domain.Interfaces
{
    public interface IProcessadorDocumentoFiscal
    {
        IReadOnlyCollection<string> TiposSuportados { get; }
        Task<ResultadoProcessamentoDto> ValidarAsync(string tipoDocumento, string xml, CancellationToken cancellationToken = default);
        Task<ResultadoProcessamentoDto> ValidarEAssinarAsync(string tipoDocumento, RequisicaoAssinaturaDto requisicao, CancellationToken cancellationToken = default);
    }
}
