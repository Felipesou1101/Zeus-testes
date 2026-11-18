using System;
using System.Collections.Generic;
using System.Text;
using ZeusFiscalMonitor.Domain.DTOs;
using ZeusFiscalMonitor.Domain.Enums;

namespace ZeusFiscalMonitor.Domain.Interfaces
{
    public interface IValidadorDocumento
    {
        /// <summary>Identificador do tipo: "NFE", "NFCE"...</summary>
        string TipoDocumento { get; }
        ModeloDocumento Modelo { get; }
        ResultadoValidacaoDto Validar(string xml);
    }
}
