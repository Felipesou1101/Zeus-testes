using Moq;
using ZeusFiscalMonitor.Domain.DTOs;
using ZeusFiscalMonitor.Domain.Entities;
using ZeusFiscalMonitor.Domain.Enums;
using ZeusFiscalMonitor.Domain.Interfaces;
using ZeusFiscalMonitor.Domain.Services;
using ZeusFiscalMonitor.Domain.ValueObjects;
using ZeusFiscalMonitor.Testes.Helpers;

namespace ZeusFiscalMonitor.Testes.Domain;

/// <summary>
/// Testes do orquestrador com todas as dependências mockadas (Moq):
/// rápidos, isolados e sem banco/arquivo/rede.
/// </summary>
public class ProcessadorDocumentoFiscalTests
{
    private readonly Mock<IValidadorDocumento> _validadorNFe = CriarValidador("NFE", ModeloDocumento.NFe);
    private readonly Mock<IValidadorDocumento> _validadorNFCe = CriarValidador("NFCE", ModeloDocumento.NFCe);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly Mock<IAssinadorXmlService> _assinador = new();
    private readonly Mock<IFiscalRepository> _repositorio = new();
    private readonly List<TimelineEventoFiscal> _eventos = [];
    private readonly ProcessadorDocumentoFiscal _sut;

    private static readonly RequisicaoAssinaturaDto Requisicao = new(XmlExemplos.NFe, "cert.pfx", "senha");

    public ProcessadorDocumentoFiscalTests()
    {
        _repositorio
            .Setup(r => r.RegistrarEventoAsync(It.IsAny<TimelineEventoFiscal>(), It.IsAny<CancellationToken>()))
            .Callback<TimelineEventoFiscal, CancellationToken>((e, _) => _eventos.Add(e))
            .Returns(Task.CompletedTask);

        _sut = new ProcessadorDocumentoFiscal([_validadorNFe.Object, _validadorNFCe.Object], _assinador.Object, _repositorio.Object);
    }

    private static Mock<IValidadorDocumento> CriarValidador(string tipo, ModeloDocumento modelo)
    {
        var mock = new Mock<IValidadorDocumento>();
        mock.SetupGet(v => v.TipoDocumento).Returns(tipo);
        mock.SetupGet(v => v.Modelo).Returns(modelo);
        mock.Setup(v => v.Validar(It.IsAny<string>())).Returns(ResultadoValidacaoDto.Ok());
        return mock;
    }

    [Fact]
    public async Task ValidarAsync_DocumentoNovoValido_PersisteERegistraEventoDeSucesso()
    {
        // Act
        var resultado = await _sut.ValidarAsync("NFE", XmlExemplos.NFe, Ct);

        // Assert
        Assert.True(resultado.Sucesso);
        Assert.Equal("VALIDADO", resultado.Status);
        Assert.Equal(XmlExemplos.ChaveNFe, resultado.ChaveAcesso);

        _repositorio.Verify(r => r.AdicionarAsync(
            It.Is<DocumentoFiscal>(d => d.UfEmitente == new Uf("SE") && d.Modelo == ModeloDocumento.NFe),
            It.IsAny<CancellationToken>()), Times.Once);

        var evento = Assert.Single(_eventos);
        Assert.Equal(TimelineEventoFiscal.Etapas.ValidacaoXsd, evento.Etapa);
        Assert.Equal(TimelineEventoFiscal.Origens.Aplicacao, evento.OrigemEvento);
        Assert.Equal("PENDENTE", evento.StatusAnterior);
        Assert.Equal("VALIDADO", evento.StatusAtual);
        Assert.True(evento.Sucesso);
        Assert.Contains("status_validacao", evento.AlteracoesCampos);
    }

    [Fact]
    public async Task ValidarAsync_TipoNFCe_UsaSomenteAStrategyNFCe()
    {
        await _sut.ValidarAsync("nfce", XmlExemplos.NFCe, Ct); // case-insensitive

        _validadorNFCe.Verify(v => v.Validar(It.IsAny<string>()), Times.Once);
        _validadorNFe.Verify(v => v.Validar(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ValidarAsync_XmlInvalidoNoSchema_MarcaErroERegistraEventoDeFalha()
    {
        // Arrange
        _validadorNFe.Setup(v => v.Validar(It.IsAny<string>()))
            .Returns(ResultadoValidacaoDto.Falha("Elemento 'emit' ausente."));

        // Act
        var resultado = await _sut.ValidarAsync("NFE", XmlExemplos.NFe, Ct);

        // Assert
        Assert.False(resultado.Sucesso);
        Assert.Equal("ERRO", resultado.Status);
        Assert.Contains("Elemento 'emit' ausente.", resultado.Erros!);

        var evento = Assert.Single(_eventos);
        Assert.False(evento.Sucesso);
        Assert.Equal("Elemento 'emit' ausente.", evento.DetalheErro);
    }

    [Fact]
    public async Task ValidarAsync_TipoNaoSuportado_NaoAcessaRepositorio()
    {
        var resultado = await _sut.ValidarAsync("CTE", XmlExemplos.NFe, Ct);

        Assert.False(resultado.Sucesso);
        Assert.Contains("não suportado", resultado.Mensagem);
        _repositorio.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ValidarAsync_XmlSemChaveDeAcesso_RetornaFalhaSemPersistir()
    {
        var resultado = await _sut.ValidarAsync("NFE", "<NFe xmlns=\"http://www.portalfiscal.inf.br/nfe\"/>", Ct);

        Assert.False(resultado.Sucesso);
        Assert.Contains("infNFe", resultado.Mensagem);
        _repositorio.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ValidarAsync_ChaveJaExistente_ReprocessaSemInserirNovamente()
    {
        // Arrange
        var existente = new DocumentoFiscal(new ChaveAcesso(XmlExemplos.ChaveNFe), ModeloDocumento.NFe, new Uf("SE"), "<antigo/>");
        existente.RegistrarErro("falha anterior");
        _repositorio.Setup(r => r.ObterPorChaveAsync(It.IsAny<ChaveAcesso>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existente);

        // Act
        var resultado = await _sut.ValidarAsync("NFE", XmlExemplos.NFe, Ct);

        // Assert
        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusValidacao.Validado, existente.StatusValidacao);
        Assert.Equal(XmlExemplos.NFe, existente.XmlOriginal);
        _repositorio.Verify(r => r.AdicionarAsync(It.IsAny<DocumentoFiscal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ValidarEAssinarAsync_FluxoFeliz_TerminaAssinadoComDoisEventos()
    {
        // Arrange
        _assinador.Setup(a => a.Assinar(It.IsAny<RequisicaoAssinaturaDto>()))
            .Returns(ResultadoAssinaturaDto.Ok("<NFe>assinado</NFe>", "EMPRESA TESTE"));

        // Act
        var resultado = await _sut.ValidarEAssinarAsync("NFE", Requisicao, Ct);

        // Assert
        Assert.True(resultado.Sucesso);
        Assert.Equal("ASSINADO", resultado.Status);
        Assert.Equal("<NFe>assinado</NFe>", resultado.XmlAssinado);
        Assert.Equal(
            [TimelineEventoFiscal.Etapas.ValidacaoXsd, TimelineEventoFiscal.Etapas.Assinatura],
            _eventos.Select(e => e.Etapa));
    }

    [Fact]
    public async Task ValidarEAssinarAsync_XmlInvalido_NaoChamaAssinador()
    {
        _validadorNFe.Setup(v => v.Validar(It.IsAny<string>())).Returns(ResultadoValidacaoDto.Falha("inválido"));

        var resultado = await _sut.ValidarEAssinarAsync("NFE", Requisicao, Ct);

        Assert.False(resultado.Sucesso);
        _assinador.Verify(a => a.Assinar(It.IsAny<RequisicaoAssinaturaDto>()), Times.Never);
    }

    [Fact]
    public async Task ValidarEAssinarAsync_FalhaNaAssinatura_MarcaErro()
    {
        // Arrange
        _assinador.Setup(a => a.Assinar(It.IsAny<RequisicaoAssinaturaDto>()))
            .Returns(ResultadoAssinaturaDto.Falha("Senha incorreta"));

        // Act
        var resultado = await _sut.ValidarEAssinarAsync("NFE", Requisicao, Ct);

        // Assert
        Assert.False(resultado.Sucesso);
        Assert.Equal("ERRO", resultado.Status);
        var eventoAssinatura = _eventos.Last();
        Assert.Equal(TimelineEventoFiscal.Etapas.Assinatura, eventoAssinatura.Etapa);
        Assert.False(eventoAssinatura.Sucesso);
        Assert.Contains("Senha incorreta", eventoAssinatura.DetalheErro);
    }
}
