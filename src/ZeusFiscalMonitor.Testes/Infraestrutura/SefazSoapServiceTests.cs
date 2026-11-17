using System.Net;
using Moq;
using Moq.Protected;
using ZeusFiscalMonitor.Domain.ValueObjects;
using ZeusFiscalMonitor.Infraestrutura.Servicos;

namespace ZeusFiscalMonitor.Testes.Infraestrutura;

/// <summary>
/// O HttpMessageHandler é mockado: nenhum tráfego de rede real (testes rápidos e determinísticos).
/// </summary>
public class SefazSoapServiceTests
{
    private const string RespostaEmOperacao = """
        <?xml version="1.0" encoding="utf-8"?>
        <soap:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope">
          <soap:Body>
            <nfeResultMsg xmlns="http://www.portalfiscal.inf.br/nfe/wsdl/NFeStatusServico4">
              <retConsStatServ versao="4.00" xmlns="http://www.portalfiscal.inf.br/nfe">
                <tpAmb>2</tpAmb>
                <verAplic>SVRS202611101200</verAplic>
                <cStat>107</cStat>
                <xMotivo>Servico em Operacao</xMotivo>
                <cUF>28</cUF>
                <dhRecbto>2026-11-17T10:30:00-03:00</dhRecbto>
                <tMed>1</tMed>
              </retConsStatServ>
            </nfeResultMsg>
          </soap:Body>
        </soap:Envelope>
        """;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly Mock<HttpMessageHandler> _handler = new(MockBehavior.Strict);
    private HttpRequestMessage? _requisicaoEnviada;
    private string? _corpoEnviado;

    private SefazSoapService CriarSut() => new(new HttpClient(_handler.Object));

    private void ConfigurarResposta(HttpStatusCode status, string corpo) =>
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                _requisicaoEnviada = req;
                _corpoEnviado = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            })
            .ReturnsAsync(new HttpResponseMessage(status) { Content = new StringContent(corpo) });

    [Fact]
    public async Task ConsultarStatusServicoAsync_RespostaEmOperacao_RetornaCStat107()
    {
        // Arrange
        ConfigurarResposta(HttpStatusCode.OK, RespostaEmOperacao);

        // Act
        var retorno = await CriarSut().ConsultarStatusServicoAsync(new Uf("SE"), Ct);

        // Assert
        Assert.True(retorno.Sucesso);
        Assert.Equal("107", retorno.CStat);
        Assert.Equal("Servico em Operacao", retorno.XMotivo);
        Assert.Equal(1, retorno.TempoMedioSegundos);
        Assert.Equal(new DateTimeOffset(2026, 11, 17, 10, 30, 0, TimeSpan.FromHours(-3)), retorno.DataHoraRecebimento);
    }

    [Fact]
    public async Task ConsultarStatusServicoAsync_Sergipe_EnviaSoap12ParaSvrsEmHomologacao()
    {
        // Arrange
        ConfigurarResposta(HttpStatusCode.OK, RespostaEmOperacao);

        // Act
        await CriarSut().ConsultarStatusServicoAsync(new Uf("SE"), Ct);

        // Assert
        Assert.Equal(HttpMethod.Post, _requisicaoEnviada!.Method);
        Assert.Contains("svrs.rs.gov.br", _requisicaoEnviada.RequestUri!.Host);
        Assert.Equal("application/soap+xml", _requisicaoEnviada.Content!.Headers.ContentType!.MediaType);
        Assert.Contains("<tpAmb>2</tpAmb>", _corpoEnviado);
        Assert.Contains("<cUF>28</cUF>", _corpoEnviado);
        Assert.Contains("<xServ>STATUS</xServ>", _corpoEnviado);
    }

    [Fact]
    public async Task ConsultarStatusServicoAsync_Http403_RetornaFalhaSemLancar()
    {
        ConfigurarResposta(HttpStatusCode.Forbidden, "Forbidden");

        var retorno = await CriarSut().ConsultarStatusServicoAsync(new Uf("SP"), Ct);

        Assert.False(retorno.Sucesso);
        Assert.Contains("HTTP 403", retorno.Erro);
    }

    [Fact]
    public async Task ConsultarStatusServicoAsync_RespostaSemRetConsStatServ_RetornaFalha()
    {
        ConfigurarResposta(HttpStatusCode.OK, "<Envelope><Body/></Envelope>");

        var retorno = await CriarSut().ConsultarStatusServicoAsync(new Uf("SE"), Ct);

        Assert.False(retorno.Sucesso);
        Assert.Contains("retConsStatServ", retorno.Erro);
    }

    [Fact]
    public async Task ConsultarStatusServicoAsync_FalhaDeRede_RetornaFalhaComOrientacao()
    {
        // Arrange
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("handshake TLS falhou"));

        // Act
        var retorno = await CriarSut().ConsultarStatusServicoAsync(new Uf("SE"), Ct);

        // Assert
        Assert.False(retorno.Sucesso);
        Assert.Contains("handshake TLS falhou", retorno.Erro);
        Assert.Contains("certificado A1", retorno.Erro);
    }

    [Theory]
    [MemberData(nameof(TodasAsUfs))]
    public void SefazEndpoints_TodaUfPossuiUrlHttps(string sigla)
    {
        var url = SefazEndpointsHomologacao.StatusServico(new Uf(sigla));

        Assert.StartsWith("https://", url);
    }

    public static TheoryData<string> TodasAsUfs() => new(Uf.Siglas);
}
