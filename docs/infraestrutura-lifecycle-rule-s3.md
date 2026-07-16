# Lifecycle rule do bucket S3 (pré-requisito operacional)

Esta change não provisiona infraestrutura AWS via código/IaC (ver `design.md` — Non-Goals). O
bucket S3 usado pelo backend deve ter, **fora do código**, a seguinte lifecycle rule configurada
manualmente (console, AWS CLI ou Terraform, conforme a infraestrutura do ambiente):

- **Abort Incomplete Multipart Upload**: após **7 dias**, aborta uploads multipart incompletos e
  remove as partes órfãs já enviadas ao S3 (evita custo de armazenamento de partes de uploads
  nunca finalizados nem reconciliados).

Esse prazo (7 dias) é consistente com a janela de reconciliação diária (`ReconciliarArquivos`,
que atua sobre registros não finalizados há mais de 24h): mesmo que a reconciliação já tenha
chamado `AbortMultipartUpload` para os casos identificados como `Incompleto`, a lifecycle rule
funciona como uma rede de segurança para uploads multipart iniciados diretamente no S3 fora do
fluxo do backend, ou para partes que sobrarem por qualquer falha na reconciliação.

## Exemplo de configuração (AWS CLI)

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

```bash
aws s3api put-bucket-lifecycle-configuration \
  --bucket <nome-do-bucket> \
  --lifecycle-configuration file://lifecycle-rule.json
```
