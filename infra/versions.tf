terraform {
  required_version = ">= 1.9.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
    tls = {
      source  = "hashicorp/tls"
      version = "~> 4.3"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.9"
    }
    local = {
      source  = "hashicorp/local"
      version = "~> 2.9"
    }
    docker = {
      source  = "kreuzwerker/docker"
      version = "~> 4.5"
    }
  }
}

# Profile e region explícitos por variável: o objetivo é nunca depender de credenciais do
# ambiente do shell (ver R2 do design — carregar o .env gerado no mesmo shell de um `destroy`
# poderia sequestrar o provider com a credencial restrita da aplicação).
provider "aws" {
  profile = var.perfil_aws
  region  = var.regiao_aws
}
