# ARMAZÉM — MODELO FUNCIONAL

OPEN OWNER QUESTIONS: 4

Regra de ouro: **não reinterpretar limitações de implementação como regras funcionais**. Se a implementação atual só materializa CM/MF, isso é implementação + reconciliação técnica; não significa que BQ esteja funcionalmente adiado no Armazém.

---

## Índice

1. Objetivo e papel na aplicação
2. Âmbito funcional
3. Ownership funcional
4. Ferramentas / tipos abrangidos
5. Utilizadores, perfis e acesso
6. Estrutura interna do módulo
7. Modelo de localização física
8. Funcionamento geral
9. Entrada / Repor
10. Saída / Retirar
11. Saídas Programadas
12. Correção de localização
13. + Criar novo
14. Consulta, pesquisa e filtros
15. Estados, destinos, validações e avisos
16. Informação introduzida pelo utilizador
17. Informação gerada ou derivada pelo sistema
18. Quantidades / stock / saldos
19. Histórico e auditoria
20. Documentos / impressão / exportação
21. Informação consumida de outros módulos
22. Informação fornecida a outros módulos
23. Relação com Ferramentas
24. Relação com Job On
25. Relação com Boquilhas
26. Relação com Reparação Interna / Externa
27. Relação com Tampões
28. Relação com História
29. Regras negativas — ARMAZÉM NÃO...
30. Casos especiais / exceções
31. Histórico / Superseded
32. Questões Funcionais em Aberto
33. Detalhe Funcional Adiado
34. Resumo Funcional Final

---

## 1. Objetivo e papel na aplicação

O **Armazém** é o módulo funcional responsável pela **localização física** e pelos **movimentos físicos** das ferramentas dentro do BA DMO.

A sua finalidade real não é simplesmente “guardar ferramentas”, mas sim:

- guardar a **posição física atual** onde cada ferramenta/lote está, ou registar que está fora do Armazém;
- registar o **histórico de movimentos** de Entrada/Saída;
- expor **disponibilidade, presença, paragem física e whereabouts** às outras áreas, em particular ao Job On para planeamento;
- registar o **destino operacional** de uma Saída: **Fabricação**, **Reparação** ou **Sucata**;
- quando a Saída tem destino **Reparação**, registar o **reparador selecionado**, associado ao movimento e preservado no histórico;
- permitir **consulta/pesquisa** de ferramentas e posições;
- permitir **correção de localização** quando a realidade física difere do registado;
- alimentar alertas operacionais, tais como:
  - ferramenta sem localização operacional;
  - conflito de referências na mesma posição;
  - pendência de atualização da % de uso.

### Destino da Saída ≠ Estado técnico

Regra fechada por clarificação do Owner:

- **Destino da Saída** é operacional e responde à pergunta:  
  **“para onde vai a ferramenta?”**  
  Exemplos: Fabricação / Reparação / Sucata.

- **Estado técnico** é a condição técnica da ferramenta e responde à pergunta:  
  **“qual é a condição da ferramenta?”**  
  Exemplos: Novo / Reparado / Por reparar / Sucatado / Arquivado.

Por isso:

- Saída → Reparação **não implica automaticamente** estado técnico “Por reparar” ou “Reparado”.
- Saída → Sucata **não implica automaticamente** estado técnico “Sucatado”.
- Um movimento de Armazém **nunca altera silenciosamente** o estado técnico da ferramenta.

### Separação central de ownership

| Conceito | Owner funcional |
|---|---|
| Whereabouts físico: localização, presença, em produção, em reparação, fora | **ARMAZÉM** via movimentos/destino |
| Estado técnico da ferramenta: Novo / Reparado / Por reparar / Sucatado / Arquivado | **Ferramentas** |
| Identidade / master record da ferramenta | **Ferramentas** |
| Atribuição / alocação de produção | **Job On** |
| Workflow / registo de reparação | **Reparação** |

O Armazém **não é o master das ferramentas**. O master record — identidade, referência, lote, estado técnico, % de uso, máquina/linha e dados técnicos — está fora do Armazém. O Armazém pode consumir esses dados como contexto read-only para identificar uma ferramenta, mas não os possui nem os altera como módulo owner.

---

## 2. Âmbito funcional

O Armazém é um **módulo funcional de topo**, atribuível individualmente por utilizador em Admin.

### Classificação

- **Classificação:** TOP-LEVEL FUNCTIONAL MODULE.
- **Atribuível por utilizador:** SIM.
- **Variantes dependentes de perfil:** Operador / Controlador e Responsável, confirmadas como requisito.
- **Não existem módulos separados** “Armazém Operador” e “Armazém Responsável”: são variantes do mesmo módulo **ARMAZÉM**.
- **Admin não é um perfil operacional do Armazém** e não recebe implicitamente o módulo operacional Armazém.

### O que está no âmbito do Armazém

- posição física atual de ferramenta/lote;
- movimentos físicos de Entrada/Saída;
- destino operacional da Saída;
- seleção de reparador quando a Saída é para Reparação;
- histórico de movimentos físicos;
- consulta de localização e contexto;
- correção física auditável;
- alertas operacionais relacionados com localização física.

### O que não está no âmbito do Armazém

- master record da ferramenta;
- estado técnico da ferramenta;
- % de uso como dado editável;
- planeamento ou atribuição de produção;
- workflow de reparação;
- movimentos de reparação externa de BQ;
- saldos de Tampões;
- histórico transversal de auditoria como owner.

### Módulo atribuível e perfil independentes

- **Módulo atribuído** define acesso: se o módulo não estiver atribuído, não aparece na navegação e é inutilizável.
- **Perfil** define a experiência dentro do módulo.

Esta distinção é transversal e aplica-se ao Armazém.

---

## 3. Ownership funcional

### ARMAZÉM possui / é owner de

- localização física atual: posição e ocupação;
- movimentos físicos de Entrada/Saída;
- destino operacional de uma Saída, quando aplicável;
- observações livres do movimento;
- operador e data/hora do movimento;
- diferenças/correções físicas posteriores, via correção de localização auditável;
- facto “fora”, derivado da ausência de ocupação ativa.

### ARMAZÉM não possui / não altera automaticamente

- master record da ferramenta — **Ferramentas**;
- estado técnico da ferramenta — **Ferramentas**;
- produção, planeamento ou revisão — **Job On**;
- fluxo de movimentos de reparação externa de BQ — **Boquilhas**;
- BQ master — **Ferramentas**;
- registo/workflow de reparação interna ou externa — **Reparação**;
- saldos de Tampões — **Tampões**;
- eventos transversais de História como owner — **História** é leitura.

### Entrada de UI não muda ownership

Uma página ou detalhe alcançado via Armazém pode expor dados de Ferramentas. Isso **não torna o Armazém owner desses dados**.

Em especial:

- o Armazém pode mostrar dados master da ferramenta;
- o Armazém pode permitir abrir o detalhe/ficha da ferramenta;
- Operador / Controlador pode abrir/ler esse detalhe quando aplicável;
- abrir o detalhe **não dá ao Operador permissão descontrolada de edição de master**;
- Responsável pode abrir o detalhe a partir do Armazém;
- Responsável pode editar master/detalhe quando autorizado;
- essa edição permanece uma operação de **Ferramentas**;
- a escrita/update é persistida na fonte de verdade **Ferramentas**;
- iniciar a ação pela UI do Armazém **não transfere ownership** para o Armazém.

---

## 4. Ferramentas / tipos abrangidos

### Modelo funcional

O modelo funcional fechado é:

- **CM**, **MF** e **BQ** usam o mesmo modelo normal de ferramenta/master/armazém:
  - master em **Ferramentas**;
  - registo/localização/movimentos físicos em **Armazém**.
- A diferença funcional relevante do **BQ** é o workflow de reparação externa, que pertence a **Boquilhas**.
- **BQ nunca é Reparação Interna**.
- **PU** e **CS** são ferramentas em Ferramentas, mas atualmente **não estão integrados/registados no Armazém**; são configuração específica de produção do Job On.

### Tabela de abrangência

| Tipo | Ferramentas possui master? | Armazém funcional atual? | Fluxo de reparação especial | Classificação |
|---|---:|---:|---|---|
| CM | YES | YES | modelo de reparação CM atual | FUNCTIONAL CURRENT; implementação presente |
| MF | YES | YES | modelo de reparação MF atual | FUNCTIONAL CURRENT; implementação presente |
| BQ | YES | YES funcional, normal | Boquilhas: fluxo de reparação externa; BQ nunca RI | FUNCTIONAL CURRENT; implementação em falta = TECHNICAL RECONCILIATION REQUIRED |
| PU | YES | NO | — | ferramenta em Ferramentas; config produção Job On; sem registo Armazém |
| CS | YES | NO | — | ferramenta em Ferramentas; config produção Job On; sem registo Armazém |
| Pinças / Calibres / equivalentes | config | NO | — | config específica de produção do Job On |

### BQ não está funcionalmente adiado no Armazém

A eventual ausência de suporte técnico a BQ na implementação atual é **TECHNICAL RECONCILIATION REQUIRED**, não adiamento funcional.

Funcionalmente:

- BQ master → **Ferramentas**;
- BQ localização/movimentos normais → **Armazém**;
- BQ external repair workflow → **Boquilhas**;
- BQ Reparação Interna → **NO**.

### Identidade no Armazém

- O Armazém guarda o **ID estável** da ferramenta/lote.
- A identidade é resolvida através do mecanismo próprio do Armazém, sem depender diretamente do repositório do domínio dono da ferramenta.
- A representação de identidade pode distinguir o domínio de origem (ex. Ferramentas vs Boquilhas), mas isso não implica que BQ fique fora do modelo normal de Armazém; o ramo Boquilhas diz respeito apenas ao fluxo de reparação externa de BQ.

---

## 5. Utilizadores, perfis e acesso

O modelo global define exatamente **três perfis**:

1. **Admin**
2. **Operador / Controlador**
3. **Responsável**

Não existe quarto perfil. Não existe perfil read-only autónomo. Não existe perfil de consulta/gestão/metrologia como perfil separado.

### Matriz de acesso

| Perfil | Acesso ao Armazém | Comportamento por perfil | Estado |
|---|---:|---|---|
| Admin | NO, não operacional | — | confirmado; não inventar ações operacionais de Admin |
| Operador / Controlador | YES quando atribuído | experiência operacional | variante confirmada; detalhe exato pendente |
| Responsável | YES quando atribuído | experiência de revisão/gestão | variante confirmada; detalhe exato pendente |

### Acesso ao módulo vs perfil

- **Acesso ao módulo** = o utilizador pode entrar no Armazém.
- **Perfil** = como o utilizador trabalha dentro do Armazém.

O Armazém segue a regra transversal: módulo atribuído é acesso; perfil é experiência dentro do módulo.

### Admin

- Admin puro não recebe implicitamente o módulo operacional Armazém.
- Admin não é perfil operacional do Armazém.
- Não inventar ações operacionais de Admin no Armazém.

### Operador / Controlador

Onde suportado pela evidência, o Operador / Controlador pode:

- pesquisar ferramentas;
- consultar ferramentas;
- abrir/ler detalhe da ferramenta;
- criar/registrar movimentos de Entrada;
- criar/registrar movimentos de Saída;
- definir o destino operacional da Saída: Fabricação / Reparação / Sucata;
- selecionar o reparador quando Saída → Reparação, a partir do diretório canónico;
- fornecer a informação requerida pelo movimento;
- confirmar movimentos físicos;
- corrigir um registo operacional quando houve erro, preservando auditoria/histórico.

Limites importantes:

- abrir o detalhe da ferramenta **não dá ao Operador permissão descontrolada para editar master**;
- o Operador não se torna editor de master apenas porque dados master estão visíveis;
- o Operador não altera silenciosamente estado técnico através de movimento físico;
- correção operacional não é edição de master irrestrita.

### Responsável

O Responsável pode, quando o módulo estiver atribuído:

- aceder ao Armazém;
- pesquisar e consultar informação física/localização;
- abrir o detalhe da ferramenta a partir do Armazém;
- editar master/detalhe da ferramenta quando autorizado.

Mas:

- **MASTER OWNER = Ferramentas**.
- **MASTER SOURCE OF TRUTH = Ferramentas**.
- **UI ENTRY POINT pode ser Armazém**.

Portanto, se o Responsável edita a ficha da ferramenta aberta a partir do Armazém, a operação e a persistência permanecem em **Ferramentas**. O Armazém não passa a ser owner do master.

### Correção de Operador vs Edição de Master do Responsável

Estas permissões não são a mesma coisa.

**Correção operacional do Operador**

- corrige registo físico/operacional do Armazém;
- preserva auditoria/histórico;
- não apaga silenciosamente movimentos anteriores;
- não se torna manutenção irrestrita de master.

**Edição de master pelo Responsável**

- altera deliberadamente master/detalhe da ferramenta;
- pertence a Ferramentas;
- pode ser iniciada através de detalhe aberto via Armazém;
- é persistida na fonte de verdade Ferramentas.

### Estado da divisão Operador / Responsável

- A **existência** da divisão Operador/Responsável no Armazém está **confirmada e fechada**.
- A **distribuição exata** das ações específicas do Armazém entre Operador e Responsável permanece em aberto como questão funcional genuína.

Não importar cegamente permissões de outros módulos, nem transformar ações master de Ferramentas em ações próprias do Armazém.

### Autorização na aplicação

O acesso ao Armazém exige que o módulo esteja atribuído ao utilizador. Sem atribuição, o módulo não aparece na navegação e não é funcionalmente acessível (barreira de acesso, não apenas ocultação de interface).

A identidade do executor é derivada do utilizador autenticado; nunca é escolhida pelo cliente.

---

## 6. Estrutura interna do módulo

O design BRIEF define, para V1, **2 tabs**:

- Registo
- Consulta

A implementação atual apresenta **4 tabs**:

- Registo
- Consulta
- Programadas
- Histórico

As tabs **Programadas** e **Histórico** foram adicionadas a partir da visual authority. Isto gera reconciliação técnica e/ou questão funcional limitada quanto ao alvo final da estrutura e da tab Programadas.

Não criar dentro do Armazém áreas de “Definições” para reparadores, estados ou vida útil.

### Classificação das áreas/candidatos

| Candidato | Classificação |
|---|---|
| Registo | área interna do módulo; workflows de Entrada/Saída; Substituir está presente apenas na implementação, fora do alvo funcional |
| Consulta | área interna do módulo; pesquisa/localização |
| Programadas | área interna atualmente shell; superfície simples implementada; fluxo completo de saída programada com checkboxes não implementado |
| Histórico | área interna do módulo; histórico de movimentos de localização |
| Saídas programadas / ciclo de saída para Reparação | workflow partilhado Reparação ↔ Armazém; não é módulo separado; implementação via port existe, fluxo completo em UI não está ativo |
| Substituir | ação presente na implementação; fora do alvo funcional atual; divergência = TECHNICAL RECONCILIATION REQUIRED |
| Definições | não é módulo; não criar dentro do Armazém |
| Corrigir localização | correção de operador; requisito funcional confirmado; implementação ausente |
| + Criar novo | workflow de criação de nova ferramenta a partir do Armazém; requisito funcional estabelecido; implementação ausente |

---

## 7. Modelo de localização física

### Sem hierarquia

O modelo atual não possui hierarquia de:

- armazém;
- zona;
- corredor;
- prateleira;
- pallet;
- slot.

Existe apenas uma **posição física** identificada por um código único.

Não inventar hierarquia de localização.

### Código de posição

- Código de posição = exatamente **4 dígitos** (padrão `^\d{4}$`).
- Validação: “A posição deve ter exatamente 4 dígitos.”
- Exemplos válidos: `2421`, `0001`.
- Exemplos inválidos: `242`, `24211`, `24A1`, string vazia.

### Lista plana de posições

A localização é uma lista plana de posições. Uma posição existe quando criada; a criação pode ser automática quando necessário.

Não existe flag de “posição ativa/inativa”. A libertação é do **stock/ocupação**, não da posição enquanto entidade.

### Ocupação 1:1

Uma posição é ocupada por, no máximo, **um lote de ferramenta ativo** de cada vez.

Princípios:

- A ocupação representa o facto posição ↔ lote de ferramenta.
- Ocupação ativa = linha de ocupação sem data de libertação.
- Libertar mantém a linha histórica com a data de libertação preenchida.
- Factos históricos são preservados.
- Não podem coexistir duas ocupações ativas da mesma posição/lote.

### Conflito de referências

Duas referências diferentes ativas na mesma posição não são permitidas. Se, por dados corrompidos ou históricos, coexistirem, a consulta por posição devolve aviso de qualidade de dados.

Nunca normalizar silenciosamente o conflito.

---

## 8. Funcionamento geral

O Armazém regista onde a ferramenta está. O fluxo geral é:

1. **Entrada / Repor**  
   A ferramenta ocupa uma posição de 4 dígitos; cria movimento `in`; a posição passa a ser a localização atual.

2. **Saída imediata / Retirar**  
   A ferramenta deixa de ocupar a posição; cria movimento `out`; regista destino operacional Fabricação / Reparação / Sucata; quando Reparação, seleciona e regista reparador. A posição só é libertada após persistência com sucesso.

3. **Consulta**  
   Pesquisa por tipo, referência, lote ou posição; mostra contexto de localização: `armazem`, `fora` ou `nao_registado`, posição e avisos de conflito.

4. **Histórico**  
   Mostra movimentos por ferramenta/lote: Entrada/Saída, posição, destino, reparador quando aplicável, observações, data/hora e operador.

5. **Saídas Programadas**  
   Workflow funcional partilhado com Reparação. A lista é criada na Reparação e executada fisicamente no Armazém. A superfície atual pode ser parcial/shell; a existência funcional do workflow está preservada.

6. **Correção de localização**  
   Quando o operador encontra diferença física, pode abrir Corrigir localização. Requisito funcional confirmado; implementação ausente.

7. **Alertas**  
   Alertas operacionais podem sinalizar:
   - localização operacional não registada;
   - conflito de contexto;
   - atualização de % uso pendente.

### Interação

- Cartões abrem inline em Registo/Consulta, sem nova página/modal no fluxo normal.
- A interface só mostra sucesso depois da persistência.
- Não deve existir falso sucesso.

---

## 9. Entrada / Repor

### Trigger

Registo manual pelo utilizador.

- **Entrada** = ocupação inicial ou registo de presença.
- **Repor** = reocupação após Saída.

### Actor

- Utilizador autenticado com módulo Armazém.
- O ator/executor é derivado do servidor (server-derived).
- Nunca escolhido pelo cliente/browser.

### Validações

- Posição obrigatória.
- Posição deve ter exatamente 4 dígitos.
- Referência ou lote obrigatórios.
- Ferramenta deve existir.
- Posição não pode estar ocupada por outra ferramenta.

### Efeito funcional

A Entrada:

- cria stock ativo / ocupação;
- cria movimento de entrada;
- persiste ocupação e movimento na mesma operação atómica;
- regista o evento de auditoria;
- torna a posição introduzida a localização atual da ferramenta/lote.

A operação protege a ocupação contra operações concorrentes na mesma posição.

### Re-entrada na mesma posição

Reentrada da mesma ferramenta na mesma posição já ocupada deve ser tratada como conflito controlado, não como violação cega de índice único.

### Máquina / Estado na Entrada

O BRIEF refere que a Entrada pode registar Máquina e Estado (Reparado | Por reparar | Novo), para além de Posição, Referência, Lote e Observações.

No modelo de dados atual, o movimento regista apenas dados próprios do Armazém.

Assim:

- a presença funcional de Máquina/Estado na Entrada carece de reconciliação técnica;
- a classificação exata do campo Estado na Entrada permanece como questão funcional genuína (Q4);
- está fechado que o Armazém **não altera silenciosamente o estado técnico**.

---

## 10. Saída / Retirar

### Trigger

Registo manual de Saída/Retirar.

### Actor

- Utilizador autenticado.
- Actor server-derived.

### Validações

- Ferramenta deve existir.
- Ferramenta deve estar registada como presente no Armazém.
- Destino operacional: suporte funcional a Fabricação / Reparação / Sucata.
- A obrigatoriedade do Destino em todas as Saídas permanece como questão funcional genuína (Q3).

### Efeito funcional

A Saída:

- cria movimento de saída;
- liberta a ocupação ativa;
- regista a data/hora e o executor da libertação, apenas se a posição ainda não estiver libertada;
- só liberta a posição após persistência com sucesso;
- regista o evento de auditoria.

### Destino operacional

Destinos funcionais fechados:

- Fabricação;
- Reparação;
- Sucata.

O destino responde “para onde vai a ferramenta?” e não é uma transição automática de estado técnico.

### Nota de nomenclatura

Algumas fontes ou implementação usam “Produção” ou “Fabrico”. A clarificação owner fixa o destino operacional como **Fabricação / Reparação / Sucata**. O termo implementação “Produção” pode ser operacionalmente equivalente, mas requer reconciliação de nomenclatura/design.

---

### 10.1 Fabricação

Quando a Saída tem destino **Fabricação**:

- a ferramenta vai operacionalmente para produção/fabricação;
- o Armazém regista o movimento físico e o destino;
- “Em produção” ou “Em fabrico” é contexto operacional/físico;
- não é estado técnico;
- não altera automaticamente o estado técnico da ferramenta.

Se houver necessidade de alterar estado técnico, isso pertence ao workflow de **Ferramentas**, não ao movimento de Armazém.

---

### 10.2 Reparação

Quando a Saída tem destino **Reparação**:

- o utilizador deve poder selecionar o reparador;
- a seleção deve ocorrer a partir do **diretório canónico de reparadores registados**;
- se o reparador necessário não existir, deve ser possível registá-lo/adicioná-lo na fonte canónica;
- o resultado da adição deve ser um reparador registado no modelo canónico;
- não deve ser texto livre arbitrário no movimento;
- o reparador selecionado fica associado ao movimento físico;
- o reparador permanece historicamente rastreável.

Regras importantes:

- Saída → Reparação **não muda automaticamente** o estado técnico para “Por reparar”.
- Saída → Reparação **não muda automaticamente** o estado técnico para “Reparado”.
- A seleção de reparador na Saída → Reparação é dado do movimento físico/ciclo, não edição de master da ferramenta.
- O ciclo/registo de reparação pertence à Reparação.
- Na Reparação Interna, o reparador é o utilizador autenticado; não é selecionado manualmente.
- A seleção de reparador tratada aqui diz respeito ao fluxo de reparação externa CM/MF e ao diretório canónico de repairers.

---

### 10.3 Sucata

Quando a Saída tem destino **Sucata**:

- a ferramenta vai operacionalmente para sucata;
- o Armazém regista o movimento físico e o destino operacional;
- o destino **não implica automaticamente** estado técnico “Sucatado”.

Se o estado técnico “Sucatado” tiver de ser atribuído, isso ocorre no workflow próprio de **Ferramentas**, nunca por mutação silenciosa do movimento de Armazém.

---

## 11. Saídas Programadas

As **Saídas Programadas existem funcionalmente**.

A sua existência funcional está fechada. A ativação completa da superfície/UI pode ser escopo de implementação/reconciliação técnica.

### Trigger

- Um utilizador autorizado no módulo de Reparação cria a lista de Saída programada.
- O Armazém recebe a lista como pendente.
- O operador do Armazém executa a recolha/saída física.
- A criação/seleção dos lotes não é responsabilidade do Armazém.

### Estados operacionais da lista

Sem alterar estados técnicos:

- Pendente de saída;
- Em reparação;
- Retorno parcial;
- Concluída.

### Regras funcionais

- Checkboxes são confirmação de recolha, não seleção.
- Receber, abrir ou imprimir a lista não cria Saídas nem liberta posições.
- A posição só muda quando a fase de Saída é fechada pelo último check e persistida.
- O fecho deve ser atómico:
  - falha ⇒ nenhuma posição libertada;
  - lista permanece pendente.
- Entrada de retorno fecha a linha.
- A lista só fica Concluída quando todas as linhas tiverem Entrada.

### Posição na criação vs posição atual

Se a posição atual diferir do snapshot existente na criação da lista:

- mostrar as duas;
- apresentar alerta;
- não corrigir/substituir silenciosamente o snapshot.

### Reparador no fluxo programado

No fluxo programado/externo:

- o reparador é definido na Reparação, na lista de envio;
- deve ser preservado como facto histórico, por exemplo snapshot do reparador;
- a recolha no Armazém confirma apenas o movimento físico;
- o reparador permanece rastreável.

### Impressão

- A impressão é opcional.
- Nunca é condição para a lista ficar disponível.
- Não altera estado da lista nem das posições.

### Implementação

O workflow de saída programada partilha com a Reparação um mecanismo de confirmação física atómica (recolha/retorno) gerido pelo Armazém.

A superfície/UI do fluxo completo pode não estar totalmente ativa; isso é reconciliação de implementação, não ausência funcional.

---

## 12. Correção de localização

A correção de localização é um **requisito funcional confirmado**.

### Funcionamento esperado

Quando o operador encontra uma diferença física:

- ferramenta está numa posição mas não está registada;
- ou está registada mas não está fisicamente presente;

o operador pode abrir **Corrigir localização**, separada de uma Entrada normal, mostrando valores registados vs encontrados.

### Regras

- A correção não reescreve silenciosamente movimentos anteriores.
- Usa mecanismo auditável, por exemplo nova linha/facto.
- Preserva histórico.
- Distingue-se da edição de master do Responsável.

O requisito funcional existe e está confirmado; a sua materialização é matéria de reconciliação técnica. Não existe questão owner sobre a existência do requisito.

---

## 13. + Criar novo

O workflow **+ Criar novo** pode ser iniciado a partir do Armazém.

É um requisito funcional estabelecido:

- permite criar uma nova ferramenta a partir do contexto Armazém;
- a criação do master record pertence a **Ferramentas**;
- depois da criação do master, ocorre Entrada física no **Armazém**;
- não duplicar master.

Fluxo:

1. criação do master da ferramenta → Ferramentas;
2. registo de Entrada física → Armazém.

A UI pode começar no Armazém, mas o ownership do master permanece em Ferramentas.

A materialização deste workflow é matéria de reconciliação técnica.

---

## 14. Consulta, pesquisa e filtros

A Consulta do Armazém permite pesquisar ferramentas e posições.

### Campos de pesquisa

Pesquisa suportada por:

- tipo;
- referência;
- lote;
- posição.

A chamada exige pelo menos um critério.

### Comportamento por critério

- Por posição: devolve ocupante(s) e pode mostrar aviso de conflito de referências.
- Por tipo/referência/lote: pesquisa identidades e devolve estado de localização.

### Resultado mínimo

Resultado funcional mínimo inclui:

- Tipo;
- Referência;
- Nome técnico;
- Lote;
- Localização/contexto;
- Posição;
- Último movimento/contexto relevante.

Quando a ferramenta não está no Armazém:

- a posição atual aparece como `—` ou equivalente;
- a posição anterior permanece no histórico.

### Filtros

Filtros funcionais referidos:

- Tipo: CM/MF/BQ;
- Localização/contexto;
- Posição;
- intervalo de datas do movimento;
- apenas com alertas.

Não duplicar filtros pertencentes a outros domínios, como:

- vida útil;
- estado técnico;
- máquina/linha;
- reparador.

O filtro de tipo aplica-se às ferramentas abrangidas (CM, MF, BQ); o suporte efetivo de cada tipo é matéria de reconciliação técnica.

### Interação na lista canónica

- Clique seleciona.
- Duplo clique abre histórico de localização.
- Filtros nunca selecionam automaticamente um resultado.

### Colunas read-only

Vida útil e estado técnico podem aparecer como colunas read-only, com origem no domínio Ferramentas.

- Não são dados próprios do Armazém.
- Não são filtros próprios do Armazém em V1.
- Percentagem de uso pode ser coluna read-only.
- Edição de % uso ocorre apenas na ficha da ferramenta.

### Deep-link

Permite abrir a Consulta já filtrada por posição (ex. a partir de alertas).

---

## 15. Estados, destinos, validações e avisos

É crítico separar dois eixos independentes de “estado”.

---

### 15.1 Estado físico / whereabouts

Pertence ao Armazém e é derivado dos factos/movimentos de localização.

Exemplos:

- `armazem` — presente no Armazém;
- `fora` — fora do Armazém;
- `nao_registado` — localização operacional não registada;
- destinos operacionais: Fabricação, Reparação, Sucata.

“Em produção” ou “Em fabrico” é estado operacional/físico, não estado técnico.

---

### 15.2 Destino operacional

O destino operacional responde:

**Para onde vai a ferramenta?**

Destinos fechados:

- Fabricação;
- Reparação;
- Sucata.

O destino:

- é registado no movimento/histórico;
- não é estado técnico;
- não altera silenciosamente o master;
- pode ser obrigatório ou opcional conforme decisão owner ainda em aberto.

---

### 15.3 Estado técnico

O estado técnico pertence a **Ferramentas**.

Exemplos:

- Novo;
- Reparado;
- Por reparar;
- Sucatado;
- Arquivado.

O Armazém pode mostrar estado técnico como contexto read-only, mas:

- não cria;
- não altera;
- não recalcula;
- não sincroniza silenciosamente.

Se uma alteração de estado técnico for necessária, ela deve ocorrer no workflow de Ferramentas.

---

### 15.4 Estado na Entrada

O campo Estado na superfície de Entrada — Reparado | Por reparar | Novo — aparece no BRIEF.

Regra fechada:

- o Armazém não cria/altera/recalcula estados técnicos;
- um movimento de Armazém não pode mutar silenciosamente o estado técnico.

A classificação exata do campo Estado na Entrada permanece em aberto:

- read-only técnico?
- contexto apenas do movimento?
- não pertence ao Armazém?

Se existir atualização explicitamente confirmada do estado técnico, essa atualização deve ser ação cross-module com entrada no workflow de Ferramentas, nunca inferida pelo movimento de Armazém.

---

### 15.5 Validações

| Significado | Classificação |
|---|---|
| Posição deve ter exatamente 4 dígitos | STRUCTURAL VALIDATION |
| Indique referência ou lote | STRUCTURAL VALIDATION |
| Ferramenta não encontrada; verifique referência e lote | STRUCTURAL VALIDATION / NOT FOUND |
| Posição já está ocupada por outra ferramenta | HARD BUSINESS / STRUCTURAL BLOCK — ocupação 1:1 |
| Ferramenta não está registada como presente no Armazém | STRUCTURAL / BUSINESS BLOCK |
| Indique tipo/referência/lote/posição | STRUCTURAL VALIDATION |
| Ferramenta não registada como presente para ser libertada | STRUCTURAL / BUSINESS BLOCK |
| Posição desta ferramenta já foi libertada | STRUCTURAL / BUSINESS BLOCK |
| Posição de retorno deve ter 4 dígitos | STRUCTURAL VALIDATION |
| Posição de retorno já está ocupada por outra ferramenta | HARD BUSINESS / STRUCTURAL BLOCK |
| Módulo não autorizado | STRUCTURAL — fail closed |

---

### 15.6 Avisos

Avisos não são bloqueios automáticos de produção.

- **Conflito de referências na mesma posição**  
  Duas referências diferentes ativas na mesma posição geram aviso de qualidade de dados. Nunca normalização silenciosa.

- **Localização operacional não registada**  
  Ferramenta sem contexto operacional válido: nem posição ativa, nem Fabricação ativa, nem Reparação ativa. O Armazém sinaliza; não inventa estado nem cria movimento.

- **Ferramenta em mais de um contexto**  
  Mostrar conflito e encaminhar para correção humana. Não aplicar prioridade automática.

- **Atualização de % uso pendente**  
  Quando uma ferramenta sai de produção e recebe Entrada no Armazém com flag de uso pendente, pode ser apresentado alerta idempotente.
  - Abrir alerta, consultar SAP ou entrar no Armazém não limpa a flag.
  - Só a gravação de nova % uso na ficha da ferramenta limpa.
  - % uso não é editada no Armazém.

Regra transversal:

- warning ≠ decisão automática de produção;
- o Armazém aplica bloqueios estruturais duros;
- não inventar bloqueios de negócio adicionais.

---

## 16. Informação introduzida pelo utilizador

Dados próprios do Armazém introduzidos pelo utilizador:

### Entrada

- Posição — 4 dígitos;
- Referência;
- Lote;
- Observações;
- Tipo, na implementação atual CM/MF;
- No BRIEF também Máquina e Estado, embora não persistidos no movimento V1 atual.
- Destino não é pedido na Entrada na implementação V1 atual.

### Saída

- Referência;
- Lote;
- Destino operacional: Fabricação / Reparação / Sucata;
- Reparador, quando Destino = Reparação, selecionado do diretório canónico;
- Observações.

### Consulta

- tipo;
- referência;
- lote;
- posição.

---

## 17. Informação gerada ou derivada pelo sistema

O sistema gera ou deriva informação que não deve ser tratada como entrada manual:

- ID estável da ferramenta/lote, resolvido;
- ator (executor), derivado do utilizador autenticado;
- data/hora (timestamps) das operações;
- contexto de localização: `armazem`, `fora`, `nao_registado`;
- o facto “fora”, derivado;
- a proveniência de reparação, quando aplicável;
- o conflito de referências, derivado;
- o último reparador, derivado dos factos históricos;
- disponibilidade/presença derivadas para o Job On (planeamento);
- flags/alertas (ex. atualização de % uso pendente), quando suportadas.

O reparador selecionado é resolvido a partir da escolha do utilizador e pode ser preservado como snapshot no histórico.

---

## 18. Quantidades / stock / saldos

O modelo de stock do Armazém é por **identidade individual de ferramenta/lote**, não por quantidade/saldo agregado.

- A ocupação é um facto posição ↔ lote.
- Uma linha ativa representa uma posição ocupada por um lote.
- Não representa quantidade de stock livre.
- A quantidade registada no movimento não é usada como saldo.
- “fora” é derivado e nunca é saldo contabilístico.

### Regras negativas de quantidade

O Armazém não calcula:

- saldos de Tampões — pertencem a Tampões;
- saldo do fluxo de reparação de BQ — pertence a Boquilhas;
- quantidade planeada de produção — pertence a Job On.

Portanto:

- quantidade de Armazém ≠ saldo Boquilhas;
- quantidade de Armazém ≠ saldo Tampões;
- quantidade de Armazém ≠ quantidade planeada Job On.

---

## 19. Histórico e auditoria

### Histórico de localização do Armazém

O histórico do Armazém guarda apenas localização/movimentos da ferramenta/lote:

- Entrada/Saída;
- posição;
- destino/origem: Fabricação / Reparação / Sucata;
- reparador quando Destino = Reparação;
- observações;
- data/hora;
- operador.

### Append-only

Movimentos são factos imutáveis.

- Os movimentos são append-only; não podem ser alterados nem eliminados.
- Correções não apagam movimentos anteriores.
- Correção de localização usa mecanismo auditável, por exemplo nova linha/facto.

### Saídas programadas no histórico

Saída programada entra no histórico de movimentos apenas quando a fase de recolha/Saída é fechada pelo último check e persistida.

A criação/impressão da lista pertence ao histórico operacional da lista, não ao histórico de localização da ferramenta.

### Reparador no histórico

Regra fechada:

O histórico de uma ferramenta deve preservar, por ciclo:

- ferramenta/lote;
- reparador;
- data/hora;
- contexto/movimento de reparação.

Ordem cronológica. Reparações/históricos anteriores nunca são sobreescritos por uma reparação nova.

O “último reparador” é determinado a partir dos factos históricos:

- movimento/ciclo mais recente com reparador;
- não é um campo mutável mantido como única verdade;
- a verdade histórica preservada é autoritativa.

### O que não repetir no histórico do Armazém

Não duplicar no histórico do Armazém:

- ciclo de reparação completo;
- vida útil;
- alterações de estado técnico;
- arquivo/sucata;
- histórico de produção.

O Armazém guarda o movimento físico e o reparador associado quando aplicável; o workflow de reparação pertence à Reparação.

### Auditoria transversal

O Armazém regista eventos de auditoria para as suas operações (entrada, saída, correção, saída programada concluída).

História lê estes eventos read-only; não se torna owner dos movimentos do Armazém.

### Actor e timestamps

- Ator da movimentação/ocupação: derivado do utilizador autenticado (server-derived).
- Timestamps em UTC.

---

## 20. Documentos / impressão / exportação

### Picking / Saída programada

A impressão da lista de recolha é opcional.

Conteúdo funcional referido:

- identificação da saída programada;
- data de criação;
- Tipo;
- Referência;
- lote;
- posição;
- espaço de confirmação física;
- observação.

Regras:

- imprimir não altera o estado da lista;
- imprimir não liberta posições;
- o fluxo deve ser executável integralmente no computador sem impressão.

### Etiquetas

Etiquetas/labels de posição: **NOT PRESENT** no V1.

### Exportação

Exportação PDF/CSV de stock/movimentos: **NOT PRESENT** no V1 de implementação.

### Não inventar

Não inventar funcionalidades não evidenciadas, tais como:

- barcodes/QR;
- gestão de pallets;
- listas de picking avançadas;
- PDF próprio do Armazém fora dos casos evidenciados.

---

## 21. Informação consumida de outros módulos

| Informação | Módulo fonte | Owner funcional | Uso no Armazém | O Armazém pode modificar? | Live / snapshot / derivado |
|---|---|---|---|---|---|
| Identidade da ferramenta: tipo, referência, lote, nome técnico | Ferramentas | Ferramentas | identificar ferramenta/lote; guardar ID estável | NÃO como módulo Armazém | Live / consulta |
| ID estável do lote | Ferramentas | Ferramentas | ocupação / movimento | NÃO; referência | Live / referência |
| Estado técnico | Ferramentas | Ferramentas | contexto read-only opcional | NÃO | Live opcional |
| % uso / utilização | Ferramentas | Ferramentas | alerta de entrada; coluna read-only | NÃO calcula nem edita como módulo | Live / flag derivada |
| Diretório de reparadores registados | fonte canónica partilhada | Reparação como vocabulário canónico | seleção de reparador em Saída → Reparação; eventual adição na fonte canónica | Armazém não edita como módulo; pode disparar criação na fonte canónica | Live; snapshot histórico quando aplicável |
| Contexto de produção/atribuição | Job On | Job On | contextual; Armazém fornece mais do que consome | N/A | — |
| Contexto operacional de reparação | Reparação | Reparação | proveniência de reparação; saídas programadas | NÃO; Reparação owner do ciclo | Snapshot / derivação |

Nota crítica: quando a tabela diz que o Armazém não modifica dados de Ferramentas, isso refere-se ao **Armazém como módulo owner desses dados**. Não implica que um Responsável autorizado esteja proibido de editar a ficha da ferramenta através de um detalhe aberto via Armazém. Essa edição, quando válida, permanece operação e persistência de **Ferramentas**.

---

## 22. Informação fornecida a outros módulos

### Para Job On

O Armazém fornece ao Job On contexto físico para planeamento:

- posição/localização;
- presença;
- em produção;
- fora para reparação;
- regressada;
- disponibilidade;
- estado físico que exige atenção antes do início.

Funcionalmente isto aplica-se a CM/MF/BQ, porque BQ usa o modelo normal de Armazém. A implementação atual pode só materializar CM/MF; isso é divergência técnica.

### Último reparador para Job On

Job On pode exibir o último reparador relevante da ferramenta/lote selecionada.

Exemplo: `Reparador: MOLDIN`.

Regras:

- contexto read-only;
- derivado do histórico autoritativo;
- Job On não cria, não edita e não possui histórico de reparação/reparadores;
- apenas consome a informação mais recente;
- a origem dos factos é Reparação e/ou movimento físico com reparador, conforme arquitetura.

### Para Reparação externa

A Reparação confirma fisicamente através do mecanismo próprio do Armazém:

- **Recolha (pickup):** liberta a ocupação ativa e cria movimento de saída, associado à reparação.
- **Retorno (return):** ocupa a posição e cria movimento de entrada, associado à reparação.

Princípios:

- o ciclo de reparação e o movimento físico correm numa única operação atómica;
- o estado físico só muda após confirmação explícita persistida;
- nunca inferir movimento físico;
- o Armazém permanece dono do estado físico;
- a Reparação não escreve diretamente dados físicos do Armazém.

### Em geral

- O Armazém é o único dono do stock/ocupação e dos movimentos físicos.
- Não fornece stock/movimentos de Armazém a Controlo, Tampões, Boquilhas ou História como consumidor operacional direto.
- Controlo pode consumir identidade/contexto de ferramentas, não stock/movimentos do Armazém.
- Armazém não fornece PU/CS/TP/Pinças/Calibres ao Job On; essas entidades são configuração específica de produção, registadas manualmente no Job On pelo Responsável, porque não estão integradas no Armazém.

---

## 23. Relação com Ferramentas

### Ownership split

**Ferramentas possui:**

- identidade;
- nome técnico;
- desenho;
- compatibilidade;
- vida/uso;
- % uso;
- máquina/linha;
- estado técnico;
- BQ master.

**Armazém possui:**

- posição física;
- movimentos;
- destino operacional;
- whereabouts físico.

### UI entry point não muda ownership

Uma página alcançada via Armazém pode expor dados de Ferramentas e permitir abertura da ficha da ferramenta.

- Operador pode abrir/ler detalhe quando aplicável.
- Responsável pode editar master quando autorizado.
- A escrita permanece Ferramentas.
- O Armazém não passa a ser owner.

### Independência entre estado técnico e localização

Estado técnico e localização são independentes.

- A ferramenta pode estar armazenada enquanto o master continua editável.
- Uma mudança de posição não muda o master.
- Uma edição de master não muda a posição.

### Destino operacional ≠ estado técnico

- Saída → Reparação/Sucata não altera automaticamente estado técnico.
- Se estado técnico deve mudar, acontece no workflow Ferramentas.
- Movimento de Armazém nunca redefine silenciosamente estado técnico.

### Reparador ≠ master

O reparador selecionado numa Saída → Reparação:

- é dado do movimento físico;
- fica no histórico;
- não é edição de master;
- o ciclo de reparação pertence à Reparação.

### Identidade reutilizada, não copiada

O Armazém guarda o ID estável do lote. Não cria identidades paralelas.

### Movimento físico não altera master

Entrada/Saída não mudam:

- estado técnico;
- % uso;
- máquina/linha.

### Conflito de duas referências

Duas referências na mesma posição geram aviso de qualidade de dados, não normalização silenciosa.

### Acesso reverse e + Criar novo

- Ao pesquisar no Armazém, o utilizador deve poder abrir o detalhe da ferramenta.
- `+ Criar novo` cria master em Ferramentas e depois Entrada no Armazém, sem duplicar master.

### % uso

- % uso pertence ao lote/Ferramentas.
- Pode ser atualizada enquanto a ferramenta está armazenada.
- A aplicação não calcula % uso.
- O Armazém pode mostrar read-only e alertar para atualização pendente.

---

## 24. Relação com Job On

### Separação

- Produção/planeamento = Job On.
- Localização física = Armazém.

O Armazém fornece contexto de localização/presença/disponibilidade para planeamento.

### Seleção no Job On não cria movimento

Selecionar/associar ferramenta no Job On:

- NÃO cria movimento de Armazém;
- NÃO cria reserva;
- NÃO infere movimento físico.

Movimentos físicos continuam operações do Armazém.

### Filtragem de tooling

O filtro da lista de ferramentas no Job On baseia-se, por exemplo, em:

- Referência;
- Máquina/Linha;
- tool/lot registado.

O Responsável faz a seleção final. O Armazém fornece contexto complementar de localização/disponibilidade, mas não decide a ferramenta.

### Live vs snapshot

- Posição/localização atual é live, proveniente do Armazém.
- Snapshot histórico/revisão do Job On é guardado para produção.
- Em modo consulta, a folha do Job On mostra apenas a associação guardada.
- Informação live do Armazém não ocupa a folha guardada.
- A impressão do Job On nunca consulta dados live para substituir valores do snapshot.

### Último reparador

Job On pode mostrar o último reparador da ferramenta/lote como contexto read-only.

- Não possui histórico.
- Não edita histórico.
- Apenas consome o valor mais recente derivado dos factos históricos.

### Ferramentas não fornecidas

O Armazém não fornece ao Job On:

- PU;
- CS;
- TP;
- Pinças;
- Calibres.

Esses são configurações específicas de produção, registadas manualmente no Job On pelo Responsável.

### Em produção

“Em produção” é contexto/whereabouts operacional do Armazém, não estado técnico de Ferramentas.

---

## 25. Relação com Boquilhas

### Ownership fechado

- **BQ master owner = Ferramentas**.
- **BQ localização/movimentos físicos normais = Armazém**.
- **BQ external repair movement/history workflow = Boquilhas**.
- **BQ Reparação Interna = NO**.

Não reabrir este ownership.

### Divergência de implementação

A implementação pode representar Boquilhas com modelo próprio em vez de usar o modelo de lote das ferramentas.

Isso é divergência técnica/histórica e não é contradição funcional, porque a posse funcional está fechada.

### BQ no Armazém

Funcionalmente, BQ usa o modelo normal de Armazém:

- localização;
- movimentos;
- entrada/saída.

A diferença funcional relevante é o fluxo de reparação externa.

A falta de suporte técnico a BQ na implementação atual do Armazém é apenas reconciliação técnica, sem redesenhar o Armazém.

### Três domínios separados

- Ferramentas = BQ master.
- Boquilhas = movimentos de reparação externa de BQ, saldos e histórico próprio.
- Armazém = localização/movimento físico.

Boquilhas não é módulo de Armazém. Os saldos/movimentos Boquilhas são próprios e não reutilizam o modelo de warehouse.

---

## 26. Relação com Reparação Interna / Externa

### Ownership

- Reparação owns o registo/workflow de reparação interna e externa.
- Armazém owns o movimento físico/localização.

### Envio para reparação

Enviar ferramenta para reparação:

- não cria movimento físico automaticamente;
- só cria/remove/altera localização do Armazém através de confirmação física explícita;
- a recolha física é confirmada através do mecanismo próprio do Armazém;
- nada é inferido.

### Reparador e histórico

Em Saída → Reparação externa:

- reparador selecionado do diretório canónico;
- permanece no histórico da ferramenta;
- ferramenta/lote · reparador · data/hora · ciclo;
- ordem cronológica;
- sem sobreescrita;
- último reparador derivado dos factos históricos.

Regras de Reparação Externa/Boquilhas suportam o mesmo princípio:

- cada envio pode guardar snapshot do reparador usado;
- alterar associação não reescreve listas ou movimentos antigos.

### Reparação Interna

Na Reparação Interna:

- o reparador é o utilizador autenticado;
- não é selecionado manualmente;
- corrige/anula apenas os próprios registos, conforme regra própria.

A seleção manual de reparador tratada na Saída → Reparação do Armazém diz respeito ao fluxo de reparação externa CM/MF.

### Estado de reparação

Estado de reparação não muda automaticamente a partir de movimento de Armazém.

- Armazém regista físico.
- Reparação regista ciclo.

### Retorno de reparação

Retorno de reparação cria:

- Entrada/reocupação física no Armazém;
- movimento de entrada;
- com proveniência de reparação quando aplicável.

### Confirmação atómica

Qualquer confirmação que mude simultaneamente:

- o estado do ciclo de reparação;
- o estado físico;

corre numa única operação atómica, através do mecanismo de confirmação física do Armazém.

### Saída programada

- Criada na Reparação por um utilizador autorizado.
- Executada no Armazém pelo Operador.
- Fecho atómico.
- Retorno parcial → estado Retorno parcial.
- Só Concluída quando todas as linhas tiverem Entrada.

### Reparação Interna é CM/MF only

- BQ nunca Reparação Interna.
- BQ usa fluxo de reparação externa próprio de Boquilhas.

---

## 27. Relação com Tampões

Tampões é independente do modelo de Armazém.

- Possui saldos próprios.
- Possui movimentos próprios.
- Possui configurações/settings próprios.
- Não reutiliza o modelo de warehouse.
- Não tem posições/lotação no Armazém.

Não assumir que Tampões usa o mesmo modelo de warehouse.

Regra transversal:

- planear ≠ reservar.

Não existe relação funcional evidenciada com movimentos de localização física do Armazém além das regras transversais de não inferência e não reserva.

---

## 28. Relação com História

A **História** é uma superfície transversal de leitura — não é um módulo funcional atribuível.

- Lê eventos de auditoria read-only.
- Não possui eventos.
- Mostra apenas eventos dos módulos concedidos ao utilizador.
- Eventos administrativos podem exigir permissão específica de auditoria.

O Armazém gera eventos de auditoria, mas:

- História não se torna owner dos movimentos do Armazém;
- História não é o histórico operacional de localização do Armazém.

São coisas distintas:

- **História** = leitura transversal de auditoria;
- **Histórico do Armazém** = factos append-only de localização/movimento.

---

## 29. Regras negativas — ARMAZÉM NÃO...

### Master e ownership

- O Armazém **não é owner** do master da ferramenta.
- O Armazém pode expor dados master e permitir abrir a ficha da ferramenta.
- Quando um Responsável autorizado edita essa ficha, a operação e persistência permanecem em **Ferramentas**.
- UI entry point não transfere ownership.
- Operador não ganha permissão irrestrita de edição master apenas porque o detalhe está visível.

### Estado técnico

- Movimento de Armazém **não altera automaticamente** estado técnico.
- Saída → Reparação **não muda** automaticamente para “Por reparar”.
- Saída → Reparação **não muda** automaticamente para “Reparado”.
- Saída → Sucata **não muda** automaticamente para “Sucatado”.
- Um movimento de Armazém **não redefine silenciosamente** estado técnico.
- “Em produção”, “Em reparação” ou “Em fabrico” são contextos operacionais/físicos, não estados técnicos.

### Reparador

- A seleção de reparador na Saída → Reparação **não é edição de master**.
- Reparador é dado do movimento físico/ciclo.
- O ciclo/registo de reparação pertence à Reparação.
- Reparadores anteriores **não são sobreescritos**.
- Último reparador é derivado dos factos históricos.

### Job On

- Selecionar ferramenta no Job On **não cria movimento** de Armazém.
- Selecionar ferramenta no Job On **não cria reserva**.
- Job On **não cria/edita/possui** histórico de reparação.
- Job On apenas consome último reparador read-only.

### Reparação

- Registo de reparação **não se torna registo de warehouse**.
- Enviar para reparação **não cria movimento físico automaticamente**.
- Só confirmação explícita persistida move o estado físico.

### Produção

- Localização do Armazém **não é atribuição de produção**.
- Armazém **não calcula quantidade planeada** de produção.

### BQ / Boquilhas

- Armazém **não possui BQ master**.
- Armazém **não possui movimentos de reparação externa de BQ**.
- BQ master = Ferramentas.
- BQ external repair flow = Boquilhas.
- A representação técnica de Boquilhas fora do modelo de lote das ferramentas é divergência técnica/histórica e não muda a posse funcional.

### Histórico

- Localização histórica **não é sobreescrita/apagada**.
- Movimentos são append-only.
- Posição atual não apaga histórico.
- Correção **não reescreve silenciosamente** movimentos anteriores.

### Substituir

- O alvo funcional atual **não inclui ação normal Substituir**.
- A presença de Substituir na implementação é divergência técnica.
- Não promover Substituir a regra funcional.

### Normalização silenciosa

- Duas referências na mesma posição geram aviso.
- Nunca fusão/substituição automática.
- Armazém não normaliza silenciosamente conflitos.

### Invenção de dados

- Armazém não inventa estado, condição, reparador ou localização.
- Armazém não cria movimento automaticamente ao sinalizar inconsistência.
- `fora` é derivado, nunca persistido.

### Conteúdo do histórico

- Não duplicar no histórico do Armazém:
  - reparações completas;
  - vida útil;
  - estado técnico;
  - arquivo/sucata;
  - histórico de produção.

---

## 30. Casos especiais / exceções

### Ferramenta sem localização operacional

Quando a ferramenta não está associada a:

- Armazém;
- Produção;
- Reparação;

deve existir alerta **Localização operacional não registada**, por exemplo na página principal com contagem, podendo abrir Consulta filtrada.

O Armazém apenas sinaliza:

- não inventa estado;
- não cria movimento.

### Ferramenta em mais de um contexto

Se a ferramenta aparecer em contextos incompatíveis ao mesmo tempo:

- mostrar conflito;
- encaminhar para correção humana;
- não aplicar prioridade automática.

### Posição atual vs posição na criação

Em saída programada, se posição atual diferir do snapshot na criação:

- mostrar as duas;
- alerta;
- não corrigir/substituir silenciosamente o snapshot.

### Diferença física

Quando existir diferença física entre realidade e registo:

- o frontend não deteta magicamente;
- não inventar alertas preditivos;
- o operador seleciona o registo e abre Corrigir localização;
- correção auditável.

### Re-entrada na mesma posição

Reentrada da mesma ferramenta na mesma posição já ocupada:

- conflito controlado;
- não violação cega de índice único.

### Saída de ferramenta ausente

Saída de ferramenta que não está no Armazém:

- bloqueio: ferramenta não está registada como presente no Armazém.

### Estados vazios / limites técnicos

- referência inexistente → Ferramenta não encontrada;
- lote inexistente → Não existem lotes registados;
- posição vazia → Posição sem ocupação registada;
- erro de carregamento → Tentar novamente;
- falha ao guardar → manter dados e localização anterior; sem falso sucesso.

---

## 31. Histórico / Superseded

Material histórico ou recuperado não deve competir com o modelo funcional atual.

| Item | Classificação |
|---|---|
| Substituir atómico como produto normal / recovery | HISTORICAL / SUPERSEDED + TECHNICAL RECONCILIATION REQUIRED; alvo funcional atual é sem Substituir |
| Redação antiga sugerindo reparadores como texto livre ou nomes exemplificativos | SUPERSEDED pela regra fechada de diretório canónico de reparadores |
| Redação antiga “Destino como Produção/sem Sucata” | SUPERSEDED pela clarificação owner: Fabricação / Reparação / Sucata |
| Qualquer implicação antiga de que movimento muda estado técnico | SUPERSEDED pela regra fechada: Destino ≠ Estado técnico |
| Campos SAP legacy, tais como `sap_start`, `sap_end`, `value_added`, `value_cumulative` | LEGACY/UNCONFIRMED; não tratar como dados funcionais atuais; requisito atual é % uso |
| Documentação antiga que apresente Boquilhas como dona do master BQ | SUPERSEDED/HISTORICAL; BQ master owner fechado = Ferramentas |
| Planos recovery de U-14 que suportem ideias não confirmadas por owner decisions atuais | HISTORICAL; não promover |
| Correções de mojibake/encoding/markup antigas | RECOVERED/SUPERSEDED; corrigidas |

---

## 32. Questões Funcionais em Aberto

Permanecem exatamente **quatro** questões funcionais genuínas para Owner.

Não devem ser respondidas aqui. Não devem ser transformadas em novas questões owner. Subdetalhes não documentados devem ser tratados como parte destas questões ou como detalhe adiado.

### Q1. Distribuição exata de ações por perfil dentro do Armazém

A existência da divisão Operador / Responsável está fechada.

Permanece em aberto a distribuição exata das ações específicas do Armazém, por exemplo:

- que ações são exclusivas do Responsável;
- criação/gestão de posições de Armazém;
- determinadas ações de histórico ou rastreio;
- aprovação/cancelamento de registos físicos;
- que correções/configurações o Operador pode fazer.

Estes pontos são subdetalhes da mesma questão central: **distribuição exata de ações por perfil dentro do Armazém**.

### Q2. Tabs / Programadas — alvo funcional da tab e da área Programadas

Permanece em aberto:

- qual é o alvo funcional da tab Programadas;
- se a estrutura final de áreas deve ser 2 tabs ou 4 tabs;
- se Programadas deve ter fluxo completo com checkboxes, apenas indicação/shell, ou lista pendente perspetivada.

A tab Histórico, enquanto detalhe de apresentação com filtros De/Até vs calendário, é reconciliação de apresentação e não questão funcional central.

### Q3. Destino da Saída — obrigatoriedade

Está fechado que os destinos operacionais são:

- Fabricação;
- Reparação;
- Sucata.

Permanece em aberto se o Destino é:

- obrigatório em todas as Saídas;
- ou opcional.

O formato/taxonomia técnica do destino é reconciliação técnica.

### Q4. Estado na Entrada — classificação exata

Permanece em aberto a classificação exata do campo Estado na Entrada:

- read-only técnico?
- contexto apenas do movimento?
- campo que não pertence ao Armazém?

Está fechado que:

- o Armazém não deve alterar silenciosamente o estado técnico;
- a resposta não deve ser escolhida a partir da implementação atual.

---

## 33. Detalhe Funcional Adiado

Detalhes funcionais adiados, não bloqueantes para o modelo central do Armazém.

### Detalhes da lista de Saída Programada

Permanecem adiados:

- cancelamento da lista;
- quem pode cancelar;
- adicionar/remover linhas após publicação;
- confirmação final automática pelo último check vs ação adicional;
- encerrar/cancelar linha que não regressa;
- motivo obrigatório para linha que não regressa.

Estes detalhes não bloqueiam o modelo funcional atual do Armazém. A sua decisão espera priorização/ativação do fluxo completo de saída programada.

Não promover estes pontos a novas questões owner independentes.

---

## 34. Resumo Funcional Final

O **Armazém** é um módulo funcional de topo, owner da **localização física** e dos **movimentos físicos** de ferramentas.

### Modelo confirmado

- CM, MF e BQ usam o modelo normal de Armazém.
- BQ master pertence a Ferramentas.
- BQ localização/movimentos pertencem ao Armazém.
- BQ external repair workflow pertence a Boquilhas.
- BQ nunca é Reparação Interna.
- PU e CS são ferramentas em Ferramentas, mas atualmente não integrados no Armazém.
- A eventual ausência técnica de BQ na implementação é reconciliação técnica, não adiamento funcional.

### Papéis

- Admin não é perfil operacional do Armazém.
- Existem apenas Admin, Operador / Controlador e Responsável.
- A existência da divisão Operador/Responsável no Armazém está fechada.
- A distribuição exata de ações específicas permanece em aberto.
- Operador executa movimentos físicos, define destino, seleciona reparador quando aplicável, confirma e corrige erros operacionais com auditoria.
- Responsável pode editar master via Ferramentas, mesmo quando o ponto de entrada é Armazém.
- Correção operacional do Operador ≠ edição master do Responsável.

### Localização

- Posição única de 4 dígitos.
- Ocupação 1:1.
- Fora é derivado.
- Sem hierarquia de zona/rack/prateleira.
- Conflitos são avisados, nunca normalizados silenciosamente.

### Movimentos

- Entrada/Saída append-only.
- Entrada ocupa atomicamente.
- Saída liberta só após persistência.
- Destinos operacionais: Fabricação / Reparação / Sucata.
- Saída programada existe funcionalmente e partilha fluxo com Reparação.

### Destino ≠ Estado técnico

- Destino responde “para onde vai?”.
- Estado técnico responde “qual é a condição?”.
- Movimento de Armazém não altera silenciosamente estado técnico.

### Reparador

- Saída → Reparação seleciona reparador do diretório canónico.
- Reparador fica associado ao movimento/histórico.
- Não é master data.
- Não é sobreescrito.
- Último reparador é derivado dos factos históricos.
- Job On consome último reparador read-only.

### Stock/saldo

- Ocupação por lote de ferramenta.
- Sem saldo agregado.
- A quantidade do movimento não é usada como saldo.
- Armazém não calcula saldos Boquilhas, Tampões ou quantidades Job On.

### Validações

- Bloqueios estruturais duros apenas.
- Avisos não bloqueiam produção automaticamente.
- Warning ≠ decisão automática.

### Histórico

- Append-only.
- Correções não apagam factos anteriores.
- Auditoria transversal.
- História é leitura, não owner dos movimentos do Armazém.

### Ownership fechado

Armazém não possui:

- master — Ferramentas;
- estado técnico — Ferramentas;
- produção — Job On;
- movimentos de reparação externa de BQ — Boquilhas;
- BQ master — Ferramentas;
- workflow de reparação — Reparação;
- saldos Tampões — Tampões.

### Questões genuínas

Permanecem 4 questões genuínas:

1. Distribuição exata de ações por perfil dentro do Armazém.
2. Tabs/Programadas: alvo funcional da tab Programadas e estrutura final.
3. Destino da Saída: obrigatório vs opcional.
4. Estado na Entrada: classificação exata.

### Recomendação funcional

O modelo funcional confirmado está pronto como base para validação owner e posterior tratamento de reconciliação técnica. Não há contradições funcionais de autoridade atual em aberto além das quatro questões genuínas listadas.

---

## Implementation Pointers

### Relevant implementation areas

- Database: physical state owned by Armazém — tables referenced as `warehouse_stock` and `warehouse_movements`; movements `in`/`out`; location context values `armazem` / `fora` (derived, never persisted) / `nao_registado`; 4-digit position codes; occupancy 1:1.
- Application: Entrada/Repor (validações, re-entrada na mesma posição), Saída/Retirar (destino Fabricação/Reparação/Sucata; reparador quando Reparação), Saídas Programadas (estados, impressão opcional, confirmação física atómica), Correção de localização, "+ Criar novo" (new tool record), pesquisa/filtros/deep-link, etiquetas/exportação.
- Application: repair integration via the Armazém movement port — see `70_REPARACAO_EXTERNA_FUNCTIONAL.md` (`IArmazemRepairMovementPort`, same UoW, atomic physical confirmations), shared with the Reparação flow.
- Technical map: `maps\09_ARMAZEM.md` (verify freshness before use).

### Known implementation gaps (verified in this document set)

- Implementation may not (yet) support BQ in Armazém — BQ is functionally current; absence of technical support is "reconciliação técnica", NOT functional deferral (§4).
- Saída Programada full-flow UI may not be fully active — implementation reconciliation, not functional absence (§11 "Implementação").
- `Substituir` exists as an action in the current implementation but is outside the current functional target (target: without Substituir) — divergence = technical reconciliation required (§7, §31).

### Design reference

- `AI-CONTEXT\design-coder\32_ARMAZEM_01_VISUAL_AUTHORITY_armazem.html`

### Cross-module dependencies

- Ferramentas (identidade/master; `% uso`; Entrada `Estado` synchronisation — open question); Job On (seleção não cria movimento; live vs snapshot; último reparador); Reparação Interna/Externa (confirmações físicas via porto; estado stored-tool `Por reparar`/`Reparado` vive no contexto Armazém); Boquilhas (master BQ = Ferramentas; BQ comporta-se como CM/MF no modelo normal de ferramenta/master/armazém).