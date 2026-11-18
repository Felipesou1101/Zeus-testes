using System;
using System.Collections.Generic;
using System.Text;
using ZeusFiscalMonitor.Domain.DTOs;

namespace ZeusFiscalMonitor.Domain.Interfaces
{
    public interface IAssinadorXmlService
    {
        ResultadoAssinaturaDto Assinar(RequisicaoAssinaturaDto requisicao);
    }
}
