using UploadAndDownloadFiles.Aplicacao.Modelos;
using UploadAndDownloadFiles.Aplicacao.Portas;
using UploadAndDownloadFiles.Dominio;
using UploadAndDownloadFiles.Shared;

namespace UploadAndDownloadFiles.Aplicacao.CasosDeUso;

public sealed class RegistrarArquivo(IRepositorioArquivos repositorio, IArmazenamentoObjetos armazenamento)
{
    private readonly IRepositorioArquivos _repositorio = repositorio;
    private readonly IArmazenamentoObjetos _armazenamento = armazenamento;

    public async Task<RegistroArquivoResultado> ExecutarAsync(string nomeArquivo, long tamanhoDeclarado, CancellationToken cancellationToken = default)
    {
        var arquivo = Arquivo.Registrar(nomeArquivo, tamanhoDeclarado);

        string? urlUpload = null;

        if (arquivo.Modo == ModoUpload.PutUnico)
        {
            urlUpload = await _armazenamento.CriarUrlDeUploadUnicoAsync(arquivo.Chave, tamanhoDeclarado, cancellationToken);
        }
        else
        {
            var idUploadS3 = await _armazenamento.IniciarMultipartAsync(arquivo.Chave, cancellationToken);
            arquivo.IniciarMultipart(idUploadS3);
        }

        await _repositorio.AdicionarAsync(arquivo, cancellationToken);
        await _repositorio.SalvarAlteracoesAsync(cancellationToken);

        return new RegistroArquivoResultado(
            arquivo.Id,
            arquivo.Modo,
            urlUpload,
            arquivo.TamanhoParte,
            arquivo.QuantidadePartesEsperada);
    }
}
