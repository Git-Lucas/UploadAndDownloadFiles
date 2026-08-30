resource "docker_image" "sql_server" {
  name = var.imagem_banco
}

# Símbolos restritos aos que são seguros dentro de uma connection string ADO.NET (sem ";", "="
# nem aspas) e no comando de healthcheck (sem "$", "\"" nem crase), mantendo a exigência de
# complexidade do SQL Server (3 das 4 categorias de caractere).
resource "random_password" "sa" {
  length           = 20
  special          = true
  override_special = "!#%*+-_?"
  min_upper        = 2
  min_lower        = 2
  min_numeric      = 2
  min_special      = 2
}

resource "docker_container" "sql_server" {
  name  = "${var.prefixo}-sqlserver"
  image = docker_image.sql_server.image_id

  env = [
    "ACCEPT_EULA=Y",
    "MSSQL_SA_PASSWORD=${random_password.sa.result}",
    "SA_PASSWORD=${random_password.sa.result}",
  ]

  ports {
    internal = 1433
    external = var.porta_banco
  }

  # O "-C" é obrigatório: o Driver ODBC 18 exige TLS e o certificado do container é
  # self-signed. Verificado empiricamente contra a imagem do healthcheck (D5 do design):
  # senha errada retorna "Login failed for user 'sa'" com código de saída 1.
  healthcheck {
    test         = ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"$SA_PASSWORD\" -C -Q \"SELECT 1\""]
    interval     = "5s"
    timeout      = "3s"
    retries      = 20
    start_period = "10s"
  }

  # O apply só retorna com o banco aceitando conexões, para que as migrações do EF Core no
  # startup (sem retry) não falhem.
  wait         = true
  wait_timeout = 120
}
