# REPARAÇÃO INTERNA — MODELO FUNCIONAL

OPEN OWNER QUESTIONS: 0 (Q1/Q2 FECHADAS POR CLARIFICAÇÃO EXPLÍCITA DO OWNER)

<a id="indice"></a>
## Índice

1. [Objetivo](#objetivo)
2. [Âmbito e Classificação](#ambito-e-classificacao)
3. [Utilizadores e Acesso](#utilizadores-e-acesso)
4. [Áreas Internas e Fluxo de Registo](#areas-internas-e-fluxo-de-registo)
5. [Contexto de Produção](#contexto-de-producao)
6. [Identificação e Validação](#identificacao-e-validacao)
7. [Ocorrências Repetidas vs Correções](#ocorrencias-repetidas-vs-correcoes)
8. [Modelo de Correção e Anulação](#modelo-de-correcao-e-anulacao)
9. [Dados, Histórico e Auditoria](#dados-historico-e-auditoria)
10. [Fronteiras e Ownership](#fronteiras-e-ownership)
11. [Regras Negativas — RI DOES NOT...](#regras-negativas-ri-does-not)
12. [Material Histórico / Superseded](#material-historico-superseded)
13. [Clarificações do Owner Fechadas](#clarificacoes-do-owner-fechadas)
14. [Detalhes Adiados](#detalhes-adiados)
15. [Resumo Funcional Final](#resumo-funcional-final)

<a id="objetivo"></a>
## Objetivo

A Reparação Interna (RI) é o registo operacional rápido de factos de reparação interna de ferramentas CM e MF durante a produção, executado pelo reparador de turno.

A RI regista ocorrências de reparação efetivamente realizadas por operadores durante os turnos. Cada registo RI responde a:

- qual CM ou MF foi reparada;
- qual o número individual da ferramenta;
- quem reparou;
- em que Linha;
- sob que contexto de Produção/Referência;
- quando a reparação ocorreu.

A RI regista ocorrências de reparação. Não gere um ciclo de vida de reparação.

Não existe pré-registo obrigatório. A RI não gere `Por reparar` / `Reparado`, nem início/fim, fila, duração ou transições de estado de reparação. Esses estados pertencem à gestão/estado da ferramenta armazenada no contexto Armazém (stored-tool / warehouse-tool) e não são estados de registo RI. Registar uma ocorrência na RI não escreve, transiciona, infere nem altera automaticamente nenhum deles. A RI é dona apenas dos registos de ocorrências de reparação e da cadeia de correção/histórico.

A mesma ferramenta CM/MF individual pode ser reparada várias vezes na mesma produção ou turno. Cada ocorrência efetiva cria um novo registo RI independente. Nunca deduplicar nem fundir reparações repetidas.

O registo mínimo exige apenas:

- Linha (B1–C3);
- Tipo (CM/MF);
- número individual da ferramenta.

Referência, lote, produção e contexto do Job On são associados automaticamente quando disponíveis. A falta de contexto nunca bloqueia o registo operacional (R009 — NO operational hard blocks).

O registo serve de evidência factual, por exemplo para diário anual de auditoria. O sistema não calcula pontuação, ranking, produtividade ou avaliação automática.

<a id="ambito-e-classificacao"></a>
## Âmbito e Classificação

### Ferramentas abrangidas

A RI trata apenas:

- CM;
- MF.

### Exclusão de BQ

BQ nunca é:

- selecionável;
- reparável;
- processada como input de reparação na RI.

BQ pode aparecer apenas como identificação contextual dentro da referência completa de produção.

Exemplo:

`5447T173`

`T173` é contexto apenas.

RI e Boquilhas são módulos independentes. Não existe dependência funcional. Não existe dependência técnica afirmada. Não existe reutilização afirmada de serviço, API ou contexto entre RI e Boquilhas. O master BQ pertence a Ferramentas.

### Classificação

A RI é um TOP-LEVEL MODULE.

- Módulo funcional de topo.
- Atribuível por utilizador no Admin.
- Posição no primeiro nível de navegação operacional.
- Não é área interna, workflow ou variante de perfil de outro módulo.

As tabs Registo e Consulta são áreas internas do módulo RI. Nunca são módulos independentes.

### Independência

- A RI é independente do Controlo; ambos consomem o contexto do Job On diretamente.
- A RI é distinta da Reparação Externa; o workflow externo pertence à sua própria documentação.

<a id="utilizadores-e-acesso"></a>
## Utilizadores e Acesso

### Reparador

O reparador é sempre o utilizador autenticado.

- Nunca é selecionado manualmente a partir de qualquer diretório.
- “Reparador de turno” é o título funcional do papel.
- “Reparador de turno” não é um quarto perfil.

Existem exatamente três perfis globais funcionais:

- Admin;
- Responsável;
- Operador / Controlador.

Nenhum perfil novo é criado.

### Acesso

O acesso é controlado pela atribuição individual do módulo.

Módulo atribuído ≠ perfil.

O Admin não é operacional por omissão. O privilégio administrativo não concede módulos operacionais automaticamente.

### Comportamento por perfil dentro da RI

#### Operador / Controlador

Quando a RI lhe está atribuída:

- é o utilizador operacional de registo da RI;
- o utilizador autenticado é o reparador;
- regista as próprias ocorrências de reparação CM/MF;
- a vista operacional da RI baseia-se na própria atividade;
- pode corrigir/anular os próprios registos de acordo com as regras estabelecidas da RI.

#### Responsável

O Responsável tem visibilidade adicional read-only ao nível da Produção através do Job On.

Para uma Produção / run de fabrico, o Responsável pode consultar:

- Controlo for that Production;
- RI repair occurrences during that Production;
- who performed those RI repairs;
- relevant BQ repair/movement information associated with that Production;
- actors where the source record provides them.

Esta vista é:

- read-only;
- contextual à Produção;
- exposta através do Job On;
- informação adicional para o Responsável.

Não está estabelecido que o Responsável:

- repara;
- regista ocorrências RI;
- corrige registos RI;
- anula registos RI;
- edita registos RI de outros utilizadores.

A única diferença de perfil confirmada é a consulta adicional read-only ao nível da Produção através do Job On. Esta consulta não confere poderes de escrita sobre registos RI e não transfere posse para o Job On.

### Permissões funcionais

- Registar: **Operador / Controlador com RI atribuída**, enquanto utilizador autenticado/reparador.
- Consultar própria atividade: registos do utilizador autenticado.
- Consultar a partir do Job On (Produção): perfil Responsável — consulta read-only de Controlo + RI (reparações e atores) + informação relevante de reparação/movimentos BQ daquela Produção.
- Corrigir/Anular: apenas os próprios registos; regra de autorização no backend, não apenas UI.
- Consultar todos os utilizadores: apenas no Admin/Auditoria.

<a id="areas-internas-e-fluxo-de-registo"></a>
## Áreas Internas e Fluxo de Registo

### Tab Registo

1. Seletor de Linha: cartões B1–C3, de largura total. Cada cartão mostra Linha, Referência completa (ex.: `5447T173`, sem truncar) e Produção correspondentes ao **contexto RI aplicável à Linha**.
2. Contexto automático: após selecionar a Linha, o ecrã mostra apenas Linha, Referência e Produção. O painel de contexto interno resolve ID do Job On, revisão exata, lote, IDs estáveis e data/hora. Estes elementos internos não ocupam o ecrã de Registo.
3. Tipo e Número: seleção CM/MF e introdução do número individual.
4. “Os meus últimos registos”: lista recente com paginação. Botões de Corrigir/Apagar fora da tabela.
5. Layout obrigatório: seletor de Linha em cartão horizontal no topo; painel de contexto em linha própria por baixo. Nunca lado a lado; sem scroll horizontal.

### Tab Consulta

1. Filtros: Datas, Linha, Produção, Referência/lote, Tipo, número e “apenas corrigidos”.
2. Lista e Detalhe: coluna Estado (`Atual` / `Corrigido`). Duplo clique abre detalhe read-only com histórico completo de correções.
3. Correção inline: cartão com Linha, Tipo, Número e Nota opcional. O contexto original permanece read-only.

### Fluxo de registo operacional

1. Escolher a Linha, por exemplo B1. O sistema resolve automaticamente o Job On aplicável e mostra ao reparador Linha, Referência completa e Produção. A revisão exata, lote e restantes relações de contexto são resolvidos e preservados internamente quando disponíveis.
2. Escolher CM ou MF.
3. Introduzir o número individual.
4. Confirmar `OK · Registar`.

### Pós-persistência

Após persistência real:

- é criado um registo RI independente;
- Linha e Tipo permanecem selecionados onde estabelecido;
- o campo número é limpo;
- o foco regressa ao campo número;
- o sucesso só é mostrado após persistência real.

O utilizador não volta a introduzir manualmente:

- Referência;
- Produção;
- Job On;
- revisão;
- lote;
- reparador;
- data/hora.

<a id="contexto-de-producao"></a>
## Contexto de Produção

A associação conceptual é:

Linha  
→ produção aplicável  
→ Job On exato  
→ revisão exata do Job On  
→ Produção  
→ Referência completa  
→ relações de lote/ferramenta quando resolvíveis

### Contexto visível operacional

No ecrã de registo são visíveis:

- Linha;
- Referência completa;
- Produção.

### Contexto resolvido/guardado internamente quando disponível

Internamente, quando disponível, a RI resolve/preserva:

- Job On ID;
- revisão exata;
- relações de lote;
- relações de ferramenta;
- Production ID;
- identificadores estáveis;
- metadados de auditoria.

Os IDs internos não devem ser apresentados como UI visível obrigatória.

### Regra temporal 06:00 / 09:00

- A mudança/preparação física da produção ocorre às 06:00.
- Entre 06:00 e 08:59, a RI mantém como contexto a produção anterior dessa Linha.
- Às 09:00, a RI passa automaticamente para o novo contexto indicado pelo Job On.
- A data final, isolada, não provoca troca de contexto.
- Esta regra é exclusiva da projeção de contexto da RI.
- Esta regra não altera datas, estados, planeamento ou calendário do Job On.

### Sem contexto / contexto ambíguo

A falta de contexto Job On/Produção não bloqueia o registo.

Se não houver contexto resolvível:

- o registo continua;
- a UI pode mostrar `Sem associação`;
- a RI mantém o facto operacional de reparação;
- nenhuma relação é inventada.

Se o contexto estiver ambíguo:

- não auto-selecionar;
- não inventar certeza;
- o registo continua sem associação inequívoca.

Não introduzir seleção manual de Produção/Referência.

`Editar contexto` não faz parte da verdade funcional. Se existir, deve ser tratado apenas como divergência da implementação atual.

<a id="identificacao-e-validacao"></a>
## Identificação e Validação

### Identificação

A identificação operacional do registo é:

- Tipo: CM ou MF;
- número individual.

### Números repetidos

Números repetidos são ocorrências independentes válidas.

Nunca deduplicar.

### Validação — princípio NO operational hard blocks

Bloqueios estruturais:

- Linha inválida;
- Tipo fora de CM/MF, incluindo BQ;
- Número vazio;
- Ator não autenticado;
- Módulo não atribuído.

Não bloqueante:

- falta de contexto;
- número não encontrado;
- tipo divergente do master;
- número repetido.

O sistema aceita o facto introduzido pelo reparador.

Não cria ferramentas automaticamente.  
Não troca CM↔MF.  
Não inventa lotes.

<a id="ocorrencias-repetidas-vs-correcoes"></a>
## Ocorrências Repetidas vs Correções

A mesma ferramenta CM ou MF individual pode ser reparada múltiplas vezes.

Exemplo:

CM 45 reparada às 10:00  
→ regressa à produção  
→ volta a sair às 12:30  
→ é reparada novamente

Isto cria dois registos RI independentes.

Regras:

- cada reparação efetiva cria um novo registo RI;
- reparações repetidas são normais;
- nunca deduplicar;
- nunca fundir reparações repetidas;
- nunca bloquear porque a mesma ferramenta foi reparada anteriormente;
- não modelar um único “ciclo de vida de reparação” persistente para a ferramenta.

### Ocorrência repetida de reparação

A mesma ferramenta é genuinamente reparada de novo mais tarde.

Resultado: novo registo RI independente.

### Correção

Um registo RI anterior contém informação incorreta.

Resultado: nova versão append-only desse mesmo registo histórico.

Os dois conceitos permanecem completamente separados. Não fundir ocorrência repetida com correção.

<a id="modelo-de-correcao-e-anulacao"></a>
## Modelo de Correção e Anulação

### Correção — append-only

As correções são append-only.

Uma correção cria uma nova versão. O original nunca é sobrescrito nem desaparece.

Correções sucessivas são suportadas:

Original  
→ Correção 1  
→ Correção 2  
→ Correção 3  
→ ...

Um registo já marcado `Corrigido` pode ser corrigido novamente.

A versão válida mais recente é operacional. A sequência completa permanece historicamente preservada.

Não introduzir:

- limite de correção a um nível;
- edição in-place;
- sobrescrita;
- colapso de histórico.

O reparador original e a data/hora originais permanecem read-only.

Mudança de Linha na correção: recalcula o contexto para a nova Linha sem alterar o Job On original.

### Anulação

`Apagar registo` é uma anulação auditável.

A anulação:

- remove o registo da vista operacional ativa;
- não executa hard delete do facto histórico.

Regras:

- apenas os próprios registos;
- confirmação em 2 passos na UI;
- sem motivo obrigatório.

<a id="dados-historico-e-auditoria"></a>
## Dados, Histórico e Auditoria

### Campos do registo

- ID estável;
- Linha;
- Tipo;
- Número;
- Operador (servidor);
- Data/hora (servidor);
- Job On/revisão (snapshot);
- Produção/Referência (snapshot);
- Lote efetivo, quando resolvido;
- Motivo da correção, opcional.

### Imutabilidade

Revisões posteriores do Job On não reinterpretam registos antigos. O contexto exato fica historicamente preservado.

### Auditoria

Cada ação preserva:

- ator canónico;
- nome legível em snapshot;
- data/hora;
- módulo;
- ação;
- entidade;
- resultado.

As ações integram o diário global de auditoria, append-only.

### História / Auditoria

História/Auditoria lê e apresenta. Não é dona dos factos RI.

<a id="fronteiras-e-ownership"></a>
## Fronteiras e Ownership

| Domínio factual | Owner funcional | Fronteira / regra |
|---|---|---|
| Registos de reparação interna e histórico | Reparação Interna | Dona dos registos de ocorrências de reparação e da cadeia de correção. |
| Master da ferramenta / identidade / dados-mestre técnicos | Ferramentas | A RI consome identidade em leitura. Não altera master, identidade nem dados-mestre técnicos. |
| Estado stored-tool (`Por reparar` / `Reparado`) | Armazém — contexto stored-tool / warehouse-tool | Gerido fora da RI. A RI não escreve, transiciona, infere nem gere estes estados. Registar uma ocorrência RI não altera automaticamente nenhum deles. Não existe transição RI→Armazém nem sincronização. |
| Localização física + movimentos | Armazém | Registar reparação não move a ferramenta fisicamente. A RI não infere movimentos. |
| Contexto de produção / referência | Job On | A RI consome o contexto. Não cria, não altera, não reconstrói o Job On. |
| Registos de Controlo | Controlo | O Controlo é dono dos seus registos. O Job On apenas os apresenta/integra na vista de Produção. |
| Registos de reparação/movimentos BQ | Boquilhas | A Boquilhas é dona dos seus registos BQ. A RI não depende da Boquilhas. |
| Leitura de factos de auditoria | História / Auditoria | Lê e apresenta. Não possui os factos RI. |
| BQ no contexto da RI | — | BQ nunca é reparada na RI. Pode aparecer apenas como contexto da referência completa de produção. RI e Boquilhas são módulos independentes. O master BQ pertence a Ferramentas. |
| Vista de Produção read-only | Job On — integração/apresentação | O Responsável pode consultar, a partir do Job On, Controlo + ocorrências RI + atores + informação relevante de reparação/movimentos BQ daquela Produção. Apenas leitura. O Job On fornece contexto de Produção e navegação/integração; não é dono destes registos. |

A consulta read-only de Produção disponibilizada ao Responsável através do Job On não transfere posse.

Cada módulo mantém-se dono dos seus registos:

- Controlo é dono dos seus registos de controlo;
- Reparação Interna é dona das suas ocorrências de reparação;
- Boquilhas é dona dos seus registos de reparação/movimentos BQ;
- o estado stored-tool `Por reparar` / `Reparado` pertence ao contexto Armazém;
- o Job On apenas fornece o contexto de Produção e a integração/navegação read-only.

<a id="regras-negativas-ri-does-not"></a>
## Regras Negativas — RI DOES NOT...

1. NÃO repara BQ.
2. NÃO torna BQ selecionável na RI.
3. NÃO processa BQ como input de reparação RI.
4. NÃO é dona do master da ferramenta; Ferramentas é.
5. NÃO é dona do estado stored-tool `Por reparar` / `Reparado`.
6. NÃO escreve, transiciona, infere nem sincroniza `Por reparar` / `Reparado`.
7. NÃO altera automaticamente o estado stored-tool após uma reparação.
8. NÃO move ferramentas fisicamente.
9. NÃO infere movimentos físicos.
10. NÃO participa no ciclo físico de Armazém.
11. NÃO seleciona o reparador manualmente; reparador = utilizador autenticado.
12. NÃO inventa associações Job On/Produção/Referência.
13. NÃO resolve ambiguidade automaticamente.
14. NÃO bloqueia o registo por falta de contexto.
15. NÃO deduplica números repetidos.
16. NÃO deduplica ocorrências de reparação repetidas.
17. NÃO funde ocorrências repetidas com correções.
18. NÃO altera o Job On.
19. NÃO reescreve factos históricos.
20. NÃO faz hard delete.
21. NÃO faz do Job On dono do histórico RI.
22. NÃO faz da História dona dos factos RI.
23. NÃO cria um quarto perfil; “reparador de turno” não é perfil.
24. NÃO transforma screen/tab em módulo; Registo/Consulta são áreas internas.
25. NÃO calcula pontuação, ranking, produtividade ou avaliação.
26. NÃO gere máquina de estados de reparação.
27. NÃO gere início/fim de reparação.
28. NÃO gere fila, reparação ativa ou duração.
29. NÃO tem modelo de quantidades.
30. NÃO tem documentos, impressão, PDF ou exportação.
31. NÃO permite corrigir/anular registos de outros; regra de backend.
32. NÃO cria dependência RI↔Boquilhas.
33. NÃO transfere ownership pela consulta read-only de Produção.
34. NÃO dá ao Responsável poderes de escrita na RI a partir da vista de Produção.

<a id="material-historico-superseded"></a>
## Material Histórico / Superseded

| Material | Afirmação | Estado |
|---|---|---|
| Cabeçalhos/Enums antigos | “BQ é um terceiro tipo registável.” | SUPERSEDED — correção definitiva 2026-08-22. |
| Plano antigo da RI | “Delete is NOT part of V1.” | SUPERSEDED — anulação auditável definida. |
| Comentários de migração | “Job On ativo obrigatório no registo.” | SUPERSEDED — R009 / NO operational hard blocks. |
| Brief antigo | Wording de hard blocks operacionais. | SUPERSEDED — contexto = assistência. |
| Aplicação pré-DES-014 | Layout antigo / BQ na UI. | SUPERSEDED — autoridade visual + DES-014. |
| Mockups demo | Referência truncada “5774”. | NÃO SÃO REGRA — regra: referência completa, ex. `5447T173`. |

<a id="clarificacoes-do-owner-fechadas"></a>
## Clarificações do Owner Fechadas

Não existe nenhuma questão genuína em aberto.

As duas questões anteriormente registadas foram fechadas por clarificação explícita do Owner. Não reabrir Q1/Q2. Não criar novas questões.

### Q1 — FECHADA

A RI regista ocorrências de reparação efetivamente realizadas.

Regra confirmada:

- não existe lifecycle RI `Por reparar` / `Reparado`;
- a RI não gere nem possui esses estados;
- esses estados pertencem à gestão/estado da ferramenta armazenada no contexto Armazém;
- registar uma ocorrência RI não altera automaticamente nenhum deles;
- a mesma ferramenta pode ter múltiplas ocorrências de reparação independentes;
- nunca deduplicar;
- nunca bloquear por reparação anterior;
- ocorrência repetida (novo registo) e correção (nova versão append-only do registo histórico) são conceitos separados.

### Q2 — FECHADA

O perfil Responsável tem consulta adicional read-only ao nível da Produção através do Job On.

Para uma Produção, o Responsável pode consultar:

- Controlo;
- RI repairs + actors;
- relevant BQ repair/movement information.

Esta clarificação não estabelece qualquer comportamento de escrita RI para o Responsável.

<a id="detalhes-adiados"></a>
## Detalhes Adiados

Detalhes adiados documentados, específicos da RI, não bloqueantes. Não são questões Owner. Não devem ser promovidos a comportamento confirmado.

1. Formato/intervalo do número individual de CM e MF.
2. Futura exigência de observação/motivo; hoje é opcional.
3. Mais de um lote do mesmo tipo ativo na Linha.
4. Campo “turno”, listado no brief, sem definição na autoridade.
5. Representação exata da anulação em listas/consultas.
6. Superfícies de UI em aberto, por exemplo nota da correção e composição do fluxo.
7. Política de relógio/offset de fábrica (DST).
8. Entrega da consulta ligada do Job On (`Ver reparações`).

<a id="resumo-funcional-final"></a>
## Resumo Funcional Final

A Reparação Interna regista ocorrências de reparação interna efetivamente realizadas de CM e MF durante a produção.

O reparador autenticado seleciona a Linha (B1–C3), o Tipo (CM/MF) e o número individual. A Referência não é escolhida manualmente. A partir da Linha, a RI resolve automaticamente o contexto aplicável no Job On e apresenta Linha + Referência completa + Produção quando disponível. A regra temporal é 06:00/09:00: entre 06:00 e 08:59 mantém-se a produção anterior; às 09:00 a RI muda automaticamente para o novo contexto. A referência completa, por exemplo `5447T173`, é preservada; `T173` é contexto apenas.

Se o contexto estiver ausente ou ambíguo, o registo continua permitido sem associação inequívoca, sem inventar relações e sem bloqueio.

Cada ocorrência de reparação efetiva cria um registo RI independente. A mesma ferramenta pode ser reparada várias vezes na mesma produção ou turno. Não existe deduplicação, não existe fusão e não existe bloqueio por reparação anterior. Não existe um único “ciclo de vida” de reparação.

Ocorrência repetida e correção são conceitos completamente separados:

- ocorrência repetida: a mesma ferramenta é genuinamente reparada de novo → novo registo RI independente;
- correção: um registo anterior contém informação incorreta → nova versão append-only desse registo histórico.

As correções são versões append-only que formam uma sequência histórica completa. O original e cada correção intermédia permanecem preservados. Um registo já marcado `Corrigido` pode ser corrigido novamente. A versão válida mais recente é usada operacionalmente.

A anulação remove o registo da vista operacional ativa sem apagar o facto histórico. Não existe hard delete.

A RI é dona dos seus registos e do seu histórico. História/Auditoria apresentam em leitura. O Job On integra para consulta sem posse. RI e Boquilhas são módulos independentes. BQ nunca é reparada, selecionável ou processada na RI.

A RI não gere `Por reparar` / `Reparado`, nem qualquer máquina de estados de reparação. Esses conceitos pertencem ao estado da ferramenta armazenada, contexto Armazém / stored-tool. Registar uma ocorrência RI não escreve, transiciona, infere nem sincroniza esses estados.

O Responsável tem, adicionalmente, uma consulta read-only ao nível da Produção através do Job On: Controlo + ocorrências RI e respetivos atores + informação relevante de reparação/movimentos BQ dessa Produção, com o ator quando a origem o fornecer. Esta consulta não altera registos, não confere poderes de escrita extra e não transfere ownership. Não é afirmado qualquer comportamento operacional de escrita do Responsável na RI.

Zero questões genuínas em aberto. Q1 e Q2 foram fechadas por clarificação explícita do Owner.

## Implementation Pointers

### Relevant implementation areas

- Application: occurrence registration (Linha cards B1–C3 → automatic Job On context resolution → Tipo CM/MF + individual number → `OK · Registar`); temporal context rule 06:00/09:00 (06:00–08:59 keeps previous production; 09:00 switches to the new context) — RI context projection only, never changes Job On data/calendar.
- Domain: record fields — stable ID, Linha, Tipo, Número, Operador (server), Data/hora (server), Job On/revisão (snapshot), Produção/Referência (snapshot), lote efetivo quando resolvido, motivo da correção opcional. Corrections = append-only version chain (`Atual`/`Corrigido`); anulação removes from the active view, never hard-delete. Snapshot semantics: later Job On revisions never reinterpret old records.
- Application: Corrigir/Anular only own records — backend authorization rule, not UI-only; structural blocks only: invalid Linha, Tipo outside CM/MF (incl. BQ), empty number, unauthenticated actor, module not assigned (R009 — NO operational hard blocks: missing/ambiguous context never blocks).
- Database/domain implications: RI does NOT write/transition/sync stored-tool state `Por reparar`/`Reparado` (Armazém context); no RI→Armazém transition; no master writes (Ferramentas, read-only identity); no physical movements; records integrate the global append-only audit diary.
- Technical map: `maps\11_REPARACAO_INTERNA.md` (verify freshness before use).

### Known implementation gaps

- `Editar contexto` is NOT part of the functional truth — if it exists in the implementation, treat it as a divergence of the current implementation (Contexto de Produção).
- Job On linked query delivery (`Ver reparações`) for the Responsável production-level read-only view is listed as a deferred detail (Detalhes Adiados nº 8) — the Job On query surface is not yet delivered.
- Deferred (non-blocking) context-resolution details affecting implementation: individual-number format/range, more than one active lot of the same type per Linha, "turno" field, factory clock/DST offset policy (affects the 06:00/09:00 rule).

### Design reference

- `AI-CONTEXT\design-coder\34_REPARACAO_INTERNA_01_VISUAL_AUTHORITY_reparacao-interna.html` (visual authority; pre-DES-014 layout / BQ in UI is superseded).

### Cross-module dependencies

- Job On (production context provider: exact Job On, revision, Produção, Referência; per-Linha resolution; Responsável read-only Production view); Ferramentas (CM/MF identity, read-only); Armazém (stored-tool state `Por reparar`/`Reparado` ownership); Controlo (independent; both consume Job On context; Controlo records shown in the Production view); Boquilhas (independent; BQ never RI — context only in the full reference); História (reads audit facts).