using ZeusFiscalMonitor.Domain.Entities;
using ZeusFiscalMonitor.Domain.Enums;
using ZeusFiscalMonitor.Domain.Exceptions;
using ZeusFiscalMonitor.Domain.ValueObjects;
using ZeusFiscalMonitor.Testes.Helpers;

namespace ZeusFiscalMonitor.Testes.Domain;

public class DocumentoFiscalTests
{
    private static DocumentoFiscal NovoDocumento() =>
        new(new ChaveAcesso(XmlExemplos.ChaveNFe), ModeloDocumento.NFe, new Uf("SE"), "<NFe/>");

    [Fact]
    public void Construtor_NovoDocumento_IniciaPendenteComId()
    {
        var documento = NovoDocumento();

        Assert.NotEqual(Guid.Empty, documento.Id);
        Assert.Equal(StatusValidacao.Pendente, documento.StatusValidacao);
        Assert.Null(documento.XmlAssinado);
    }

    [Fact]
    public void Construtor_SemXml_LancaDomainException()
    {
        Assert.Throws<DomainException>(() =>
            new DocumentoFiscal(new ChaveAcesso(XmlExemplos.ChaveNFe), ModeloDocumento.NFe, new Uf("SE"), " "));
    }

    [Fact]
    public void FluxoCompleto_ValidarEAssinar_TerminaAssinado()
    {
        // Arrange
        var documento = NovoDocumento();

        // Act
        documento.MarcarComoValidado();
        documento.RegistrarAssinatura("<NFe><Signature/></NFe>");

        // Assert
        Assert.Equal(StatusValidacao.Assinado, documento.StatusValidacao);
        Assert.Equal("<NFe><Signature/></NFe>", documento.XmlAssinado);
    }

    [Fact]
    public void RegistrarAssinatura_SemValidarAntes_LancaDomainException()
    {
        var documento = NovoDocumento();

        Assert.Throws<DomainException>(() => documento.RegistrarAssinatura("<NFe/>"));
        Assert.Equal(StatusValidacao.Pendente, documento.StatusValidacao);
    }

    [Fact]
    public void MarcarComoValidado_DocumentoJaValidado_LancaDomainException()
    {
        var documento = NovoDocumento();
        documento.MarcarComoValidado();

        Assert.Throws<DomainException>(documento.MarcarComoValidado);
    }

    [Fact]
    public void RegistrarErro_GuardaMensagemEMudaStatus()
    {
        var documento = NovoDocumento();

        documento.RegistrarErro("cStat 225 - Falha no Schema XML");

        Assert.Equal(StatusValidacao.Erro, documento.StatusValidacao);
        Assert.Equal("cStat 225 - Falha no Schema XML", documento.MensagemErro);
    }

    [Fact]
    public void SubstituirXml_AposErro_ReiniciaCicloComoPendente()
    {
        // Arrange
        var documento = NovoDocumento();
        documento.RegistrarErro("erro qualquer");

        // Act
        documento.SubstituirXml("<NFe>corrigido</NFe>", ModeloDocumento.NFe);

        // Assert
        Assert.Equal(StatusValidacao.Pendente, documento.StatusValidacao);
        Assert.Null(documento.MensagemErro);
        Assert.Equal("<NFe>corrigido</NFe>", documento.XmlOriginal);
    }
}
