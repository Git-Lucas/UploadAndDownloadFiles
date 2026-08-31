# UploadAndDownloadFiles

Aplicação Blazor WebAssembly (hosted) para upload (simples ou multipart) e download de arquivos
grandes usando Amazon S3 como armazenamento e Amazon CloudFront (Signed URLs) para o download.

- `UploadAndDownloadFiles/` — Server (Minimal API, .NET 10) — Domínio / Aplicação / Infraestrutura / Api
- `UploadAndDownloadFiles.Client/` — Blazor WebAssembly + JS interop (upload direto ao S3)
- `UploadAndDownloadFiles.Shared/` — DTOs e enums compartilhados entre Client e Server
- `UploadAndDownloadFiles.Testes.Unidade` / `UploadAndDownloadFiles.Testes.Integracao` — testes xUnit

Mais contexto de arquitetura e requisitos em `docs/prd-upload-and-download-files-S3.md`.

## Regras de negócio e arquitetura

### Por que os bytes não passam pelo backend

Arquivos vão de poucos MB a TB. Fazer o browser enviar para o backend e o backend reenviar para o
S3 dobraria tráfego, memória e tempo, e tornaria o backend o gargalo/ponto de falha de uploads
grandes. Por isso o Server nunca vê os bytes: ele só gera URLs pré-assinadas do S3, e o browser
troca dados diretamente com a AWS. O papel do backend é orquestrar (registrar, decidir o modo,
assinar URLs, verificar conclusão) e manter o status confiável no banco — não transportar dados.

### Limiar entre PUT único e multipart

- **< 100 MB** → **PUT único**: uma única URL pré-assinada de `PUT`.
- **≥ 100 MB** → **Multipart upload**: o S3 recebe o arquivo em partes independentes, cada uma
  com sua própria URL pré-assinada.

100 MB é também o **tamanho mínimo de parte** (`Arquivo.TamanhoMinimoParteEmBytes`), o que não é
coincidência: no multipart, o tamanho de cada parte é adaptativo —
`max(100 MB, arredondaCima(tamanhoDeclarado / 9500))` — para nunca ultrapassar o limite de
**10.000 partes** do S3 mesmo em arquivos de até 5 TB (o teto do S3). Usar exatamente o piso do
particionamento como limiar evita duas regras de tamanho independentes para manter sincronizadas.

Por que não usar multipart para tudo (já que o S3 aceita)? Multipart tem mais overhead de
orquestração — iniciar o upload, assinar N URLs, rastrear N ETags, finalizar com a lista de
partes — que só compensa quando o ganho (paralelismo e retomada) importa. Para arquivos pequenos
esse overhead é puro custo sem benefício perceptível, então PUT único é mais simples e mais rápido
de principio a fim.

### Fluxo de upload — do clique ao objeto no S3

1. **Registro (Client → Server).** O browser lê nome e tamanho do arquivo (via JS interop, sem
   ler o conteúdo) e chama `POST /api/arquivos`. O Server gera a *key* do objeto no formato
   `{id}/{nome-sanitizado}` — **nunca aceita a key vinda do cliente**, o que impede um cliente de
   sobrescrever ou apontar para o objeto de outro registro. O nome é sanitizado para ASCII porque
   a assinatura das CloudFront Signed URLs precisa bater byte a byte com a URL requisitada, e o
   browser percent-encoda acentos/espaços; o nome de exibição original fica preservado à parte
   (`NomeOriginal`) e devolvido depois no download via `Content-Disposition`.
2. **Decisão de modo (Server).** Com base no tamanho declarado, o Server decide `PutUnico` ou
   `Multipart` e já devolve ao Client tudo que ele precisa para o modo escolhido: uma URL de PUT
   assinada (PUT único) ou o tamanho de parte calculado (multipart, URLs de parte vêm depois, sob
   demanda). Nos dois casos o registro nasce com status `Pendente`.
3. **Envio (Client → S3, direto).**
   - *PUT único*: o browser faz um único `PUT` para a URL assinada, incluindo o
     `Content-Disposition` que fez parte da assinatura.
   - *Multipart*: o Client abre um `uploadId` no S3 (via Server), e para cada parte pede sob
     demanda `GET /multipart/{id}/partes/{n}/url` e faz o `PUT` da parte direto ao S3, até 4 partes
     em paralelo (`ConcorrenciaMaxima`). Pedir a URL de cada parte só na hora do envio (em vez de
     todas de uma vez no registro) é o que permite reassinar sem custo extra uma parte cuja URL
     expirou numa tentativa anterior.
4. **Confirmação (Client → Server → S3).**
   - *PUT único*: o Client chama `POST /put-unico/{id}/confirmar`; o Server faz `HeadObject` no S3
     para confirmar que o objeto existe antes de marcar `Completo` — a confirmação do cliente é só
     um gatilho, quem valida é o próprio storage.
   - *Multipart*: o Client soma os ETags recebidos de cada `PUT` de parte e chama
     `POST /multipart/{id}/finalizar`; o Server executa `CompleteMultipartUpload` no S3 (que
     valida os ETags) e grava o tamanho real do objeto. Essa finalização é **idempotente**:
     chamar duas vezes não falha, sempre retorna `Completo`.
5. **Retomada.** Se a conexão cai no meio de um multipart, `GET /multipart/{id}/partes/faltantes`
   diz exatamente quais partes o S3 ainda não tem (consulta feita pelo Client ao reiniciar o
   envio), e só essas são reenviadas — nunca as partes já aceitas. Essa consulta também é
   idempotente: um arquivo já `Completo` responde lista vazia em vez de erro, o que permite
   reexecutar o fluxo de envio sem tratamento especial para "já terminou".

### Máquina de estados

- **PUT único:** `Pendente → Completo`.
- **Multipart:** `Pendente → Enviando → Completo` (ou `Incompleto`, se abandonado).

`Enviando` só existe no multipart porque é o único modo com uma janela de tempo real entre o
início (abrir o `uploadId`) e o fim (`CompleteMultipartUpload`) em que o servidor sabe que um envio
está em progresso; no PUT único essa janela não é observável pelo backend.

### Reconciliação e limpeza (o que acontece quando o cliente some)

Um cliente pode fechar a aba, perder a rede ou nunca voltar. Sem uma rotina de auditoria, o
registro ficaria `Pendente`/`Enviando` para sempre, mesmo que o upload tenha (ou não) sido
concluído no S3. Por isso:

- Um `BackgroundService` roda **1x/dia** e resolve todo registro pendente há mais de 24h: PUT
  único vira `Completo` se o objeto existe no S3, ou fica pendente aguardando envio; multipart
  lista as partes no S3 — se todas presentes, finaliza como `Completo`; se faltam partes, marca
  `Incompleto` e **aborta** o multipart upload no S3 (o que libera as partes já enviadas).
- Mesmo sem essa rotina rodar a tempo, uma **lifecycle rule do bucket S3** aborta automaticamente
  qualquer multipart incompleto após 7 dias, removendo as partes órfãs e o custo de armazená-las —
  uma segunda camada de limpeza que não depende do backend estar no ar.

### Fluxo de download

O Client pede `GET /api/arquivos/{id}/download`; o Server assina uma **CloudFront Signed URL**
(expiração curta) e devolve. O browser baixa direto da CDN, que serve do cache de edge quando
possível — o bucket S3 nunca fica publicamente acessível (acesso só via **Origin Access Control**),
e o nome de exibição do arquivo salvo vem do `Content-Disposition` gravado no objeto durante o
upload, não da key (que é ASCII sanitizado). A cache policy do CloudFront ignora a query string na
chave de cache — necessário porque cada Signed URL tem uma assinatura distinta na query string, e
sem esse ajuste o mesmo arquivo nunca teria cache hit entre downloads diferentes.

### Por que essas escolhas, resumido

| Preocupação | Como é resolvida |
|---|---|
| Backend não pode ser gargalo/custo de bytes | Upload e download direto entre browser e AWS; backend só assina URLs |
| Cliente não pode escolher onde o objeto é gravado | Key gerada pelo servidor, nunca aceita do cliente |
| Queda de rede não pode custar horas de reenvio | Multipart com partes independentes + endpoint de partes faltantes |
| Credenciais AWS de vida curta (IAM Role) | URLs pré-assinadas de expiração curta, reassinadas sob demanda por parte |
| Cliente pode abandonar o processo | Reconciliação diária + lifecycle rule do S3 como segunda camada |
| Conteúdo privado, mas baixado globalmente | CloudFront com Signed URL + OAC, cache policy que ignora query string |
| Nome de exibição com acentos/espaços | Key ASCII sanitizada; nome original preservado via `Content-Disposition` |

## Pré-requisitos

- [Terraform](https://developer.hashicorp.com/terraform/install)
- [AWS CLI](https://docs.aws.amazon.com/cli/latest/userguide/getting-started-install.html)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) com integração WSL habilitada
  (usado pelo Terraform para subir o container do banco)
- [.NET SDK 10](https://dotnet.microsoft.com/download)

## Configuração (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "ArquivosDb": ""
  },
  "ArmazenamentoS3": {
    "NomeBucket": ""
  },
  "CloudFront": {
    "DominioDistribuicao": "",
    "IdParDeChaves": "",
    "CaminhoChavePrivada": ""
  }
}
```

| Chave | Descrição |
|---|---|
| `ConnectionStrings:ArquivosDb` | Connection string do SQL Server. As migrações do EF Core rodam automaticamente no startup (`Program.cs`). |
| `ArmazenamentoS3:NomeBucket` | Nome do bucket S3 privado usado para os objetos enviados. |
| `CloudFront:DominioDistribuicao` | Domínio da distribuição CloudFront (ex. `d111111abcdef8.cloudfront.net` ou domínio customizado), **sem** `https://`. |
| `CloudFront:IdParDeChaves` | `Key ID` da chave pública cadastrada no CloudFront (par de chaves usado para assinar as Signed URLs). |
| `CloudFront:CaminhoChavePrivada` | Caminho, no servidor onde a aplicação roda, do arquivo `.pem` com a chave privada correspondente. |

**Não commitar segredos** (connection string, caminho real da chave privada em produção). Para
desenvolvimento local, prefira `dotnet user-secrets` em vez de editar `appsettings.Development.json`
com valores reais:

```bash
cd UploadAndDownloadFiles
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:ArquivosDb" "Server=localhost;Database=ArquivosDb;Trusted_Connection=True;TrustServerCertificate=True"
dotnet user-secrets set "ArmazenamentoS3:NomeBucket" "meu-bucket-de-arquivos"
dotnet user-secrets set "CloudFront:DominioDistribuicao" "d111111abcdef8.cloudfront.net"
dotnet user-secrets set "CloudFront:IdParDeChaves" "K2JCJMDEHXQW5F"
dotnet user-secrets set "CloudFront:CaminhoChavePrivada" "/caminho/local/private_key.pem"
```

Em produção, as mesmas chaves podem ser definidas via variáveis de ambiente (o ASP.NET Core
substitui `:` por `__`):

```
ConnectionStrings__ArquivosDb=...
ArmazenamentoS3__NomeBucket=...
CloudFront__DominioDistribuicao=...
CloudFront__IdParDeChaves=...
CloudFront__CaminhoChavePrivada=...
```

## Provisionamento na AWS

A infraestrutura é provisionada via [Terraform](https://developer.hashicorp.com/terraform), em
`infra/`. Nenhuma etapa passa pelo console da AWS.

### Pré-condição (feita uma única vez)

Crie manualmente, fora do Terraform, uma identidade IAM administrativa (`terraform-admin`) com
permissão para criar bucket S3, distribuição CloudFront, chave pública/key group e usuário IAM, e
configure-a como profile padrão do AWS CLI. Ela fica fora do Terraform de propósito: se entrasse no
state, o `destroy` revogaria, no meio da própria execução, a credencial que está usando — deixando a
distribuição órfã, desabilitada e não deletável.

### Provisionar

```bash
cd infra
terraform init
terraform apply
```

Ao final, o comando gera `infra/aplicacao.env` com todos os valores que a aplicação precisa —
credenciais e região da AWS, nome do bucket, domínio da distribuição, id do par de chaves, caminho
da chave privada e connection string do banco. Carregue-o antes de rodar a aplicação:

```bash
set -a && source aplicacao.env && set +a
cd ../UploadAndDownloadFiles
dotnet run
```

O `apply` leva de **5 a 8 minutos** e é dominado pela propagação da distribuição CloudFront; o banco
sobe em paralelo, então não soma ao tempo total. Deixar de destruir mantém custo (distribuição
ativa) e uma credencial válida na conta.

### Destruir

```bash
cd infra
terraform destroy
```

> **Não carregue `infra/aplicacao.env` no mesmo shell usado para rodar `terraform destroy`.** Como
> variável de ambiente vence profile na cadeia de credenciais da AWS, isso trocaria o
> `terraform-admin` pela identidade restrita da aplicação no meio da destruição — que não tem
> permissão para remover bucket, distribuição, chave, key group ou usuário IAM.

Remove bucket (com os objetos dentro), distribuição, chave pública, key group, usuário IAM da
aplicação e o container do banco. A identidade `terraform-admin` não é afetada. Leva de **10 a 20
minutos**, dominado pela mesma propagação da CDN; um `Ctrl-C` nessa janela deixa recursos órfãos,
mas o state local sobrevive à interrupção e reexecutar o comando retoma de onde parou.

### O que é criado

- **Bucket S3** privado, CORS liberando `PUT` das origens locais da aplicação e expondo `ETag`
  (necessário para o client ler o `ETag` da resposta de cada parte do multipart — detalhes em
  `docs/infraestrutura-cloudfront-e-cors.md`), e lifecycle rule abortando multipart incompleto após
  7 dias (detalhes em `docs/infraestrutura-lifecycle-rule-s3.md`).
- **Distribuição CloudFront** com Origin Access Control e a bucket policy derivada da ARN da
  distribuição (sem transcrição manual), key group exigido para acesso e cache policy gerenciada
  `Managed-CachingOptimized`, que não inclui a query string na chave de cache — condição para que
  assinaturas distintas do mesmo arquivo compartilhem cache no edge.
- **Identidade IAM restrita** com a policy mínima usada pela aplicação, incluindo `s3:ListBucket` a
  nível de bucket — sem ele, consultar uma chave inexistente resulta em "acesso negado" em vez de
  "não encontrado", quebrando a reconciliação.
- **Container local do SQL Server**, com a senha do `sa` gerada e o provisionamento só reportando
  sucesso quando o banco já aceita conexões (as migrações do EF Core rodam no startup sem retry).

## Rodando localmente

```bash
cd UploadAndDownloadFiles
dotnet run
```

O Kestrel serve a API (`/api/arquivos/...`) e os arquivos estáticos do Blazor WASM na mesma origem
(sem necessidade de CORS entre Client e Server — CORS é necessário apenas no bucket S3, para o
upload direto do browser).
