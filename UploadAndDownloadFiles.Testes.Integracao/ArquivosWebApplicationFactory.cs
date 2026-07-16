using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using UploadAndDownloadFiles.Aplicacao.Portas;
using UploadAndDownloadFiles.Infraestrutura.Jobs;
using UploadAndDownloadFiles.Infraestrutura.Persistencia;

namespace UploadAndDownloadFiles.Testes.Integracao;

/// <summary>
/// Sobe o host via `WebApplicationFactory`, trocando `IRepositorioArquivos` para EF Core
/// InMemory e as portas de S3/CloudFront por mocks (Moq), conforme a estratégia de testes do
/// design (evita dependência de SQL Server/AWS reais).
/// </summary>
public sealed class ArquivosWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _nomeBancoInMemory = Guid.NewGuid().ToString();

    public Mock<IArmazenamentoObjetos> MockArmazenamento { get; } = new();
    public Mock<IAssinadorCdn> MockAssinadorCdn { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove todos os descritores ligados ao `ArquivosDbContext` (DbContextOptions,
            // IDbContextOptionsConfiguration, o próprio DbContext etc.) — a partir do EF Core 8,
            // múltiplas chamadas a `AddDbContext` ACUMULAM configurações em vez de substituí-las,
            // então remover só `DbContextOptions<T>` não evita o provider do Program.cs (SqlServer)
            // de continuar aplicado junto com o InMemory.
            var descritoresDoDbContext = services
                .Where(sd => sd.ServiceType == typeof(ArquivosDbContext)
                    || (sd.ServiceType.IsGenericType && sd.ServiceType.GetGenericArguments().Contains(typeof(ArquivosDbContext))))
                .ToList();
            foreach (var descritor in descritoresDoDbContext)
                services.Remove(descritor);

            services.AddDbContext<ArquivosDbContext>(options => options.UseInMemoryDatabase(_nomeBancoInMemory));

            services.RemoveAll<IArmazenamentoObjetos>();
            services.AddSingleton(MockArmazenamento.Object);

            services.RemoveAll<IAssinadorCdn>();
            services.AddSingleton(MockAssinadorCdn.Object);

            // Evita que a reconciliação automática (1x/dia) rode em paralelo com os testes,
            // que invocam `ReconciliarArquivos` explicitamente e de forma determinística.
            var hostedService = services.SingleOrDefault(sd =>
                sd.ServiceType == typeof(IHostedService) && sd.ImplementationType == typeof(ReconciliacaoDiariaBackgroundService));
            if (hostedService is not null)
                services.Remove(hostedService);
        });
    }
}
