using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using ZeusFiscalMonitor.Domain.DTOs;
using ZeusFiscalMonitor.Domain.Interfaces;
using ZeusFiscalMonitor.Domain.Services;

namespace ZeusFiscalMonitor.Infraestrutura.Servicos;

/// <summary>
/// Assinatura XMLDSig enveloped da tag &lt;infNFe&gt; conforme o Manual de Orientação do
/// Contribuinte: RSA-SHA1, digest SHA1, transforms enveloped + C14N, KeyInfo com X509Certificate.
/// </summary>
public sealed class AssinadorXmlService : IAssinadorXmlService
{
    public ResultadoAssinaturaDto Assinar(RequisicaoAssinaturaDto requisicao)
    {
        if (string.IsNullOrWhiteSpace(requisicao.CaminhoCertificado) || !File.Exists(requisicao.CaminhoCertificado))
            return ResultadoAssinaturaDto.Falha("Arquivo de certificado (.pfx) não encontrado.");

        try
        {
            using var certificado = X509CertificateLoader.LoadPkcs12FromFile(requisicao.CaminhoCertificado, requisicao.SenhaCertificado);
            return AssinarComCertificado(requisicao.Xml, certificado);
        }
        catch (CryptographicException ex)
        {
            return ResultadoAssinaturaDto.Falha($"Não foi possível abrir o certificado (senha incorreta ou arquivo inválido): {ex.Message}");
        }
    }

    /// <summary>Sobrecarga sem I/O de arquivo: usada pelos testes e por quem já tem o certificado em memória.</summary>
    public ResultadoAssinaturaDto AssinarComCertificado(string xml, X509Certificate2 certificado)
    {
        if (!certificado.HasPrivateKey)
            return ResultadoAssinaturaDto.Falha("O certificado não possui chave privada.");

        var agora = DateTime.Now;
        if (agora < certificado.NotBefore || agora > certificado.NotAfter)
            return ResultadoAssinaturaDto.Falha($"Certificado fora da validade ({certificado.NotBefore:d} a {certificado.NotAfter:d}).");

        using var chavePrivada = certificado.GetRSAPrivateKey();
        if (chavePrivada is null)
            return ResultadoAssinaturaDto.Falha("A NF-e exige certificado com chave RSA.");

        try
        {
            // PreserveWhitespace = false: a SEFAZ rejeita espaços/quebras entre tags (rejeição 588).
            var documento = new XmlDocument { PreserveWhitespace = false, XmlResolver = null };
            documento.LoadXml(xml);

            var infNFe = documento.GetElementsByTagName("infNFe", LeitorChaveAcessoXml.NamespaceNFe).OfType<XmlElement>().FirstOrDefault();
            if (infNFe is null)
                return ResultadoAssinaturaDto.Falha("Tag <infNFe> não encontrada.");

            var id = infNFe.GetAttribute("Id");
            if (string.IsNullOrEmpty(id))
                return ResultadoAssinaturaDto.Falha("A tag <infNFe> não possui o atributo Id.");

            var raiz = (XmlElement)infNFe.ParentNode!;
            RemoverAssinaturasExistentes(raiz);

            var signedXml = new SignedXml(documento) { SigningKey = chavePrivada };
            signedXml.SignedInfo!.SignatureMethod = SignedXml.XmlDsigRSASHA1Url;
            signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl;

            var referencia = new Reference($"#{id}") { DigestMethod = SignedXml.XmlDsigSHA1Url };
            referencia.AddTransform(new XmlDsigEnvelopedSignatureTransform());
            referencia.AddTransform(new XmlDsigC14NTransform());
            signedXml.AddReference(referencia);

            var keyInfo = new KeyInfo();
            keyInfo.AddClause(new KeyInfoX509Data(certificado));
            signedXml.KeyInfo = keyInfo;

            signedXml.ComputeSignature();

            // <Signature> é irmã de <infNFe>, filha de <NFe>.
            raiz.AppendChild(documento.ImportNode(signedXml.GetXml(), deep: true));

            return ResultadoAssinaturaDto.Ok(documento.OuterXml, certificado.GetNameInfo(X509NameType.SimpleName, forIssuer: false));
        }
        catch (XmlException ex)
        {
            return ResultadoAssinaturaDto.Falha($"XML malformado: {ex.Message}");
        }
        catch (CryptographicException ex)
        {
            return ResultadoAssinaturaDto.Falha($"Erro criptográfico ao assinar: {ex.Message}");
        }
    }

    private static void RemoverAssinaturasExistentes(XmlElement raiz)
    {
        var assinaturas = raiz.ChildNodes.OfType<XmlElement>()
            .Where(e => e.LocalName == "Signature" && e.NamespaceURI == SignedXml.XmlDsigNamespaceUrl)
            .ToList();

        foreach (var assinatura in assinaturas)
            raiz.RemoveChild(assinatura);
    }
}
