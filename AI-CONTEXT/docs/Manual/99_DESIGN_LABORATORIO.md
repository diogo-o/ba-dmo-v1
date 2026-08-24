# PORTAL DMO — MANUAL TRANSVERSAL DA APLICAÇÃO

## Índice

- [1. Propósito do Manual](#1-propósito-do-manual)
- [2. Modelo Geral da Aplicação](#2-modelo-geral-da-aplicação)
- [3. Perfis Funcionais](#3-perfis-funcionais)
- [4. Módulos, Templates e Acesso](#4-módulos-templates-e-acesso)
- [5. Superfícies Transversais](#5-superfícies-transversais)
- [6. Shell Global](#6-shell-global)
- [7. Header](#7-header)
- [8. Navegação Principal](#8-navegação-principal)
- [9. Navegação Secundária / Tabs](#9-navegação-secundária--tabs)
- [10. Área Principal de Trabalho](#10-área-principal-de-trabalho)
- [11. Side Panel](#11-side-panel)
- [12. Componentes Globais](#12-componentes-globais)
- [13. Tabelas e Listas](#13-tabelas-e-listas)
- [14. Ordenação e Paginação](#14-ordenação-e-paginação)
- [15. Formulários e Validação](#15-formulários-e-validação)
- [16. Calendário](#16-calendário)
- [17. Modais e Confirmações](#17-modais-e-confirmações)
- [18. Feedback e Toasts](#18-feedback-e-toasts)
- [19. Loading / Empty / Error](#19-loading--empty--error)
- [20. Responsive / Mobile](#20-responsive--mobile)
- [21. Acessibilidade](#21-acessibilidade)
- [22. Design Tokens e CSS Global](#22-design-tokens-e-css-global)
- [23. JavaScript e Interações Globais](#23-javascript-e-interações-globais)
- [24. Responsabilidade Global vs Módulo](#24-responsabilidade-global-vs-módulo)
- [25. Autorização vs Apresentação](#25-autorização-vs-apresentação)
- [26. Ownership e Fronteiras](#26-ownership-e-fronteiras)
- [27. Como um Módulo se Integra no Shell](#27-como-um-módulo-se-integra-no-shell)
- [28. Consistência Entre Módulos](#28-consistência-entre-módulos)
- [29. História](#29-história)
- [30. Design Laboratório](#30-design-laboratório)
- [31. Regras Globais da Aplicação](#31-regras-globais-da-aplicação)
- [32. Regras Negativas](#32-regras-negativas)

## 1. Propósito do Manual

Este manual define como a Portal DMO funciona como uma aplicação única e coerente, e como os módulos funcionais de negócio operam dentro da sua shell partilhada. 

O documento estabelece os contratos de interface, as regras de navegação, os limites de responsabilidade e os padrões de interação que garantem que a aplicação se comporta como um sistema unificado, independentemente do módulo específico que o utilizador está a operar.

## 2. Modelo Geral da Aplicação

A Portal DMO é uma aplicação web Razor Pages construída sobre uma arquitetura limpa e organizada em torno de uma **shell única e persistente**. A navegação entre módulos e vistas mantém uma experiência contínua e coerente sem transformar os módulos em aplicações separadas.

A aplicação rege-se pela seguinte hierarquia de autoridade:
1. Clarificações explícitas e mais recentes do Owner.
2. Modelo canónico do `design-review`.
3. Contratos aceites do `design-coder` (Design System).
4. Evidências técnicas de implementação.

Os catálogos canónicos de módulos e páginas são a fonte única de verdade, validados no arranque da aplicação. A navegação ocorre em dois níveis: Nível 1 (Módulos/Tarefas) e Nível 2 (Vistas internas do módulo). Toda a auditoria cross-module é registada de forma imutável (`append-only`) na tabela global `audit_events`.

## 3. Perfis Funcionais

Existem exatamente **três** perfis funcionais na aplicação:

1. **Admin**
2. **Responsável**
3. **Operador / Controlador**

**Regras de Perfil:**
- Não existe um quarto perfil, nem perfis autónomos de "apenas consulta" ou "metrologia".
- "Reparador de turno" é um atributo de registo ou contexto operacional, não um perfil funcional de sistema.
- Títulos ou funções de texto livre (ex: "Chefe", "Engenheiro", "Metrologia") são apenas rótulos visuais exibidos no Header. Eles **nunca** concedem permissões ou criam novos perfis de acesso.
- O perfil determina **como** o utilizador interage e o que vê dentro das áreas que já tem autorização para aceder (a variante de experiência), mas não determina **quais** módulos lhe são atribuídos.

## 4. Módulos, Templates e Acesso

Existem exatamente **nove** módulos funcionais atribuíveis:

1. Job On
2. Controlo
3. Ferramentas
4. Armazém
5. Boquilhas
6. Reparação Interna
7. Reparação Externa
8. Tampões
9. Admin

**Modelo de Acesso:**
O acesso efetivo é determinado pela fórmula: `UTILIZADOR → PERFIL + TEMPLATES ASSOCIADOS → ACESSO A MÓDULOS`.

- **Templates:** Configurações reutilizáveis de acesso, geridas no módulo Admin, que definem *quais* módulos o utilizador pode ver e aceder.
- **Módulo não atribuído:** Se um utilizador não tem um módulo concedido via template, a entrada de navegação não é renderizada, o acesso direto via URL é bloqueado (fail-closed) e deep-links não contornam a barreira de segurança.
- **Admin:** O perfil Admin é não-operacional por defeito em relação aos módulos de chão de fábrica.
- **Landing:** O destino operacional padrão após o login é o **Job On** (quando aplicável). Um utilizador puramente administrativo aterrará no módulo **Admin** (`/admin`).

## 5. Superfícies Transversais

Nem tudo na Portal DMO é um módulo de negócio. A aplicação possui superfícies transversais que servem todo o sistema:

- **Login / Auth:** Superfície de entrada e gestão de sessão.
- **Users / Access:** Gestão de utilizadores, templates e auditoria (alojado no módulo Admin).
- **História:** Superfície de consulta transversal de auditoria (ver sec. 29).
- **Design Laboratório:** Superfície técnica de validação de design (ver sec. 30).

## 6. Shell Global

A Shell é o invólucro comum a todas as sessões autenticadas. Fornece a estrutura persistente e os serviços de contexto:

```text
APP SHELL
├─ Header Global          (Logo, identidade, utilizador, logout)
├─ Navegação Primária     (Módulos concedidos à esquerda, Admin à direita)
├─ Título da Vista Ativa  (Cabeçalho da página do módulo)
├─ Toolbar / Filtros      (Ações globais da vista atual)
├─ Área de Trabalho       (Conteúdo operacional do módulo)
├─ Side Panel             (Painel de contexto lateral, quando aplicável)
└─ Camada de Feedback     (Modais, Toasts, Loading)
```

A Shell fornece o frame; o módulo fornece o conteúdo e as regras de negócio.

## 7. Header

O Header global mantém a identidade da aplicação e o contexto do utilizador:

- **Logo:** Oficial, liga à raiz da aplicação ou landing operacional.
- **Identidade da Página:** Exibe o título do módulo ou vista atual (ex: "Portal DMO / Administração" em escopos administrativos).
- **Utilizador:** Exibe o nome autenticado e o título/função em texto livre.
- **Logout:** Acesso imediato à terminação de sessão.
- O Header é resolvido e renderizado server-side. Os módulos não injetam dados de identidade diretamente no HTML do Header.

## 8. Navegação Principal

A navegação de Nível 1 (módulos e tarefas principais) é derivada server-side da interseção entre os grants do utilizador e o catálogo canónico.

- **Operacionais:** Alinhados à esquerda.
- **Administração / Definições:** Alinhados à direita.
- Tabs não executam comandos de negócio; servem apenas para trocar a vista ativa dentro do módulo.
- A tab ativa é indicada por tipografia e cor de destaque (linha inferior).
- Módulos não concedidos não existem na barra de navegação.

## 9. Navegação Secundária / Tabs

A navegação de Nível 2 ocorre dentro do módulo (ex: "Registos", "Histórico", "Configurações" dentro de Ferramentas).

- O Nível 1 permanece sempre visível e acessível; entrar numa subárea nunca remove a capacidade de sair para outro módulo.
- As tabs internas podem variar dinamicamente consoante o perfil funcional do utilizador (ex: um Responsável vê tabs de aprovação que um Operador não vê).
- O design, espaçamento e estados das tabs são estritamente herdados do Design System global.

## 10. Área Principal de Trabalho

A área principal (`<main>`) hospeda o fluxo de trabalho do módulo. A anatomia canónica de uma página é:

1. **Page Header:** Título e descrição da vista.
2. **Ações Primárias / Filtros:** Botões de criação e cartões de pesquisa.
3. **Contexto / Métricas:** Sumários essenciais (ex: totais do dia).
4. **Conteúdo Principal:** Tabelas, listas ou formulários.
5. **Barra de Ações de Seleção:** Ações que dependem de um item selecionado na tabela.

Exceções a esta anatomia (ex: ecrãs de consulta rápida, master-detail) devem ser justificadas e documentadas no design do módulo. Nunca ocorre scroll horizontal ao nível da página inteira; o scroll confina-se a cartões ou tabelas específicas quando inevitável.

## 11. Side Panel

O Side Panel é um frame global para exibição de contexto lateral contínuo (ex: estado da máquina atual, lote em processamento).

- **Comportamento Global:** Fixo no desktop (base escura, foco no estado operacional atual); transforma-se em gaveta (drawer) ou bloco recolhível em mobile.
- **Interação:** O clique num cartão do Side Panel abre o registo associado, conforme definido pela regra do módulo. Conflitos são exibidos no próprio cartão, sem recurso a pop-ups bloqueantes.
- **Responsabilidade do Módulo:** O módulo fornece os dados, o significado dos cartões e as regras de clique. O Side Panel nunca substitui a navegação principal.

## 12. Componentes Globais

A aplicação possui uma biblioteca de componentes partilhados. O contrato global define a interação e apresentação; o módulo define a semântica.

| Componente | Contrato Global | O Módulo Fornece |
|---|---|---|
| **Botões** | Estados (hover, focus, disabled, loading). Bloqueio de duplo-clique. | Verbo, posição, variante semântica. |
| **Campos (Inputs)** | Label acima, erro abaixo, limites de casas decimais. | Formatos, validações de domínio. |
| **Pills / Status** | Texto + cor semântica. Cor nunca é o único indicador. | Significado do estado de negócio. |
| **Dropdowns** | Pesquisa incremental, navegação por teclado, estado "Sem resultados". | Catálogos e fontes de dados. |
| **Alertas** | Inline junto ao problema. Info, Success, Warning, Danger. | Mensagem e ação de recuperação. |

## 13. Tabelas e Listas

O contrato de interação com tabelas e listas é estritamente padronizado:

- **Um clique:** Seleciona a linha (estado visual explícito e `aria-selected`).
- **Duplo clique:** Abre o registo, detalhe ou editor (o módulo define o que "abrir" significa).
- **Ações de Seleção:** Botões que atuam sobre a linha selecionada residem fora da tabela, na barra de ações (toolbar). Não há botões de ação repetidos em cada linha.
- **Mudanças de Contexto:** Aplicar um filtro ou mudar o tamanho da página limpa qualquer seleção invisível e retorna à página 1.

**Contrato de Teclado:**
- `Space`: Seleciona a linha focada.
- `Enter`: Abre a linha focada.
- `Arrow Up` / `Arrow Down`: Move o foco entre as linhas.
- `Home` / `End`: Move o foco para a primeira / última linha da página.
- Mover o foco **não** seleciona automaticamente a linha.
- `Ctrl+Enter` não faz parte do contrato canónico.

## 14. Ordenação e Paginação

**Contrato de Ordenação (Sorting):**
- Apenas colunas explicitamente marcadas como ordenáveis permitem ordenação.
- Primeiro clique: Ascendente (ASC).
- Segundo clique: Descendente (DESC).
- Cliques subsequentes alternam entre ASC e DESC.
- Apenas uma ordenação primária pode estar ativa por vez.
- Mudar a ordenação reinicia a paginação para a página 1.
- A ordenação aplica-se à consulta completa (server-side), não apenas aos dados da página visível.
- O módulo define as colunas ordenáveis e a ordenação padrão.
- A ordenação não é persistida entre sessões por defeito.

**Paginação:**
- Tamanhos padrão: 20, 40, 60 registos por página.
- Exibe o total de registos e a página atual. Limites desativados quando não aplicável.

## 15. Formulários e Validação

- **Estrutura:** Label (sempre visível) → Controlo → Helper → Mensagem de Erro.
- **Validação:** Erros de obrigatórios e formato são exibidos imediatamente abaixo do campo. Formulários extensos exibem um sumário de erros no topo.
- **Formulários Extensos:** Abrem inline (cartão expansível) com o foco no primeiro campo. O botão "Cancelar" limpa rascunhos. O estado "dirty" (alterado) exige confirmação se o utilizador tentar sair sem guardar.
- **Layout:** Agrupamento por tarefa. Números alinhados com unidades. Cancelar precede Guardar.

## 16. Calendário

Existe um único componente de calendário partilhado por todos os módulos.

- Semana começa à segunda-feira.
- Um clique seleciona/filtra o dia.
- Mudar de mês **nunca** auto-seleciona uma data.
- Dias com registos exibem um ponto discreto; o dia atual é marcado distintamente.
- A ação "Mostrar todas" remove o filtro de data.
- O módulo é responsável por fornecer as datas que contêm registos e o significado de negócio da seleção.

## 17. Modais e Confirmações

- A aplicação **não** utiliza APIs nativas do browser (`alert`, `confirm`, `prompt`).
- **Uso:** Confirmação de ações destrutivas, perda de alterações (dirty-state), reset de passwords ou ações rápidas focadas.
- Formulários extensos ou criação de registos complexos ocorrem inline, não em modais.
- **Comportamento:** Focus trap ativo. `Escape` ou clique no backdrop pode fechar o modal quando não existe trabalho por guardar; se existir estado dirty, aplica-se a confirmação de perda de alterações. Ao fechar, o foco regressa ao elemento que abriu o modal. A submissão bloqueia cliques repetidos. Falhas de rede mantêm o modal aberto e preservam o input do utilizador.

## 18. Feedback e Toasts

| Situação | Componente | Persistência |
|---|---|---|
| Campo inválido | Field Error (Inline) | Até corrigir |
| Múltiplos erros | Summary + Field Errors | Até corrigir |
| Sucesso simples | Toast (`aria-live`) | Temporário (auto-dismiss) |
| Save falhado | Erro inline persistente + Toast opcional | Inline até correção/novo intento; Toast temporário |
| Load falhado | Error State com Retry | Persistente |
| Comando em curso | Loading no trigger (spinner) | Até conclusão |
| Ação destrutiva | Confirmation Dialog | Bloqueante |

O sucesso só é emitido após autorização, validação e persistência confirmada. Ações de consulta ou filtro nunca geram toasts de sucesso.

## 19. Loading / Empty / Error

Os estados de ausência de dados ou processamento são distintos e não podem ser confundidos:

- **Loading:** Skeleton ou spinner que preserva o layout para evitar saltos visuais (CLS).
- **Empty:** Mensagem explicativa e botão de "próximo passo" (ex: "Criar primeiro registo"). Sem áreas tracejadas vazias.
- **No Results:** Resultado de um filtro ativo. Botão para "Limpar filtros".
- **Error:** Falha de rede ou servidor. Mensagem clara e botão de "Tentar novamente" (Retry).
- **Forbidden:** Acesso negado. Mensagem de contacto ao administrador.

A apresentação visual é global; a condição que dispara o estado é determinada pelo serviço do módulo.

## 20. Responsive / Mobile

- **Breakpoints de referência:** 1200px, 980px, 720px.
- **Reflow:** Grelhas reordenam-se antes de reduzir o tamanho do texto. Campos essenciais são preservados.
- **Tabelas:** Em viewports pequenas, tabelas largas ganham scroll horizontal interno (confina-se ao cartão), nunca à página inteira.
- **Side Panel:** Transforma-se em drawer (gaveta) deslizante ou bloco recolhível.
- **Touch Targets:** Áreas de toque cumprem os mínimos de acessibilidade para dispositivos móveis.

## 21. Acessibilidade

A aplicação cumpre o alvo mínimo **WCAG AA**:

- Foco visível e lógico em todos os controlos interativos.
- Operação completa por teclado em tabs, listas, dropdowns, calendários e menus.
- Uso de `aria-label` em botões de ícone único.
- Uso de `aria-expanded` e `aria-controls` em secções expansíveis.
- Uso de `aria-live` para feedback dinâmico (toasts, erros de validação).
- Respeito pela preferência de movimento reduzido (`prefers-reduced-motion`).
- A cor nunca é o único meio de transmitir informação ou estado.

## 22. Design Tokens e CSS Global

A consistência visual é garantida por tokens CSS (`--dmo-*`) que definem cores, espaçamentos, raios, sombras e tipografia.

- **Load Order:** Tokens → Foundation → Components → Layout → Utilities.
- **CSS de Módulo:** Os módulos apenas escrevem CSS de **composição** (grelhas, ordem, larguras específicas).
- É proibido criar novos códigos hexadecimais quando existe um token de cor de marca ou semântico.
- É proibido criar implementações paralelas de componentes que já existem no Design System global.
- Estilos inline (`style="..."`) para design são proibidos.

## 23. JavaScript e Interações Globais

Os scripts globais (`dmo-interactions.js`, `dmo-calendar.js`) fornecem os contratos de interação (seleção de listas, foco, navegação por teclado, calendários).

- O JavaScript global **não contém lógica de negócio**.
- Os módulos injetam dados e significados através de atributos `data-*` (ex: `data-dmo-list`, `data-record-dates`) que os scripts globais leem para orquestrar a interface.
- A lógica de domínio reside estritamente no servidor ou em scripts de módulo isolados e específicos.

## 24. Responsabilidade Global vs Módulo

| Preocupação | Shell Global / Design System | Módulo de Negócio |
|---|---|---|
| **Header e Navegação** | Frame, renderização, acessibilidade. | Disponibilidade (via grants), títulos. |
| **Tabelas e Listas** | Interação, teclado, paginação, estados. | Dados, colunas, significado de "abrir". |
| **Formulários** | Layout, validação visual, focus trap. | Campos, regras de domínio, submissão. |
| **Side Panel** | Frame, responsividade, drawer. | Dados contextuais, regras de clique. |
| **Autorização** | Infraestrutura, fail-closed, routing. | Verificações funcionais específicas. |
| **Propriedade (Ownership)**| Nenhuma. | Domínio e regras de negócio. |

## 25. Autorização vs Apresentação

- A navegação reflete o acesso, mas **ocultar um botão ou tab não é autorização**.
- Toda a ação sensível ou acesso a dados exige validação server-side (fail-closed).
- Deep-links para módulos não atribuídos resultam numa página de "Acesso Negado" (Forbidden), não num erro 404 genérico.
- Atributos HTML como `data-can-edit="true"` servem apenas para orquestrar a apresentação visual (CSS/JS); a autoridade real reside no servidor.

## 26. Ownership e Fronteiras

Princípios transversais de domínio que regem a interação entre módulos:

- **Aviso ≠ Bloqueio:** Um aviso de sistema não bloqueia automaticamente um fluxo, a menos que a regra de negócio o dite explicitamente.
- **Estado Técnico ≠ Estado Físico:** Um movimento físico no armazém não deve mutar silenciosamente o estado técnico de uma ferramenta sem o workflow adequado.
- **UI Entry Point ≠ Ownership:** Iniciar uma operação a partir da UI de um módulo não transfere a propriedade do registo master.
- **Job On:** É dono do contexto de produção e dos snapshots históricos. Edições posteriores em masters não reescrevem snapshots de Job On já fechados.
- **Ferramentas:** É dono do master de todas as ferramentas (incluindo o master de BQ).
- **Armazém:** É dono das localizações físicas e movimentos gerais de stock.
- **Boquilhas:** Regista movimentos de reparação externa de BQ. **Nunca** utiliza o fluxo de Reparação Interna.
- **Reparação Interna / Externa:** RI gere ocorrências internas (CM/MF). RE gere batches externos. Partilham um diretório canónico de reparadores.
- **Tampões:** É dono das suas quantidades, configurações e movimentos específicos.
- **História:** Lê e apresenta factos de auditoria, mas não é dona dos eventos de origem.
- **Admin:** Gere utilizadores, templates e superfícies de auditoria. Catálogos de sistema no Admin são apenas para visualização, não concedem acesso direto.

## 27. Como um Módulo se Integra no Shell

1. **Declaração:** O módulo é registado no Catálogo Canónico (ID, rota, capabilities).
2. **Acesso:** O servidor resolve os templates do utilizador e injeta os grants na sessão.
3. **Renderização:** A página Razor do módulo renderiza dentro do layout partilhado (`_Layout`).
4. **Componentes:** O módulo utiliza as classes `dmo-*` e os eventos canónicos.
5. **Anatomia:** O módulo organiza o seu conteúdo seguindo a anatomia de página padrão.
6. **Autorização:** O módulo aplica as suas verificações de domínio sobre a infraestrutura de acesso já validada pela Shell.

Os módulos não são aplicações separadas; são extensões de domínio que habitam a mesma casa.

## 28. Consistência Entre Módulos

**O que é igual em todos os módulos:**
Header, modelo de navegação, comportamento de botões, interação de listas (clique/teclado), formulários, calendário, modais, responsividade, acessibilidade e tokens de design.

**O que varia por módulo:**
Campos de negócio, workflows específicos, permissões de perfil, dados do Side Panel e mensagens de validação de domínio.

Um módulo nunca pode alterar silenciosamente as convenções básicas de interação (ex: criar um calendário próprio ou mudar o comportamento de duplo-clique em tabelas).

## 29. História

A **História** é uma superfície transversal de leitura (`read-only`).

- Apresenta e consulta eventos de auditoria cross-module (`audit_events`).
- **Não** é um módulo funcional atribuível.
- **Não** é dona dos eventos de origem; os módulos operacionais mantêm a propriedade dos seus factos.
- O acesso à História é restringido pelo escopo efetivo dos módulos concedidos ao utilizador via templates: o utilizador só vê eventos pertencentes às áreas a que tem acesso. Eventos administrativos continuam sujeitos às permissões específicas de auditoria.

## 30. Design Laboratório

O **Design Laboratório** é uma superfície técnica transversal de validação e regressão do Design System.

- Permanece permanentemente disponível para validação visual, de acessibilidade e de responsividade.
- **Não** é um módulo funcional de negócio.
- **Não** faz parte da navegação operacional diária dos utilizadores de chão de fábrica.
- Não possui registos de negócio, workflows, ou persistência de dados (usa apenas dados de demonstração).
- Screenshots e baselines de regressão visual complementam a sua função, mas não a substituem como ferramenta de desenvolvimento e auditoria de UI.

## 31. Regras Globais da Aplicação

- A Shell fornece o frame e a infraestrutura; o módulo fornece o conteúdo e o domínio.
- O acesso é determinado por Templates; o Perfil determina a experiência.
- A ocultação visual de elementos não substitui a autorização server-side.
- Um clique seleciona, duplo clique abre.
- O teclado é cidadão de primeira classe em todas as grelhas e formulários.
- A ordenação é server-side e reinicia a paginação.
- O sucesso só é reportado após persistência confirmada.
- Os snapshots históricos (Job On) são imutáveis face a edições posteriores de masters.

## 32. Regras Negativas

- **Não** criar design systems paralelos ou concorrentes dentro de módulos.
- **Não** simular persistência de negócio (fake persistence) no Design Laboratório.
- **Não** colocar lógica de negócio ou domínio dentro de scripts globais de UI.
- **Não** confiar na ocultação de CSS/HTML como mecanismo de segurança ou autorização.
- **Não** utilizar APIs nativas de bloqueio (`alert`, `confirm`, `prompt`).
- **Não** permitir scroll horizontal ao nível da página inteira (o scroll confina-se a componentes).
- **Não** inventar novos perfis funcionais fora dos 3 canónicos.
- **Não** promover a superfície de História ou Design Laboratório a módulos operacionais atribuíveis.
- **Não** reabrir áreas internas (ex: Peso, Pegamentos) como módulos de topo independentes.

## Implementation Pointers

### Design reference

- Design system: `AI-CONTEXT\design-coder\0_GLOBAL_DESIGN_SYSTEM.md` (v2.7); `AI-CONTEXT\design-coder\90_DESIGN_LAB_02_DESIGN_SYSTEM.md`; implementation contract: `AI-CONTEXT\design-coder\90_DESIGN_LAB_03_IMPLEMENTATION_CONTRACT.md`; lab README: `AI-CONTEXT\design-coder\90_DESIGN_LAB_00_README.md`.
- Technical map: `maps\17_DESIGN_LABORATORIO.md`; guard tests: `maps\05_TESTS.md` (DesignSystemGuardTests, ShellAndCalendarGuardTests).

### Relevant implementation areas

- Web / Razor: shell pages `_Layout`, `_Header`, `_Navigation`; global scripts `dmo-interactions.js`, `dmo-calendar.js`. The global patterns (shell, header, navigation, tabs, side panel, tables, forms, modals, toasts, loading/empty/error, responsive, accessibility) must be implemented once globally and reused by every module — never duplicated per module (see §12, §31, §32).