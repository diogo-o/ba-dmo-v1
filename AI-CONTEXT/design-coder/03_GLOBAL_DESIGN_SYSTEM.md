# Portal DMO — especificação global de UI

Versão: 2.7  
Estado: normativa  
Âmbito: todos os módulos presentes e futuros do Portal DMO

## 1. Autoridade deste documento

Para a passagem completa de implementação, ler primeiro `CODER_IMPLEMENTATION_HANDOFF.md`. Este documento é a autoridade visual e de componentes; os briefs de módulo são a autoridade funcional específica.

Este é o contrato global de interface. Define como uma página é composta, como os componentes se apresentam e como respondem ao utilizador.

Ordem de autoridade:

1. regras de negócio confirmadas do módulo;
2. esta especificação global;
3. handoff específico do módulo;
4. mockup visual.

Um handoff de módulo pode definir campos, permissões e fluxos próprios, mas não pode alterar silenciosamente botões, listas, calendários, cabeçalhos, tabs, estados ou tipografia. Uma exceção precisa de ser documentada e aprovada.

## 2. Princípios obrigatórios

1. A mesma ação tem sempre a mesma aparência e interação.
2. O utilizador permanece na página sempre que a tarefa puder ser concluída inline.
3. Informação compacta, sem campos desproporcionais ao conteúdo.
4. A cor complementa o texto; nunca é o único indicador.
5. Permissões pertencem à aplicação, não ao CSS.
6. Componentes usam tokens globais, sem cores ou medidas arbitrárias por módulo.
7. Dados de demonstração nunca passam para produção.
8. Não inventar campos, estados ou fontes de dados para preencher lacunas.

## 3. Anatomia obrigatória de um módulo

Cada módulo autenticado usa esta ordem:

1. Header global.
2. Barra de tabs.
3. Título e descrição da vista ativa.
4. Toolbar de ações e filtros, quando necessária.
5. Contexto ativo ou métricas essenciais.
6. Conteúdo operacional.
7. Ações dependentes de seleção fora das listas.
8. Feedback inline/toast.

Não repetir o título da tab em cartões sem acrescentar contexto.

## 4. Tokens canónicos

```css
:root {
  --dmo-brand-950: #0f1d2a;
  --dmo-brand-900: #193046;
  --dmo-brand-800: #234463;
  --dmo-brand-700: #315d88;
  --dmo-brand-600: #3c73a8;
  --dmo-brand-500: #568dc3;
  --dmo-brand-300: #98b9da;
  --dmo-brand-200: #bdd3e8;
  --dmo-brand-100: #d9e6f2;
  --dmo-brand-050: #e8eff7;

  --dmo-surface-page: #f6f9fc;
  --dmo-surface-card: #ffffff;
  --dmo-surface-subtle: #f1f6fa;
  --dmo-border: #d9e6f2;
  --dmo-text: #172d42;
  --dmo-text-muted: #64778a;
  --dmo-text-on-color: #ffffff;

  --dmo-success: #527c72;
  --dmo-success-soft: #e5f0eb;
  --dmo-warning: #a97943;
  --dmo-warning-soft: #f7f0e7;
  --dmo-danger: #9a625d;
  --dmo-danger-soft: #f3e9e7;
  --dmo-pending: #315d88;
  --dmo-pending-soft: #e7eef5;
  --dmo-disabled: #cbd5df;

  --dmo-space-1: 4px;
  --dmo-space-2: 8px;
  --dmo-space-3: 12px;
  --dmo-space-4: 16px;
  --dmo-space-5: 20px;
  --dmo-space-6: 24px;
  --dmo-space-8: 32px;

  --dmo-radius-control: 8px;
  --dmo-radius-card: 12px;
  --dmo-radius-modal: 16px;
  --dmo-radius-pill: 999px;
  --dmo-control-height: 40px;
  --dmo-control-height-compact: 34px;
  --dmo-header-height: 76px;
  --dmo-tabs-height: 52px;
  --dmo-sidebar-width: 276px;
  --dmo-shadow-card: 0 8px 24px rgba(25,48,70,.055);
  --dmo-shadow-menu: 0 10px 24px rgba(15,29,42,.22);
  --dmo-shadow-modal: 0 25px 70px rgba(15,29,42,.35);
  --dmo-transition-fast: 150ms ease;
}
```

É proibido introduzir um novo hexadecimal num componente quando já existe um token com o mesmo papel.

## 5. Tipografia

Família:

```css
font-family: Inter, ui-sans-serif, -apple-system, BlinkMacSystemFont,
  "Segoe UI", sans-serif;
```

| Elemento | Tamanho | Peso | Cor |
|---|---:|---:|---|
| Título principal da vista | 23–24px | 700–800 | `--dmo-text` |
| Título do header | 18px | 800 | `--dmo-text` |
| Título de modal | 18px | 700 | `--dmo-text` |
| Título de cartão | 15–16px | 700 | `--dmo-text` |
| Corpo | 14px | 400 | `--dmo-text` |
| Botão | 12–13px | 700 | depende do estado |
| Label | 11px | 700–750 | `--dmo-text-muted` |
| Ajuda/metadados | 10–12px | 400–600 | `--dmo-text-muted` |
| Cabeçalho de tabela | 10–11px | 750–800 | `--dmo-text-muted` |

Não criar tamanhos intermédios apenas para fazer um elemento “caber”. Ajustar primeiro o layout.

## 6. Header global

Todas as páginas autenticadas usam:

```html
<header class="dmo-app-header">
  <img class="dmo-app-header__logo" src="logo_recolored(1).png" alt="BA Glass">
  <div class="dmo-app-header__page">
    <h1>Título da página</h1>
    <p>Contexto curto do módulo</p>
  </div>
  <div class="dmo-app-header__user">
    <strong data-user-profile-name>Nome</strong>
    <span data-user-profile-title>Título ou função</span>
  </div>
</header>
```

Regras:

- logo oficial com 44px no desktop;
- título da página ao lado do logo;
- subtítulo curto, sem repetir a tab;
- nome e título/função no canto direito;
- título/função vem do perfil editável na Administração;
- `profileTitle` é informativo e não concede acesso;
- altura aproximada de 76px;
- o módulo não escreve diretamente o nome ou título do utilizador na implementação final.

## 7. Tabs

- A navegação usa dois níveis canónicos: `dmo-primary-nav` para tarefas/módulos e `dmo-secondary-nav` para vistas internas do módulo.
- O primeiro nível permanece acessível em todas as vistas, incluindo Controlo; entrar numa subárea nunca remove a saída para as tarefas principais.
- Tabs operacionais ficam à esquerda.
- `Definições` e `Administração` ficam à direita.
- Tab ativa: texto azul e linha inferior azul.
- Tab inativa: texto muted, sem fundo preenchido.
- Hover: fundo azul muito claro.
- Uma tab troca a vista dentro do módulo; não executa comandos.
- Tabs não autorizadas não são renderizadas.
- A tab não substitui o título da vista.
- Tamanho, fonte, espaçamento, hover e estado ativo vêm exclusivamente de `dmo-design-system.css`; não duplicar estas regras no CSS do módulo.

## 8. Botões

Implementação canónica: `dmo-design-system.css` aplica o componente a `.dmo-button` e aos aliases legados `.button`/`.btn` durante a migração. Código novo usa `.dmo-button`. Um módulo pode controlar apenas posição, largura responsiva ou composição do grupo; não redefine cor, tipografia, altura, padding, hover, focus ou disabled.

### Estados visuais

Todos os botões têm dois estados principais:

1. Repouso: fundo preenchido, contorno da mesma cor e texto branco.
2. Hover/foco: fundo branco, contorno e texto na cor original.

Não usar `brightness`, transparência ou alteração mínima de tom no hover.

### Tamanhos

| Tipo | Altura | Padding horizontal | Uso |
|---|---:|---:|---|
| Compacto | 34px | 10–12px | tabelas, toolbars, ações secundárias |
| Normal | 40px | 12–16px | formulários e ações principais |
| Apenas ícone | 34–40px | 0 | fechar, menu, navegação calendário |

- A largura segue o texto; não criar botões largos sem necessidade.
- Botões do mesmo grupo têm a mesma altura.
- Ação destrutiva usa `--dmo-danger`.
- Ação positiva usa `--dmo-success` apenas quando a semântica o exige.
- Desativado: cinzento, sem hover, com motivo compreensível pelo contexto.
- Ações primárias ficam à direita no rodapé de formulários; `Cancelar` vem antes de `Guardar/Criar`.

## 9. Ações que expandem cartões

Esta é a interação padrão para criar, editar ou filtrar sem sair da página.

Ao clicar num botão como `Criar novo`, `Editar` ou `Filtros`:

1. expande um cartão imediatamente abaixo da toolbar ou do bloco que originou a ação;
2. o botão atualiza `aria-expanded="true"` e aponta para o cartão com `aria-controls`;
3. o primeiro campo útil recebe foco;
4. o conteúdo existente permanece visível quando ajuda a tarefa;
5. apenas um editor principal fica aberto na mesma zona;
6. `Cancelar` fecha o cartão e limpa apenas o rascunho não guardado;
7. se houver alterações, fechar exige confirmação;
8. após guardar, o cartão fecha ou muda para modo de detalhe, e a lista é atualizada;
9. abrir o formulário não cria ainda um registo persistente;
10. não navegar para outra página nem usar modal para formulários extensos.

### Cartão de filtros

- O botão chama-se `Filtros` e pode mostrar a contagem ativa: `Filtros · 2`.
- Ao abrir, apresenta apenas filtros relevantes para essa lista.
- `Aplicar filtros` atualiza resumo, calendário e lista relacionados.
- `Limpar filtros` repõe todos os filtros dessa vista.
- Fechar o cartão não limpa filtros aplicados.
- Filtros simples e permanentes podem ficar sempre visíveis; não duplicar os mesmos filtros num cartão.

## 10. Campos e formulários

- Altura normal: 40px; compacta: 34px.
- Um botão colocado na mesma linha de inputs/selects usa `40px` para alinhar com os controlos. Esta regra contextual não altera os botões standard de `36px`, as ações de rodapé nem os botões de paginação `36 × 36px`.
- Esta regra é obrigatória no componente global: não deve ser recriada ou ajustada manualmente em cada módulo. As filas canónicas usam `.filters`, `.search`, `.history-filters` ou `.dmo-filter-row`; o botão filho direto herda automaticamente `40px`.
- Em seletores segmentados, a opção selecionada fica preenchida com a cor principal e texto branco; as restantes ficam brancas, delineadas e com texto na cor principal. O primeiro valor funcional do módulo deve iniciar selecionado.
- Labels ficam acima dos campos.
- Foco: borda azul e halo discreto.
- Erro: mensagem imediatamente abaixo do campo.
- Campos obrigatórios são validados antes de guardar.
- Placeholder dá exemplo, não substitui label.
- Valores numéricos apresentam no máximo duas casas decimais, salvo regra de domínio explícita.

### Largura proporcional

| Dado | Largura esperada |
|---|---|
| Pesquisa, nome, referência, observações | flexível |
| Máquina/linha, estado curto | 90–140px |
| Lote, quantidade, percentagem | 90–130px |
| Data | 150–180px |
| Produção | 110–140px |
| Notas | ocupa o espaço restante |

Não usar largura total para dois ou três dígitos.

### Ordem

- campos mais usados e de identificação primeiro;
- data no final da linha de identificação, salvo razão operacional;
- observações/notas na última linha;
- campos relacionados ficam juntos;
- uma linha não deve ficar artificialmente cheia quando campos pequenos podem ser compactados.

## 11. Dropdowns e pesquisa

- Usar o dropdown canónico, nunca `datalist` nativo quando o menu precisa de estilo consistente.
- Mesma altura e borda dos inputs.
- Hover de opção: `--dmo-brand-050`.
- Opção selecionada: `--dmo-brand-100`, texto `--dmo-brand-700`.
- Pesquisa incremental quando existirem muitas opções.
- `Escape` fecha; setas percorrem; `Enter` escolhe.
- Lista vazia mostra `Sem resultados` dentro do menu.
- Dados filtrados devem indicar a origem quando isso evita ambiguidade operacional.

### Opções de negócio configuráveis

- Valores que possam crescer ou mudar — materiais, tipos, versões, adaptadores, reparadores e equivalentes — não ficam hardcoded no HTML ou no frontend.
- Cada opção pertence a um catálogo do módulo e campo corretos, configurável em `Definições` por utilizador autorizado.
- A gestão permite adicionar, editar, ordenar e desativar; não usar eliminação destrutiva quando a opção já aparece no histórico.
- Desativar retira a opção de novas escolhas, mas preserva o rótulo guardado em registos e snapshots antigos.
- Máquinas, paginação, estados de sistema e outros valores técnicos usam os respetivos catálogos canónicos; não devem ser misturados num catálogo genérico só por também aparecerem num dropdown.

### Seletor contextual de registos relacionados

Usar quando um campo escolhe um registo existente de outro módulo, por exemplo um lote de CM, MF ou BQ:

- o menu rápido apresenta apenas resultados fornecidos pela fonte autoritativa e compatíveis com os filtros explícitos do contexto;
- quando a quantidade ou o detalhe dos resultados não couber no menu, incluir `Ver todos os resultados compatíveis` e abrir a lista canónica;
- a lista completa usa um clique para selecionar e duplo clique para abrir o registo; a confirmação da relação usa uma ação externa à lista;
- um resultado nunca é escolhido automaticamente apenas por ser o primeiro;
- cada opção mostra contexto suficiente para distinguir entidades semelhantes;
- se um valor copiado deixar de ser compatível, preservá-lo como valor anterior visível e apresentar aviso; só bloquear ou exigir nova escolha quando o contrato do domínio o determinar explicitamente;
- o seletor associa o ID estável do registo e não cria, edita nem elimina dados do módulo de origem;
- ausência real de resultados, erro de carregamento e falta de permissão são três estados diferentes e devem ter mensagens diferentes.
- uma pesquisa sem correspondência nunca cria automaticamente a entidade pesquisada; a criação continua no fluxo autorizado do módulo de origem.

## 12. Cartões

- Fundo branco, borda subtil, raio 12px e sombra muito leve.
- Padding: 16–20px.
- Espaço entre cartões: 12–16px.
- Evitar cartões dentro de cartões sem hierarquia.
- Cartão selecionável tem hover discreto e foco visível.
- Alerta usa indicador lateral e mensagem orientada à ação.
- Não usar selos para estados normais desnecessariamente.

## 13. Listas e tabelas canónicas

Todos os módulos usam a mesma regra, sem botões adicionais de abertura:

- um clique seleciona uma única linha;
- duplo clique abre o registo/detalhe associado;
- `Enter` seleciona;
- `Ctrl+Enter` abre;
- seleção usa classe `selected` e `aria-selected="true"`;
- contentor usa `data-dmo-list`;
- linha usa `data-dmo-row` e `data-id` estável;
- não existe botão `Abrir folha selecionada`;
- ações como corrigir, eliminar ou editar ficam fora da lista e dependem da seleção;
- linhas não contêm botões de ação repetidos;
- se um filtro remover a seleção, limpar o detalhe ou selecionar explicitamente o primeiro resultado conforme o fluxo documentado.

### Tabelas

- Cabeçalho neutro, não azul vivo.
- Cabeçalho fixo quando existir scroll vertical.
- Linhas com 40–46px.
- Números alinhados de forma consistente.
- Densidade suficiente para evitar scroll horizontal desnecessário.
- Scroll horizontal fica dentro do cartão e apenas quando inevitável.
- Não repetir colunas disponíveis no detalhe sem valor operacional.

### Limite e paginação

- Todas as listas com dados usam paginação; não crescer indefinidamente na página.
- O seletor de limite apresenta exclusivamente `20`, `40` e `60` linhas.
- O valor inicial é `20`, salvo requisito funcional explícito diferente.
- O rodapé mostra `N registos · Página X de Y`.
- Navegação usa setas `Anterior` e `Seguinte`, com rótulo acessível e estado desativado nos limites.
- Alterar filtros ou limite regressa à página 1 e limpa uma seleção que deixe de estar visível.
- Paginação é controlada pelos dados/serviço; o frontend não carrega todo o universo apenas para o cortar visualmente.

### Movimentos

- Entrada: texto explícito e fundo verde muito claro.
- Saída: texto explícito e fundo laranja muito claro.
- Seleção sobrepõe-se visualmente ao tom do movimento sem esconder o texto.

## 14. Filtros de listas

Ordem recomendada:

1. pesquisa livre;
2. tipo;
3. estado;
4. entidade específica do domínio;
5. intervalo de datas;
6. quantidade de linhas/página.

Regras:

- pesquisa descreve o que aceita: `Referência, lote ou linha`;
- intervalo usa `Desde` e `Até`;
- filtros afetam todos os resumos ligados à lista;
- mostrar um resumo curto dos filtros ativos;
- `Limpar filtros` é uma única ação;
- não ocultar a ausência de resultados: apresentar estado vazio claro.

## 15. Calendário canónico

- Todos os módulos reutilizam o mesmo componente e CSS.
- Usar `dmo-calendar__head`, `dmo-calendar__week`, `dmo-calendar__grid` e `dmo-calendar__day`; não copiar CSS do calendário para a página.
- Cabeçalho: mês/ano ao centro, anterior e seguinte nas extremidades.
- Semana começa em segunda-feira.
- Dias usam grelha de sete colunas.
- Dia com registos apresenta ponto discreto.
- Um clique seleciona o dia e filtra a lista associada.
- Dia selecionado usa fundo azul e texto branco.
- `Mostrar todas as datas` remove apenas o filtro de data.
- Alterar mês não seleciona automaticamente um dia.
- Datas sem registos continuam clicáveis quando o fluxo permite consulta vazia.
- Quando um módulo permite criar a partir de uma data futura, a ação de criação fica junto da lista/estado vazio associado ao dia; o calendário em si não contém botões diferentes por módulo.
- Uma data passada pode filtrar movimentos e uma data futura pode iniciar criação, mas ambos reutilizam exatamente o mesmo estado visual de seleção.
- registos planeados usam ID estável; mudar uma data atualiza o mesmo evento e não duplica entradas;
- alterações de período só aparecem no calendário depois de persistidas;
- um campo de data operacional pode ser atualizado enquanto o registo estiver ativo; depois de fechado, o último valor persistido torna-se o valor final e as alterações anteriores permanecem auditáveis;
- Teclado e `aria-pressed` são obrigatórios.
- É proibido criar um calendário visual diferente dentro de um módulo.

## 16. Estados visuais

| Estado | Fundo | Texto |
|---|---|---|
| Pendente | `--dmo-pending-soft` | `--dmo-pending` |
| Aprovado/ativo | `--dmo-success-soft` | `--dmo-success` |
| Atenção | `--dmo-warning-soft` | `--dmo-warning` |
| Não aprovado/erro | `--dmo-danger-soft` | `--dmo-danger` |
| Inativo | superfície cinzenta | `--dmo-text-muted` |

- Tipo de registo não é estado.
- Estado aparece com texto; a cor não basta.
- Tons são dessaturados e próximos da base do sistema.

## 17. Alertas e feedback

### Inline

- junto do elemento que exige ação;
- mensagem curta e concreta;
- não usar `alert()` ou `prompt()` para explicar conflitos;
- exemplo: `Referências diferentes nesta linha. Remova uma referência para continuar.`

### Toast

- confirmação breve e não bloqueante;
- canto inferior;
- desaparece automaticamente;
- erro que exige correção permanece também junto do campo.

### Confirmação

Usar apenas para eliminar, fechar, perder alterações, reset de palavra-passe ou outra ação difícil de reverter.

## 18. Modais

- Apenas para ações rápidas, confirmação ou edição focada.
- Formulários longos abrem inline.
- Cabeçalho: título, contexto e fechar.
- Rodapé compacto com ações à direita.
- `Escape` fecha quando não há perda de dados.
- Fundo escurecido, mas reconhecível.
- Largura acompanha o conteúdo; não ocupar o ecrã sem necessidade.

## 19. Menu contextual `…`

- Extensão visual do cartão que o abriu.
- Largura ajustada ao maior texto.
- Padding compacto.
- Hover azul suave/escuro conforme a superfície de origem.
- Não usar branco dominante sobre painel escuro.
- Ação destrutiva mantém texto vermelho discreto.
- Com registo: mostrar apenas ações válidas, por exemplo substituir/remover.
- Sem registo: mostrar apenas adicionar.

## 20. Painel lateral

- Fixo no desktop quando o módulo acompanha máquinas/linhas.
- Fundo `--dmo-brand-900/950`.
- Mostra apenas estado operacional atual.
- Totais analíticos pertencem ao conteúdo da página.
- Clique no cartão abre o registo associado quando essa regra estiver definida.
- Conflitos aparecem no próprio cartão com mensagem curta; não abrir escolha por `prompt`.
- Em mobile transforma-se em gaveta ou bloco recolhível.

## 21. Paginação e estados vazios

- Paginação fica abaixo da lista.
- `Anterior` e `Seguinte` desativam nos limites.
- Mostrar `N registos · Página X de Y`.
- Estado vazio explica o motivo e o próximo passo.
- Não mostrar grandes áreas tracejadas sem conteúdo útil.
- Carregamento usa skeleton ou mensagem curta; não deslocar drasticamente o layout.

## 22. Responsividade

Breakpoints de referência: 1200px, 980px e 720px.

- Reorganizar grelhas antes de reduzir texto.
- Preservar campos essenciais.
- Tabelas podem ter scroll dentro do cartão.
- Botões mantêm área adequada ao toque.
- Header preserva logo, título e identidade.
- Side panel torna-se recolhível.
- Nunca criar scroll horizontal na página inteira.

## 23. Acessibilidade

- Objetivo mínimo WCAG AA.
- Foco visível em todos os controlos.
- Operação por teclado em tabs, listas, dropdowns, calendários e menus.
- `aria-label` em botões apenas com ícone.
- `aria-expanded` e `aria-controls` em expansores.
- `aria-live` para feedback importante.
- Não depender apenas de cor.
- Respeitar `prefers-reduced-motion`.

## 24. Login e Administração

### Login

- Identidade à esquerda e formulário à direita no desktop.
- Conteúdo centrado, sem vazio vertical excessivo.
- Email, palavra-passe, mostrar/ocultar e Entrar.
- Erros no formulário, sem pop-up.
- Não mostrar credenciais de teste.

### Administração

- Página própria para administradores.
- Gestão de utilizadores, templates de acesso e aplicações. Para utilizadores operacionais, a landing page Job On não é configurável; o Administrador puro entra diretamente na shell administrativa e não recebe módulos operacionais.
- O campo livre `profileTitle` alimenta o título/função no header.
- `profileTitle` não altera permissões.
- Reset de palavra-passe exige confirmação e auditoria; nunca mostra a palavra-passe atual.
- A tab `Auditoria` usa a tabela/lista canónica: um clique seleciona, duplo clique abre detalhe, 20/40/60 linhas e filtros compactos.
- O histórico anual mostra factos por utilizador e módulo; não inclui pontos, ranking nem interpretação automática.
- Eventos são append-only e visualmente distinguem sucesso, falha, acesso negado e correção sem depender apenas da cor.

## 25. Contrato para criar um módulo novo

Antes de desenhar:

1. confirmar objetivo, atores e permissões;
2. confirmar entidades e fontes de dados existentes;
3. listar ações e estados reais;
4. identificar o que é seleção, abertura, criação, edição e filtro;
5. não inventar nomes de campos ou regras em falta.

Construção obrigatória:

1. aplicar o header global;
2. criar tabs operacionais e colocar Definições/Administração à direita;
3. criar título e descrição da vista;
4. definir toolbar compacta;
5. ações de criação/edição/filtro expandem cartões inline;
6. usar campos proporcionais;
7. aplicar lista canónica: clique seleciona, duplo clique abre;
8. reutilizar o calendário canónico quando houver datas;
9. colocar ações dependentes de seleção fora da lista;
10. usar estados e cores globais;
11. documentar estados vazios, erros e carregamento;
12. verificar desktop, 980px e 720px;
13. garantir teclado e atributos ARIA;
14. criar handoff do módulo apenas com regras específicas do domínio.

O handoff de cada módulo deve explicar:

- finalidade de cada tab;
- origem de cada grupo de dados;
- comportamento de cada botão;
- cartão que expande e campos apresentados;
- filtros e componentes afetados;
- ações de clique simples e duplo clique;
- estados, validações e mensagens;
- permissões necessárias;
- o que acontece depois de guardar, cancelar, eliminar ou fechar;
- integrações futuras sem inventar contratos ainda não confirmados.

## 26. Contratos JavaScript/HTML partilhados

| Componente | Contrato |
|---|---|
| Lista | `data-dmo-list`, `data-dmo-row`, `data-id` |
| Seleção | classe `selected`, `aria-selected` |
| Calendário | `data-dmo-calendar`, `data-date` |
| Expansor | `aria-expanded`, `aria-controls` |
| Perfil | `data-user-profile-name`, `data-user-profile-title` |
| Toast | região `aria-live` |

Eventos partilhados recomendados:

- `dmo:list-select`;
- `dmo:list-open`;
- `dmo:date-select`;
- `dmo:filters-change`;
- `dmo:editor-open`;
- `dmo:editor-close`.

A lógica de domínio não deve ser duplicada dentro dos componentes visuais.

## 27. Organização técnica

```text
shared/
  styles/
    dmo-tokens.css
    dmo-components.css
    dmo-layout.css
    dmo-utilities.css
  scripts/
    dmo-interactions.js
modules/
  <module>/
    <module>.css
    <module>.js
    HANDOFF.md
```

Pode existir um único `dmo-design-system.css` na primeira implementação, mas deve manter tokens, layout e componentes claramente separados. CSS do módulo contém apenas layout específico, nunca versões próprias dos componentes globais.

## 28. Critérios de aceitação global

- Uma alteração num token atualiza todos os módulos dependentes.
- Nenhum botão usa `brightness`.
- Todos os botões respeitam filled → hover invertido.
- Formulários extensos expandem inline.
- Filtros usam o mesmo padrão de cartão e limpeza.
- Todas as listas usam clique para selecionar e duplo clique para abrir.
- Não existem botões redundantes `Abrir folha selecionada`.
- Todos os calendários são o mesmo componente.
- Header contém logo, página, nome e título/função.
- Campos pequenos não ocupam largura excessiva.
- Estados usam texto e tokens semânticos.
- A interface funciona por teclado e a 720px.
- Cada módulo possui handoff específico sem contradizer esta especificação.

## 29. Verdade dos dados, pesquisa e ambiguidade

- A UI apresenta factos vindos da fonte autoritativa; não deduz relações a partir de nomes, códigos ou ausência de dados.
- Informação em falta aparece como `Não definido`, `Não disponível` ou `Por confirmar`, conforme o significado real.
- Não converter ausência de dados em estado operacional, avaria, reparação ou indisponibilidade.
- Relações entre áreas usam IDs estáveis; o texto apresentado ao utilizador não é uma chave de integração.
- Resultados de pesquisa mostram contexto suficiente para distinguir registos semelhantes.
- Uma pesquisa ambígua nunca seleciona automaticamente um resultado.
- Quando existirem vários resultados, o utilizador escolhe explicitamente.
- Filtros reduzem apenas factos registados; não “descodificam” uma referência para inventar associações.

Contexto mínimo recomendado num resultado ambíguo, quando disponível e relevante:

- referência;
- lote;
- tipo;
- máquina/linha;
- estado;
- data ou produção associada.

## 30. Estado atual versus snapshot histórico

Quando uma página apresenta passado e presente, os dois contextos devem estar visualmente separados.

### Snapshot histórico

- mostra o que estava selecionado, conhecido ou registado naquele momento;
- inclui uma etiqueta `Na produção/registo` ou `Snapshot histórico`;
- apresenta a data do snapshot;
- não muda quando o estado atual é alterado.

### Estado atual

- apresenta dados live provenientes da respetiva fonte autoritativa;
- inclui etiqueta `Estado atual` e, quando relevante, hora da última atualização;
- pode indicar alterações ocorridas desde o snapshot;
- não reescreve o histórico.

Comparações devem apresentar ambos lado a lado ou em blocos consecutivos claramente titulados. Não misturar valores históricos e atuais no mesmo cartão sem identificação por coluna.

## 31. Comandos, persistência e feedback

- Um botão solicita uma ação sem conter a regra de negócio.
- A UI só apresenta sucesso depois da autorização, validação e persistência confirmadas.
- Durante a operação, o botão mostra estado de processamento e bloqueia submissões repetidas.
- Se a persistência falhar, preservar os dados introduzidos sempre que possível.
- Uma falha não fecha o editor nem substitui o último estado válido.
- A mensagem de erro explica a ação mínima seguinte.
- Consultas, filtros, seleção de tabs e abertura de detalhes não alteram dados persistentes.
- Estado puramente visual — tab, pesquisa, modal, seleção e scroll — não se torna dado de domínio sem motivo confirmado.
- Ocultar um botão não substitui autorização no servidor/aplicação.

## 32. Correção e auditoria na interface

Quando uma correção altera um facto relevante, o fluxo deve conseguir apresentar:

- autor;
- data/hora;
- registo afetado;
- valor anterior e novo quando aplicável;
- motivo quando exigido pelo fluxo.

Regras visuais:

- `Corrigir` é diferente de `Eliminar`;
- histórico significativo não é silenciosamente reescrito;
- registos referenciados por outros fluxos preferem desativar/arquivar ao apagamento destrutivo, quando a regra de negócio o confirmar;
- revisão anterior permanece consultável;
- alteração de um registo aprovado deve indicar claramente quando perde aprovação e exige nova decisão.

## 33. Padrão de registo rápido

Usar apenas em fluxos frequentes e simples confirmados, como uma intervenção operacional curta.

Sequência:

1. caixa de pesquisa/scan recebe foco automaticamente;
2. utilizador introduz ou seleciona o registo;
3. contexto mínimo é confirmado;
4. ação principal guarda;
5. após sucesso, formulário limpa e regressa ao estado pronto;
6. autor e data/hora são capturados automaticamente.

Regras:

- deve ser possível concluir com teclado, sem rato;
- evitar grelhas extensas quando um identificador direto é suficiente;
- notas são opcionais salvo regra explícita;
- `Enter` avança/confirma apenas quando não houver ambiguidade;
- erro mantém valor e foco no campo relevante;
- o modo rápido não reduz validação, autorização ou auditoria.

## 34. Reutilização visual sem fusão de domínio

- BQ, CM, MF e outros tipos podem partilhar cartões, listas, filtros e padrões de reparação.
- Aparência semelhante não significa que campos, estados, frequência ou regras são iguais.
- Um componente partilhado recebe dados e ações específicas; não decide regras comuns por conveniência visual.
- Agrupar fluxos numa tab ou módulo não funde identidades nem históricos.
- A apresentação recebe apenas os dados necessários para a vista, através de um modelo próprio da página.
- A interface deve poder mudar sem alterar regras, permissões, histórico ou significado persistido.

## 35. Checklist de operação em lote

Usar apenas quando várias entidades participam na mesma operação confirmada, como uma Saída programada.

- a seleção múltipla usa checkboxes explícitos e não altera a regra das listas normais;
- clique na linha continua a selecionar e duplo clique continua a abrir o detalhe, quando aplicável;
- marcar um checkbox representa progresso da tarefa, não necessariamente alteração do estado de domínio;
- a UI explica claramente quando a alteração real acontece;
- progresso individual pode ser persistido sem produzir movimentos parciais quando o domínio exige conclusão conjunta;
- a operação final deve mostrar estado de processamento e impedir submissão duplicada;
- se a conclusão atómica falhar, não apresentar sucesso parcial;
- a versão impressa contém apenas informação operacional necessária e imprimir nunca executa o comando.

## 36. Verificações recorrentes por contexto

- separar a regra reutilizável da ocorrência concreta apresentada ao utilizador;
- a regra define o contexto e a repetição; a ocorrência guarda confirmação, operador e data/hora;
- uma confirmação nunca apaga a regra nem o histórico;
- itens concluídos podem ficar ocultos por defeito, com `Mostrar concluídos`;
- recorrência usa IDs estáveis do contexto, nunca comparação de texto;
- alterações à regra aplicam-se ao futuro e não reescrevem snapshots;
- arquivar impede novas ocorrências e preserva as anteriores;
- resetar cria nova pendência e preserva a confirmação anterior, o autor e a data do reset;
- falha ao confirmar mantém o item pendente;
- checkboxes representam comandos persistidos, não efeitos visuais locais.

## Normalização de cabeçalhos, títulos e botões

- Todas as páginas operacionais usam o cabeçalho comum `dmo-app-header`, com título a 18 px e descrição a 11 px.
- O título funcional usa `dmo-page-head` (ou os aliases de migração `page-head` / `page-heading`), a 24 px e peso 800.
- Não apresentar IDs internos, chaves de revisão, nomes de campos da base de dados ou explicações da arquitetura no interface operacional.
- Botões de ação ficam preenchidos com a cor da ação e texto branco em repouso; no hover/foco invertem para branco.
- As classes antigas `secondary` e `ghost` não devem produzir botões brancos em repouso.
- Tabs, ligações de navegação, dias do calendário e ícones sem ação textual não usam o estilo de botão de ação.

## Persistência dos dois níveis de navegação

- Toda a página operacional mantém o primeiro nível com `Job On`, `Controlo`, `Reparação Interna`, `Boquilhas` e `Armazém`.
- Abrir um subfluxo, como Peso, Pegamentos, Consulta ou Histórico, nunca substitui nem remove o primeiro nível.
- O segundo nível identifica apenas as áreas internas do módulo atual e usa `dmo-secondary-nav`.
- Quando as duas barras são linhas diretas da página, ficam empilhadas e visíveis durante o scroll.
