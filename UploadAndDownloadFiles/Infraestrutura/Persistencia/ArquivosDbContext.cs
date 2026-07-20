using Microsoft.EntityFrameworkCore;
using UploadAndDownloadFiles.Dominio;

namespace UploadAndDownloadFiles.Infraestrutura.Persistencia;

public sealed class ArquivosDbContext(DbContextOptions<ArquivosDbContext> options) : DbContext(options)
{
    public DbSet<Arquivo> Arquivos => Set<Arquivo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ArquivoConfiguracao());
    }
}
