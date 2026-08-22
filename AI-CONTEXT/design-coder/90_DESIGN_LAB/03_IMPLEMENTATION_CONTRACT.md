# BA DMO — Design Implementation Contract

Versão da auditoria: 1.0  
Data: 2026-08-16  
Âmbito: exclusivamente design, UI e comportamento de interação  
Estado: auditoria final antes do planeamento da aplicação

## 0. Resultado executivo

O pacote comunica bem a direção visual, a hierarquia operacional e grande parte dos comportamentos. No entanto, **ainda não constitui por si só uma fundação executável totalmente fechada para uma fresh build**.

As duas causas principais são:

1. `DMO_DESIGN_SYSTEM.md` é mais completo do que `dmo-design-system.css`; vários componentes descritos ainda não existem como implementação canónica;
2. os mockups continuam a conter `<style>` local, valores hardcoded e variantes próprias de botões, calendários, tabelas e layouts.

Este documento transforma o pacote numa auditoria utilizável pelo agente de planeamento. Não altera regras de negócio nem confirma relações funcionais que pertencem a Verified Knowledge.

### Legenda

- `DEFINED`: suficiente e inequívoco para implementar.
- `PARTIAL`: direção existente, mas falta contrato ou implementação canónica.
- `MISSING`: decisão necessária antes de criar o componente/fundação.
- `READY`: módulo visualmente especificado sem bloqueio próprio conhecido.

## 1. Objetivo e fronteira

O futuro agente deve conseguir usar o pacote para decidir:

- composição da shell;
- navegação visual;
- anatomia das páginas;
- componentes universais;
- estados e interações globais;
- composição específica dos módulos;
- ordem de construção visual.

Este contrato **não** define:

- base de dados;
- entidades ou agregados;
- cálculos;
- permissões reais;
- invariantes de domínio;
- ownership funcional;
- arquitetura técnica C#;
- integração com serviços externos.

Sempre que a UI dependa destes pontos, o documento usa `FUNCTIONAL INPUT REQUIRED`.

## 2. Design System Foundation

### 2.1 Auditoria dos fundamentos

| Fundamento | Estado | Evidência atual | Falta para fechar |
|---|---|---|---|
| Design tokens | PARTIAL | conjunto em `DMO_DESIGN_SYSTEM.md` e `dmo-design-system.css` | tokens tipográficos, layers, breakpoints, border widths, ícones e motion |
| Cores da marca | DEFINED | escala 950–050 | documentar contraste calculado por combinação |
| Backgrounds/surfaces | DEFINED | page, card, subtle | estado elevado/overlay poderia ser alias explícito |
| Cores de texto | DEFINED | principal, muted, on-color | texto disabled e link não têm token próprio |
| Cores semânticas | DEFINED | success, warning, danger, pending e soft | info depende da marca; confirmar alias `info` |
| Borders | PARTIAL | cor global definida | falta escala/token de espessura e estilo para focus/divider/strong |
| Radius | DEFINED | control, card, modal, pill | nenhum gap bloqueante |
| Shadows | DEFINED | card, menu e modal | falta elevação de sticky header/sidebar se necessária |
| Spacing scale | PARTIAL | 4, 8, 12, 16, 20, 24 e 32px | falta convenção de uso por componente e aliases para page/gutter/section |
| Sizing scale | PARTIAL | control 40, compact 34, header 76, tabs 52, sidebar 276 | falta altura de row, icon sizes, max page widths, modal widths e touch target token |
| Typography family | DEFINED | Inter + system fallback | falta regra de carregamento/fallback quando Inter não está disponível |
| Font sizes | PARTIAL | tabela por papéis, alguns intervalos | transformar intervalos em tokens exatos; evitar 23–24 e 12–13 ambíguos |
| Font weights | PARTIAL | pesos por papel | falta escala/token e limitar pesos disponíveis |
| Line heights | MISSING | apenas corpo implícito 1.45 e alguns valores locais | definir body, heading, label, button e compact |
| Letter spacing | MISSING | não definido | definir especialmente uppercase/table headers |
| Z-index/layers | MISSING | valores locais 80/100 nos mockups/CSS | criar escala base, sticky, dropdown, overlay, modal, toast |
| Breakpoints | PARTIAL | 1200, 980 e 720px citados | criar tokens/mixins/contrato exato por breakpoint |
| Responsive behavior | PARTIAL | princípios gerais e algumas media queries | grid, page gutter, sidebar/drawer e action bars precisam de padrões executáveis |
| Animation/transitions | PARTIAL | `150ms ease` | definir propriedades permitidas, duração normal, modal/dropdown e reduced motion |
| Icon sizing | MISSING | apenas botão de ícone 34–40px | definir ícone 16/20/24, stroke e alinhamento |
| Density/compactness | PARTIAL | alturas de campos, rows e tabelas | falta matriz compact/regular por componente e comportamento mobile |
| Focus ring | PARTIAL | halo azul descrito e usado em fields | falta token e aplicação a todos os controlos |
| Page width/gutters | MISSING | layouts variam nos HTML | definir max-width ou fluid layout, gutters desktop/tablet/mobile |

### 2.2 Tokens que devem ser adicionados antes dos módulos

Requer `DESIGN DECISION REQUIRED`:

```css
:root {
  /* borders */
  --dmo-border-width: 1px;
  --dmo-border-width-strong: 2px;

  /* typography: valores finais a confirmar */
  --dmo-font-family: Inter, ui-sans-serif, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
  --dmo-font-size-xs: ...;
  --dmo-font-size-sm: ...;
  --dmo-font-size-md: ...;
  --dmo-font-size-lg: ...;
  --dmo-font-size-xl: ...;
  --dmo-line-height-tight: ...;
  --dmo-line-height-normal: ...;
  --dmo-line-height-relaxed: ...;

  /* layout */
  --dmo-page-gutter-desktop: ...;
  --dmo-page-gutter-tablet: ...;
  --dmo-page-gutter-mobile: ...;
  --dmo-page-max-width: ...;
  --dmo-row-height: ...;
  --dmo-touch-target: 44px;

  /* icons */
  --dmo-icon-sm: 16px;
  --dmo-icon-md: 20px;
  --dmo-icon-lg: 24px;

  /* layers */
  --dmo-z-base: ...;
  --dmo-z-sticky: ...;
  --dmo-z-dropdown: ...;
  --dmo-z-overlay: ...;
  --dmo-z-modal: ...;
  --dmo-z-toast: ...;

  /* motion */
  --dmo-duration-fast: 150ms;
  --dmo-duration-normal: ...;
  --dmo-ease-standard: ...;
}
```

Os `...` não devem ser inventados pelo coder. Precisam de uma pequena passagem de decisão visual antes da construção dos componentes.

## 3. CSS Architecture Contract

### 3.1 Estrutura obrigatória

```text
GLOBAL TOKENS
    ↓
GLOBAL COMPONENTS
    ↓
GLOBAL LAYOUT/SHELL
    ↓
MODULE COMPOSITION ONLY
```

### 3.2 Regras normativas

- Um componente universal é implementado uma vez.
- CSS de módulo só organiza composição, grid, ordem e larguras específicas.
- CSS de módulo não redefine cor, raio, sombra, tipografia, botão, field, table, modal, calendar ou feedback.
- Sem hexadecimal, `rgb()`, dimensão visual ou sombra hardcoded quando existe token.
- Sem `style="..."` para design.
- Sem `<style>` nas páginas finais.
- Sem `site.css` legacy a competir com o design system.
- Sem segunda implementação do mesmo componente por módulo.
- Markup usa classes/componentes canónicos.
- Exceções são raras, nomeadas, documentadas e testadas.
- Load order é único: tokens → components → layout → module composition.

### 3.3 Organização recomendada, independente da framework

```text
styles/
  dmo-tokens.css
  dmo-foundation.css
  dmo-components.css
  dmo-layout.css
  dmo-utilities.css
  modules/
    <module>-layout.css
```

### 3.4 Estado real do pacote

`PARTIAL`.

- 12 de 18 HTML ligam `dmo-design-system.css`; 6 não ligam a folha global.
- 17 de 18 HTML contêm pelo menos um `<style>` local.
- existem dezenas de `style="..."` inline;
- vários mockups redefinem `.btn`, fields, tables e calendários;
- `dmo-design-system.css` implementa apenas parte do inventário normativo.

Portanto, os HTML são referências de composição e prioridade, não código CSS a copiar.

## 4. Universal Component Inventory

As colunas `Size` e `States` referem o contrato esperado; `Estado` mede a definição atual no pacote.

| Componente | Purpose / variants | Size | States e interaction | Content / quando usar | Referência | Estado |
|---|---|---|---|---|---|---|
| Button | ação; primary, secondary semântico, danger, success, compact | 34/40px; 44 touch | default filled; hover/focus inverted; pressed; disabled; loading | verbo curto; não usar para navegação passiva | todos os mockups | DEFINED |
| Icon Button | fechar, menu, setas; neutral/danger | 34–40; ícone por definir | default, hover, focus, pressed, disabled | exige `aria-label`; não usar sem ícone reconhecível | modal, calendar, pagination | PARTIAL |
| Input | texto/pesquisa/número | 40; compact 34 | default, hover, focus, readonly, disabled, error, success | label sempre; placeholder só exemplo | formulários globais | PARTIAL |
| Textarea | notas/justificação | min-height por contexto | mesmos estados de field; resize controlado | apenas texto realmente longo | Peso, Job On, movimentos | PARTIAL |
| Select | lista curta e estável | 40/34 | default, hover, focus, open, selected, disabled, error | não usar para pesquisa longa | filtros e campos | PARTIAL |
| Custom Dropdown | pesquisa contextual/longa | anchor 40; menu limitado | open, hover option, active option, selected, empty, loading, error | teclado completo; não autoescolher primeiro | Pegamentos, Job On | PARTIAL |
| Checkbox | multi-select/confirmação | target 40/44 | unchecked, hover, focus, checked, indeterminate, disabled, error | batch/checklist; não substituir seleção de row | saídas programadas/verificações | PARTIAL |
| Radio | escolha exclusiva pequena | target 40/44 | unchecked, hover, focus, checked, disabled | 2–4 opções visíveis | Peso legado | PARTIAL |
| Segmented Selector | escolha exclusiva operacional | 40–48 | selected filled; unselected outline; hover/focus; disabled | tipos/linhas/opções de alta frequência | BQ, ferramentas, tampões | DEFINED |
| Date Input | introdução de data | 150–180 × 40 | default, focus, invalid, disabled, readonly | data isolada; formato localizado | vários formulários | PARTIAL |
| Date Picker | seleção assistida de data | associado ao date input | open, today, selected, disabled, keyboard | quando input de data necessita calendário | forms | MISSING |
| Calendar | filtrar/planear por dia | 300–340 desktop | month nav, today, selected, has-record, empty day, disabled, hover, focus | um componente global | Boquilhas, Peso, Job On | PARTIAL |
| Card | agrupar contexto/tarefa | padding 16–20; radius 12 | default, hover/selectable, selected, disabled/loading | não criar cartão por valor sem contexto | toda a app | DEFINED |
| Expandable Card | editor/filtros inline | largura do bloco origem | closed/open/loading/error/dirty | criação/edição extensa; não modal grande | BQ, forms | PARTIAL |
| List | coleção selecionável | fluida | loading, ready, empty, error | itens não tabulares | referências/controlo | PARTIAL |
| List Row | seleção/abertura | row 40–46 | hover, focus, selected, disabled | clique seleciona; duplo abre | listas globais | DEFINED |
| Table | dados tabulares | row 40–46 | loading/empty/error; sticky head | colunas comparáveis | históricos/movimentos | PARTIAL |
| Table Row | seleção/abertura/movimento | 40–46 | hover/focus/selected; entrada/saída subtis | sem botões repetidos na row | históricos | DEFINED |
| Filter Bar | filtros permanentes/expansíveis | fields 40 | collapsed/open, dirty/applied, loading | uma fonte para resumo+lista | históricos | PARTIAL |
| Search | filtro incremental | 40; flex | empty typing, loading, results, no result, error | dizer o que pesquisa | todos os módulos | PARTIAL |
| Tabs | vistas do módulo | 52px bar | default, hover, focus, active, disabled/hidden | não executar comandos | todos autenticados | DEFINED |
| Badge | metadata curta/tipo | compacto | default/neutral | não usar como estado normal sem necessidade | listas/cards | PARTIAL |
| Status | estado semântico com texto | pill compacto | pending/success/warning/error/inactive | nunca apenas cor | Peso, reparação | DEFINED |
| Alert | ação/risco persistente | inline no contexto | info/success/warning/error | problema que exige atenção | BQ/Armazém | PARTIAL |
| Toast | confirmação não bloqueante | conteúdo curto | enter/show/exit; success/error/info | sucesso breve; erro persistente também inline | mockups | PARTIAL |
| Modal | tarefa rápida/focada | sm/md; width final ausente | closed/open/loading/error/dirty | não usar para formulário extenso | forms/actions | PARTIAL |
| Confirmation Dialog | consequência difícil de reverter | small/medium | open, processing, error | verbo específico; nunca prompt nativo | delete/close/reset | PARTIAL |
| Context Menu `…` | ações contextuais | largura ao conteúdo | closed/open/hover/focus/disabled | só ações válidas; fecha Escape/outside | side panel BQ | PARTIAL |
| Tooltip | explicar ícone/estado truncado | auto | delayed open, hover/focus, close | ajuda curta; não esconder regra crítica | icon buttons | MISSING |
| Pagination | navegar dados paginados | arrows 36; select 40 | default/hover/focus/disabled/loading | total + página X/Y + 20/40/60 | listas globais | DEFINED |
| Empty State | ausência de dados/resultados | compacto no conteúdo | initial/no-results/no-data | causa + próximo passo | todos | PARTIAL |
| Loading State | aguardar dados/comando | preserva layout | initial, refresh, action | skeleton ou texto; evitar layout shift | transversal | PARTIAL |
| Error State | falha de load/save | inline/card | recoverable/fatal/field | explicar próxima ação e Retry quando possível | transversal | PARTIAL |
| Sidebar | contexto operacional persistente | 276 desktop | default/collapsed/mobile drawer/conflict | estado atual; não analytics | Boquilhas/shell | PARTIAL |
| Header | identidade global | 76 desktop | default/compact mobile | logo, page title, subtitle, user/profile | componentes globais | DEFINED |
| Page Header | título da vista e descrição | fluido | default/action/loading optional | não repetir tab sem contexto | módulos | PARTIAL |
| Section Header | título de cartão/secção | 15–16px | default/action attached | uma hierarquia clara | formulários/cards | PARTIAL |
| Form Group | campos relacionados | grid responsiva | default/error/disabled | legend/título quando necessário | forms | PARTIAL |
| Field | label+control+help+error | control 40 | required/optional/readonly/disabled/error/success | unidade e formato explícitos | global | PARTIAL |
| Action Bar | ações de página/editor/seleção | 36/40 | default, sticky optional, loading | ações dependentes fora da lista | tables/forms | PARTIAL |
| Detail Panel | detalhe da seleção | card/side/inline | empty/loading/ready/error/dirty | quando seleção precisa de contexto | Peso/Job On | PARTIAL |
| Tool Availability Picker | substituir associação no Job On | expandable table + filters | closed, loading, ready, selected, empty, partial-source, error | só em Modo edição; posição + estado técnico + uso com origem explícita | Job On | PARTIAL |
| History Entry | evento auditável | row/card | normal/correction/void/expanded | ator, módulo, ação, entidade, data, resultado, reason, before/after | históricos + Admin/Auditoria | READY |
| User/Profile Indicator | identidade da sessão | header right | default/compact/menu-open | nome+título; título não é permissão | header/admin | PARTIAL |
| Local Directory Selector | autorizar o único diretório principal atual | field + action 34/40 | unconfigured, requesting, authorized, permission-lost, unavailable, error | apenas em Definições; não existe configuração por Referência, Produção ou tipo | documentos do Controlo | PARTIAL |
| Resolved Report Path | mostrar o destino gerado automaticamente | read-only compact | resolved, missing-root, permission-lost, unavailable | `Diretório / Referência / Produção / Produção_Referência_Linha_Tipo`; nunca é editável | histórico/documentos | PARTIAL |

### 4.1 Componentes em falta que bloqueiam reutilização

Antes de construir módulos, fechar e implementar:

- Tooltip;
- Date Picker;
- Dropdown pesquisável;
- Loading/Empty/Error como componentes reais;
- Confirmation Dialog sem APIs nativas;
- Field completo com helper/error;
- Sidebar responsiva/drawer;
- User/Profile menu e logout;
- History Entry expandível;
- Page Header e Action Bar canónicos.

### 4.2 Interaction e `when not to use` por componente

Esta tabela completa o inventário anterior. O comportamento de estado continua na secção 5.

| Componente | Interaction principal | Quando não usar |
|---|---|---|
| Button | click/keyboard executa uma ação explícita | navegação passiva, status ou mero destaque |
| Icon Button | click executa ação compacta; tooltip/aria identifica | ação ambígua ou sem ícone universal |
| Input | introdução direta, paste e keyboard | conjunto finito pequeno de opções |
| Textarea | texto multilinha | códigos, números ou notas de uma linha |
| Select | abrir e escolher uma opção curta | catálogo longo, pesquisável ou contextual |
| Custom Dropdown | escrever/navegar/escolher resultado | 2–5 opções estáveis visíveis |
| Checkbox | alternar item independente ou batch | escolha mutuamente exclusiva ou seleção normal de row |
| Radio | escolher exatamente uma opção | lista longa ou ação que muda imediatamente de página |
| Segmented Selector | clique numa opção visível e frequente | mais opções do que cabem claramente ou seleção múltipla |
| Date Input | escrever/escolher uma data | navegar/visualizar densidade de registos mensais |
| Date Picker | abrir popover e escolher data | calendário operacional sempre visível |
| Calendar | navegar mês e selecionar dia | introduzir apenas uma data num formulário simples |
| Card | agrupar conteúdo relacionado | valor isolado sem contexto ou cada célula de uma tabela |
| Expandable Card | trigger expande editor inline e gere dirty state | confirmação curta ou detalhe de leitura simples |
| List | selecionar/abrir itens com layout flexível | dados que exigem comparação por colunas |
| List Row | clique seleciona; duplo abre | comando imediato sem estado de seleção |
| Table | ordenar/filtrar/paginar dados tabulares | cartões sem relação colunar |
| Table Row | clique seleciona; duplo abre; keyboard equivalente | inserir vários botões repetidos de abertura |
| Filter Bar | alterar filtros e aplicar/limpar | único search field simples sem filtros adicionais |
| Search | input incremental/submissão conforme volume | seleção obrigatória de ID sem lista de resultados |
| Tabs | trocar vistas pares dentro do módulo | executar guardar, criar, aprovar ou eliminar |
| Badge | mostrar categoria/metadado curto | representar sozinho um estado crítico |
| Status | comunicar estado textual e semântico | tipo de registo ou ação |
| Alert | mensagem persistente junto do problema | sucesso breve sem consequência |
| Toast | confirmação breve após ação | erro que exige correção ou decisão |
| Modal | tarefa curta, focus trap e retorno ao trigger | formulário extenso ou fluxo que requer contexto da página |
| Confirmation Dialog | confirmar consequência difícil de reverter | ação segura/reversível normal |
| Context Menu | mostrar poucas ações válidas do item | ação primária, navegação principal ou lista longa |
| Tooltip | ajuda suplementar em hover/focus | instrução essencial, erro ou conteúdo necessário em mobile |
| Pagination | navegar páginas de dados | coleção pequena fixa ou virtual scrolling aprovado |
| Empty State | explicar ausência e próximo passo | mascarar erro/loading |
| Loading State | indicar espera preservando layout | operação síncrona instantânea |
| Error State | explicar falha e recuperação | validação simples de um único field |
| Sidebar | manter contexto operacional transversal à vista | analytics geral ou navegação duplicada |
| Header | identidade global e acesso à conta | repetir detalhe operacional da página |
| Page Header | identificar tarefa/vista e ação primária | repetir literalmente a tab sem informação adicional |
| Section Header | separar secções semânticas | criar hierarquia visual para cada field |
| Form Group | reunir fields da mesma tarefa | envolver um único field sem benefício |
| Field | label/control/helper/error | control sem label ou informação apenas de leitura |
| Action Bar | reunir ações do contexto/seleção | repetir a mesma ação dentro de cada row |
| Detail Panel | mostrar contexto da seleção | substituir navegação quando detalhe é uma página completa |
| History Entry | mostrar evento e expandir auditoria | apresentar estado atual sem evento |
| User/Profile Indicator | identidade atual e trigger de conta | conceder/representar permissões através do título visual |

## 5. Component State Contract

### 5.1 Matriz global

| Estado | Regra visual e de interação |
|---|---|
| Default | componente legível, disponível e sem sinal falso de seleção |
| Hover | alteração clara mas discreta; nunca única pista da ação |
| Focus | ring visível, consistente e independente do hover |
| Active/pressed | feedback imediato enquanto pressionado; não confundir com selected |
| Selected | estado persistente de escolha, com texto/ARIA quando aplicável |
| Disabled | contraste reduzido, sem hover/comando; contexto deve explicar a indisponibilidade |
| Loading | preserva dimensões, bloqueia reenvio e apresenta progresso |
| Success | apenas depois de persistência confirmada |
| Warning | atenção recuperável, sem simular erro fatal |
| Error | mensagem concreta, próxima ação e preservação de input quando possível |

`Focus`, `selected` e `active` são estados diferentes e não podem partilhar apenas o mesmo fundo sem outro indicador.

### 5.2 Button state machine

| Estado | Fundo | Border | Texto | Comportamento |
|---|---|---|---|---|
| Normal | cor da variante | mesma cor | branco | ação disponível |
| Hover | branco | cor da variante | cor da variante | cursor pointer |
| Focus visible | branco | cor da variante | cor da variante | ring global adicional |
| Pressed/active | branco | cor da variante, strong opcional | cor da variante | deslocamento máximo 1px opcional; sem brightness |
| Loading | cor da variante ou surface controlada | mesma cor | branco + indicador | label preserva largura; comando bloqueado |
| Disabled | token disabled | token disabled | branco/contraste validado | sem hover e `aria-disabled`/disabled |

A expressão original “filled rest e ao hover invertido” continua válida. O pacote não define ainda um token específico de `pressed`; isso é `DESIGN DECISION REQUIRED`, embora deva permanecer dentro do estado invertido.

### 5.3 Campos

- Hover: border ligeiramente mais forte, sem competir com focus.
- Focus: border brand + focus ring.
- Readonly: legível, fundo subtle, selecionável/copiável.
- Disabled: menor contraste e não focável quando nativo.
- Error: border danger + mensagem junto ao field; não apagar helper necessário.
- Success: usar apenas quando confirmar explicitamente validação remota relevante; evitar green em cada campo válido.
- Loading select/search: indicador dentro do control e menu não interativo.

### 5.4 Rows e cards selecionáveis

- Hover não equivale a seleção.
- Clique cria seleção única.
- Selected permanece até mudar de linha, filtro, contexto ou ação que remova o registo.
- Duplo clique abre sem exigir botão adicional.
- Focus por teclado é visível mesmo sem seleção.

## 6. List/Table Interaction Contract

### 6.1 Contrato principal

| Interação | Resultado |
|---|---|
| Um clique | seleciona uma linha e ativa ações externas |
| Duplo clique | abre detalhe/folha/registo associado |
| Hover | mostra que a linha é interativa |
| `Enter` | contrato atual diverge: Design System sec. 13 diz selecionar; Coder Handoff diz abrir |
| `Ctrl+Enter` | abre segundo o Design System atual |
| `Espaço` | Coder Handoff propõe selecionar |
| Escape | fecha menu/contexto, não perde filtros |
| Filtro/limite novo | regressa à página 1; limpa seleção invisível |

### 6.2 Exceções

- Checklist/batch usa checkbox para inclusão/progresso; clique na linha pode continuar a selecionar.
- Calendário não segue duplo clique: um clique seleciona/filtra.
- Segmented selector e cards de linha são escolhas diretas, não listas de detalhe.
- Rows com ação inline só são aceitáveis quando a própria célula representa uma decisão individual, como `Manter/Colocar de parte`; não usar para abrir.

### 6.3 Estados obrigatórios

- Loading inicial.
- Refresh mantendo a tabela.
- Empty sem dados.
- No results por filtros.
- Error de carregamento com Retry.
- Ready sem seleção.
- Ready com seleção.
- Processing numa ação sobre a seleção.

### 6.4 Sorting e scroll

`PARTIAL`:

- filtering e paginação estão definidos;
- sorting não tem contrato global: falta definir headers sortable, direção, estado inicial e persistência;
- scroll vertical/sticky header está mencionado;
- scroll horizontal é último recurso e deve ficar dentro do card.

`DESIGN DECISION REQUIRED`: fechar o comportamento de teclado (`Enter` vs `Ctrl+Enter`) e sorting antes de implementar o componente.

## 7. Calendar Contract

### 7.1 Definição canónica existente

- semana começa na segunda-feira;
- mês/ano centrado;
- setas nas extremidades;
- sete colunas;
- um clique seleciona e filtra;
- dia com registo tem ponto discreto;
- selecionado usa brand fill e texto branco;
- `Mostrar todas as datas` remove o filtro;
- alteração de mês não auto-seleciona;
- teclado e `aria-pressed` obrigatórios;
- desktop aproximadamente 300–340px quando ao lado de lista;
- mobile passa para cima.

### 7.2 Estados auditados

O contrato transversal de persistência e consulta está em `AUDITORIA_GLOBAL_HANDOFF.md`. Todos os módulos emitem eventos factuais append-only para ações relevantes, associados ao utilizador, módulo e entidade. A vista anual no Admin não atribui pontuações nem rankings.

| Estado | Estado do contrato |
|---|---|
| Mês anterior/seguinte | DEFINED |
| Dia selecionado | DEFINED |
| Dia com registos | DEFINED |
| Dia sem registos | DEFINED |
| Dia disabled/fora do mês | PARTIAL |
| Hover | DEFINED |
| Focus/teclado | DEFINED no texto, parcial no CSS |
| Hoje | MISSING |
| Loading/error do calendário | MISSING |
| Vários tipos de registo no mesmo dia | MISSING |
| Relação com lista | DEFINED |
| Responsivo | DEFINED no texto, parcial na implementação |

### 7.3 Peso versus Boquilhas

`DESIGN GAP` confirmado.

O próprio `HANDOFF_INDEX.md` declara que o calendário do Peso ainda precisa da passagem visual final para reutilizar exatamente o de Boquilhas. O futuro build não deve portar nenhum dos dois diretamente: deve criar primeiro o calendar canónico e fazer ambos consumi-lo.

## 8. Shell / App Frame

### 8.1 Definido

- header global de 76px;
- logótipo 44px;
- título do módulo/página e descrição junto ao logo;
- nome e título do perfil à direita;
- tabs operacionais à esquerda;
- Definições/Administração à direita;
- page surface, cards e cores;
- side panel contextual fixo quando o módulo necessita tracking;
- drawer/bloco recolhível em mobile.

### 8.2 Contrato visual proposto sem inventar permissões

```text
APP FRAME
├─ Global Header
│  ├─ Logo
│  ├─ Module/Page identity
│  └─ User/Profile indicator + account trigger
├─ Module Navigation
│  ├─ Operational tabs
│  └─ Settings tab (right aligned)
└─ Work Area
   ├─ Optional contextual sidebar
   └─ Page content
```

- O título do header identifica a área atual; o título da vista identifica a tarefa dentro dela.
- Module switching deve ter uma indicação ativa consistente, mas o pacote não contém um componente canónico de launcher/menu global.
- Logout/account deve partir do indicador do utilizador ou menu de conta, mas a interação visual não está desenhada.
- Sidebars de contexto não substituem navegação global.
- O conteúdo deve ser fluido e usar o espaço disponível sem ficar numa coluna pequena no centro.

### 8.3 Estado

`SHELL VISUAL CONTRACT: PARTIAL`.

Falta `DESIGN DECISION REQUIRED` para:

- mecanismo visual de troca de módulos;
- menu do perfil/logout;
- largura máxima/gutters da área de conteúdo;
- comportamento exato do side panel em tablet/mobile;
- sticky behavior e layers;
- apresentação quando não existem tabs.

## 9. Canonical Page Anatomy

```text
APP SHELL
→ MODULE NAVIGATION
→ PAGE HEADER
→ PRIMARY ACTION / FILTER AREA
→ ACTIVE CONTEXT OR ESSENTIAL SUMMARY
→ MAIN CONTENT
→ DETAIL / SECONDARY CONTENT
→ SELECTION ACTION BAR
→ FEEDBACK
```

### Regras

- Page Header contém título, descrição e no máximo a ação primária contextual.
- Filtros ficam imediatamente antes da coleção que afetam.
- Contexto ativo permanece próximo das ações que dependem dele.
- Ações dependentes de seleção ficam após/ao lado do footer da lista, nunca repetidas em cada row.
- Feedback aparece junto do contexto ou no toast conforme persistência.
- Empty state ocupa o main content sem criar um grande vazio tracejado.

### Exceções justificadas

- Login não usa tabs nem page header autenticado.
- Job On em modo consulta comporta-se como folha técnica e dá prioridade ao contexto e às ferramentas.
- Side panel de Boquilhas persiste fora da anatomia interna da tab.
- Responsável Peso usa calendar + master list + detail, uma composição master-detail.
- Armazém e reparação interna usam registo rápido, reduzindo o número de áreas.

## 10. Form Contract

### Estrutura de Field

```text
Label [required marker]
Control [unit/suffix when applicable]
Helper text
Validation message
```

### Regras

- Label sempre visível.
- Obrigatório indicado visualmente e em acessibilidade; o símbolo final ainda é `DESIGN DECISION REQUIRED`.
- Opcional pode usar `(opcional)` quando evita dúvida; não marcar todos os opcionais.
- Helper explica formato/consequência; placeholder apenas exemplifica.
- Erro diretamente abaixo do field e resumo no topo apenas para formulários extensos.
- Agrupar por tarefa, não por estrutura de dados.
- Números alinhados; unidades visíveis; mobile keyboard apropriado.
- Máximo de duas casas decimais quando a apresentação assim estiver definida; regra de cálculo vem da spec funcional.
- Campos curtos usam largura proporcional.
- Data no final da linha de identificação salvo exceção documentada.
- Notas ocupam espaço restante e não competem com campos de 2–3 caracteres.
- Readonly é legível e copiável; disabled é indisponível.
- Save/Cancel no footer à direita, Cancel antes de Save.
- Ao fechar dirty state, usar Confirmation Dialog.
- Ação destrutiva fica separada das ações normais.

### Estados

`PARTIAL`: labels, focus, width e ordem estão definidos. Falta componente completo para required marker, validation summary, success validation, async validation e dirty-state indicator.

## 11. Modal / Dialog Contract

### Quando usar

- confirmação destrutiva;
- perder alterações;
- reset de palavra-passe;
- ação rápida e focada;
- edição curta que não beneficia do contexto completo da página.

Não usar para formulários extensos; esses expandem inline.

### Anatomia

- Backdrop.
- Header: título, contexto curto e Close icon.
- Body: mensagem ou fields.
- Error region persistente.
- Footer: secondary/cancel primeiro, primary/destructive no fim.

### Comportamento

- foco inicial no título ou primeiro campo seguro;
- focus trap;
- `Escape` e click no backdrop fecham apenas sem perda de dados/processamento;
- close restaura foco ao trigger;
- submit loading impede repetição;
- falha mantém modal aberto e input preservado;
- conteúdo longo faz scroll no body, não na página;
- backdrop não desaparece durante processamento.

### Variantes

`DESIGN DECISION REQUIRED`: larguras exatas small/medium/large e altura máxima. O contrato atual só define modal genérico `min(560px,100%)`.

## 12. Feedback Contract

| Situação | Componente | Persistência |
|---|---|---|
| Campo inválido | field error | até corrigir |
| Vários campos inválidos | field errors + summary | até corrigir |
| Informação contextual | inline info | enquanto relevante |
| Alerta recuperável | inline warning | até resolver/dispensar quando permitido |
| Conflito operacional | inline error/alert | até resolver |
| Sucesso simples | toast | temporário |
| Save falhado | inline error no editor + toast opcional | persistente |
| Load falhado | Error State com Retry | persistente |
| Loading inicial | skeleton/compact loader | até concluir |
| Comando em curso | estado loading no trigger | até concluir |
| Ação destrutiva pendente | Confirmation Dialog | bloqueante |
| Operação destrutiva falhada | dialog ou inline no mesmo contexto | até decisão |

Não usar modal para mensagens informativas normais. Não usar toast como única apresentação de um erro que exige correção. Não apresentar sucesso antes de confirmação da operação.

## 13. History / Audit Visualization

Este contrato trata apenas da representação visual. Os eventos e campos efetivamente disponíveis exigem validação funcional.

### 13.1 Vista compacta

Cada row/entry deve conseguir apresentar:

- data/hora;
- ator;
- ação;
- objeto/contexto curto;
- estado;
- motivo quando crítico;
- indicador de correção/anulação.

### 13.2 Detalhe expandido

Ao abrir:

| Informação | Representação |
|---|---|
| Ator | nome legível + identificador apenas se necessário |
| Ação | verbo explícito, não código técnico |
| Data/hora | data e hora local; timezone no detalhe se necessário |
| Valor anterior | bloco/coluna `Anterior` |
| Valor novo | bloco/coluna `Novo` |
| Motivo | texto próprio, não misturado com observações |
| Correção | status `Corrigido` + relação visual com o evento de correção |
| Void/cancel | estado textual moderado; registo original continua consultável |
| Estado | componente Status canónico |

### 13.3 Consistência

- O mesmo actor/date/status usa o mesmo formato em todos os módulos.
- Não misturar snapshot histórico com estado live.
- Comparação anterior/novo deve ter labels e alinhamento estáveis.
- Correção não deve parecer eliminação.
- Um clique seleciona e duplo clique abre quando o histórico é tabela canónica.
- Uma timeline só deve ser usada quando a sequência é mais importante do que comparar colunas.

Estado atual: `PARTIAL`. Existem muitas tabelas de histórico, mas não existe ainda um componente visual único `History Entry/Detail` implementado.

## 14. Cross-Module Visual Language

| Conceito | Representação canónica |
|---|---|
| Machine/Line | código curto em field/card compacto; label `Máquina` ou `Linha` conforme texto funcional confirmado; selector segmentado quando ação frequente |
| Reference | texto principal em bold; nunca truncar sem tooltip/detail |
| Lot | valor compacto junto da Referência, nunca campo excessivamente largo |
| Tool | tipo + Referência + Lote + número individual quando disponível; tipo não é status |
| Status | pill/status textual com cor semântica dessaturada |
| Date | PT-PT na apresentação; date control em edição; date/time juntos em históricos |
| User | nome legível; perfil/título apenas no header ou detalhe relevante |
| Repairer | valor textual/filter; visualmente pessoa/entidade, não status |
| Production | código compacto e consistente, próximo da Referência/Máquina |
| History | tabela/lista canónica + detalhe auditável |
| Approval | status pendente/aprovado/não aprovado + ações externas; nota obrigatória mostrada no contexto |
| Quantity | número alinhado + unidade explícita quando necessária |
| Current vs historical | blocos separados `Estado atual` e `Snapshot histórico` |

O significado e a origem destes conceitos são `FUNCTIONAL INPUT REQUIRED`; a tabela define apenas linguagem visual.

## 15. Module Design Coverage

Critério estrito: `READY` exige mockup/brief, estrutura, componentes, interações, estados e responsive sem gap específico do módulo. Dependências globais são registadas separadamente.

| Área | Mockup | Brief | Estrutura | Componentes | Interações | Empty/Loading/Error | Responsive | Design ready |
|---|---|---|---|---|---|---|---|---|
| Login | READY | READY | READY | READY | READY | PARTIAL | READY | READY |
| Admin | READY | READY | READY | PARTIAL | READY | PARTIAL | PARTIAL | PARTIAL |
| Shell | PARTIAL | READY | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL |
| Peso Operador | READY | READY | READY | PARTIAL | READY | PARTIAL | PARTIAL | PARTIAL |
| Peso Responsável | READY | READY | READY | PARTIAL | READY | PARTIAL | PARTIAL | PARTIAL |
| Peso Comparação | READY | READY | READY | PARTIAL | READY | PARTIAL | PARTIAL | PARTIAL |
| Boquilhas | READY | READY | READY | PARTIAL | READY | PARTIAL | PARTIAL | PARTIAL |
| Job On | READY | READY | READY | PARTIAL | READY | PARTIAL | READY | PARTIAL |
| CM | READY | PARTIAL | READY | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL |
| MF | READY | PARTIAL | READY | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL |
| Warehouse/Armazém | READY | READY | READY | PARTIAL | READY | READY | PARTIAL | PARTIAL |
| Internal Repair | READY | READY | READY | PARTIAL | READY | READY | PARTIAL | PARTIAL |
| External Repair | READY | READY | READY | PARTIAL | READY | PARTIAL | PARTIAL | PARTIAL |
| Pegamentos | READY | READY | READY | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL |
| Tampões | READY | READY | READY | PARTIAL | READY | READY | READY | PARTIAL |
| Tool creation | PARTIAL | READY | READY | PARTIAL | READY | PARTIAL | PARTIAL | PARTIAL |
| History transversal | PARTIAL | PARTIAL | PARTIAL | PARTIAL | READY | PARTIAL | PARTIAL | PARTIAL |

Resultado estrito: **1/17 READY**. Isto não significa que os outros módulos devam ser redesenhados; significa que devem ser montados sobre componentes globais ainda por fechar e completar os estados indicados.

## 16. Mockup → Component Map

| Mockup | Global components usados | Composição específica | Elementos únicos | Deve ser reutilizável / não duplicar |
|---|---|---|---|---|
| `login.html` | Input, Button, Alert, Card | split identity + form | painel de marca | Field/password reveal e form feedback globais |
| `admin.html` | Header, Tabs/nav, Table, Search, Status, Modal, Toast | utilizadores + aplicações | profileTitle editor/reset | User row, Confirmation Dialog e permission/template selector |
| `boquilhas.html` | Header, Tabs, Buttons, Fields, Table, Modal, Context Menu, Pagination | fixed line sidebar + lote/detail/history | cards B1–C3 e alerta de conflito | sidebar card e movement table sem nova implementação de base |
| `peso-operador.html` | Header, Tabs, Fields, Tables, Status, Action Bar | reference/control/comparison workspaces | readings/results matrix | Reading Group, Result Summary e canonical list/calendar |
| `peso-responsavel.html` | Header, Tabs, Calendar, List, Detail, Status | master calendar/list + approval detail | decisão individual CM | Calendar, Approval Panel, CM Decision Row |
| `job-on-v48-folha-producao.html` | Header, Tabs, Calendar, Fields, Cards, Status, Action Bar | technical production sheet | ferramenta groups, image, verifications | Tool Group shell, contextual selector, image field, checklist |
| `armazem.html` | Header, Tabs, Filters, Table, Status, Modal | quick movement + programmed list | location/context blocks | Quick Register, Batch Checklist, history components |
| `reparacao-interna.html` | Header, Tabs, segmented selector, Field, Table | top full-width line selector then register | active Job On context | Line Selector and Quick Register; table global |
| `reparacao-externa-v1.html` | Header, Tabs, Filters, Table, Status | external cycle/list | programmed repair batch | Batch Checklist shared with Armazém |
| `moldes.html` + `moldes-v4x` | Header, Tabs/segmented controls, Fields, Table | CM/MF separate areas | type-specific selectors | Tabs global; tool list/card; no separate button system |
| `pegamentos.html` | Header, Tabs, Dropdown, Fields, Tables, Print Action | context selection → control sheet | measurement grids | contextual selector and table; remove legacy local database UI |
| `tampoes.html` | Header, Tabs, Fields, Status, Table, Pagination | config selector + quantities + planning | atomic config transformation | Quantity editor, configuration selector, movement history |
| `job-on.html` | redirect only | canonical entry | none | not a UI component |
| versioned standalone files | mesma composição em iterações | historical visual variants | none authoritative | do not implement alongside canonical file |

### 16.1 Component-first build set

Construir antes das páginas:

1. tokens/foundation;
2. Button/Icon Button;
3. Field family;
4. Card/headers;
5. Tabs;
6. Status/Badge;
7. List/Table/Row/Pagination;
8. Filter Bar/Search/Dropdown;
9. feedback states;
10. Modal/Confirmation/Context Menu/Tooltip;
11. Calendar;
12. Header/Shell/Sidebar;
13. Page Header/Action Bar/Detail Panel;
14. History Entry;
15. module compositions.

## 17. Design vs Business Boundary

### DESIGN STATEMENTS REQUIRING FUNCTIONAL VALIDATION

As afirmações seguintes aparecem nos documentos para explicar a UI, mas **não são confirmadas por esta auditoria**. O agente de planeamento deve cruzá-las com Verified Knowledge:

- template de acesso determina módulos, capabilities e ordem; para utilizadores operacionais, a landing page é Job On;
- o Administrador puro é a exceção: abre `/admin`, fica na shell administrativa e não recebe Job On nem módulos operacionais;
- apenas o papel/template técnico Responsável pode criar, duplicar, editar, guardar revisões e gerir Definições do Job On;
- título do perfil é independente de permissões;
- Processo NNPB/PS é escolhido na criação do lote no Peso e herdado pelos registos; não é pedido no Novo controlo;
- máquinas permitidas associam Referência/lote às linhas;
- Peso Operador cria e envia; Peso Responsável aprova e não regista leituras;
- existem dois tipos de registo de Peso: Novo controlo e Comparação, ambos referenciando o Job On da produção;
- Novo controlo usa o contexto do Job On; Comparação usa os CM já em produção e a base aprovada do Novo controlo desse Job On;
- comparação não altera o Novo controlo aprovado base;
- decisão de comparação é individual por CM;
- destinatários de email são escolhidos pela máquina/linha;
- uma linha de Boquilhas não pode ter duas Referências diferentes;
- dois lotes da mesma Referência na mesma linha são permitidos;
- utilização de BQ representa tempo de vida e não quantidade;
- saldos/movimentos e fecho de lote geram snapshots específicos;
- Job On duplica a produção anterior da mesma Referência e reseta/atualiza datas;
- Job On obtém opções CM/MF/BQ por Referência e Máquina;
- Job On guarda as instâncias e lotes concretos de CM/MF/BQ usados por uma produção; Peso e Pegamentos consomem essas escolhas sem segunda seleção;
- Job On separa consulta não editável de edição; apenas na edição a lista de ferramentas agrega posição do Armazém, estado técnico e `% de uso` para suportar uma substituição;
- em edição, todos os campos do snapshot Job On são editáveis, incluindo CAL, PI, quantidades, notas e grupos secundários; informação live das fontes externas permanece apenas contextual;
- cada Job On/revisão guarda um snapshot completo de todos os grupos para duplicação; as bases mestre mantêm identidade, estado técnico, vida e localização; `JOB_ON_DATA_MODEL.md` define explicitamente as tabelas e o limite de ownership autorizado pelo produto;
- verificações `Uma vez neste lote` e `Por fabrico` geram ocorrências;
- saída de Armazém liberta imediatamente uma posição;
- saída programada conclui quando todos os checks estão confirmados;
- Reparação interna usa a projeção por Linha: mudança física às 06:00, contexto anterior até 08:59 e novo Job On às 09:00; sem contexto guarda `Sem associação`;
- Reparação interna regista apenas CM/MF; BQ é contexto read-only; o reparador vê/corrige/anula apenas os próprios registos, com validação server-side pelo utilizador autenticado;
- Reparação externa é separada da interna;
- CM e MF têm identidades/históricos separados;
- Tampões transforma quantidades atomicamente entre configurações;
- Pegamentos usa os CM, BQ e MF concretos associados ao Job On, cujas entidades vêm dos respetivos módulos de domínio;
- o servidor guarda os dados estruturados e históricos de Peso/Pegamentos, enquanto os PDFs de Produção são guardados na pasta local configurada;
- configura-se apenas o diretório principal atual em Definições; Referência, Produção e pastas de Peso/Pegamentos/Resume são criadas automaticamente a partir do Job On, sem texto livre;
- histórico preserva snapshots, correções, ator e motivo;
- eliminar, anular, arquivar e corrigir têm consequências específicas.

Nenhuma destas afirmações deve ser usada para desenhar entidades ou relações de DB apenas porque aparece no handoff visual. A exceção explícita é `JOB_ON_DATA_MODEL.md`, criado como contrato técnico de persistência após validação funcional do produto.

## 18. Contradictions Inside Design

| Conflict | Source A | Source B | Recommended canonical design |
|---|---|---|---|
| Altura de botão | Design System: compact 34, normal 40 | CSS `.dmo-button` min 36; Coder Handoff chama 36 standard | fixar API: compact 34, default 36 ou 40 a decidir, form/filter 40; um token por size |
| Hover/active | regra filled → hover inverted | alguns mockups têm `.btn` próprios e variantes de tom | usar o state machine da sec. 5.2; proibir brightness |
| Enter numa lista | Design System: Enter seleciona, Ctrl+Enter abre | Coder Handoff: Enter abre, Espaço seleciona | escolher um contrato único; recomendação acessível: Espaço seleciona, Enter abre |
| Calendário | componente canónico documentado/Boquilhas | Peso apresenta variante visual | construir um único componente antes de Peso/Boquilhas |
| Inputs | contrato global 40px | HTML locais usam alturas/paddings diferentes | Field global 40; compact só por variante explícita |
| Card radius/shadow | 12px + shadow token | mockups redefinem radius/shadow localmente | usar Card global; module CSS só composição |
| CSS architecture | tokens/components globais | 17 HTML com `<style>` e inline styles; 6 sem global CSS | não copiar CSS dos mockups; reconstruir por componentes |
| Buttons vs tabs em Moldes | tabs representam áreas | mockup mostra `Contra moldes`/`Moldes finais` como botões | usar Tabs globais, ativa filled apenas se for segmented selector funcional |
| Page width | princípio de usar largura disponível | alguns mockups centralizados estreitos em ecrã largo | definir container fluido/max-width e gutters antes dos módulos |
| Sidebar | side panel fixo contextual | não existe sidebar global única noutros módulos | separar App Navigation de Context Sidebar; definir responsive |
| Header naming | header global usa título da página | alguns mockups misturam app title/module title | fixar dois níveis: module identity no header, view title no content |
| Job On tool label | exemplos usam `MP`; discussões referem `CM` como prioritário | mockup/documento usa `MP/CM` em alguns pontos | `FUNCTIONAL INPUT REQUIRED`; visual aceita label configurada, não fundir identidades |
| Modal confirmations | contrato proíbe APIs nativas | vários mockups usam `confirm/prompt/alert` | Confirmation Dialog/Modal global |
| Dropdown | contrato pede custom styled/searchable | alguns HTML usam select/datalist/browser menu | Select nativo estilizado para curto; custom Dropdown para contextual/pesquisa |
| Pegamentos | brief remove base local/actions duplicadas | HTML ainda contém código legacy | brief prevalece; não portar legacy UI |
| Versioned mockups | ficheiros canónicos coexistem com versões v38/v42/v43/v44 | agente pode implementar múltiplos | README/index define entrada canónica; versões são só evidência histórica |

## 19. Design Gaps

### P0 — bloqueia foundation/design system

1. Definir tokens exatos de typography/line-height/letter-spacing.
2. Definir escala de z-index/layers.
3. Definir page width e gutters responsivos.
4. Resolver tamanho default do Button: 36 ou 40px; manter variantes explícitas.
5. Resolver teclado de row: Enter selecionar ou abrir.
6. Fechar border/focus tokens e regras de reduced motion.
7. Declarar `dmo-design-system` como única fonte visual e impedir legacy/inline/local component CSS.

### P1 — bloqueia componente reutilizável

1. Calendar completo: today, disabled/outside month, loading/error e responsive implementado.
2. Field completo: required, helper, error, readonly, async/loading.
3. Custom Dropdown pesquisável e Select curto.
4. Modal/Confirmation com focus trap e size variants.
5. Loading, Empty e Error components.
6. Tooltip e icon system.
7. Sidebar/drawer responsiva.
8. User/Profile menu e logout visual.
9. History Entry/detail.
10. Sorting contract para Table.
11. Page Header, Action Bar e Detail Panel canónicos.

### P2 — bloqueia módulo

1. Peso deve adotar o Calendar canónico.
2. Shell precisa do module switcher e account interaction.
3. CM/MF precisam de handoff visual individual completo ou validação de que o brief conjunto basta.
4. Pegamentos precisa de mockup canónico limpo sem áreas legacy contraditórias.
5. Tool creation precisa de mockup próprio consolidado.
6. History transversal precisa de composição canónica.
7. Boquilhas/Admin/Armazém precisam de substituir confirmações nativas no mockup de referência final.
8. Job On precisa de validação funcional da nomenclatura MP/CM antes do label final.
9. Fechar convenção técnica dos nomes de PDF e estratégia quando a pasta local/partilhada não está acessível noutro computador.
10. Fechar estados e recuperação de permissão do File System Access API sem representar um ficheiro local como dado garantidamente disponível no servidor.

### P3 — cosmético / pode ser refinado depois

1. afinação da intensidade de sombras;
2. motion normal para entrada de menus/modais;
3. truncation/tooltip em referências muito longas;
4. microcopy uniforme de empty states;
5. refinamento de densidade em ecrãs ultra-wide;
6. animação de abertura de cartões expansíveis.

## 20. Implementation Order — Design Only

Esta ordem constrói a fundação visual; não é um plano da aplicação.

1. Resolver decisões P0.
2. Tokens completos.
3. Typography e foundation/reset.
4. Primitive controls e icon rules.
5. Buttons e state machine.
6. Fields e forms.
7. Cards, headers e layout primitives.
8. Lists, Tables, Rows, sorting e Pagination.
9. Filter Bar, Search, Select e Dropdown.
10. Status, Alert, Toast, Loading, Empty e Error.
11. Modal, Confirmation Dialog, Context Menu e Tooltip.
12. Calendar canónico.
13. Header, navigation, profile/account, Sidebar e Shell.
14. Page Anatomy components: Page Header, Action Bar e Detail Panel.
15. History Entry/detail.
16. Login e shell test page como prova da fundação.
17. Module layouts sem redefinir componentes.
18. Responsive pass em 1200, 980, 720 e mobile estreito.
19. Keyboard/accessibility pass.
20. Visual regression pass entre módulos.

Gate obrigatório antes do passo 17: uma página-laboratório deve apresentar todos os componentes e estados usando apenas CSS global.

## 21. Design Acceptance Checklist

### Foundation

- [ ] Todos os valores visuais vêm de tokens aprovados.
- [ ] Tipografia usa tokens exatos, sem intervalos ambíguos.
- [ ] Layers/z-index usam escala única.
- [ ] Breakpoints e gutters são canónicos.
- [ ] `prefers-reduced-motion` está implementado.
- [ ] Contraste WCAG AA foi verificado.

### CSS architecture

- [ ] Nenhuma página contém `<style>` de design.
- [ ] Nenhum markup contém `style="..."` de design.
- [ ] Não existe `site.css` legacy a sobrepor componentes.
- [ ] Module CSS contém apenas composição/layout.
- [ ] Universal components não são redefinidos por módulo.
- [ ] Load order é tokens → components → layout → module.
- [ ] Existe uma página de catálogo/teste dos componentes.

### Components

- [ ] Um único Button system.
- [ ] Filled → inverted hover/focus está consistente.
- [ ] Loading e disabled impedem ação repetida.
- [ ] Um único Field system de 40px.
- [ ] Um único Select/Dropdown contract.
- [ ] Um único Card system.
- [ ] Um único Table/List/Row system.
- [ ] Um único Calendar.
- [ ] Modal e Confirmation não usam APIs nativas.
- [ ] Tooltip e icon sizing são consistentes.
- [ ] Feedback usa inline/toast/modal conforme o contrato.

### Interaction

- [ ] Um clique seleciona rows aplicáveis.
- [ ] Duplo clique abre rows aplicáveis.
- [ ] Contrato de teclado final é único.
- [ ] Focus é sempre visível.
- [ ] Filter change limpa seleção invisível e regressa à página 1.
- [ ] Paginação apresenta 20/40/60, total e página.
- [ ] Ações dependentes ficam fora da lista.
- [ ] Card expandido gere dirty state e foco.

### Shell

- [ ] Header contém logo, módulo/página e utilizador/título.
- [ ] Module navigation tem active indication.
- [ ] Definições está alinhado à direita.
- [ ] Account/logout interaction está definida.
- [ ] Sidebar contextual não se confunde com navegação.
- [ ] Conteúdo usa largura e gutters canónicos.
- [ ] Tablet/mobile não criam scroll horizontal da página.

### Page/module

- [ ] Cada página segue a anatomia canónica ou documenta exceção.
- [ ] Hierarquia visual preserva informação operacional crítica.
- [ ] Empty, loading e error foram implementados.
- [ ] Responsive foi verificado nos breakpoints.
- [ ] O mesmo conceito tem o mesmo visual entre módulos.
- [ ] Histórico separa snapshot de estado atual.
- [ ] Dados fictícios dos mockups não chegaram à aplicação.
- [ ] Nenhuma regra de negócio foi inferida do layout.

### Visual regression

- [ ] Capturas de referência existem para desktop/tablet/mobile.
- [ ] Button, Field, Card, Table, Calendar e Modal têm testes visuais.
- [ ] Comparação cross-module deteta divergências de altura, radius, spacing e typography.
- [ ] Temas/zoom/text scaling não quebram o layout.

## 22. Final Verdict

**DESIGN SYSTEM READY FOR FRESH BUILD: NO**

Motivo: a especificação normativa é forte, mas tokens essenciais e a arquitetura CSS executável ainda não estão completos; mockups contêm implementações concorrentes.

**COMPONENT CONTRACT READY: NO**

Motivo: componentes principais têm direção, mas Date Picker, Dropdown, Tooltip, feedback states, sorting, profile menu, History Entry e detalhes de Modal/Field ainda estão incompletos.

**SHELL VISUAL CONTRACT READY: NO**

Motivo: header e tabs estão bem definidos, mas module switching, account/logout, page width/gutters, layers e sidebar responsive não estão fechados.

**MODULE DESIGN COVERAGE: 1/17 READY**

Login é a única área sem bloqueio visual específico significativo. Os restantes módulos estão `PARTIAL`, sobretudo por dependência de componentes globais, estados incompletos ou contradições registadas.

### Blocking design gaps

- tokens P0;
- decisão de Button size;
- contrato de teclado de rows;
- uma arquitetura CSS sem estilos locais/legacy;
- Calendar único;
- Shell navigation/account/responsive;
- componentes P1 indicados na secção 19.

### Non-blocking design gaps

- sombras/motion;
- microcopy de empty states;
- densidade ultra-wide;
- tooltips de truncation;
- animações de expansão.

### Information that must come from Functional Spec / Verified Knowledge

- atores, roles, capabilities e acesso real;
- significado e ownership de Referência, lote, ferramenta, máquina, produção e estados;
- regras de criação, duplicação, correção, fecho, eliminação e arquivo;
- cálculos e arredondamentos;
- origem autoritativa de cada dado;
- compatibilidades e filtros funcionais;
- lifecycle de aprovações, reparações, movimentos e Job On;
- regras de histórico/snapshot/auditoria;
- email, documentos, imagens e storage;
- nomenclatura final MP/CM;
- integrações e comportamento em concorrência/falha.

### Condição para mudar o veredicto para YES

1. resolver P0;
2. implementar e documentar o catálogo global de componentes P1;
3. fechar Shell visual;
4. criar um Calendar único;
5. remover competição de CSS na implementação nova;
6. rever cada módulo contra a matriz e elevar estados/response pendentes;
7. executar visual regression e checklist da secção 21.

O pacote deve continuar a acompanhar Verified Knowledge. Este contrato indica **como construir o design desde a fundação**, mas não substitui validação funcional.
