using System;
using System.Collections.Generic;
using System.Text;

namespace ZeusFiscalMonitor.Domain.DTOs
{
    public sealed record RequisicaoAssinaturaDto(string Xml, string CaminhoCertificado, string SenhaCertificado);

}
