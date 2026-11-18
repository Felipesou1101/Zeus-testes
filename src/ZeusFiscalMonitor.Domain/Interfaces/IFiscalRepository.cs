using System;
using System.Collections.Generic;
using System.Text;
using ZeusFiscalMonitor.Domain.DTOs;
using ZeusFiscalMonitor.Domain.Entities;
using ZeusFiscalMonitor.Domain.ValueObjects;

namespace ZeusFiscalMonitor.Domain.Interfaces
{
    public interface IFiscalRepository
    {
        Task<DocumentoFiscal?> ObterPorChaveAsync(ChaveAcesso chave, CancellationToken cancellationToken = default);
        Task AdicionarAsync(DocumentoFiscal documento, CancellationToken cancellationToken = default);
        Task AtualizarAsync(DocumentoFiscal documento, CancellationToken cancellationToken = default);
        Task RegistrarEventoAsync(TimelineEventoFiscal evento, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<HistoricoOperacaoDto>> ListarHistoricoAsync(ChaveAcesso? filtro, int limite, CancellationToken cancellationToken = default);
    }
}
