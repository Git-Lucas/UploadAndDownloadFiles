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
// Decide se a internet está de fato alcançável e notifica o .NET a cada mudança de estado.
// `navigator.onLine` sozinho é enganoso: ele só vira `false` quando o SO não tem NENHUMA interface
// de rede ativa — com um adaptador virtual sempre "up" (ex.: vEthernet do WSL, Hyper-V, VPN),
// permanece `true` mesmo com o WiFi desligado. Por isso combinamos:
//   (A) uma sondagem ativa periódica a um recurso EXTERNO (heartbeat), que reflete a internet real;
//   (B) navigator.onLine como sinal rápido e definitivo de "sem rede" (ex.: modo Offline do DevTools).
// A URL do heartbeat é acessada em modo `no-cors`: não lemos o corpo/status, só nos importa se o
// fetch resolve (rede alcançável) ou rejeita (sem internet).

let monitorConexao = null;
let intervaloHeartbeat = null;
let urlHeartbeat = null;
let periodoHeartbeatMs = 5000;
let sondagemEmAndamento = false;
let ultimoReportado = null;

function reportarEstado(alcancavel) {
    if (alcancavel === ultimoReportado) {
        return;
    }
    ultimoReportado = alcancavel;
    if (monitorConexao) {
        monitorConexao.invokeMethodAsync("AtualizarConexao", alcancavel);
    }
}

async function sondar() {
    // navigator.onLine === false é definitivo: sem interface de rede, não há o que sondar.
    if (!navigator.onLine) {
        reportarEstado(false);
        return;
    }
    if (!urlHeartbeat || sondagemEmAndamento) {
        return;
    }

    sondagemEmAndamento = true;
    const controlador = new AbortController();
    const cancelamento = setTimeout(() => controlador.abort(), periodoHeartbeatMs);
    try {
        const separador = urlHeartbeat.includes("?") ? "&" : "?";
        await fetch(`${urlHeartbeat}${separador}_=${Date.now()}`, {
            method: "GET",
            mode: "no-cors",
            cache: "no-store",
            signal: controlador.signal,
        });
        reportarEstado(true);
    } catch {
        reportarEstado(false);
    } finally {
        clearTimeout(cancelamento);
        sondagemEmAndamento = false;
    }
}

function aoMudarRedeNavegador() {
    // Reage na hora aos eventos do SO; ao voltar "online", confirma com uma sondagem real, já que
    // ter interface de rede não garante acesso à internet.
    if (!navigator.onLine) {
        reportarEstado(false);
    } else {
        sondar();
    }
}

export function iniciarMonitorConexao(referenciaDotNet, url, periodoMs) {
    monitorConexao = referenciaDotNet;
    urlHeartbeat = url;
    periodoHeartbeatMs = periodoMs > 0 ? periodoMs : 5000;

    window.addEventListener("online", aoMudarRedeNavegador);
    window.addEventListener("offline", aoMudarRedeNavegador);

    sondar();
    intervaloHeartbeat = setInterval(sondar, periodoHeartbeatMs);

    return navigator.onLine;
}

// Dispara uma sondagem imediata (usada quando um envio falha) para confirmar rapidamente se é uma
// queda real de internet, sem esperar o próximo tick do heartbeat.
export function sondarAgora() {
    ultimoReportado = null;
    return sondar();
}

export function pararMonitorConexao() {
    window.removeEventListener("online", aoMudarRedeNavegador);
    window.removeEventListener("offline", aoMudarRedeNavegador);
    if (intervaloHeartbeat) {
        clearInterval(intervaloHeartbeat);
        intervaloHeartbeat = null;
    }
    monitorConexao = null;
    ultimoReportado = null;
}
