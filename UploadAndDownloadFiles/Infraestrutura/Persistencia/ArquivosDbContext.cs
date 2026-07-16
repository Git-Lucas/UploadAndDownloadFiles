using Microsoft.EntityFrameworkCore;
using UploadAndDownloadFiles.Dominio;

namespace UploadAndDownloadFiles.Infraestrutura.Persistencia;

public sealed class ArquivosDbContext : DbContext
{
    public ArquivosDbContext(DbContextOptions<ArquivosDbContext> options) : base(options)
    {
    }

    public DbSet<Arquivo> Arquivos => Set<Arquivo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ArquivoConfiguracao());
    }
}
