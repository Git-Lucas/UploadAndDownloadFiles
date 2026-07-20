# CloudFront e CORS do S3 (pré-requisitos operacionais)

Como em `infraestrutura-lifecycle-rule-s3.md`, esta change não provisiona infraestrutura AWS via
código/IaC. Os itens abaixo devem ser configurados manualmente (console, AWS CLI ou Terraform).

## CloudFront (download, C10)

- **Origin Access Control (OAC)**: a distribuição deve acessar o bucket S3 via OAC, com a bucket
  policy do S3 permitindo apenas o principal do CloudFront (o bucket permanece privado; acesso
  direto sem a Signed URL deve ser negado).
- **Trusted key group**: o par de chaves usado por `AssinadorCdnCloudFront` (`Key-Pair-Id`/chave
  privada) deve estar associado a um trusted key group na distribuição, para que as Signed URLs
  geradas pelo backend sejam aceitas.
- **Cache Policy**: a política de cache deve **ignorar a query string** na chave de cache (`Query
  strings: None`), mantendo apenas o path como chave. Assim, `Expires`/`Signature`/`Key-Pair-Id`
  (que variam a cada assinatura) não fragmentam o cache — múltiplos usuários com URLs assinadas
  distintas para o mesmo arquivo compartilham o mesmo cache hit no edge.

## Chave do objeto e assinatura (C1/C10)

A Signed URL de política canônica assina um `Resource` que precisa bater **byte a byte** com a URL
que o navegador requisita. Como o navegador percent-encoda espaços e acentos, uma chave como
`{id}/Currículo Lucas.pdf` é assinada crua mas requisitada como `{id}/Curr%C3%ADculo%20Lucas.pdf`
— assinatura divergente, e o CloudFront responde `AccessDenied` (corpo `<Message>Access denied`,
sem `RequestId`, o que o distingue de um `Access Denied` vindo do S3/OAC).

Por isso `Arquivo.Registrar` sanitiza o nome ao compor a chave, mantendo-a ASCII-safe. O nome de
exibição original continua em `NomeOriginal` e é gravado no objeto como `Content-Disposition`
(`CabecalhoContentDisposition`), para que o download preserve o nome original mesmo com a chave
sanitizada. O cabeçalho é gravado em momentos diferentes conforme o modo:

- **PUT único:** entra na assinatura da URL pré-assinada, e o cliente precisa reenviá-lo
  literalmente no `fetch` (o backend devolve o valor em `RegistrarArquivoResponse`). Qualquer
  divergência resulta em `SignatureDoesNotMatch`.
- **Multipart:** é gravado no `InitiateMultipartUpload`, no início do upload — as partes não o
  carregam, então o envio das partes não muda.

O atributo `download` do `<a>` **não** substitui isso: navegadores o ignoram em URLs cross-origin,
e o domínio do CloudFront é sempre cross-origin em relação ao app.

## CORS do bucket S3 (upload multipart, C4/C7)

O cliente Blazor WASM lê o header `ETag` da resposta do `fetch` de cada `UploadPart` (necessário
para depois chamar `CompleteMultipartUpload`). Por padrão, navegadores **não expõem o header
`ETag`** em respostas cross-origin a menos que o CORS do bucket o exponha explicitamente:

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

O `AllowedHeaders: ["*"]` também é o que libera o `Content-Disposition` que o PUT único envia — é
um header não-simples, então o navegador dispara um preflight `OPTIONS` antes do PUT.

Sem `ExposeHeaders: ["ETag"]`, `enviarParte` (em `wwwroot/js/uploadInterop.js`) falha com "Resposta
sem header ETag para a parte N", mesmo que o S3 tenha aceitado a parte corretamente.
