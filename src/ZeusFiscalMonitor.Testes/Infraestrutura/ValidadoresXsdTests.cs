using ZeusFiscalMonitor.Infraestrutura.Validadores;
using ZeusFiscalMonitor.Testes.Helpers;

namespace ZeusFiscalMonitor.Testes.Infraestrutura;

public class ValidadoresXsdTests
{
    private readonly ValidadorNFe _validadorNFe = new();
    private readonly ValidadorNFCe _validadorNFCe = new();

    [Fact]
    public void ValidadorNFe_XmlDeExemplo_EhValido()
    {
        var resultado = _validadorNFe.Validar(XmlExemplos.NFe);

        Assert.True(resultado.Valido, string.Join(Environment.NewLine, resultado.Erros));
    }

    [Fact]
    public void ValidadorNFCe_XmlDeExemplo_EhValido()
    {
        var resultado = _validadorNFCe.Validar(XmlExemplos.NFCe);

        Assert.True(resultado.Valido, string.Join(Environment.NewLine, resultado.Erros));
    }

    [Fact]
    public void ValidadorNFCe_ComXmlModelo55_RejeitaPeloModelo()
    {
        var resultado = _validadorNFCe.Validar(XmlExemplos.NFe);

        Assert.False(resultado.Valido);
        Assert.Contains(resultado.Erros, e => e.Contains("<mod>65</mod>"));
    }

    [Fact]
    public void ValidadorNFe_SemGrupoEmitente_CapturaErroDeSchema()
    {
        // Arrange: remove o bloco <emit> inteiro.
        var xml = XmlExemplos.NFe;
        var inicio = xml.IndexOf("<emit>", StringComparison.Ordinal);
        var fim = xml.IndexOf("</emit>", StringComparison.Ordinal) + "</emit>".Length;
        var semEmitente = xml.Remove(inicio, fim - inicio);

        // Act
        var resultado = _validadorNFe.Validar(semEmitente);

        // Assert
        Assert.False(resultado.Valido);
        Assert.StartsWith("Schema (linha", Assert.Single(resultado.Erros));
    }

    [Fact]
    public void ValidadorNFe_CnpjComLetras_ViolaPatternDoSchema()
    {
        var xml = XmlExemplos.NFe.Replace("<CNPJ>11222333000181</CNPJ>", "<CNPJ>11ABC333000181</CNPJ>");

        var resultado = _validadorNFe.Validar(xml);

        Assert.False(resultado.Valido);
        Assert.Contains("CNPJ", resultado.Erros[0]);
    }

    [Fact]
    public void ValidadorNFCe_SemConsumidorFinal_ViolaRegraEspecifica()
    {
        var xml = XmlExemplos.NFCe.Replace("<indFinal>1</indFinal>", "<indFinal>0</indFinal>");

        var resultado = _validadorNFCe.Validar(xml);

        Assert.False(resultado.Valido);
        Assert.Contains(resultado.Erros, e => e.Contains("consumidor final"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("<NFe><infNFe>")]
    [InlineData("<outro xmlns=\"urn:qualquer\"/>")]
    public void ValidadorNFe_EntradasInvalidas_RetornamFalhaSemLancarExcecao(string xml)
    {
        var resultado = _validadorNFe.Validar(xml);

        Assert.False(resultado.Valido);
        Assert.NotEmpty(resultado.Erros);
    }
}
