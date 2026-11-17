using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZeusFiscalMonitor.Domain.Interfaces;
using ZeusFiscalMonitor.Domain.Services;
using ZeusFiscalMonitor.Infraestrutura.Data;
using ZeusFiscalMonitor.Infraestrutura.Repositories;
using ZeusFiscalMonitor.Infraestrutura.Servicos;
using ZeusFiscalMonitor.Infraestrutura.Validadores;

namespace ZeusFiscalMonitor.Infraestrutura;

public static class DependencyInjection
{
    public static IServiceCollection AddInfraestrutura(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ZeusFiscal")
                               ?? throw new InvalidOperationException("ConnectionStrings:ZeusFiscal não configurada.");

        services.AddDbContextFactory<AppDbContext>(o => o.UseNpgsql(connectionString));
        services.AddSingleton<IFiscalRepository, FiscalRepository>();

        // Strategies: adicionar um novo modelo = registrar uma nova linha aqui (OCP).
        services.AddSingleton<IValidadorDocumento, ValidadorNFe>();
        services.AddSingleton<IValidadorDocumento, ValidadorNFCe>();

        services.AddSingleton<IAssinadorXmlService, AssinadorXmlService>();
        services.AddSingleton<IProcessadorDocumentoFiscal, ProcessadorDocumentoFiscal>();

        services.Configure<SefazOptions>(configuration.GetSection(SefazOptions.Secao));
        services.AddHttpClient<ISefazSoapService, SefazSoapService>((sp, client) =>
            {
                client.Timeout = TimeSpan.FromSeconds(sp.GetRequiredService<IOptions<SefazOptions>>().Value.TimeoutSegundos);
            })
            .ConfigurePrimaryHttpMessageHandler(sp => CriarHandlerComCertificado(sp.GetRequiredService<IOptions<SefazOptions>>().Value));

        return services;
    }

    private static HttpMessageHandler CriarHandlerComCertificado(SefazOptions opcoes)
    {
        var handler = new HttpClientHandler { SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13 };

        if (!string.IsNullOrWhiteSpace(opcoes.CaminhoCertificado) && File.Exists(opcoes.CaminhoCertificado))
        {
            handler.ClientCertificateOptions = ClientCertificateOption.Manual;
            handler.ClientCertificates.Add(X509CertificateLoader.LoadPkcs12FromFile(opcoes.CaminhoCertificado, opcoes.SenhaCertificado));
        }

        return handler;
    }
}
