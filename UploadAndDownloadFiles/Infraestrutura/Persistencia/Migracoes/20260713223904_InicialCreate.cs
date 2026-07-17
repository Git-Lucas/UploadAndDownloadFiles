using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UploadAndDownloadFiles.Infraestrutura.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class InicialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Arquivos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Chave = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    NomeOriginal = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    TamanhoDeclarado = table.Column<long>(type: "bigint", nullable: false),
                    IdUploadS3 = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TamanhoParte = table.Column<long>(type: "bigint", nullable: true),
                    QuantidadePartesEsperada = table.Column<int>(type: "int", nullable: true),
                    TamanhoReal = table.Column<long>(type: "bigint", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Arquivos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Arquivos_Chave",
                table: "Arquivos",
                column: "Chave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Arquivos_Status_AtualizadoEm",
                table: "Arquivos",
                columns: new[] { "Status", "AtualizadoEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Arquivos");
        }
    }
}
