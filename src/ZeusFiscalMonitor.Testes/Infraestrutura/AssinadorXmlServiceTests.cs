using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using ZeusFiscalMonitor.Domain.DTOs;
using ZeusFiscalMonitor.Infraestrutura.Servicos;
using ZeusFiscalMonitor.Testes.Helpers;

namespace ZeusFiscalMonitor.Testes.Infraestrutura;

public class AssinadorXmlServiceTests : IDisposable
{
    private readonly AssinadorXmlService _sut = new();
    private readonly X509Certificate2 _certificado = CertificadoTesteFactory.Criar();

    public void Dispose() => _certificado.Dispose();

    private static XmlDocument Carregar(string xml)
    {
        var documento = new XmlDocument { PreserveWhitespace = false };
        documento.LoadXml(xml);
        return documento;
    }

    [Fact]
    public void AssinarComCertificado_XmlValido_GeraAssinaturaVerificavel()
    {
        // Act
        var resultado = _sut.AssinarComCertificado(XmlExemplos.NFe, _certificado);

        // Assert
        Assert.True(resultado.Sucesso, resultado.Erro);

        var documento = Carregar(resultado.XmlAssinado!);
        var signature = (XmlElement)documento.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl)[0]!;
        Assert.Equal("NFe", signature.ParentNode!.LocalName); // irmã de <infNFe>

        var signedXml = new SignedXml(documento);
        signedXml.LoadXml(signature);
        Assert.True(signedXml.CheckSignature(_certificado, verifySignatureOnly: true));
        Assert.Equal($"#NFe{XmlExemplos.ChaveNFe}", ((Reference)signedXml.SignedInfo!.References[0]!).Uri);
        Assert.Equal(SignedXml.XmlDsigRSASHA1Url, signedXml.SignedInfo.SignatureMethod);
    }

    [Fact]
    public void AssinarComCertificado_XmlAlteradoAposAssinatura_InvalidaAssinatura()
    {
        // Arrange
        var assinado = _sut.AssinarComCertificado(XmlExemplos.NFe, _certificado).XmlAssinado!;
        var adulterado = assinado.Replace("<vNF>300.00</vNF>", "<vNF>3.00</vNF>");

        // Act
        var documento = Carregar(adulterado);
        var signedXml = new SignedXml(documento);
        signedXml.LoadXml((XmlElement)documento.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl)[0]!);

        // Assert
        Assert.False(signedXml.CheckSignature(_certificado, verifySignatureOnly: true));
    }

    [Fact]
    public void AssinarComCertificado_XmlJaAssinado_SubstituiAssinaturaAnterior()
    {
        var primeira = _sut.AssinarComCertificado(XmlExemplos.NFe, _certificado).XmlAssinado!;

        var segunda = _sut.AssinarComCertificado(primeira, _certificado);

        Assert.True(segunda.Sucesso);
        Assert.Equal(1, Carregar(segunda.XmlAssinado!).GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl).Count);
    }

    [Fact]
    public void AssinarComCertificado_CertificadoSemChavePrivada_RetornaFalha()
    {
        using var somentePublico = X509CertificateLoader.LoadCertificate(_certificado.RawData);

        var resultado = _sut.AssinarComCertificado(XmlExemplos.NFe, somentePublico);

        Assert.False(resultado.Sucesso);
        Assert.Contains("chave privada", resultado.Erro);
    }

    [Fact]
    public void AssinarComCertificado_CertificadoVencido_RetornaFalha()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        var requisicao = new CertificateRequest("CN=VENCIDO", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var vencido = requisicao.CreateSelfSigned(DateTimeOffset.UtcNow.AddYears(-2), DateTimeOffset.UtcNow.AddYears(-1));

        // Act
        var resultado = _sut.AssinarComCertificado(XmlExemplos.NFe, vencido);

        // Assert
        Assert.False(resultado.Sucesso);
        Assert.Contains("validade", resultado.Erro);
    }

    [Fact]
    public void AssinarComCertificado_XmlSemInfNFe_RetornaFalha()
    {
        var resultado = _sut.AssinarComCertificado("<NFe xmlns=\"http://www.portalfiscal.inf.br/nfe\"/>", _certificado);

        Assert.False(resultado.Sucesso);
        Assert.Contains("infNFe", resultado.Erro);
    }

    [Fact]
    public void Assinar_ArquivoDeCertificadoInexistente_RetornaFalha()
    {
        var resultado = _sut.Assinar(new RequisicaoAssinaturaDto(XmlExemplos.NFe, @"C:\nao\existe.pfx", "x"));

        Assert.False(resultado.Sucesso);
        Assert.Contains("não encontrado", resultado.Erro);
    }

    [Fact]
    public void Assinar_PfxComSenhaCorretaESenhaErrada_ComportaSeConformeEsperado()
    {
        // Arrange
        var caminho = Path.Combine(Path.GetTempPath(), $"zeus-teste-{Guid.NewGuid():N}.pfx");
        CertificadoTesteFactory.ExportarPfx(_certificado, caminho, "senha-certa");

        try
        {
            // Act
            var ok = _sut.Assinar(new RequisicaoAssinaturaDto(XmlExemplos.NFe, caminho, "senha-certa"));
            var falha = _sut.Assinar(new RequisicaoAssinaturaDto(XmlExemplos.NFe, caminho, "senha-errada"));

            // Assert
            Assert.True(ok.Sucesso, ok.Erro);
            Assert.False(falha.Sucesso);
            Assert.Contains("senha incorreta", falha.Erro);
        }
        finally
        {
            File.Delete(caminho);
        }
    }
}
