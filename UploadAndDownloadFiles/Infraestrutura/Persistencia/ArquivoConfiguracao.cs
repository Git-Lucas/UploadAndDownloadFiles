using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UploadAndDownloadFiles.Dominio;

namespace UploadAndDownloadFiles.Infraestrutura.Persistencia;

public sealed class ArquivoConfiguracao : IEntityTypeConfiguration<Arquivo>
{
    public void Configure(EntityTypeBuilder<Arquivo> builder)
    {
        builder.ToTable("Arquivos");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Chave)
            .IsRequired()
            .HasMaxLength(1024);

        builder.Property(a => a.NomeOriginal)
            .IsRequired()
            .HasMaxLength(1024);

        builder.Property(a => a.TamanhoDeclarado).IsRequired();
        builder.Property(a => a.IdUploadS3).HasMaxLength(512);
        builder.Property(a => a.Status).HasConversion<int>().IsRequired();
        builder.Property(a => a.TamanhoParte);
        builder.Property(a => a.QuantidadePartesEsperada);
        builder.Property(a => a.TamanhoReal);
        builder.Property(a => a.CriadoEm).IsRequired();
        builder.Property(a => a.AtualizadoEm).IsRequired();

        builder.HasIndex(a => a.Chave).IsUnique();
        builder.HasIndex(a => new { a.Status, a.AtualizadoEm });
    }
}
