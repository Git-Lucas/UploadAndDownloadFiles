output "dominio_distribuicao" {
  description = "Domínio da distribuição CloudFront usado para gerar as Signed URLs de download."
  value       = aws_cloudfront_distribution.arquivos.domain_name
}

output "nome_bucket" {
  description = "Nome do bucket S3 privado que armazena os arquivos enviados."
  value       = aws_s3_bucket.arquivos.bucket
}

output "caminho_arquivo_configuracao" {
  description = "Caminho absoluto do arquivo de ambiente gerado, para ser carregado antes de rodar a aplicação."
  value       = local_sensitive_file.env.filename
}

output "caminho_chave_privada" {
  description = "Caminho absoluto da chave privada usada para assinar as Signed URLs."
  value       = local_sensitive_file.chave_privada_cdn.filename
}

output "credenciais_aws" {
  description = "Access key e secret key da identidade IAM restrita da aplicação."
  sensitive   = true
  value = {
    access_key_id     = aws_iam_access_key.app.id
    secret_access_key = aws_iam_access_key.app.secret
  }
}

output "connection_string_banco" {
  description = "Connection string do SQL Server local, incluindo a senha gerada do sa."
  sensitive   = true
  value       = local.connection_string_banco
}
