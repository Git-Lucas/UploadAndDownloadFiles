// Fatia o arquivo com Blob.slice e envia via XMLHttpRequest, inteiramente fora do runtime .NET,
// para não estourar a memória do browser com arquivos de até TB. Usa XHR (em vez de fetch) porque
// só o XHR expõe o progresso de upload byte a byte (evento `upload.onprogress`), reportado ao .NET
// para alimentar a barra de progresso.
// Pré-requisito de infraestrutura: a configuração CORS do bucket S3 deve expor o header
// "ETag" (CORSRule ExposeHeaders), senão `getResponseHeader('ETag')` retorna null.

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

// Envia um corpo via PUT reportando o total de bytes já carregados para o slot informado.
// `relatorProgresso` é uma referência .NET (DotNetObjectReference) cujo método `Reportar`
// recebe (slot, bytesCarregados); `slot` identifica o trecho no agregado de progresso
// (0 para o arquivo inteiro no PUT único, número da parte no multipart).
function enviarComProgresso(url, corpo, headers, slot, relatorProgresso) {
    return new Promise((resolve, reject) => {
        const xhr = new XMLHttpRequest();
        xhr.open("PUT", url, true);

        if (headers) {
            for (const [nome, valor] of Object.entries(headers)) {
                xhr.setRequestHeader(nome, valor);
            }
        }

        if (relatorProgresso) {
            xhr.upload.onprogress = (evento) => {
                if (evento.lengthComputable) {
                    relatorProgresso.invokeMethodAsync("Reportar", slot, evento.loaded);
                }
            };
        }

        xhr.onload = () => {
            if (xhr.status >= 200 && xhr.status < 300) {
                resolve(xhr);
            } else {
                reject(new Error(`status ${xhr.status}`));
            }
        };
        xhr.onerror = () => reject(new Error("Falha de rede."));
        xhr.ontimeout = () => reject(new Error("Tempo de envio esgotado."));
        xhr.onabort = () => reject(new Error("Envio cancelado."));

        xhr.send(corpo);
    });
}

// O cabeçalho Content-Disposition faz parte da assinatura da URL pré-assinada e precisa ser
// repetido aqui exatamente como o backend o montou — alterá-lo ou omiti-lo causa
// SignatureDoesNotMatch. Ele grava o nome de exibição original no objeto, já que a chave do S3 é
// sanitizada para ASCII.
export async function enviarArquivoCompleto(idInputArquivo, url, contentDisposition, relatorProgresso) {
    const arquivo = obterArquivoSelecionado(idInputArquivo);
    if (!arquivo) {
        throw new Error("Nenhum arquivo selecionado.");
    }

    try {
        await enviarComProgresso(url, arquivo, { "Content-Disposition": contentDisposition }, 0, relatorProgresso);
    } catch (erro) {
        throw new Error(`Falha ao enviar arquivo (${erro.message}).`);
    }
}

export async function enviarParte(idInputArquivo, numeroParte, tamanhoParte, url, relatorProgresso) {
    const arquivo = obterArquivoSelecionado(idInputArquivo);
    if (!arquivo) {
        throw new Error("Nenhum arquivo selecionado.");
    }

    const inicio = (numeroParte - 1) * tamanhoParte;
    const fim = Math.min(inicio + tamanhoParte, arquivo.size);
    const parte = arquivo.slice(inicio, fim);

    let resposta;
    try {
        resposta = await enviarComProgresso(url, parte, null, numeroParte, relatorProgresso);
    } catch (erro) {
        throw new Error(`Falha ao enviar a parte ${numeroParte} (${erro.message}).`);
    }

    const etag = resposta.getResponseHeader("ETag");
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

// --- Monitor de conexão ---------------------------------------------------------------------
// Reflete o estado de rede do browser (navigator.onLine) e notifica o .NET a cada mudança, para
// que a UI mostre um indicador de online/offline. Útil para simular queda de internet (ex.: modo
// "Offline" do DevTools) e observar a retentativa de envio.

let monitorConexao = null;

function notificarConexao() {
    if (monitorConexao) {
        monitorConexao.invokeMethodAsync("AtualizarConexao", navigator.onLine);
    }
}

export function iniciarMonitorConexao(referenciaDotNet) {
    monitorConexao = referenciaDotNet;
    window.addEventListener("online", notificarConexao);
    window.addEventListener("offline", notificarConexao);
    return navigator.onLine;
}

export function pararMonitorConexao() {
    window.removeEventListener("online", notificarConexao);
    window.removeEventListener("offline", notificarConexao);
    monitorConexao = null;
}
