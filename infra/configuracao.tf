locals {
  connection_string_banco = "Server=localhost,${var.porta_banco};Database=ArquivosDb;User Id=sa;Password=${random_password.sa.result};TrustServerCertificate=True"
}

resource "local_sensitive_file" "env" {
  filename        = abspath("${path.module}/aplicacao.env")
  file_permission = "0600"

  content = <<-EOT
    AWS_ACCESS_KEY_ID="${aws_iam_access_key.app.id}"
    AWS_SECRET_ACCESS_KEY="${aws_iam_access_key.app.secret}"
    AWS_REGION="${var.regiao_aws}"
    ArmazenamentoS3__NomeBucket="${aws_s3_bucket.arquivos.bucket}"
    CloudFront__DominioDistribuicao="${aws_cloudfront_distribution.arquivos.domain_name}"
    CloudFront__IdParDeChaves="${aws_cloudfront_public_key.cdn.id}"
    CloudFront__CaminhoChavePrivada="${local_sensitive_file.chave_privada_cdn.filename}"
    ConnectionStrings__ArquivosDb="${local.connection_string_banco}"
  EOT
}
