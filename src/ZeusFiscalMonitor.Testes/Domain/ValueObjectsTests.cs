using ZeusFiscalMonitor.Domain.Exceptions;
using ZeusFiscalMonitor.Domain.ValueObjects;
using ZeusFiscalMonitor.Testes.Helpers;

namespace ZeusFiscalMonitor.Testes.Domain;

public class UfTests
{
    [Theory]
    [InlineData("SE", "SE")]
    [InlineData("sp", "SP")]
    [InlineData(" df ", "DF")]
    public void Construtor_ComSiglaValida_NormalizaParaMaiusculas(string entrada, string esperado)
    {
        // Act
        var uf = new Uf(entrada);

        // Assert
        Assert.Equal(esperado, uf.Sigla);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("XX")]
    [InlineData("S")]
    [InlineData("SEE")]
    public void Construtor_ComSiglaInvalida_LancaDomainException(string entrada)
    {
        Assert.Throws<DomainException>(() => new Uf(entrada));
    }

    [Fact]
    public void Siglas_ContemAs27UnidadesFederativas()
    {
        Assert.Equal(27, Uf.Siglas.Count);
    }

    [Fact]
    public void DoCodigoIbge_ComCodigo28_RetornaSergipe()
    {
        var uf = Uf.DoCodigoIbge(28);

        Assert.Equal(new Uf("SE"), uf); // igualdade por valor (record)
        Assert.Equal(28, uf.CodigoIbge);
    }

    [Fact]
    public void DoCodigoIbge_ComCodigoInexistente_LancaDomainException()
    {
        Assert.Throws<DomainException>(() => Uf.DoCodigoIbge(99));
    }
}

public class ChaveAcessoTests
{
    [Fact]
    public void Construtor_ComChaveValida_ExpoeUfEModelo()
    {
        // Act
        var chave = new ChaveAcesso(XmlExemplos.ChaveNFe);

        // Assert
        Assert.Equal(XmlExemplos.ChaveNFe, chave.Valor);
        Assert.Equal(28, chave.CodigoUf);
        Assert.Equal("55", chave.CodigoModelo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("2826111122233300018155001000000123112345678")]   // 43 dígitos
    [InlineData("282611112223330001815500100000012311234567821")] // 45 dígitos
    [InlineData("2826111122233300018155001000000123112345678A")]  // não numérico
    public void Construtor_ComFormatoInvalido_LancaDomainException(string valor)
    {
        Assert.Throws<DomainException>(() => new ChaveAcesso(valor));
    }

    [Fact]
    public void Construtor_ComDigitoVerificadorErrado_LancaDomainException()
    {
        var chaveComDvErrado = XmlExemplos.ChaveNFe[..43] + "9";

        var ex = Assert.Throws<DomainException>(() => new ChaveAcesso(chaveComDvErrado));
        Assert.Contains("Dígito verificador", ex.Message);
    }

    [Fact]
    public void Igualdade_DuasChavesComMesmoValor_SaoIguais()
    {
        Assert.Equal(new ChaveAcesso(XmlExemplos.ChaveNFe), new ChaveAcesso(XmlExemplos.ChaveNFe));
    }
}
