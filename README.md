# UploadAndDownloadFiles

Aplicação Blazor WebAssembly (hosted) para upload (simples ou multipart) e download de arquivos
grandes usando Amazon S3 como armazenamento e Amazon CloudFront (Signed URLs) para o download.

- `UploadAndDownloadFiles/` — Server (Minimal API, .NET 10) — Domínio / Aplicação / Infraestrutura / Api
- `UploadAndDownloadFiles.Client/` — Blazor WebAssembly + JS interop (upload direto ao S3)
- `UploadAndDownloadFiles.Shared/` — DTOs e enums compartilhados entre Client e Server
- `UploadAndDownloadFiles.Testes.Unidade` / `UploadAndDownloadFiles.Testes.Integracao` — testes xUnit

Mais contexto de arquitetura e requisitos em `docs/PRD - Upload e Download de Arquivos com S3.md`.

## Pré-requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- Uma instância de SQL Server acessível (local, container ou gerenciada — ex. Azure SQL/Amazon RDS)
- Uma conta AWS com permissão para criar bucket S3, distribuição CloudFront, chave pública/key
  group e a role/usuário IAM usados pela aplicação

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

A aplicação **não provisiona infraestrutura AWS via código**. Os passos abaixo devem ser feitos uma
vez, via Console AWS, CLI ou Terraform, antes de rodar a aplicação contra uma conta real.

### 1. Criar o bucket S3

1. Console AWS → **S3** → **Create bucket** (bucket padrão/general-purpose — **não** um directory
   bucket do S3 Express One Zone, que não suporta lifecycle rules nem origem via OAC do CloudFront,
   usados nos passos 3 e 4).
2. Nome único (esse é o valor de `ArmazenamentoS3:NomeBucket`), região à sua escolha.
3. **Block all public access**: mantenha habilitado (padrão) — o bucket é privado; o acesso de
   leitura só acontece via CloudFront (OAC) e o de escrita via presigned URLs assinadas pelo backend.

### 2. Configurar CORS do bucket (upload multipart)

O client Blazor WASM lê o header `ETag` da resposta do `PUT` de cada parte (necessário para depois
chamar `CompleteMultipartUpload`). Por padrão, navegadores não expõem `ETag` em respostas
cross-origin, então o CORS do bucket precisa expô-lo explicitamente.

Bucket → **Permissions** → **Cross-origin resource sharing (CORS)**:

```json
[
  {
    "AllowedOrigins": ["https://<dominio-do-app>"],
    "AllowedMethods": ["PUT"],
    "AllowedHeaders": ["*"],
    "ExposeHeaders": ["ETag"]
  }
]
```

Em desenvolvimento local, `AllowedOrigins` deve ser o valor de `applicationUrl` configurado no
`launchSettings.json` do projeto Server (startup project).

Detalhes em `docs/infraestrutura-cloudfront-e-cors.md`.

### 3. Configurar a lifecycle rule (limpeza de multipart incompleto)

Bucket → **Management** → **Lifecycle rules** → **Create lifecycle rule**:

- **Abort Incomplete Multipart Uploads**: **7 dias**.

Ou via AWS CLI:

```bash
aws s3api put-bucket-lifecycle-configuration \
  --bucket <nome-do-bucket> \
  --lifecycle-configuration file://lifecycle-rule.json
```

com `lifecycle-rule.json`:

```json
{
  "Rules": [
    {
      "ID": "abortar-multipart-incompleto-7-dias",
      "Status": "Enabled",
      "Filter": {},
      "AbortIncompleteMultipartUpload": {
        "DaysAfterInitiation": 7
      }
    }
  ]
}
```

Detalhes em `docs/infraestrutura-lifecycle-rule-s3.md`.

### 4. Criar a distribuição CloudFront com Origin Access Control (OAC)

1. Console AWS → **CloudFront** → **Create distribution**.
2. **Origin domain**: selecione o bucket S3 criado no passo 1.
3. **Origin access**: **Origin access control settings (recommended)** → **Create new OAC** (aceite
   os padrões) → **Sign requests**.
4. Após criar a distribuição, o CloudFront mostra um aviso pedindo para atualizar a **bucket
   policy** do S3 — copie a policy sugerida (ela restringe o acesso ao principal
   `cloudfront.amazonaws.com` com uma condição pela ARN da distribuição) e aplique em
   **S3 → bucket → Permissions → Bucket policy**. Isso garante que o bucket continue privado e só
   seja acessível através dessa distribuição.
5. **Cache policy** do behavior: use a policy gerenciada **CachingOptimized** (ou uma custom com
   **Query strings: None**) — a assinatura da Signed URL (`Expires`/`Signature`/`Key-Pair-Id`) fica
   na query string e não deve fazer parte da chave de cache; assim, requests para o mesmo arquivo
   com assinaturas diferentes (de usuários diferentes) compartilham o mesmo cache no edge.
6. Anote o **domínio da distribuição** (ex. `d111111abcdef8.cloudfront.net`) — é o valor de
   `CloudFront:DominioDistribuicao`.

Detalhes em `docs/infraestrutura-cloudfront-e-cors.md`.

### 5. Gerar o par de chaves e habilitar Signed URLs (trusted key group)

O backend (`AssinadorCdnCloudFront`) assina URLs com política canônica (RSA-SHA1). É preciso gerar
um par de chaves RSA e cadastrar a chave pública no CloudFront:

```bash
openssl genrsa -out private_key.pem 2048
openssl rsa -pubout -in private_key.pem -out public_key.pem
```

1. Console AWS → **CloudFront** → **Key management** → **Public keys** → **Create public key**,
   colando o conteúdo de `public_key.pem`. Anote o **Key ID** gerado — é o valor de
   `CloudFront:IdParDeChaves`.
2. **Key management** → **Key groups** → **Create key group**, adicionando a chave pública criada.
3. Na distribuição, no behavior relevante → **Edit** → **Restrict viewer access**: **Yes**, usando
   **Trusted key groups (recommended)** → selecione o key group criado.
4. Guarde `private_key.pem` em local seguro e acessível pela aplicação (fora do controle de
   versão) — é o arquivo apontado por `CloudFront:CaminhoChavePrivada`. Em produção, prefira
   injetá-lo via secret manager/volume montado, nunca commitado no repositório.

### 6. Configurar acesso da aplicação ao S3 (IAM)

A aplicação usa a cadeia padrão de credenciais da AWS (`new AmazonS3Client()` sem credenciais
explícitas, em `Program.cs`) — em produção, o ideal é uma **IAM Role** anexada ao recurso de
computação (instance profile de EC2, task role de ECS, role de execução do App Runner/Lambda etc.).
Para desenvolvimento local, configure um usuário/perfil via `aws configure` ou variáveis de
ambiente (`AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_REGION`).

Política IAM mínima (substitua `<nome-do-bucket>`), cobrindo upload único, multipart, listagem de
partes, abort e leitura de metadados (`HeadObject`) usados pela aplicação e pela reconciliação:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "AcessoAosObjetosDoBucket",
      "Effect": "Allow",
      "Action": [
        "s3:PutObject",
        "s3:GetObject",
        "s3:ListMultipartUploadParts",
        "s3:AbortMultipartUpload"
      ],
      "Resource": "arn:aws:s3:::<nome-do-bucket>/*"
    },
    {
      "Sid": "ListagemDoBucket",
      "Effect": "Allow",
      "Action": "s3:ListBucket",
      "Resource": "arn:aws:s3:::<nome-do-bucket>"
    }
  ]
}
```

> **Importante:** o statement `s3:ListBucket` é obrigatório (recurso a nível de bucket, **sem** o
> sufixo `/*`). Sem ele, o `HeadObject` sobre uma chave **inexistente** retorna `403 Forbidden` em
> vez de `404 Not Found`, quebrando a reconciliação de arquivos que existem no banco mas não no
> bucket (a aplicação depende do 404 para marcar o registro como inválido/incompleto).

Crie a role/usuário em **IAM** → **Roles**/**Users** → **Create** → anexe uma policy customizada
com o JSON acima → associe a role ao recurso de computação (ou, em dev, configure as credenciais do
usuário localmente).

### 7. Banco de dados

Provisione uma instância SQL Server (local, container ou gerenciada) e informe a connection string
em `ConnectionStrings:ArquivosDb`. As migrações do EF Core (`Infraestrutura/Persistencia/Migracoes`)
são aplicadas automaticamente no startup da aplicação.

## Rodando localmente

```bash
cd UploadAndDownloadFiles
dotnet run
```

O Kestrel serve a API (`/api/arquivos/...`) e os arquivos estáticos do Blazor WASM na mesma origem
(sem necessidade de CORS entre Client e Server — CORS é necessário apenas no bucket S3, para o
upload direto do browser).
