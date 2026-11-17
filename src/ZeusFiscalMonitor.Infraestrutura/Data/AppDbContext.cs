using Microsoft.EntityFrameworkCore;
using ZeusFiscalMonitor.Domain.Entities;
using ZeusFiscalMonitor.Domain.Enums;
using ZeusFiscalMonitor.Domain.ValueObjects;

namespace ZeusFiscalMonitor.Infraestrutura.Data;

/// <summary>
/// Mapeia as entidades para as tabelas criadas por banco/01_init.sql
/// (o schema é dono do banco: trigger de auditoria, defaults e índices).
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<DocumentoFiscal> Documentos => Set<DocumentoFiscal>();
    public DbSet<TimelineEventoFiscal> Timeline => Set<TimelineEventoFiscal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DocumentoFiscal>(e =>
        {
            e.ToTable("notas_fiscais", t => t.HasTrigger("trg_auditar_notas_fiscais"));
            e.HasKey(d => d.Id);
            e.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();

            // Value Objects <-> colunas primitivas.
            e.Property(d => d.ChaveAcesso).HasColumnName("chave_acesso").HasMaxLength(44)
                .HasConversion(v => v.Valor, v => new ChaveAcesso(v));
            e.HasIndex(d => d.ChaveAcesso).IsUnique();

            e.Property(d => d.UfEmitente).HasColumnName("uf_emitente").HasMaxLength(2)
                .HasConversion(v => v.Sigla, v => new Uf(v));

            e.Property(d => d.Modelo).HasColumnName("modelo").HasMaxLength(2)
                .HasConversion(v => ((int)v).ToString(), v => (ModeloDocumento)int.Parse(v));

            e.Property(d => d.StatusValidacao).HasColumnName("status_validacao").HasMaxLength(20)
                .HasConversion(v => v.ParaBanco(), v => StatusValidacaoExtensions.DoBanco(v));

            e.Property(d => d.XmlOriginal).HasColumnName("xml_original");
            e.Property(d => d.XmlAssinado).HasColumnName("xml_assinado");
            e.Property(d => d.MensagemErro).HasColumnName("mensagem_erro");
            e.Property(d => d.CriadoEm).HasColumnName("criado_em");
            e.Property(d => d.AtualizadoEm).HasColumnName("atualizado_em");
        });

        modelBuilder.Entity<TimelineEventoFiscal>(e =>
        {
            e.ToTable("timeline_eventos_fiscais");
            e.HasKey(t => t.EventoId);
            e.Property(t => t.EventoId).HasColumnName("evento_id").ValueGeneratedOnAdd(); // BIGSERIAL
            e.Property(t => t.DocumentoId).HasColumnName("documento_id");
            e.Property(t => t.OrigemEvento).HasColumnName("origem_evento").HasMaxLength(20);
            e.Property(t => t.Etapa).HasColumnName("etapa").HasMaxLength(40);
            e.Property(t => t.StatusAnterior).HasColumnName("status_anterior").HasMaxLength(30);
            e.Property(t => t.StatusAtual).HasColumnName("status_atual").HasMaxLength(30);
            e.Property(t => t.AlteracoesCampos).HasColumnName("alteracoes_campos").HasColumnType("jsonb");
            e.Property(t => t.DetalheErro).HasColumnName("detalhe_erro");
            e.Property(t => t.Sucesso).HasColumnName("sucesso");
            e.Property(t => t.OcorridoEm).HasColumnName("ocorrido_em");
            e.HasIndex(t => new { t.DocumentoId, t.OcorridoEm, t.EventoId });
        });
    }
}
