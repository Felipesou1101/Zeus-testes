using Microsoft.EntityFrameworkCore;
using ZeusFiscalMonitor.Domain.ValueObjects;
using ZeusFiscalMonitor.Infraestrutura.Data;
using ZeusFiscalMonitor.Testes.Helpers;

namespace ZeusFiscalMonitor.Testes.Infraestrutura;

/// <summary>
/// Valida o mapeamento EF Core sem abrir conexão: o modelo é compilado e o SQL é apenas gerado.
/// </summary>
public class AppDbContextTests : IDisposable
{
    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql("Host=localhost;Database=nao_conecta")
        .Options);

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Modelo_MapeiaParaAsTabelasEColunasDoScriptSql()
    {
        var script = _db.Database.GenerateCreateScript();

        Assert.Contains("CREATE TABLE notas_fiscais", script);
        Assert.Contains("chave_acesso character varying(44)", script);
        Assert.Contains("status_validacao character varying(20)", script);
        Assert.Contains("CREATE TABLE timeline_eventos_fiscais", script);
        Assert.Contains("alteracoes_campos jsonb", script);
    }

    [Fact]
    public void ConsultaPorChave_ValueObjectEhTraduzidoParaSql()
    {
        var chave = new ChaveAcesso(XmlExemplos.ChaveNFe);

        var sql = _db.Documentos.Where(d => d.ChaveAcesso == chave).ToQueryString();

        Assert.Contains("chave_acesso", sql);
        Assert.Contains(XmlExemplos.ChaveNFe, sql);
    }
}
