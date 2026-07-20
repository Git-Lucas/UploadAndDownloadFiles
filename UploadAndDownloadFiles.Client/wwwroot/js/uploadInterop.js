// Fatia o arquivo com Blob.slice e envia via fetch, inteiramente fora do runtime .NET,
// para não estourar a memória do browser com arquivos de até TB.
// Pré-requisito de infraestrutura: a configuração CORS do bucket S3 deve expor o header
// "ETag" (CORSRule ExposeHeaders), senão `response.headers.get('ETag')` retorna null.

function obterArquivoSelecionado(idInputArquivo) {
    const input = document.getElementById(idInputArquivo);
    if (!input || input.files.length === 0) {
        return null;
    }
    return input.files[0];
}

export function obterTamanhoArquivo(idInputArquivo) {
    const arquivo = obterArquivoSelecionado(idInputArquivo);
    return arquivo ? arquivo.size : 0;
}

export function obterNomeArquivo(idInputArquivo) {
    const arquivo = obterArquivoSelecionado(idInputArquivo);
    return arquivo ? arquivo.name : null;
}

// O cabeçalho Content-Disposition faz parte da assinatura da URL pré-assinada e precisa ser
// repetido aqui exatamente como o backend o montou — alterá-lo ou omiti-lo causa
// SignatureDoesNotMatch. Ele grava o nome de exibição original no objeto, já que a chave do S3 é
// sanitizada para ASCII.
export async function enviarArquivoCompleto(idInputArquivo, url, contentDisposition) {
    const arquivo = obterArquivoSelecionado(idInputArquivo);
    if (!arquivo) {
        throw new Error("Nenhum arquivo selecionado.");
    }

    const resposta = await fetch(url, {
        method: "PUT",
        body: arquivo,
        headers: { "Content-Disposition": contentDisposition },
    });
    if (!resposta.ok) {
        throw new Error(`Falha ao enviar arquivo (status ${resposta.status}).`);
    }
}

export async function enviarParte(idInputArquivo, numeroParte, tamanhoParte, url) {
    const arquivo = obterArquivoSelecionado(idInputArquivo);
    if (!arquivo) {
        throw new Error("Nenhum arquivo selecionado.");
    }

    const inicio = (numeroParte - 1) * tamanhoParte;
    const fim = Math.min(inicio + tamanhoParte, arquivo.size);
    const parte = arquivo.slice(inicio, fim);

    const resposta = await fetch(url, { method: "PUT", body: parte });
    if (!resposta.ok) {
        throw new Error(`Falha ao enviar a parte ${numeroParte} (status ${resposta.status}).`);
    }

    const etag = resposta.headers.get("ETag");
    if (!etag) {
        throw new Error(`Resposta sem header ETag para a parte ${numeroParte}.`);
    }

    return etag;
}

export function iniciarDownload(url) {
    const link = document.createElement("a");
    link.href = url;
    link.rel = "noopener";
    document.body.appendChild(link);
    link.click();
    link.remove();
}
