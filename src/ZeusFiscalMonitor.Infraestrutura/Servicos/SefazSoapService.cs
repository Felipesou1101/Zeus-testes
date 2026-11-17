using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using ZeusFiscalMonitor.Domain.DTOs;
using ZeusFiscalMonitor.Domain.Interfaces;
using ZeusFiscalMonitor.Domain.ValueObjects;

namespace ZeusFiscalMonitor.Infraestrutura.Servicos;

public sealed class SefazOptions
{
    public const string Secao = "Sefaz";

    /// <summary>Certificado A1 usado na autenticação TLS mútua exigida pela SEFAZ.</summary>
    public string? CaminhoCertificado { get; set; }
    public string? SenhaCertificado { get; set; }
    public int TimeoutSegundos { get; set; } = 30;
}

/// <summary>
/// Consulta NfeStatusServico4 (SOAP 1.2) em Homologação (tpAmb = 2).
/// O HttpClient é injetado (typed client): o certificado cliente é configurado no handler pelo DI,
/// e nos testes o handler é substituído por um mock.
/// </summary>
public sealed class SefazSoapService(HttpClient httpClient) : ISefazSoapService
{
    public const string TipoAmbienteHomologacao = "2";
    private const string NamespaceWsdl = "http://www.portalfiscal.inf.br/nfe/wsdl/NFeStatusServico4";
    private const string SoapAction = NamespaceWsdl + "/nfeStatusServicoNF";
    private static readonly XNamespace Nfe = "http://www.portalfiscal.inf.br/nfe";

    public async Task<RetornoStatusSefazDto> ConsultarStatusServicoAsync(Uf uf, CancellationToken cancellationToken = default)
    {
        var cronometro = Stopwatch.StartNew();
        RetornoStatusSefazDto Falha(string erro) => new(false, uf.Sigla, null, null, null, null, cronometro.ElapsedMilliseconds, erro);

        try
        {
            using var conteudo = new StringContent(MontarEnvelope(uf), Encoding.UTF8);
            conteudo.Headers.ContentType = MediaTypeHeaderValue.Parse($"application/soap+xml; charset=utf-8; action=\"{SoapAction}\"");

            using var resposta = await httpClient.PostAsync(SefazEndpointsHomologacao.StatusServico(uf), conteudo, cancellationToken);
            var corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);

            if (!resposta.IsSuccessStatusCode)
                return Falha($"HTTP {(int)resposta.StatusCode} ({resposta.ReasonPhrase}). Verifique o certificado A1 configurado em appsettings.json.");

            return Interpretar(uf, corpo, cronometro.ElapsedMilliseconds) ?? Falha("Resposta da SEFAZ sem o grupo <retConsStatServ>.");
        }
        catch (HttpRequestException ex)
        {
            var detalhe = ex.InnerException?.Message ?? ex.Message;
            return Falha($"Falha de comunicação com a SEFAZ: {detalhe}. A SEFAZ exige TLS mútuo com certificado A1 ICP-Brasil.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Falha("Tempo limite excedido aguardando a SEFAZ.");
        }
        catch (XmlException ex)
        {
            return Falha($"Resposta SOAP inválida: {ex.Message}");
        }
    }

    public static string MontarEnvelope(Uf uf)
    {
        XNamespace soap = "http://www.w3.org/2003/05/soap-envelope";
        XNamespace wsdl = NamespaceWsdl;

        var envelope = new XElement(soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soap12", soap),
            new XElement(soap + "Body",
                new XElement(wsdl + "nfeDadosMsg",
                    new XElement(Nfe + "consStatServ",
                        new XAttribute("versao", "4.00"),
                        new XElement(Nfe + "tpAmb", TipoAmbienteHomologacao),
                        new XElement(Nfe + "cUF", uf.CodigoIbge),
                        new XElement(Nfe + "xServ", "STATUS")))));

        return new XDeclaration("1.0", "utf-8", null) + envelope.ToString(SaveOptions.DisableFormatting);
    }

    private static RetornoStatusSefazDto? Interpretar(Uf uf, string corpo, long tempoMs)
    {
        var retorno = XDocument.Parse(corpo).Descendants(Nfe + "retConsStatServ").FirstOrDefault();
        if (retorno is null)
            return null;

        string? Valor(string tag) => retorno.Element(Nfe + tag)?.Value;

        return new RetornoStatusSefazDto(
            Sucesso: true,
            Uf: uf.Sigla,
            CStat: Valor("cStat"),
            XMotivo: Valor("xMotivo"),
            TempoMedioSegundos: int.TryParse(Valor("tMed"), out var tMed) ? tMed : null,
            DataHoraRecebimento: DateTimeOffset.TryParse(Valor("dhRecbto"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dh) ? dh : null,
            TempoRespostaMs: tempoMs);
    }
}

/// <summary>
/// URLs do NfeStatusServico4 em homologação por autorizador.
/// Fonte: Portal Nacional da NF-e (relação de Web Services) — revise periodicamente.
/// </summary>
public static class SefazEndpointsHomologacao
{
    private const string Svrs = "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeStatusServico/NfeStatusServico4.asmx";
    private const string Svan = "https://hom.sefazvirtual.fazenda.gov.br/NFeStatusServico4/NFeStatusServico4.asmx";

    private static readonly Dictionary<string, string> Urls = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AM"] = "https://homnfe.sefaz.am.gov.br/services2/services/NfeStatusServico4",
        ["BA"] = "https://hnfe.sefaz.ba.gov.br/webservices/NFeStatusServico4/NFeStatusServico4.asmx",
        ["GO"] = "https://homolog.sefaz.go.gov.br/nfe/services/NFeStatusServico4",
        ["MG"] = "https://hnfe.fazenda.mg.gov.br/nfe2/services/NFeStatusServico4",
        ["MS"] = "https://hom.nfe.sefaz.ms.gov.br/ws/NFeStatusServico4",
        ["MT"] = "https://homologacao.sefaz.mt.gov.br/nfews/v2/services/NfeStatusServico4",
        ["PE"] = "https://nfehomolog.sefaz.pe.gov.br/nfe-service/services/NFeStatusServico4",
        ["PR"] = "https://homologacao.nfe.sefa.pr.gov.br/nfe/NFeStatusServico4",
        ["RS"] = "https://nfe-homologacao.sefazrs.rs.gov.br/ws/NfeStatusServico/NfeStatusServico4.asmx",
        ["SP"] = "https://homologacao.nfe.fazenda.sp.gov.br/ws/nfestatusservico4.asmx",
        ["MA"] = Svan,
        // Demais UFs (incluindo SE) são atendidas pela SEFAZ Virtual do RS.
        ["AC"] = Svrs, ["AL"] = Svrs, ["AP"] = Svrs, ["CE"] = Svrs, ["DF"] = Svrs, ["ES"] = Svrs,
        ["PA"] = Svrs, ["PB"] = Svrs, ["PI"] = Svrs, ["RJ"] = Svrs, ["RN"] = Svrs, ["RO"] = Svrs,
        ["RR"] = Svrs, ["SC"] = Svrs, ["SE"] = Svrs, ["TO"] = Svrs,
    };

    public static string StatusServico(Uf uf) => Urls[uf.Sigla];
}
