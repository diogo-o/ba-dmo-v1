# Armazém — brief funcional e de interface V1

Estado: base corrigida para mockup e handoff técnico  
Âmbito V1: registo e consulta de posições e movimentos de CM, MF e BQ

Este documento define a interface e a forma como o utilizador interage com ela. Regras de domínio, persistência, concorrência, permissões técnicas e efeitos finais dos comandos são contratos dos serviços responsáveis. O frontend apresenta os resultados devolvidos e não simula sucesso antes dessa resposta.

## 1. Limite do módulo

O Armazém é responsável apenas por:

- ferramenta que entrou ou saiu;
- posição física no Armazém;
- destino de uma Saída, quando aplicável;
- observações livres do movimento;
- operador e data/hora;
- diferenças encontradas fisicamente e correções posteriores.

Tudo o resto pertence ao domínio da ferramenta.

O Armazém não cria, altera nem recalcula:

- percentagem de vida/utilização;
- estados como `Sucatado`, `Arquivado`, `Reparado`, `Por reparar` ou `Novo`;
- Máquina/Linha associada;
- reparador;
- dados técnicos;
- Referência ou lote da ferramenta.

Esses valores podem aparecer como contexto read-only quando forem úteis para identificar uma ferramenta. A origem deve ser o domínio autoritativo de CM, MF ou BQ.

**Nota de reconciliação (OWNER-CONFIRMED Q2 + Q4 — aplicável ao registo BQ/Lote existente):** o Armazém **não calcula nem atualiza automaticamente** `% utilização` e não edita master por omissão (o parágrafo acima mantém-se para automatização e para campos sem autoridade de edição). Porém, por clarificação do Owner, o registo BQ/Lote **já existente** é **consultado/mantido a partir do Armazém**, pelo perfil **RESPONSÁVEL**, nas **características funcionalmente confirmadas como editáveis** — incluindo a **atualização manual** de `% utilização` (combinação Q2+Q4; a transição Produção → Armazém gera apenas reminder, nunca muta o valor). Isto **não** transfere posse: Ferramentas permanece o domínio master da ferramenta; o fluxo de reparação externa de BQ permanece em Boquilhas; a Q4 não torna automaticamente todos os campos editáveis.

## 2. Estrutura da página

Tabs V1:

1. `Registo`
2. `Consulta`

Não criar Definições de reparadores, estados ou vida útil dentro do Armazém.

## 3. Tab Registo

### Estado inicial

Apresenta três ações:

- `Entrada`
- `Saída`
- `Saídas programadas`

Ao escolher uma ação, expande inline um cartão de registo. Não abre nova página nem modal no fluxo normal.

### Tipo de ferramenta

Dentro do cartão aparecem seletores filled:

- `CM`
- `MF`
- `BQ`

O tipo determina apenas qual domínio é pesquisado. CM, MF e BQ mantêm identidades, campos e históricos separados.

### Seleção da ferramenta

Campos de seleção:

1. Tipo;
2. Referência;
3. Lote, quando existirem vários lotes para a Referência.

Regras:

- escrever uma Referência pesquisa no domínio do tipo escolhido;
- resultados mostram Referência e lote suficientes para distinguir registos;
- resultados mostram também o Nome técnico vindo do domínio da ferramenta;
- nenhum resultado ambíguo é escolhido automaticamente;
- o Armazém guarda o ID estável da ferramenta/lote;
- Referência e lote são apresentados, não editados;
- vida útil, estado, Máquina/Linha ou outros dados técnicos, se mostrados, são sempre read-only.

## 4. Registar Entrada

Ordem do formulário:

- `Posição`;
- `Referência`;
- `Lote / número individual`;
- `Máquina`;
- `Estado`: `Reparado | Por reparar | Novo`;
- `Observações`.

Antes de guardar, mostrar:

- Tipo;
- Referência;
- lote;
- localização atual conhecida, quando existir.

Ao confirmar com sucesso:

1. é criado o movimento de Entrada;
2. a posição passa a ser a localização atual no Armazém;
3. posição e ferramenta são associadas atomicamente;
4. Máquina e Estado ficam associados ao movimento de Entrada;
5. operador e data/hora são registados automaticamente;
6. a interface só apresenta sucesso depois da persistência.

Cancelar ou falhar não altera a localização anterior.

## 5. Registar Saída imediata

Ordem do formulário:

- `Referência`;
- `Lote / número individual`;
- `Destino`: `Fabricação | Reparação | Outros`;
- `Destino concreto`, campo obrigatório de texto com sugestões contextuais;
- `Observações`.

Quando o Destino é `Fabricação`, sugerir as Máquinas/Linhas. Quando é `Reparação`, selecionar o reparador/empresa a partir do **diretório canónico de reparadores registados** (listas/dropdown partilhado); se o reparador necessário não existir no diretório, permitir **registá-lo/adicioná-lo na fonte canónica de reparadores**, nunca como texto livre arbitrário no movimento — o reparador escolhido fica associado ao movimento e é historicamente rastreável. Quando é `Outros`, o utilizador escreve a descrição necessária.

A posição atual aparece read-only. Não é reescrita pelo utilizador durante a Saída.

Ao confirmar com sucesso:

1. é criado o movimento de Saída;
2. a ferramenta deixa de ocupar a posição na interface;
3. o tipo de destino e o destino concreto ficam registados no movimento;
4. se for Reparação, o reparador/empresa fica associado e visível na Consulta e Histórico;
5. operador e data/hora são registados automaticamente.

O formulário de Saída não pede Estado técnico. Máquina e Estado pertencem ao fluxo de Entrada; na Saída são substituídos pelo tipo de destino e destino concreto.

O campo `Observações` permite apenas informação livre relevante ao movimento. Não substitui dados estruturados do domínio da ferramenta.

“Retirar logo a posição” significa depois da confirmação e persistência com sucesso. Abrir o formulário ou escolher `Saída` não altera dados.

## 6. Saída programada para Reparação

`Saída programada` coordena a recolha física de vários lotes que vão ser enviados para Reparação.

É um fluxo partilhado entre Reparação e Armazém. A lista é criada no módulo de Reparação e executada no módulo do Armazém.

### 6.1 Origem: módulo de Reparação

1. O Manager inicia uma Reparação.
2. Seleciona os lotes/ferramentas que devem ser enviados.
3. Cria a lista de Saída programada.
4. A aplicação consulta a localização atual no Armazém para apresentar as posições de recolha.
5. Ao guardar, a lista passa a estar visível como pendente no módulo do Armazém.

Exemplo operacional confirmado: o Manager seleciona 15 lotes na Reparação; o Armazém recebe uma lista pendente para que o operador os retire.

A criação e seleção dos lotes não são responsabilidade do Armazém. O Armazém recebe a lista e executa apenas a recolha/saída.

### 6.2 Lista pendente no Armazém

O módulo do Armazém apresenta uma indicação visível de que existem Saídas programadas pendentes, por exemplo contador discreto junto de `Saídas programadas`.

A página apresenta uma lista canónica com:

| Estado | Origem | Criada por | Data | Itens | Progresso |
|---|---|---|---|---|---|

Interação:

- um clique seleciona a lista;
- duplo clique abre a lista para recolha;
- a lista aparece mesmo que nunca tenha sido impressa;
- o operador pode verificar e concluir todo o fluxo apenas no computador;
- a impressão é opcional e nunca condição para a lista ficar disponível.

Ao abrir, a tabela de recolha mostra, no mínimo:

| Retirado | Tipo | Referência | Lote | Posição |
|---|---|---|---|---|

Regras:

- a lista recebida identifica de forma estável cada ferramenta/lote;
- os checkboxes representam confirmação de recolha e não seleção de ferramentas para a lista;
- receber, abrir ou imprimir a lista não cria Saídas e não liberta posições;
- cada ferramenta continua a ocupar a sua posição até à confirmação final;
- a lista fica persistida para poder ser recuperada posteriormente noutro acesso ao módulo.

Quando a posição atual for diferente da posição registada no momento em que o Manager criou a lista, mostrar as duas separadamente como `Posição na criação` e `Posição atual`, com alerta. Não corrigir nem substituir silenciosamente o snapshot.

### 6.3 Impressão opcional

A impressão apresenta apenas a informação necessária à recolha:

- identificação da saída programada;
- data de criação;
- Tipo;
- Referência;
- lote;
- posição;
- espaço visual para confirmação física;
- observação geral, quando existir.

Imprimir não altera o estado da lista nem das posições. O operador pode executar exatamente o mesmo fluxo sem impressão.

### 6.4 Confirmar a recolha

O operador abre a lista pendente no módulo do Armazém e, à medida que retira fisicamente as ferramentas:

1. abre a lista pendente;
2. confirma cada linha através do respetivo checkbox `Retirado`;
3. os checks ficam guardados para que a lista possa ser retomada;
4. enquanto existir pelo menos uma linha sem check, nenhuma posição do conjunto é libertada;
5. ao confirmar o último item, a aplicação tenta concluir todo o conjunto;
6. apenas depois de a conclusão persistir com sucesso são criadas as Saídas e libertadas todas as posições.

O fecho do conjunto deve ser atómico:

- se falhar, nenhuma posição é libertada;
- a lista permanece pendente com as confirmações preservadas;
- a interface mostra o erro e permite tentar novamente;
- nunca apresentar conclusão parcial como sucesso total.

Quando o último check fecha o conjunto com sucesso:

- é criado um registo de Saída para cada ferramenta/lote;
- cada registo guarda a ligação à lista de Reparação;
- cada linha guarda o dia/hora da Saída e o operador que confirmou a Saída;
- todas as posições do conjunto ficam livres;
- a lista passa a estar registada e consultável no módulo de Reparação;
- o Manager consegue acompanhar posteriormente quais ferramentas saíram e quais já regressaram.

### 6.5 Regresso da Reparação e Entrada no Armazém

Quando as ferramentas regressam, o operador regista a Entrada no Armazém usando a ferramenta/lote já associado à lista.

Para cada linha, a aplicação conserva:

| Tipo | Referência | Lote | Saída | Operador da Saída | Entrada | Operador da Entrada | Estado do ciclo |
|---|---|---|---|---|---|---|---|

Ao guardar uma Entrada com sucesso:

1. é criada a posição atual no Armazém;
2. é criado o registo de Entrada;
3. são guardados dia/hora e operador da Entrada;
4. o ciclo dessa ferramenta/lote fica concluído;
5. a linha é atualizada simultaneamente no acompanhamento da Reparação.

Se apenas parte das ferramentas regressar, as linhas entradas ficam concluídas e as restantes continuam abertas. A lista completa só fica `Concluída` quando todas as linhas tiverem uma Entrada registada.

O Manager consulta este acompanhamento no módulo de Reparação; não precisa de abrir o histórico do Armazém para saber:

- quando cada ferramenta saiu;
- quem confirmou a Saída;
- quando regressou;
- quem registou a Entrada;
- quais ainda estão fora.

### 6.6 Estados e tratamento visual

Usar estados operacionais da lista, sem alterar os estados técnicos das ferramentas:

- `Pendente de saída`: criada pela Reparação e ainda não totalmente confirmada no Armazém;
- `Em reparação`: Saídas criadas e nenhuma Entrada registada;
- `Retorno parcial`: pelo menos uma Entrada registada, mas ainda existem ferramentas fora;
- `Concluída`: todas as ferramentas têm Entrada registada no Armazém.

Os checks indicam progresso de recolha, não a localização oficial da ferramenta. A localização só muda quando a fase de Saída é fechada com o último check e persistida com sucesso.

Tratamento visual recomendado:

- listas ativas mantêm fundo normal e estado textual;
- `Concluída` usa selo verde suave do design system, não verde vivo;
- a linha concluída pode usar fundo cinza muito claro para perder prioridade visual;
- cor nunca substitui o texto do estado;
- listas concluídas permanecem pesquisáveis e read-only.

## 7. Tab Consulta

### Pesquisa

Aceita:

- Referência;
- lote;
- posição;
- tipo de ferramenta.

Resultado mínimo:

| Tipo | Referência | Nome técnico | Lote | Localização/contexto | Posição | Último movimento |
|---|---|---|---|---|---|---|

Quando a ferramenta não está no Armazém, a posição aparece como `—`. A posição anterior permanece no histórico.

Vida útil e estado da ferramenta podem ser mostrados opcionalmente como colunas read-only provenientes do domínio da ferramenta, mas não são dados nem filtros próprios do Armazém na V1.

### Filtros

- Tipo: CM, MF, BQ;
- localização/contexto registado;
- posição;
- intervalo de datas do movimento;
- apenas com alertas.

Não duplicar filtros de vida, estado, Máquina/Linha ou Reparador pertencentes a outros domínios.

### Lista canónica

- um clique seleciona;
- duplo clique abre o histórico de localização/movimentos;
- ações dependentes da seleção ficam fora da lista;
- filtros nunca selecionam automaticamente um resultado;
- cada linha referencia o ID estável da ferramenta.

## 8. Histórico de localização

A ficha apresenta apenas:

- Entrada ou Saída;
- posição anterior e nova, quando aplicável;
- origem/destino registado no movimento;
- observações;
- data/hora;
- operador.

Uma Saída programada entra no histórico de movimentos quando a fase de recolha/Saída é concluída pelo último check. A criação e impressão da lista pertencem ao histórico operacional da própria lista, não ao histórico de localização da ferramenta.

Cada par Saída/Entrada conserva a ligação à mesma lista e à mesma ferramenta/lote, permitindo reconstruir o ciclo completo sem misturar dados técnicos da Reparação no Armazém.

Não repetir no histórico do Armazém:

- reparações;
- vida útil;
- alterações de estado técnico;
- arquivo ou sucata;
- histórico de produção da ferramenta.

Uma correção não reescreve silenciosamente movimentos anteriores; usa o mecanismo auditável confirmado pela implementação.

## 9. Localização registada e realidade física

No fluxo normal, uma Saída confirmada limpa a posição no sistema. Por isso, a Entrada não inclui um fluxo preventivo de `posição ocupada` nem uma ação `Substituir`.

A interface apresenta a disponibilidade devolvida pelo serviço. Se uma ferramenta estiver fisicamente numa posição mas não estiver registada, o frontend não tem forma de a detetar. Não mostrar alertas preditivos nem inventar o ocupante.

Quando o operador encontrar uma diferença física, deve poder selecionar o registo relacionado e abrir `Corrigir localização`. A correção fica separada de uma Entrada normal e apresenta claramente os valores registados e os valores encontrados.

### Ferramenta sem localização operacional

Se a informação consolidada indicar que uma ferramenta não está associada a Armazém, Produção ou Reparação, apresentar `Localização operacional não registada`.

O Armazém apenas sinaliza a inconsistência. Não inventa estado, localização, condição ou reparador e não cria um movimento automaticamente.

O alerta aparece obrigatoriamente na página principal do Armazém, com contagem e ação para abrir a Consulta já filtrada. Uma ferramenta está conciliada quando existe exatamente um contexto operacional válido:

- posição ativa no Armazém;
- `Fabricação` com Máquina/Linha ativa;
- `Reparação` com destino/reparador ativo.

Não gerar este alerta por ausência de texto visual: usar as relações/flags persistidas devolvidas pelo serviço.

### Ferramenta em mais de um contexto

Se a mesma ferramenta surgir simultaneamente em contextos incompatíveis, mostrar conflito e encaminhar para correção humana. Não aplicar prioridade automática.

### Atualização da percentagem de uso depois da produção

Quando uma ferramenta entra em Fabricação, persistir `used_in_production_since_usage_update = true` e o respetivo movimento/contexto de produção. Esta flag acompanha a ferramenta até existir uma atualização válida da percentagem de uso.

Quando essa ferramenta sai da produção e recebe uma Entrada no Armazém:

1. guardar normalmente o movimento de Entrada;
2. se `used_in_production_since_usage_update = true`, criar/manter `usage_update_pending = true`;
3. apresentar na página principal o alerta `Atualizar % uso`;
4. a ação abre a Consulta filtrada pelas ferramentas pendentes;
5. o operador consulta manualmente o valor no SAP e abre a ficha da ferramenta para registar a nova `% uso`;
6. apenas a gravação bem-sucedida da nova percentagem na ficha limpa `usage_update_pending` e atualiza `last_usage_updated_at`.

Abrir o alerta, consultar o SAP ou entrar no Armazém não limpa a flag. Entradas repetidas não criam alertas duplicados: existe uma pendência idempotente por ferramenta/ciclo desde a última atualização.

A Consulta do Armazém mostra uma coluna read-only `% uso` com o último valor confirmado da ficha da ferramenta e um estado `Atualizar % uso` quando pendente. A percentagem não é editada diretamente no Armazém.

Campos/estado mínimos a expor ao Armazém:

- `last_usage_percent`;
- `last_usage_updated_at`;
- `used_in_production_since_usage_update`;
- `usage_update_pending`;
- Produção/Máquina do ciclo que originou a pendência.

## 10. Relação com Job On

O Job On pode consultar o Armazém para apresentar:

- posição atual, quando existir;
- localização/contexto atual;
- posição atual exata, quando a ferramenta está no Armazém;
- último movimento relevante.

Regras:

- Job On não altera posições;
- selecionar uma ferramenta no Job On não cria uma Saída;
- movimentos reais são registados no Armazém;
- o Job On associa o ID estável da ferramenta/lote;
- snapshot histórico e localização atual aparecem separados;
- vida útil, estado, Máquina/Linha e reparação são obtidos no domínio da ferramenta, nunca no Armazém.

Na edição do Job On, a lista de substituição pode combinar a posição devolvida pelo Armazém com estado técnico e `% de uso` devolvidos pelo domínio da ferramenta. Esta composição é read-only. Em `Modo consulta` do Job On, a informação live de Armazém não ocupa a folha; a folha mostra apenas a associação guardada necessária à produção.

## 11. Estados vazios e erros

- Referência inexistente: `Ferramenta não encontrada`.
- Lote inexistente: `Não existem lotes registados`.
- Posição vazia: `Posição sem ocupação registada`.
- Erro de carregamento: mostrar erro e `Tentar novamente`; não apresentar como lista vazia.
- Falha ao guardar: manter os dados introduzidos e a localização anterior, sem falso sucesso.
- Sem listas programadas: `Não existem Saídas programadas pendentes`.
- Falha no fecho programado: manter a lista pendente e todas as posições ocupadas.
- Falha numa Entrada de retorno: manter essa linha aberta e preservar o último estado válido da lista.

## 12. Questões por confirmar antes do freeze técnico

- formato e limites válidos do código de posição;
- se o Destino é obrigatório em todas as Saídas;
- fluxo de correção/anulação de movimento;
- tipos adicionais depois de CM, MF e BQ.
- quem pode cancelar uma lista criada pelo Manager e em que estados;
- se a Reparação pode remover/adicionar linhas depois de a lista já estar visível no Armazém;
- se a confirmação final exige uma ação adicional ou é iniciada automaticamente pelo último check.
- se o Manager pode encerrar/cancelar uma linha que não regressará e qual o motivo obrigatório.

## 13. Critérios de aceitação do mockup V1

- Tabs Registo e Consulta usam o shell global;
- botões de ação são preenchidos com cor e texto branco; no hover/focus invertem para fundo branco, contorno e texto da cor da ação;
- Entrada/Saída expandem inline;
- o movimento contém apenas dados próprios do Armazém;
- Entrada regista Posição, Referência, Lote, Máquina, Estado e Observações;
- CM, MF e BQ são pesquisados nas respetivas fontes;
- Saída regista Referência, Lote, tipo de destino, destino concreto e Observações;
- Saída para Reparação associa e mostra o reparador/empresa escolhido;
- página principal alerta ferramentas sem posição/Fabricação/Reparação e abre a Consulta filtrada;
- entrada no Armazém após utilização em produção gera uma pendência idempotente de atualização da `% uso`;
- Consulta mostra a última `% uso` confirmada e o alerta pendente; a edição ocorre apenas na ficha da ferramenta;
- a posição só é removida após persistência da Saída;
- o Manager cria a lista no módulo de Reparação;
- o Armazém recebe uma indicação e uma lista pendente mesmo sem impressão;
- o operador pode executar a lista integralmente no computador;
- imprimir é opcional e não altera o fluxo;
- imprimir ou marcar apenas parte da lista não liberta posições;
- o último check conclui o conjunto de forma atómica e só então liberta todas as posições;
- uma falha no fecho não produz libertação parcial;
- o último check cria um registo de Saída por ferramenta com dia/hora e operador;
- a lista permanece visível na Reparação durante todo o ciclo;
- cada Entrada guarda dia/hora e operador e fecha a respetiva linha;
- a lista só fica `Concluída` quando todas as ferramentas tiverem regressado;
- listas concluídas usam estado verde suave e apresentação visual secundária/cinza;
- Consulta encontra por Referência, lote e posição;
- listas seguem clique/duplo clique canónicos;
- diferenças físicas encontradas pelo operador podem abrir uma correção de localização;
- histórico contém apenas localização e movimentos;
- Job On consulta o Armazém sem o alterar;
- nenhuma falha produz falso sucesso.
