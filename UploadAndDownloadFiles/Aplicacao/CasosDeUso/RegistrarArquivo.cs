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
        string? cabecalhoContentDisposition = null;

        if (arquivo.Modo == ModoUpload.PutUnico)
        {
            var upload = await _armazenamento.CriarUrlDeUploadUnicoAsync(arquivo.Chave, tamanhoDeclarado, arquivo.NomeOriginal, cancellationToken);
            urlUpload = upload.Url;
            cabecalhoContentDisposition = upload.CabecalhoContentDisposition;
        }
        else
        {
            var idUploadS3 = await _armazenamento.IniciarMultipartAsync(arquivo.Chave, arquivo.NomeOriginal, cancellationToken);
            arquivo.IniciarMultipart(idUploadS3);
        }

        await _repositorio.AdicionarAsync(arquivo, cancellationToken);
        await _repositorio.SalvarAlteracoesAsync(cancellationToken);

        return new RegistroArquivoResultado(
            arquivo.Id,
            arquivo.Modo,
            urlUpload,
            cabecalhoContentDisposition,
            arquivo.TamanhoParte,
            arquivo.QuantidadePartesEsperada);
    }
}
