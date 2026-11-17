using Microsoft.EntityFrameworkCore;
using ZeusFiscalMonitor.Domain.DTOs;
using ZeusFiscalMonitor.Domain.Entities;
using ZeusFiscalMonitor.Domain.Interfaces;
using ZeusFiscalMonitor.Domain.ValueObjects;
using ZeusFiscalMonitor.Infraestrutura.Data;

namespace ZeusFiscalMonitor.Infraestrutura.Repositories;

/// <summary>
/// Em uma aplicação desktop a janela vive por horas; por isso cada operação cria um
/// DbContext curto via IDbContextFactory (unidade de trabalho por operação).
/// </summary>
public sealed class FiscalRepository(IDbContextFactory<AppDbContext> contextFactory) : IFiscalRepository
{
    public async Task<DocumentoFiscal?> ObterPorChaveAsync(ChaveAcesso chave, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Documentos.AsNoTracking().FirstOrDefaultAsync(d => d.ChaveAcesso == chave, cancellationToken);
    }

    public async Task AdicionarAsync(DocumentoFiscal documento, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        db.Documentos.Add(documento);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AtualizarAsync(DocumentoFiscal documento, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        db.Documentos.Update(documento);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RegistrarEventoAsync(TimelineEventoFiscal evento, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        db.Timeline.Add(evento);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HistoricoOperacaoDto>> ListarHistoricoAsync(ChaveAcesso? filtro, int limite, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var documentos = filtro is null ? db.Documentos : db.Documentos.Where(d => d.ChaveAcesso == filtro);

        var linhas = await (
                from e in db.Timeline
                join d in documentos on e.DocumentoId equals d.Id
                orderby e.OcorridoEm descending, e.EventoId descending
                select new { Evento = e, d.ChaveAcesso })
            .AsNoTracking()
            .Take(limite)
            .ToListAsync(cancellationToken);

        return linhas.Select(l => new HistoricoOperacaoDto(
                l.Evento.EventoId, l.Evento.DocumentoId, l.ChaveAcesso.Valor, l.Evento.OrigemEvento, l.Evento.Etapa,
                l.Evento.StatusAnterior, l.Evento.StatusAtual, l.Evento.Sucesso, l.Evento.DetalheErro,
                l.Evento.AlteracoesCampos, l.Evento.OcorridoEm))
            .ToList();
    }
}
