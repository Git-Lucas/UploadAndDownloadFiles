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

Sem `ExposeHeaders: ["ETag"]`, `enviarParte` (em `wwwroot/js/uploadInterop.js`) falha com "Resposta
sem header ETag para a parte N", mesmo que o S3 tenha aceitado a parte corretamente.
