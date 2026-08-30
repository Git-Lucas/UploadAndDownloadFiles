resource "aws_s3_bucket" "arquivos" {
  bucket = local.nome_bucket

  # Ciclo é provisionar/testar/destruir: o bucket precisa poder ser destruído mesmo com objetos
  # de teste dentro, sem um esvaziamento manual antes do `terraform destroy`.
  force_destroy = true
}

resource "aws_s3_bucket_public_access_block" "arquivos" {
  bucket = aws_s3_bucket.arquivos.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_cors_configuration" "arquivos" {
  bucket = aws_s3_bucket.arquivos.id

  cors_rule {
    allowed_methods = ["PUT"]
    allowed_origins = var.origens_cors
    allowed_headers = ["*"]

    # Navegadores não expõem o header ETag em respostas cross-origin por padrão. O client
    # Blazor WASM lê o ETag da resposta de cada PUT de parte do multipart para depois chamar
    # CompleteMultipartUpload — sem essa exposição explícita, o upload multipart quebra.
    expose_headers = ["ETag"]
  }
}

resource "aws_s3_bucket_lifecycle_configuration" "arquivos" {
  bucket = aws_s3_bucket.arquivos.id

  rule {
    id     = "abortar-multipart-incompleto-7-dias"
    status = "Enabled"

    filter {}

    abort_incomplete_multipart_upload {
      days_after_initiation = 7
    }
  }
}
