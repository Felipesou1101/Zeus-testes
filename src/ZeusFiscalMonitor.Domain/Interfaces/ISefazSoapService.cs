using System;
using System.Collections.Generic;
using System.Text;
using ZeusFiscalMonitor.Domain.DTOs;
using ZeusFiscalMonitor.Domain.ValueObjects;

namespace ZeusFiscalMonitor.Domain.Interfaces
{
    public interface ISefazSoapService
    {
        Task<RetornoStatusSefazDto> ConsultarStatusServicoAsync(Uf uf, CancellationToken cancellationToken = default);
    }
}
