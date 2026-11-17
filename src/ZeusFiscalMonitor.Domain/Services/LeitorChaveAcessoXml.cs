using System.Xml;
using ZeusFiscalMonitor.Domain.Exceptions;
using ZeusFiscalMonitor.Domain.ValueObjects;

namespace ZeusFiscalMonitor.Domain.Services;

/// <summary>Extrai a chave de acesso do atributo Id="NFe{44 dígitos}" da tag &lt;infNFe&gt;.</summary>
public static class LeitorChaveAcessoXml
{
    public const string NamespaceNFe = "http://www.portalfiscal.inf.br/nfe";

    public static ChaveAcesso Extrair(string xml)
    {
        var documento = new XmlDocument { XmlResolver = null }; // bloqueia XXE
        try
        {
            documento.LoadXml(xml);
        }
        catch (XmlException ex)
        {
            throw new DomainException($"XML malformado: {ex.Message}");
        }

        var infNFe = documento.GetElementsByTagName("infNFe", NamespaceNFe).OfType<XmlElement>().FirstOrDefault()
                     ?? throw new DomainException("Tag <infNFe> não encontrada no namespace da NF-e.");

        var id = infNFe.GetAttribute("Id");
        if (!id.StartsWith("NFe", StringComparison.Ordinal))
            throw new DomainException("O atributo Id da <infNFe> deve iniciar com 'NFe'.");

        return new ChaveAcesso(id[3..]);
    }
}
