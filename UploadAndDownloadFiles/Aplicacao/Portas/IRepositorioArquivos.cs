using UploadAndDownloadFiles.Dominio;

namespace UploadAndDownloadFiles.Aplicacao.Portas;

public interface IRepositorioArquivos
{
    Task<Arquivo?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AdicionarAsync(Arquivo arquivo, CancellationToken cancellationToken = default);

    /// <summary>Registros `Pendente`/`Enviando` com `AtualizadoEm` anterior a <paramref name="limite"/>.</summary>
    Task<IReadOnlyList<Arquivo>> ObterNaoFinalizadosAntesDeAsync(DateTime limite, CancellationToken cancellationToken = default);

    Task SalvarAlteracoesAsync(CancellationToken cancellationToken = default);
}
