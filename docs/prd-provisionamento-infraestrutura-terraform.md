# PRD — Provisionamento da Infraestrutura via Terraform

## 1. Visão geral
Codificação, em Terraform, de toda a infraestrutura hoje descrita como passo a passo manual na seção
"Provisionamento na AWS" do `README`. Substitui 7 etapas de console AWS por um único comando que
provisiona os recursos, gera as credenciais e entrega a aplicação pronta para rodar — e por um único
comando que destrói tudo ao fim do teste.

## 2. Problema e contexto
Para rodar a aplicação contra uma conta AWS real é preciso hoje executar manualmente sete etapas de
console (bucket, CORS, lifecycle rule, distribuição CloudFront com OAC, par de chaves e key group,
IAM, banco), copiar valores gerados pela AWS para a configuração da aplicação e, ao terminar,
lembrar de desfazer cada recurso na mão. Isso é lento, não reproduzível e propenso a erro — em
especial o passo da bucket policy do OAC, que instrui a copiar do console uma policy contendo a ARN
da distribuição.

Por ser um projeto de estudo, o ciclo esperado é **provisionar, testar a aplicação em funcionamento
e destruir**, repetido várias vezes. Nesse ciclo, o custo do processo manual é pago a cada iteração,
e o esquecimento de um recurso deixa custo e credencial ativos na conta.

## 3. Objetivos
- Reduzir o provisionamento completo a **um comando**, sem interação com o console AWS.
- Tornar o ambiente **reproduzível**: recriar do zero quantas vezes for necessário, com resultado
  equivalente e sem colisão de nomes.
- Tornar a destruição **completa e confiável**: nenhum recurso, credencial ou custo remanescente.
- Eliminar a **configuração manual da aplicação**: os valores gerados no provisionamento chegam à
  aplicação sem copiar e colar.
- Preservar em código o conhecimento operacional hoje só documentado em prosa (ex.: a necessidade do
  `s3:ListBucket` a nível de bucket).

## 4. Fora de escopo
- **Banco de dados na AWS (RDS).** O banco roda em container local.
- **Provisionamento de computação na AWS.** A aplicação continua sendo executada localmente; não há
  deploy do Server para EC2/ECS/App Runner.
- **State remoto, múltiplos ambientes e workspaces.** Um único ambiente, state local.
- **Pipeline de CI/CD** que execute o provisionamento automaticamente.
- **Domínio customizado e certificado ACM** para a distribuição.
- **Gestão de segredos em cofre** (Secrets Manager/Parameter Store) e rotação de credenciais.
- **Mudanças no comportamento da aplicação.** Nenhum requisito funcional de upload/download é
  alterado; muda apenas como a infraestrutura que ela consome passa a existir.

## 5. Capacidades e requisitos

**C1 — Provisionamento em um comando.** A partir de um repositório limpo e de credenciais AWS
administrativas já configuradas na máquina, um único comando cria toda a infraestrutura necessária
para a aplicação funcionar, sem nenhuma etapa em console.

**C2 — Armazenamento de objetos pronto para o fluxo de upload.** O provisionamento cria um bucket
privado, com acesso público bloqueado, configurado para: expor o header `ETag` em respostas
cross-origin de `PUT` para as origens da aplicação, e abortar automaticamente uploads multipart
incompletos após 7 dias.

**C3 — Distribuição de download privada.** O provisionamento cria a distribuição de CDN com acesso
ao bucket via Origin Access Control, aplica ao bucket a policy que restringe o acesso a essa
distribuição, e usa política de cache que **não** inclui a query string na chave de cache.

**C4 — Credencial de assinatura de URLs de CDN.** O provisionamento gera o par de chaves RSA,
registra a chave pública na CDN, associa-a a um key group exigido para acesso à distribuição, e
disponibiliza a chave privada em arquivo local legível apenas pelo dono.

**C5 — Identidade de acesso da aplicação.** O provisionamento cria uma identidade IAM dedicada, com
permissão restrita às operações que a aplicação usa (envio, leitura, listagem de partes de multipart,
aborto de multipart e leitura de metadados), incluindo a permissão de listagem a nível de bucket
necessária para que a consulta de objeto inexistente resulte em "não encontrado" e não em "acesso
negado", e emite as credenciais de acesso correspondentes.

**C6 — Banco de dados local pronto para uso.** O provisionamento cria e inicia o container do banco
de dados local, com senha gerada, e só reporta sucesso quando o banco está efetivamente aceitando
conexões — de modo que a aplicação possa ser iniciada em seguida sem falhar ao aplicar as migrações.

**C7 — Configuração da aplicação gerada automaticamente.** Ao final do provisionamento, existe um
arquivo local com todos os valores de configuração que a aplicação precisa (credenciais AWS e
região, nome do bucket, domínio da distribuição, identificador do par de chaves, caminho da chave
privada e connection string do banco), em formato carregável no ambiente antes de executar a
aplicação. Nenhum desses valores precisa ser digitado ou copiado manualmente.

**C8 — Recriação sem colisão.** Provisionar novamente após uma destruição funciona sem intervenção
manual, sem conflito de nomes globalmente únicos e sem reaproveitar credenciais antigas.

**C9 — Destruição completa.** Um único comando remove todos os recursos criados, incluindo o bucket
com objetos de teste dentro, a identidade IAM e suas credenciais, o par de chaves da CDN e o
container do banco. Após a execução, não resta na conta AWS recurso gerado por este provisionamento.

**C10 — Segredos fora do controle de versão.** Os artefatos sensíveis produzidos pelo provisionamento
(estado do Terraform, chave privada, arquivo de configuração com credenciais, arquivos de variáveis)
são ignorados pelo controle de versão.

**C11 — Documentação coerente com a realidade.** A documentação do projeto reflete o provisionamento
automatizado e não contém mais instruções manuais superadas nem afirmações de que a infraestrutura
não é provisionada via código.

## 6. Critérios de aceite

- **C1:** Em uma conta AWS sem nenhum recurso do projeto, o comando de provisionamento conclui com
  sucesso e nenhuma etapa do processo exige o console AWS.
- **C2:** O bucket criado nega acesso público; um `PUT` cross-origin a partir da origem da aplicação
  retorna o header `ETag` visível ao navegador; a regra de expiração de multipart incompleto está
  ativa com prazo de 7 dias.
- **C3:** O objeto **não** é acessível pela URL direta do bucket; é acessível pela URL assinada da
  CDN; duas requisições ao mesmo arquivo com assinaturas diferentes resultam em acerto de cache.
- **C4:** Uma URL assinada com a chave privada gerada é aceita pela distribuição; uma requisição sem
  assinatura é recusada.
- **C5:** Com as credenciais emitidas, a aplicação executa upload único, upload multipart, retomada,
  finalização e reconciliação sem erro de permissão; a consulta de um objeto inexistente resulta em
  "não encontrado".
- **C6:** Imediatamente após o comando de provisionamento retornar, iniciar a aplicação aplica as
  migrações do EF Core com sucesso, sem espera manual nem nova tentativa.
- **C7:** Carregando o arquivo gerado no ambiente e iniciando a aplicação, o fluxo completo de upload
  e download funciona sem que nenhum valor de configuração tenha sido editado à mão.
- **C8:** Executar destruição e provisionamento novamente, em sequência, conclui com sucesso e produz
  um ambiente funcional.
- **C9:** Após a destruição — com objetos de teste presentes no bucket antes dela — o comando conclui
  sem erro, e uma inspeção da conta não encontra bucket, distribuição, chave pública, key group nem
  identidade IAM da aplicação — a identidade administrativa que executa o Terraform permanece,
  por não ser gerada por ele; o container do banco não existe mais.
- **C10:** `git status` após um ciclo completo de provisionamento não lista nenhum arquivo sensível
  como não rastreado.
- **C11:** O `README` descreve o fluxo de um comando; nenhum documento do projeto afirma que a
  infraestrutura não é provisionada via IaC; os pré-requisitos listam as ferramentas realmente
  necessárias.

## 7. Restrições e premissas

**Decisões técnicas fechadas (não reabrir):**
- **Terraform** como ferramenta, em um **root module único** na pasta `infra/`, fora da solução .NET.
- **Nomes de arquivo canônicos** do Terraform (`versions.tf`, `variables.tf`, `outputs.tf` etc.);
  identificadores de recursos e variáveis em português, seguindo a convenção do repositório.
- **State local**, não remoto.
- **Container do banco gerenciado pelo próprio Terraform**, no mesmo root module dos recursos AWS —
  não por um arquivo Compose separado. Consequência aceita: qualquer operação do Terraform passa a
  exigir o daemon Docker no ar.
- **Par de chaves RSA e credenciais IAM gerados pelo Terraform**, aceitando que fiquem em texto claro
  no state. Justificativa: infraestrutura efêmera, destruída após cada teste; o ganho em passos
  manuais suprimidos supera o risco no contexto de um projeto de estudo.
- **Recriação do zero**, sem importar recursos criados manualmente antes.
- **Sem RDS.**
- **Identidade administrativa fora do state.** A identidade IAM que executa o Terraform é criada uma
  única vez, manualmente, e nunca é gerenciada pelo próprio Terraform — do contrário a destruição
  revogaria, no meio da própria execução, a credencial que está usando, deixando a distribuição de
  CDN órfã e desabilitada. Consequência aceita: essa é a única credencial de longa duração do
  projeto, e sua revogação é manual.
- **Espera pela propagação da distribuição de CDN.** O provisionamento só retorna quando a
  distribuição está implantada, em vez de retornar assim que ela é criada. Justificativa: sem a
  espera o comando parece rápido, mas o download falha de forma intermitente nos minutos seguintes,
  contrariando o C7.
- **Prefixo dos recursos:** `uploaddownload`, acrescido de um sufixo aleatório por ciclo nos recursos
  de nome globalmente único (bucket, chave pública da CDN, key group e identidade IAM da aplicação),
  que é o mecanismo que atende ao C8.
- **Duração esperada**, dominada pela distribuição de CDN e aceita como custo do ciclo:
  provisionamento entre 5 e 8 minutos, destruição entre 10 e 20 minutos (desabilitar a distribuição e
  aguardar a propagação precede a remoção). Não há meta de redução.

**Premissas:**
- Ferramentas presentes na máquina de desenvolvimento: Terraform, AWS CLI e Docker. Terraform,
  container do banco e a própria aplicação são todos executados dentro do WSL; o daemon Docker é
  provido pelo Docker Desktop com integração WSL, e precisa estar no ar inclusive para destruir os
  recursos da AWS.
- Existe na conta AWS uma identidade administrativa dedicada ao Terraform, criada uma única vez pelo
  console e configurada como profile padrão do AWS CLI na máquina. Este é o único passo manual
  irredutível, pois é o Terraform que cria a identidade restrita da aplicação.
- A identidade restrita da aplicação existe apenas no arquivo de configuração gerado (C7). Como as
  variáveis de ambiente têm precedência sobre o profile do AWS CLI, deixar de carregar esse arquivo
  faz a aplicação rodar com a identidade administrativa — e os critérios de aceite do C5 passariam
  mesmo com a política IAM incorreta.
- A aplicação é servida pelo projeto Server, que hospeda o Blazor WebAssembly; as origens usadas na
  configuração de CORS são as portas locais desse projeto.
- A aplicação não fixa a região da AWS em código, obtendo-a do ambiente — logo a região precisa
  constar na configuração gerada.
- O ciclo de vida é efêmero: provisionar, testar, destruir. Não há expectativa de persistência de
  dados entre ciclos, nem de uso da infraestrutura por terceiros.
- A destruição é o mecanismo de revogação das credenciais geradas; deixar de executá-la mantém
  credenciais válidas ativas na conta.
