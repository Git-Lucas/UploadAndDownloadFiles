variable "prefixo" {
  description = "Prefixo usado no nome dos recursos globalmente únicos (bucket, chave pública, key group, usuário IAM)."
  type        = string
  default     = "uploaddownload"
}

variable "regiao_aws" {
  description = "Região AWS onde os recursos são provisionados."
  type        = string
  default     = "us-east-1"
}

variable "perfil_aws" {
  description = "Perfil do AWS CLI usado pelo provider aws — deve resolver para a identidade administrativa terraform-admin, criada fora do Terraform."
  type        = string
  default     = "default"
}

variable "origens_cors" {
  description = "Origens locais da aplicação (Server, que hospeda o Client Blazor WASM) autorizadas a fazer PUT cross-origin no bucket."
  type        = list(string)
  default     = ["https://localhost:7056", "http://localhost:5069"]
}

variable "porta_banco" {
  description = "Porta do host onde o container do SQL Server publica a porta 1433."
  type        = number
  default     = 1433
}

variable "imagem_banco" {
  description = "Imagem Docker do SQL Server usada pelo container local."
  type        = string
  default     = "mcr.microsoft.com/mssql/server:2025-latest"
}

# Sufixo por ciclo, sem `keepers`: como o `destroy` remove também este recurso, cada novo
# `apply` nasce com um sufixo diferente, evitando colisão de nome com o ciclo anterior.
resource "random_string" "sufixo" {
  length  = 8
  special = false
  upper   = false
}

locals {
  nome_bucket        = "${var.prefixo}-arquivos-${random_string.sufixo.result}"
  nome_chave_publica = "${var.prefixo}-chave-cdn-${random_string.sufixo.result}"
  nome_key_group     = "${var.prefixo}-key-group-${random_string.sufixo.result}"
  nome_usuario_iam   = "${var.prefixo}-app-${random_string.sufixo.result}"
}
