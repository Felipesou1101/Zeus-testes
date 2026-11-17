using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using ZeusFiscalMonitor.Domain.DTOs;
using ZeusFiscalMonitor.Domain.Enums;
using ZeusFiscalMonitor.Domain.Interfaces;
using ZeusFiscalMonitor.Domain.Services;

namespace ZeusFiscalMonitor.Infraestrutura.Validadores;

/// <summary>
/// Template Method: a validação estrutural (XSD) é comum; cada Strategy concreta
/// acrescenta apenas as regras específicas do seu modelo.
/// </summary>
public abstract class ValidadorDocumentoXsdBase : IValidadorDocumento
{
    private const string RecursoSchema = "ZeusFiscalMonitor.Infraestrutura.Schemas.nfe_v4.00_simplificado.xsd";
    protected static readonly XNamespace Nfe = LeitorChaveAcessoXml.NamespaceNFe;

    // Compilar o XSD é caro: feito uma única vez e compartilhado (somente leitura após Compile).
    private static readonly Lazy<XmlSchemaSet> Schemas = new(CarregarSchemas);

    public abstract string TipoDocumento { get; }
    public abstract ModeloDocumento Modelo { get; }

    public ResultadoValidacaoDto Validar(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return ResultadoValidacaoDto.Falha("O conteúdo XML está vazio.");

        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = Schemas.Value,
            DtdProcessing = DtdProcessing.Prohibit, // proteção contra XXE / billion laughs
            XmlResolver = null
        };

        XDocument documento;
        try
        {
            using var texto = new StringReader(xml);
            using var leitor = XmlReader.Create(texto, settings);
            documento = XDocument.Load(leitor);
        }
        catch (XmlSchemaValidationException ex)
        {
            return ResultadoValidacaoDto.Falha($"Schema (linha {ex.LineNumber}, coluna {ex.LinePosition}): {ex.Message}");
        }
        catch (XmlException ex)
        {
            return ResultadoValidacaoDto.Falha($"XML malformado (linha {ex.LineNumber}, coluna {ex.LinePosition}): {ex.Message}");
        }

        var erros = RegrasComuns(documento).Concat(RegrasEspecificas(documento)).ToArray();
        return erros.Length == 0 ? ResultadoValidacaoDto.Ok() : ResultadoValidacaoDto.Falha(erros);
    }

    /// <summary>Regras de negócio adicionais do modelo (além do XSD).</summary>
    protected virtual IEnumerable<string> RegrasEspecificas(XDocument documento) => [];

    protected static XElement? Ide(XDocument documento) => documento.Root?.Element(Nfe + "infNFe")?.Element(Nfe + "ide");

    private IEnumerable<string> RegrasComuns(XDocument documento)
    {
        // Sem ValidationEventHandler, elementos fora do namespace geram apenas warning;
        // por isso a raiz é conferida explicitamente.
        if (documento.Root?.Name != Nfe + "NFe")
        {
            yield return "A raiz do documento deve ser <NFe> no namespace http://www.portalfiscal.inf.br/nfe.";
            yield break;
        }

        var codigoModelo = ((int)Modelo).ToString();
        var mod = Ide(documento)?.Element(Nfe + "mod")?.Value;
        if (mod != codigoModelo)
            yield return $"O validador {TipoDocumento} exige <mod>{codigoModelo}</mod>, mas o XML possui <mod>{mod}</mod>.";

        var id = documento.Root.Element(Nfe + "infNFe")?.Attribute("Id")?.Value ?? string.Empty;
        if (id.Length == 47 && id.Substring(23, 2) != mod)
            yield return "O modelo contido na chave de acesso (Id) diverge da tag <mod>.";
    }

    private static XmlSchemaSet CarregarSchemas()
    {
        using var stream = typeof(ValidadorDocumentoXsdBase).Assembly.GetManifestResourceStream(RecursoSchema)
                           ?? throw new InvalidOperationException($"Schema embutido não encontrado: {RecursoSchema}");
        using var leitor = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });

        var set = new XmlSchemaSet();
        set.Add(LeitorChaveAcessoXml.NamespaceNFe, leitor);
        set.Compile();
        return set;
    }
}

/// <summary>NF-e modelo 55.</summary>
public sealed class ValidadorNFe : ValidadorDocumentoXsdBase
{
    public override string TipoDocumento => "NFE";
    public override ModeloDocumento Modelo => ModeloDocumento.NFe;
}

/// <summary>NFC-e modelo 65: venda presencial a consumidor final, com grupo de pagamento obrigatório.</summary>
public sealed class ValidadorNFCe : ValidadorDocumentoXsdBase
{
    public override string TipoDocumento => "NFCE";
    public override ModeloDocumento Modelo => ModeloDocumento.NFCe;

    protected override IEnumerable<string> RegrasEspecificas(XDocument documento)
    {
        if (Ide(documento)?.Element(Nfe + "indFinal")?.Value != "1")
            yield return "NFC-e deve ser emitida para consumidor final (<indFinal>1</indFinal>).";

        if (documento.Root?.Element(Nfe + "infNFe")?.Element(Nfe + "pag") is null)
            yield return "NFC-e exige o grupo de pagamento <pag>.";
    }
}
