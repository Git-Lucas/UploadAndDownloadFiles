using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using UploadAndDownloadFiles.Api;
using UploadAndDownloadFiles.Api.Endpoints;
using UploadAndDownloadFiles.Aplicacao.CasosDeUso;
using UploadAndDownloadFiles.Aplicacao.Portas;
using UploadAndDownloadFiles.Infraestrutura.Aws;
using UploadAndDownloadFiles.Infraestrutura.Jobs;
using UploadAndDownloadFiles.Infraestrutura.Persistencia;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddDbContext<ArquivosDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ArquivosDb")));

builder.Services.Configure<OpcoesArmazenamentoS3>(builder.Configuration.GetSection(OpcoesArmazenamentoS3.SecaoConfiguracao));
builder.Services.Configure<OpcoesCloudFront>(builder.Configuration.GetSection(OpcoesCloudFront.SecaoConfiguracao));

// Acesso ao S3 via IAM Role (credenciais temporárias resolvidas pela cadeia padrão da AWS).
builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client());

builder.Services.AddScoped<IRepositorioArquivos, RepositorioArquivos>();
builder.Services.AddScoped<IArmazenamentoObjetos, ArmazenamentoObjetosS3>();
builder.Services.AddSingleton<IAssinadorCdn, AssinadorCdnCloudFront>();

builder.Services.AddScoped<RegistrarArquivo>();
builder.Services.AddScoped<ObterUrlDeParte>();
builder.Services.AddScoped<ListarPartesFaltantes>();
builder.Services.AddScoped<FinalizarUpload>();
builder.Services.AddScoped<ObterDownload>();
builder.Services.AddScoped<ReconciliarArquivos>();
builder.Services.AddHostedService<ReconciliacaoDiariaBackgroundService>();

builder.Services.AddExceptionHandler<ManipuladorDeExcecoes>();
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<ArquivosDbContext>().Database;
    if (database.IsRelational())
        await database.MigrateAsync();
}

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Sua API v1");
    });
}

app.UseHttpsRedirection();

app.MapStaticAssets();

app.MapArquivosEndpoints();

app.MapFallbackToFile("index.html");

await app.RunAsync();

public partial class Program;
