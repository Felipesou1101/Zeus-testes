using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ZeusFiscalMonitor.Infraestrutura.Servicos;

/// <summary>
/// Gera certificados autoassinados no formato de um e-CNPJ A1 para testes/demonstração.
/// NÃO é aceito pela SEFAZ (não pertence à cadeia ICP-Brasil).
/// </summary>
public static class CertificadoTesteFactory
{
    public const string TitularPadrao = "CN=ZEUS FISCAL MONITOR TESTE LTDA:11222333000181, O=ICP-Brasil (TESTE), C=BR";

    public static X509Certificate2 Criar(string titular = TitularPadrao, int diasValidade = 365)
    {
        using var rsa = RSA.Create(2048);
        var requisicao = new CertificateRequest(titular, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        requisicao.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation | X509KeyUsageFlags.KeyEncipherment, critical: false));
        requisicao.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            [new Oid("1.3.6.1.5.5.7.3.2"), new Oid("1.3.6.1.5.5.7.3.4")], critical: false)); // clientAuth, emailProtection

        return requisicao.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(diasValidade));
    }

    public static void ExportarPfx(X509Certificate2 certificado, string caminho, string senha) =>
        File.WriteAllBytes(caminho, certificado.Export(X509ContentType.Pfx, senha));
}
