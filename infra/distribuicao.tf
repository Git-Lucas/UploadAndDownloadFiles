resource "aws_cloudfront_origin_access_control" "arquivos" {
  name                              = "${var.prefixo}-oac-${random_string.sufixo.result}"
  origin_access_control_origin_type = "s3"
  signing_behavior                  = "always"
  signing_protocol                  = "sigv4"
}

resource "tls_private_key" "cdn" {
  algorithm = "RSA"
  rsa_bits  = 2048
}

resource "aws_cloudfront_public_key" "cdn" {
  name        = local.nome_chave_publica
  encoded_key = tls_private_key.cdn.public_key_pem

  # A chave não pode ser deletada enquanto pertencer a um key group: sem isso, qualquer
  # alteração da chave (ex.: mudar o nome) trava o apply tentando destruir antes de recriar.
  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_cloudfront_key_group" "cdn" {
  name  = local.nome_key_group
  items = [aws_cloudfront_public_key.cdn.id]
}

data "aws_cloudfront_cache_policy" "otimizada" {
  name = "Managed-CachingOptimized"
}

resource "aws_cloudfront_distribution" "arquivos" {
  enabled = true

  # O apply só retorna com a distribuição implantada. `false` faria o comando parecer rápido,
  # mas o download falharia de forma intermitente nos minutos seguintes (D4 do design).
  wait_for_deployment = true

  origin {
    domain_name              = aws_s3_bucket.arquivos.bucket_regional_domain_name
    origin_id                = local.nome_bucket
    origin_access_control_id = aws_cloudfront_origin_access_control.arquivos.id
  }

  default_cache_behavior {
    allowed_methods        = ["GET", "HEAD"]
    cached_methods         = ["GET", "HEAD"]
    target_origin_id       = local.nome_bucket
    viewer_protocol_policy = "redirect-to-https"
    cache_policy_id        = data.aws_cloudfront_cache_policy.otimizada.id
    trusted_key_groups     = [aws_cloudfront_key_group.cdn.id]
  }

  restrictions {
    geo_restriction {
      restriction_type = "none"
    }
  }

  viewer_certificate {
    cloudfront_default_certificate = true
  }
}

# Substitui o passo manual do console que pede para copiar a bucket policy sugerida após criar
# a distribuição: a condição referencia a ARN da distribuição por atributo (.arn), não por um
# valor transcrito à mão.
data "aws_iam_policy_document" "oac" {
  statement {
    sid       = "PermitirLeituraViaOAC"
    effect    = "Allow"
    actions   = ["s3:GetObject"]
    resources = ["${aws_s3_bucket.arquivos.arn}/*"]

    principals {
      type        = "Service"
      identifiers = ["cloudfront.amazonaws.com"]
    }

    condition {
      test     = "StringEquals"
      variable = "AWS:SourceArn"
      values   = [aws_cloudfront_distribution.arquivos.arn]
    }
  }
}

resource "aws_s3_bucket_policy" "oac" {
  bucket = aws_s3_bucket.arquivos.id
  policy = data.aws_iam_policy_document.oac.json
}

resource "local_sensitive_file" "chave_privada_cdn" {
  content         = tls_private_key.cdn.private_key_pem
  filename        = abspath("${path.module}/chave-privada-cdn.pem")
  file_permission = "0600"
}
