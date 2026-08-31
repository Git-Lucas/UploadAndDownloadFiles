resource "aws_iam_user" "app" {
  name = local.nome_usuario_iam
}

data "aws_iam_policy_document" "app" {
  statement {
    sid    = "AcessoAosObjetosDoBucket"
    effect = "Allow"
    actions = [
      "s3:PutObject",
      "s3:GetObject",
      "s3:ListMultipartUploadParts",
      "s3:AbortMultipartUpload",
    ]
    resources = ["${aws_s3_bucket.arquivos.arn}/*"]
  }

  statement {
    # Recurso a nível de bucket, sem o sufixo "/*". Sem este statement, HeadObject sobre uma
    # chave inexistente retorna "acesso negado" em vez de "não encontrado", quebrando a
    # reconciliação de arquivos que existem no banco mas não no bucket.
    sid       = "ListagemDoBucket"
    effect    = "Allow"
    actions   = ["s3:ListBucket"]
    resources = [aws_s3_bucket.arquivos.arn]
  }
}

resource "aws_iam_user_policy" "app" {
  name   = "${local.nome_usuario_iam}-politica"
  user   = aws_iam_user.app.name
  policy = data.aws_iam_policy_document.app.json
}

resource "aws_iam_access_key" "app" {
  user = aws_iam_user.app.name
}
