# Portal DMO — Login e Administração

## Login

- O Portal é a única entrada nas aplicações.
- O formulário recebe email e palavra-passe e apresenta estados de validação, autenticação em curso e erro.
- O botão Mostrar/Ocultar altera apenas a visibilidade local da palavra-passe.
- Após autenticação, encaminhar sempre para o módulo `Job On`, landing page comum de todos os utilizadores autenticados.
- Não apresentar escolha manual de papel no Login.
- Mensagens de erro não devem confirmar se um email específico existe.
- Sessão, expiração e recuperação de palavra-passe usam o serviço de autenticação existente.

## Identidade no cabeçalho

- Nome: utilizador autenticado.
- Título/função: campo livre do perfil, gerido pelo Administrador.
- Exemplos: Metrologia, Chefe, Engenheiro, Responsável de qualidade.
- O título não concede permissões e não substitui template, papel ou capacidades.
- Se estiver vazio, apresentar apenas o nome.
- A shell carrega este valor uma vez e apresenta-o de forma consistente em todos os módulos.

## Administração — utilizadores

Operações:

- Criar utilizador.
- Editar nome, email e título/função.
- Associar template de acesso.
- Ativar ou desativar conta.
- Iniciar reset de palavra-passe.

Reset de palavra-passe:

- Exige confirmação explícita.
- Nunca mostra nem recupera a palavra-passe atual.
- Usa o fluxo seguro do serviço de autenticação.
- Regista administrador, utilizador afetado, data/hora e resultado da operação.

## Administração — aplicações

- Listar módulos disponíveis.
- Alterar disponibilidade e ordem.
- Associar módulos/capacidades aos templates de acesso existentes.
- Desativar em vez de eliminar quando existirem registos históricos.
- Autorizações são validadas também no comando/serviço; ocultar botões não constitui autorização.

## Comportamento das listas

- Usa o componente canónico `data-dmo-list`/`data-dmo-row` quando houver seleção.
- Um clique seleciona; duplo clique abre a edição quando esse fluxo for adotado.
- Pesquisa e estado filtram sem eliminar dados.
- Ações destrutivas ou de identidade exigem confirmação e feedback final.

## Separação de responsabilidades

- Perfil: nome e título/função visual.
- Template de acesso: módulos, capacidades e ordem. Para utilizadores operacionais, a landing page é Job On e não é configurável por utilizador; o Administrador puro é encaminhado diretamente para Administração.
- Estado da conta: possibilidade de autenticação.
- Administração: edição e auditoria destes dados.

## Auditoria global no Admin

- Todos os utilizadores autenticados geram eventos para cada ação de negócio relevante.
- Cada evento fica associado ao utilizador, módulo, ação, entidade, data/hora e resultado.
- A tab `Auditoria` permite consultar por ano e filtrar por utilizador, módulo, ação, resultado e período.
- Um clique seleciona o evento; duplo clique abre o detalhe.
- A vista usa paginação de 20, 40 ou 60 linhas e permite exportação anual autorizada.
- Não existe pontuação, ranking ou avaliação automática; a interface mostra apenas o registo factual.
- O contrato técnico completo está em `AUDITORIA_GLOBAL_HANDOFF.md`.

Não inferir permissões a partir do título/função apresentado no cabeçalho.

## Landing page e edição do Job On

- `Job On` é a landing page de Operador, Responsável e restantes utilizadores autenticados operacionais.
- O Administrador puro é a exceção: entra em `/admin`, fica na shell administrativa e não recebe `jobon.view` nem módulos operacionais.
- Todos os utilizadores operacionais recebem capacidade de consulta do Job On, Planeamento e Histórico dentro do âmbito autorizado.
- Apenas o papel/template técnico `Responsável` recebe `jobon.edit` e `jobon.configure`.
- Criar, duplicar, substituir ferramentas, alterar campos/datas, guardar revisão e gerir opções em Definições são operações de edição.
- Administração é a landing page e a única zona do Administrador puro.
- O título livre apresentado junto ao nome não concede `jobon.edit`; a autorização vem da capability validada pelo backend.
