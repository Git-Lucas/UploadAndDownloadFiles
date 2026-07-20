using Microsoft.EntityFrameworkCore;
using UploadAndDownloadFiles.Aplicacao.Portas;
using UploadAndDownloadFiles.Dominio;
using UploadAndDownloadFiles.Shared;

namespace UploadAndDownloadFiles.Infraestrutura.Persistencia;

public sealed class RepositorioArquivos(ArquivosDbContext contexto) : IRepositorioArquivos
{
    private readonly ArquivosDbContext _contexto = contexto;

    public Task<Arquivo?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _contexto.Arquivos.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task AdicionarAsync(Arquivo arquivo, CancellationToken cancellationToken = default) =>
        await _contexto.Arquivos.AddAsync(arquivo, cancellationToken);

    public async Task<IReadOnlyList<Arquivo>> ObterNaoFinalizadosAntesDeAsync(DateTime limite, CancellationToken cancellationToken = default) =>
        await _contexto.Arquivos
            .Where(a => (a.Status == StatusArquivo.Pendente || a.Status == StatusArquivo.Enviando) && a.AtualizadoEm < limite)
            .ToListAsync(cancellationToken);

    public Task SalvarAlteracoesAsync(CancellationToken cancellationToken = default) =>
        _contexto.SaveChangesAsync(cancellationToken);
}
