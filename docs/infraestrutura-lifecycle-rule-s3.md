# Lifecycle rule do bucket S3

A infraestrutura é provisionada via Terraform (`infra/armazenamento.tf`, ver `README.md` —
"Provisionamento na AWS"). Este documento registra o porquê do prazo escolhido, que o código de
infraestrutura não expressa por si só.

O bucket S3 usado pelo backend tem uma lifecycle rule de **Abort Incomplete Multipart Upload** após
**7 dias**: aborta uploads multipart incompletos e remove as partes órfãs já enviadas ao S3 (evita
custo de armazenamento de partes de uploads nunca finalizados nem reconciliados).

Esse prazo (7 dias) é consistente com a janela de reconciliação diária (`ReconciliarArquivos`,
que atua sobre registros não finalizados há mais de 24h): mesmo que a reconciliação já tenha
chamado `AbortMultipartUpload` para os casos identificados como `Incompleto`, a lifecycle rule
funciona como uma rede de segurança para uploads multipart iniciados diretamente no S3 fora do
fluxo do backend, ou para partes que sobrarem por qualquer falha na reconciliação.
