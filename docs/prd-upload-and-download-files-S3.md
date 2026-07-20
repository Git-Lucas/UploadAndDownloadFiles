# PRD — Upload e Download de Arquivos com S3

## 1. Visão geral
Sistema de **upload e download de arquivos** (de MB a TB) em que os bytes trafegam **direto entre o cliente e o Object Storage (S3)**, sem passar pelo backend. Resolve o transporte eficiente e resiliente de arquivos grandes, com **retomada após queda de rede**, **rastreamento de status** dos uploads e **distribuição global de download** via CDN com cache.

## 2. Problema e contexto
Arquivos de até vários TB não podem trafegar pelo backend (custo, memória, gargalo) nem depender de uma única requisição (uma queda de rede perderia horas de upload). Além disso, o conteúdo é **privado, porém baixado repetidamente por muitos usuários no mundo todo**, o que exige baixa latência global sem servir o bucket abertamente. É preciso, ainda, saber com confiança o estado real de cada upload, inclusive quando o cliente abandona o processo no meio.

## 3. Objetivos
- Permitir upload de arquivos de MB a TB direto para o S3, sem os bytes passarem pelo backend.
- Permitir **retomar** um upload interrompido a partir da última parte já enviada, sem reenviar o que já subiu.
- Manter no banco o **status confiável** de cada arquivo, reconciliando automaticamente casos abandonados.
- Entregar download de arquivos privados com **baixa latência global** e **redução de custo** via cache de CDN.

## 4. Fora de escopo
- **Autorização por arquivo:** qualquer usuário com acesso ao sistema pode baixar qualquer arquivo do bucket.
- **Validação de integridade por checksum** (conteúdo/tipo/tamanho por hash).
- **Verificação do tipo real pelos bytes** (magic bytes).
- **Substituição ou exclusão** de arquivos (os arquivos são imutáveis).
- **Pós-processamento** de arquivos (antivírus, thumbnail, transcode, indexação).
- Barra de **progresso fino** vinda do backend (progresso é responsabilidade do cliente).

## 5. Capacidades e requisitos

**C1 — Registro do arquivo.** Ao receber a primeira requisição, o sistema cria um registro com status `Pendente`, gerando internamente a chave (key) do objeto — nunca aceita a chave vinda do cliente. A chave é `{id}/{nome-sanitizado}`, restrita a ASCII (letras, dígitos, `.`, `_`, `-`); o nome de exibição original é preservado à parte, em `NomeOriginal`.

**C2 — Upload pequeno (< 100 MB).** O sistema fornece uma URL pré-assinada de PUT único para envio direto ao S3, com o `Content-Disposition` do nome original incluído na assinatura (o cliente o reenvia no PUT). Após o envio, o cliente confirma a conclusão; o sistema verifica a existência do objeto (`HeadObject`) e atualiza o status para `Completo`.

**C3 — Upload grande (≥ 100 MB).** O sistema inicia um multipart upload gravando ali o `Content-Disposition` do nome original, persiste o `uploadId`, e fornece URLs pré-assinadas **por parte, sob demanda**, com tamanho de parte adaptativo que respeita o limite de 10.000 partes até 5 TB.

**C4 — Retomada.** Após interrupção, o cliente pode reenviar apenas as partes faltantes; as partes já aceitas pelo S3 não são reenviadas.

**C5 — Expiração e reassinatura.** URLs pré-assinadas têm expiração curta; o cliente pode solicitar nova URL para uma parte quando a anterior expirar.

**C6 — Limite de tamanho no upload.** A política da URL aplica `Content-Length-Range` para impedir envio fora do tamanho declarado.

**C7 — Finalização.** O cliente informa a conclusão enviando os ETags das partes; o sistema valida via `CompleteMultipartUpload`, registra o tamanho real do objeto e atualiza o status para `Completo`. A operação é **idempotente**.

**C8 — Reconciliação automática.** Uma rotina diária inspeciona registros pendentes há mais de 24h e resolve o status real: PUT único via existência do objeto; multipart via listagem de partes, concluindo automaticamente quando todas as partes existem ou marcando `Incompleto` e abortando quando faltam partes.

**C9 — Limpeza de partes órfãs.** Partes de uploads incompletos são removidas automaticamente pelo storage após o prazo definido.

**C10 — Download.** O cliente solicita o download por ID; o sistema retorna uma URL assinada de CDN com expiração curta, servindo o arquivo com cache no edge, mantendo o bucket privado. O arquivo é salvo com o nome de exibição original, vindo do `Content-Disposition` gravado no objeto durante o upload — a chave do S3 é sanitizada para ASCII e não carrega esse nome.

## 6. Critérios de aceite

- **C1:** Requisição inicial cria registro `Pendente` com key gerada pelo servidor; requisição que tente informar a key é ignorada/rejeitada.
- **C2:** Arquivo < 100 MB é enviado com uma única URL de PUT, aparece no S3 e, após a confirmação do cliente, o registro passa para o status `Completo`.
- **C3:** Arquivo ≥ 100 MB gera `uploadId` persistido; o número de partes nunca excede 10.000 para arquivos de até 5 TB.
- **C4:** Após interromper e retomar, a soma das partes no S3 corresponde ao arquivo completo, sem reenvio das partes já aceitas.
- **C5:** Uma parte cuja URL expirou pode ser enviada com sucesso após o cliente solicitar nova URL.
- **C6:** Envio com tamanho fora do range declarado é rejeitado pelo S3.
- **C7:** Chamar a finalização duas vezes produz o mesmo resultado (status `Completo`) sem erro; o tamanho registrado no banco é o real do objeto.
- **C8:** Registro pendente > 24h com todas as partes presentes vira `Completo`; com partes faltando vira `Incompleto` e o multipart é abortado; PUT único existente vira `Completo`.
- **C9:** Partes de um upload abandonado deixam de existir (e de gerar custo) após o prazo da regra de ciclo de vida.
- **C10:** Download por ID retorna conteúdo correto via CDN; requisições repetidas do mesmo arquivo são servidas do cache (hit no edge); o bucket não é acessível diretamente sem a URL assinada.

## 7. Restrições e premissas

**Decisões técnicas fechadas (não reabrir):**
- Cliente **Blazor WebAssembly** com **JS interop** para o envio das partes; backend **Minimal API (.NET 10)**.
- Acesso à AWS via **IAM Role** (credenciais temporárias) — motivo pelo qual as URLs pré-assinadas têm vida curta e são reassinadas sob demanda.
- Storage **S3 privado**; keys imutáveis geradas pelo servidor.
- Limiar de **100 MB** entre PUT único e multipart; tamanho de parte adaptativo `max(100MB, arredondaCima(tamanho/9500))`.
- Reconciliação executada como **BackgroundService** no próprio backend, 1x/dia.
- Download via **CloudFront** com **OAC** e **Signed URL**; cache policy que ignora a query string na chave de cache.
- Banco **SQL Server** (EF Core).
- Estados de status: PUT único `Pendente → Completo`; multipart `Pendente → Enviando → Completo/Incompleto`.

**Premissas:**
- Arquivos são imutáveis (sem update/delete), o que dispensa invalidação de cache/versionamento de keys.
- Modelo de ameaça **não-adversarial**: cliente confiável, sem defesa contra mentira deliberada de conteúdo/tipo.
- Tamanho máximo por arquivo limitado a 5 TB (limite do S3).

**A definir:**
- Mecanismo de **autenticação de acesso ao sistema** (existe acesso ao sistema, mas o "como" não foi definido).
- **Valores concretos** de: expiração das URLs pré-assinadas (sugerido ~1h), expiração da Signed URL de download, prazo da regra de ciclo de vida para abortar multipart, e tolerância exata do `Content-Length-Range`.
- **Metas numéricas** de latência de download e de cache hit ratio esperado.
- **Política de retenção** dos arquivos (por quanto tempo permanecem disponíveis).
- Limites de **concorrência/paralelismo** e política de **retry/backoff** no cliente.
