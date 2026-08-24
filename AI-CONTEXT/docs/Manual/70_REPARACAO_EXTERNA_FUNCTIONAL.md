# REPARAÇÃO EXTERNA — MODELO FUNCIONAL

OPEN OWNER QUESTIONS: 0

## Decisões Owner fechadas

- D1 — quem usa o módulo:
  - a Reparação Externa é um módulo do Responsável;
  - o Operador não gere batches na Reparação Externa;
  - o Operador usa o Armazém para movimentos físicos individuais de reparação:
    - destino = reparação;
    - associação do reparador.

- D2 — estrutura:
  - áreas internas: Registo / Ferramentas / Histórico / Definições;
  - CM e MF são seleções/fluxos de tipo separados;
  - Boquilhas é módulo de topo separado;
  - não é área interna da Reparação Externa;
  - a antiga navegação combinada “Reparação” e a composição de seis áreas estão superseded.

- D3 — edição:
  - um batch de Reparação Externa é sempre editável pelo Responsável;
  - em qualquer fase do ciclo de vida;
  - o estado nunca bloqueia nem remove opções de edição.

## Regras Owner explícitas preservadas

1. REPARAÇÃO EXTERNA = módulo do RESPONSÁVEL para gestão de batches de reparação externa CM/MF.
2. OPERADOR não gere batches da Reparação Externa.
   - O Operador pode usar o Armazém para movimentos físicos individuais de reparação:
     - saída física de ferramenta individual;
     - destino = reparação;
     - associação do reparador externo.
3. BATCH MANAGEMENT = Reparação Externa.
   - INDIVIDUAL PHYSICAL MOVEMENT = Armazém.
4. INTERNAL AREAS:
   - Registo;
   - Ferramentas;
   - Histórico;
   - Definições.
5. CM e MF:
   - tipos separados;
   - fluxos separados;
   - nunca combinados num único tipo;
   - nunca combinados num batch misto.
6. BQ:
   - fora da Reparação Externa;
   - sem tab BQ;
   - sem tipo de batch BQ;
   - a reparação externa de BQ pertence ao módulo Boquilhas.
7. BATCH EDITING:
   - sempre editável pelo Responsável;
   - em todas as fases do ciclo de vida;
   - o estado nunca remove opções de edição;
   - sem estados congelados;
   - sem locks de aprovação;
   - sem restrições de UI baseadas em estado.
8. OWNER PRINCIPLE:
   - nunca bloquear ou remover opções do utilizador;
   - salvo se o Owner estabelecer explicitamente uma impossibilidade de negócio genuína.

---

## Índice

- [Decisões Owner fechadas](#decisões-owner-fechadas)
- [Regras Owner explícitas preservadas](#regras-owner-explícitas-preservadas)
- [1. Objetivo do Módulo](#1-objetivo-do-módulo)
- [2. Posição no Modelo Global](#2-posição-no-modelo-global)
- [3. Perfis, Utilizadores e Acesso](#3-perfis-utilizadores-e-acesso)
  - [3.1 Responsável](#31-responsável)
  - [3.2 Operador](#32-operador)
  - [3.3 Admin](#33-admin)
  - [3.4 Confirmação física](#34-confirmação-física)
- [4. Estrutura Interna](#4-estrutura-interna)
  - [4.1 Registo](#41-registo)
  - [4.2 Ferramentas](#42-ferramentas)
  - [4.3 Histórico](#43-histórico)
  - [4.4 Definições](#44-definições)
  - [4.5 CM vs MF](#45-cm-vs-mf)
  - [4.6 Envios](#46-envios)
  - [4.7 O que não pertence ao módulo](#47-o-que-não-pertence-ao-módulo)
- [5. Conceito Central do Batch de Reparação Externa](#5-conceito-central-do-batch-de-reparação-externa)
  - [5.1 O que é um batch](#51-o-que-é-um-batch)
  - [5.2 Dados do batch](#52-dados-do-batch)
  - [5.3 Ferramentas do batch](#53-ferramentas-do-batch)
  - [5.4 Edição permanente](#54-edição-permanente)
  - [5.5 Identidade e duplicação](#55-identidade-e-duplicação)
  - [5.6 Lifecycle e permanência](#56-lifecycle-e-permanência)
- [6. Fluxo Operacional Completo](#6-fluxo-operacional-completo)
  - [6.1 Preparação](#61-preparação)
  - [6.2 Criação do batch](#62-criação-do-batch)
  - [6.3 Associação do reparador](#63-associação-do-reparador)
  - [6.4 Disponibilização ao Armazém](#64-disponibilização-ao-armazém)
  - [6.5 Retirada física](#65-retirada-física)
  - [6.6 Envio](#66-envio)
  - [6.7 Acompanhamento](#67-acompanhamento)
  - [6.8 Retorno parcial](#68-retorno-parcial)
  - [6.9 Retorno completo](#69-retorno-completo)
  - [6.10 Conclusão](#610-conclusão)
- [7. Estados do Batch](#7-estados-do-batch)
  - [7.1 Estados atuais](#71-estados-atuais)
  - [7.2 Estado Cancelado](#72-estado-cancelado)
  - [7.3 Transições](#73-transições)
  - [7.4 Estado não bloqueia edição](#74-estado-não-bloqueia-edição)
- [8. Reparadores](#8-reparadores)
  - [8.1 Diretório](#81-diretório)
  - [8.2 Seleção](#82-seleção)
  - [8.3 Defaults](#83-defaults)
  - [8.4 Tipos, máquina e linha](#84-tipos-máquina-e-linha)
  - [8.5 Ativo e inativo](#85-ativo-e-inativo)
  - [8.6 Snapshot histórico](#86-snapshot-histórico)
- [9. Relação com Armazém](#9-relação-com-armazém)
  - [9.1 Movimento individual](#91-movimento-individual)
  - [9.2 Fluxo de batch](#92-fluxo-de-batch)
  - [9.3 Ownership físico](#93-ownership-físico)
  - [9.4 Confirmações físicas](#94-confirmações-físicas)
  - [9.5 Regras de integridade](#95-regras-de-integridade)
- [10. Relação com Ferramentas](#10-relação-com-ferramentas)
- [11. Relação com Job On](#11-relação-com-job-on)
- [12. Relação com Reparação Interna](#12-relação-com-reparação-interna)
- [13. Relação com Boquilhas](#13-relação-com-boquilhas)
- [14. Dados Introduzidos pelo Utilizador](#14-dados-introduzidos-pelo-utilizador)
- [15. Dados Derivados e Automáticos](#15-dados-derivados-e-automáticos)
- [16. Quantidades e Identidade das Peças](#16-quantidades-e-identidade-das-peças)
- [17. Recolha e Retorno](#17-recolha-e-retorno)
- [18. Casos Parciais e Excecionais](#18-casos-parciais-e-excecionais)
- [19. Validações de Consistência, Avisos e Limites de Âmbito](#19-validações-de-consistência-avisos-e-limites-de-âmbito)
- [20. Histórico e Auditoria](#20-histórico-e-auditoria)
- [21. Documentos, Impressão e Outputs](#21-documentos-impressão-e-outputs)
- [22. Ownership](#22-ownership)
- [23. Regras Negativas](#23-regras-negativas)
- [24. Atual vs Histórico, Superseded e Deferred](#24-atual-vs-histórico-superseded-e-deferred)
- [25. Questões Owner](#25-questões-owner)
  - [25.1 Questões fechadas](#251-questões-fechadas)
  - [25.2 Questões abertas](#252-questões-abertas)
- [26. Resumo Funcional Final](#26-resumo-funcional-final)

---

## 1. Objetivo do Módulo

REPARAÇÃO EXTERNA = GESTÃO DE BATCHES DE REPARAÇÃO EXTERNA DE CM/MF, módulo do Responsável.

O módulo existe primariamente para o Responsável:

1. preparar um batch de reparação;
2. escolher as ferramentas CM ou MF desse batch;
3. associar o reparador;
4. despachar o batch;
5. acompanhar o progresso;
6. gerir os retornos;
7. concluir o batch;
8. manter o histórico do batch.

O módulo não deve ser descrito primariamente como um ecrã genérico de movimentos de armazém.

- Os movimentos físicos individuais pertencem ao Armazém.
- A Reparação Externa resolve o problema real de a fábrica ter de enviar ferramentas de moldes, CM e MF, para reparadores externos.
- O módulo acompanha esse ciclo:
  - que ferramentas saem;
  - para qual reparador;
  - quando voltam;
  - o que fica registado.
- É a gestão de batches / listas de saída programada para reparação externa:
  - preparados com antecedência;
  - com confirmação física explícita de saída e retorno;
  - coordenada com o Armazém.

Ponto operacional chave:

- O fluxo não parte da ferramenta que está em produção.
- Essa associação pertence exclusivamente à Reparação Interna.
- A Reparação Externa parte de uma produção futura planeada.
- O Responsável prepara o batch dias antes do início previsto do fabrico.
- A reparação externa não carrega a produção atualmente ativa.
- A lista parte de uma produção futura e mostra a data prevista de início.

A menção de BQ no propósito do brief V1 é histórica / superseded.

- O BQ não participa na Reparação Externa.
- O fluxo de movimentos de reparação externa de BQ é o módulo Boquilhas.

---

## 2. Posição no Modelo Global

- Módulo de topo, top-level.
- Atribuível por utilizador no Admin.
- Módulo canónico: `reparacao_externa`.
- Ordem: 70.
- Rota: `/reparacao-externa`.
- Uma única page id: `reparacao_externa.listas`.
- É um módulo lógico de acesso atribuído por utilizador.
- Sem atribuição, o módulo não aparece na navegação nem dá acesso.
- Sem capabilities declaradas.
- Acesso = módulo atribuído.
- Sem capability adicional documentada.

Posição relativa:

- Ferramentas:
  - dono do master CM/MF, peças/lotes;
  - a Reparação Externa consome identidade em leitura.
- Armazém:
  - dono único do estado físico: stock, posições e movimentos;
  - a Reparação Externa nunca escreve tabelas do Armazém;
  - consome o porto de movimentos;
  - detém também o fluxo de movimentos físicos individuais de reparação do Operador.
- Job On:
  - contexto de produção/planeamento;
  - relação explícita e pontual, campo no batch;
  - não automática.
- Reparação Interna:
  - módulo irmão, separado;
  - intervenções internas de turno vs ciclos externos;
  - fronteira explícita.
- Boquilhas:
  - módulo de topo separado;
  - dono do fluxo de movimentos de reparação externa de BQ;
  - o BQ não participa na Reparação Externa;
  - sem tab BQ;
  - sem batches BQ;
  - sem mistura com CM/MF.

---

## 3. Perfis, Utilizadores e Acesso

- Modelo de perfis global: exatamente três perfis funcionais:
  - Admin;
  - Responsável;
  - Operador / Controlador.
- Não existe quarto perfil.
- A Reparação Externa é um módulo do RESPONSÁVEL, decisão do Owner D1.

### 3.1 Responsável

- O fluxo de preparação, edição, disponibilização, acompanhamento e conclusão de batches é executado pelo Responsável com o módulo atribuído.
- Brief: “o responsável seleciona uma produção futura planeada e prepara a lista”.
- Mockup: cabeçalho “Responsável de moldes”.
- O Responsável gere o batch durante todo o ciclo.
- O batch é sempre editável pelo Responsável, decisão D3.

### 3.2 Operador

- O Operador NÃO usa a Reparação Externa para gerir batches.
- Não existe comportamento de Operador dentro da Reparação Externa.
- O Operador pode usar o Armazém para:
  - registar uma saída física individual de ferramenta;
  - marcar o destino como reparação;
  - associar o reparador externo.
- Essa é uma operação de Armazém, segundo as regras do Armazém.
- Não é gestão de batches na Reparação Externa.

### 3.3 Admin

- O Admin não é operacional dentro do módulo.
- O Admin atua ao nível da atribuição do módulo ao utilizador:
  - criar/editar utilizador;
  - escolher módulos.
- Pode consultar auditoria transversal via História/diário.
- Comportamento geral do Admin, não específico do módulo.

### 3.4 Confirmação física

- Quem confirma fisicamente é o operador do Armazém.
- Confirma cada retirada/entrada de posição.
- Ação executada no âmbito do ciclo.
- O efeito físico pertence ao Armazém.

---

## 4. Estrutura Interna

ÁREAS INTERNAS DO MÓDULO, decisão do Owner D2.

Composição atual preservada do mockup Moldes:

1. Registo
2. Ferramentas
3. Histórico
4. Definições

### 4.1 Registo

- Criação/edição de batches e listas programadas.
- Construtor de batches CM e MF.
- Tipos separados.
- Pode apresentar as listas programadas.

### 4.2 Ferramentas

- Seleção/pesquisa das ferramentas CM ou MF que compõem os batches.

### 4.3 Histórico

- Consulta transversal do ciclo.
- Inclui, quando aplicável:
  - saída/entrada;
  - operadores;
  - reparador;
  - estado.

### 4.4 Definições

- Reparadores.
- Associações por tipo.
- Associações por Linha/máquina.

### 4.5 CM vs MF

- Dentro das áreas relevantes, CM e MF são seleções/fluxos de TIPO separados.
- Padrão do mockup de autoridade visual Moldes: botões segmentados.
- CM e MF permanecem funcionalmente separados.
- Nunca são combinados num único tipo.
- Nunca são combinados num batch misto.

### 4.6 Envios

- Não inventar um tab independente “Envios”.
- “Envios” é tratado funcionalmente como o ciclo de vida do batch/lista:
  - batches programados;
  - progresso de despacho;
  - recolha;
  - retorno;
  - conclusão.
- A autoridade visual atual pode apresentar as listas programadas no Registo.
- O modelo funcional explica o ciclo de vida sem inventar navegação extra.

### 4.7 O que não pertence ao módulo

- Boquilhas NÃO é uma área interna deste módulo, decisão do Owner D2.
- Não existe tab Boquilhas na Reparação Externa.
- O BQ não tem comportamento aqui.
- O fluxo BQ é o módulo de topo Boquilhas.
- A antiga composição de seis áreas:
  - Boquilhas;
  - Contra moldes;
  - Moldes finais;
  - Envios;
  - Histórico;
  - Definições;
  está SUPERSEDED / DO NOT IMPLEMENT.
- Inclui a decisão anterior “buttons vs tabs → tabs globais” do contrato de implementação.

---

## 5. Conceito Central do Batch de Reparação Externa

### 5.1 O que é um batch

O batch representa uma saída programada de ferramentas para um reparador externo.

- Batch/envio de ferramentas para reparação externa.
- Preparado para uma produção futura.
- Não parte da ferramenta atualmente em produção.
- É preparado com antecedência.
- O Responsável prepara, compõe, associa reparador, despacha, acompanha, gere retorno e conclui.

### 5.2 Dados do batch

Cabeçalho do batch:

- código da lista;
- tipo:
  - CM ou MF;
  - nunca misturados;
- reparador;
- data prevista;
- criado por/data;
- estado.

Itens do batch, para CM/MF:

- Referência;
- lote;
- número individual;
- máquina/linha;
- posição atual, quando conhecida.

Não há itens BQ nesta autoridade.

### 5.3 Ferramentas do batch

CM:

- ATUAL/ATIVO.
- Unidade operacional: ferramenta CM individual.
- Seleção por:
  - Referência;
  - lote;
  - máquina permitida;
  - número individual.
- Estado e localização vêm dos domínios respetivos.
- Saída programada referencia IDs estáveis de CM.
- Retorno pode incluir observação.
- A observação não altera dados mestres automaticamente.
- Batch preparado para produção futura.

MF:

- ATUAL/ATIVO.
- Segue exatamente o mesmo ciclo externo do CM.
- Usa exclusivamente ferramentas MF.
- Usa os respetivos IDs, campos, reparadores e histórico.
- CM e MF são tipos separados, decisão do Owner D2.
- Partilhar UI não autoriza combinar CM e MF.
- Nunca num único tipo.
- Nunca num batch misto.

### 5.4 Edição permanente

Decisão do Owner D3:

- O batch é sempre editável pelo Responsável.
- Em qualquer fase do ciclo de vida.
- Inclui:
  - antes da criação;
  - após ficar disponível ao Armazém;
  - em “A retirar”;
  - em “Enviado”;
  - em retorno parcial;
  - em qualquer outro estado.
- O estado nunca bloqueia nem remove opções de edição.
- Inclui a composição/ações relevantes do batch.
- Nenhuma regra limita a edição a “antes de criar/enviar”.
- A decisão do Owner substitui qualquer redação anterior que limitasse adicionar/remover itens a antes de criar/enviar.

Regra do Owner:

- Nunca bloquear nem remover opções sem uma regra de negócio explícita do Owner.

### 5.5 Identidade e duplicação

- CM e MF usam listas e coleções temporárias separadas.
- Mudar o seletor nunca converte nem mistura ferramentas já adicionadas.
- Ao regressar ao tipo anterior, o rascunho é preservado.
- CM e MF nunca são fundidos num único tipo.
- Criar uma lista CM/MF exige pelo menos uma ferramenta individual adicionada.
- O mesmo item lógico, tipo + ferramenta/lote, não pode figurar duas vezes no mesmo batch/contexto de saída aberta.
- Duplicá-lo criaria dados duplicados.
- Trata-se de prevenção de duplicidade de dados, identidade.
- Não é um bloqueio de workflow.
- Dentro da mesma lista, o construtor evita/não persiste o duplicado.

### 5.6 Lifecycle e permanência

- O estado deriva de confirmações persistidas.
- Nunca de abrir a página.
- Um batch concluído não desaparece.
- Passa para o Histórico.

Momentos de ciclo:

- disponibilizado → “A retirar”;
- todas as retiradas confirmadas → “Enviado”, saída concluída;
- retorno parcial → “Retorno parcial”;
- todos os itens de volta → “Concluído”, ciclo fechado.

---

## 6. Fluxo Operacional Completo

Sequência cronológica:

### 6.1 Preparação

- O Responsável prepara o batch vários dias antes do início previsto do fabrico.
- Parte de uma produção futura planeada.
- Não parte da ferramenta em produção.
- Isso pertence à Reparação Interna.
- A página não mostra cartões de produções ativas.
- Começa diretamente pela seleção CM/MF e pesquisa de ferramentas.
- Quando o batch precisar de associação a uma produção prevista, essa escolha aparece como campo compacto dentro do formulário do batch.

### 6.2 Criação do batch

- O Responsável escolhe o tipo de batch:
  - CM;
  - MF.
- Pesquisa por:
  - Referência;
  - lote;
  - nº individual.
- Seleciona:
  - Referência;
  - lote;
  - número individual da ferramenta.
- Escolhe um reparador permitido.
- Define a data prevista.
- Adiciona itens.
- Sem pelo menos uma ferramenta, o botão “Criar lista” fica desativado.
- O batch pode ser editado em qualquer fase, decisão D3.

### 6.3 Associação do reparador

- O reparador é escolhido no batch de envio.
- A seleção é feita a partir do diretório registado.
- O dropdown é filtrado por tipo e Linha/máquina.
- Pode existir default do último reparador conhecido.
- O utilizador pode alterar manualmente antes de guardar.
- O envio guarda snapshot do reparador usado.

### 6.4 Disponibilização ao Armazém

- O batch fica disponível no Armazém.
- Estado associado: “A retirar”.
- Pode ser impresso, conforme suporte V1.
- A edição permanece disponível.

### 6.5 Retirada física

- O operador do Armazém confirma cada retirada com um check.
- Confirmação explícita persistida.
- A confirmação item a item pertence ao Armazém.
- Os efeitos físicos ocorrem através do porto do Armazém.

### 6.6 Envio

- Quando todos os itens estão confirmados, a saída é concluída.
- As posições ficam livres.
- Estado: “Enviado”.
- A saída está concluída.
- O ciclo ainda não fechou.

### 6.7 Acompanhamento

- O Responsável acompanha o envio.
- Não duplica os movimentos do Armazém.
- Os movimentos físicos são do Armazém.
- A Reparação Externa acompanha o ciclo de vida do batch.

### 6.8 Retorno parcial

- Parte dos itens regressou.
- Ainda há itens fora.
- Estado: “Retorno parcial”.
- O progresso é mostrado explicitamente.
- O ciclo só fecha quando todos os itens regressarem.

### 6.9 Retorno completo

- No retorno, o Armazém confirma cada entrada e posição.
- Confirmação explícita.
- O retorno pode incluir observação, no caso CM.
- A observação não altera dados mestres automaticamente.
- O retorno pode ocorrer item a item.

### 6.10 Conclusão

- Quando todos os itens regressam, o ciclo fecha.
- Estado: “Concluído”.
- “O retorno fecha o ciclo item a item”.
- O batch concluído passa para o Histórico.
- Os factos históricos persistidos não são reescritos.

Regra transversal SOT C/D:

- Qualquer confirmação que altere simultaneamente:
  - o estado do ciclo de reparação;
  - o estado físico do Armazém;
- corre num único unit of work.
- Nenhum efeito físico é inferido.
- Só confirmações explícitas persistidas movem ferramentas.

---

## 7. Estados do Batch

### 7.1 Estados atuais

Estados visuais V1 documentados no brief §6 e reconhecidos no design atual.

- Preparação (`Preparacao`):
  - o batch está a ser construído;
  - ainda não concluído para retirada.

- A retirar (`ARetirar`):
  - o batch está disponível no Armazém;
  - retiradas em curso;
  - confirmação item a item.

- Enviado:
  - todas as retiradas confirmadas;
  - saída concluída;
  - posições livres;
  - aguarda retorno.

- Retorno parcial:
  - parte dos itens regressou;
  - ainda há itens fora.

- Concluído:
  - todos os itens regressaram;
  - ciclo fechado.

### 7.2 Estado Cancelado

- Cancelado é estado apenas de compatibilidade de schema.
- A funcionalidade de cancelamento, `CancelarLista`, está adiada.
- DES-015 MUST NOT: “expose CancelarLista”.
- Não fazer do Cancelado uma regra operacional ativa.

### 7.3 Transições

- As transições não são inferidas pela abertura da página.
- Cada transição corresponde a confirmações persistidas.
- O mapeamento exato confirmação→estado é coerente com o ciclo:
  - primeira recolha → A retirar;
  - todas recolhidas → Enviado;
  - parcial → Retorno parcial;
  - todas → Concluído.
- Esse mapeamento é executado pela máquina de estados.
- A autoridade não detalha mais transições.

### 7.4 Estado não bloqueia edição

Decisão do Owner D3:

- Nenhum estado remove ou bloqueia a edição do batch pelo Responsável.
- Exemplos de estados que não bloqueiam edição:
  - “A retirar”;
  - “Enviado”;
  - “Retorno parcial”;
  - qualquer outro estado válido.
- Avançar o batch não remove ações de edição.
- Não existem bloqueios de aprovação.
- Não existem estados congelados.
- Não existem restrições de interface baseadas em estado sem regra de negócio explícita.

---

## 8. Reparadores

### 8.1 Diretório

- Existe um diretório canónico de reparadores.
- Vocabulário partilhado.
- Diretório registado: `repairers/line_repairer_defaults`.
- Reutilizado pelos fluxos de reparação.
- Não modelar reparadores como texto livre dentro de cada registo.
- Clarificação Ferramentas §11.1.

### 8.2 Seleção

- O reparador é escolhido no batch de envio.
- A seleção é feita a partir do dropdown do diretório registado.
- Filtrado por tipo:
  - CM;
  - MF.
- Filtrado por Linha/máquina.
- Aceitação: “reparadores são filtrados pelo tipo e Linha/máquina”.

### 8.3 Defaults

- Para reparação externa CM/MF:
  - pré-preencher o último reparador conhecido da ferramenta;
  - com possibilidade de alteração manual antes de guardar.
- Clarificação Ferramentas §11.4.
- Regra própria do fluxo CM/MF.
- Distinta da lógica BQ por linha, que não é desta autoridade.
- Existe também armazenamento técnico de default por linha/tipo, `line_defaults`.
- Isso é evidência técnica.

### 8.4 Tipos, máquina e linha

- Um reparador pode suportar múltiplos tipos.
- Associação reparador ↔ tipo CM/MF.
- Tabela `repairer_repair_types`.
- A capacidade por tipo é separada da associação por linha.

### 8.5 Ativo e inativo

- Os reparadores têm estado ativo/inativo.
- Desativar, não eliminar.
- DES-015 fala em “repairer/line settings”.
- Eliminação: NÃO ESPECIFICADA na autoridade funcional.

### 8.6 Snapshot histórico

- Alterar uma associação não reescreve listas ou movimentos antigos.
- Cada envio guarda snapshot do reparador usado.
- README MUST PRESERVE: “repairer snapshots”.

---

## 9. Relação com Armazém

Duas vias válidas, decisão do Owner D1.

### 9.1 Movimento individual

A. MOVIMENTO FÍSICO INDIVIDUAL DE REPARAÇÃO, via Armazém.

O Operador pode usar o Armazém diretamente para:

- dar saída a uma ferramenta individual;
- marcar o destino como reparação;
- associar o reparador externo.

Isto não exige usar a gestão de batches da Reparação Externa.

- É uma operação de Armazém.
- Segue as regras do Armazém.
- Não implicar que toda a ferramenta enviada para reparação externa tem de provir de um batch da Reparação Externa.

### 9.2 Fluxo de batch

B. FLUXO DE BATCH.

Quando é usado um batch da Reparação Externa:

- a Reparação Externa é dona do batch:
  - plano;
  - ciclo;
  - itens;
  - estado;
  - histórico.
- o Armazém é dono do estado/movimentos físicos.
- as confirmações físicas continuam a ser do Armazém:
  - recolha/retorno item a item;
  - posições.
- a Reparação Externa acompanha o ciclo de vida do batch de reparação externa.

### 9.3 Ownership físico

- O Armazém é o DONO ÚNICO do estado físico.
- Inclui:
  - `warehouse_stock`;
  - `warehouse_movements`.
- O Armazém é dono da libertação/reocupação de posições.

### 9.4 Confirmações físicas

- As confirmações de recolha/retorno executam os efeitos físicos através do porto do Armazém.
- Porto: `IArmazemRepairMovementPort`.
- No mesmo unit of work da alteração do estado do ciclo.
- Nenhum efeito físico é inferido.
- Só confirmações explícitas persistidas movem ferramentas.

### 9.5 Regras de integridade

- A Reparação Externa NUNCA escreve diretamente tabelas do Armazém.
- DES-015 MUST NOT: “write warehouse tables directly”.
- A Reparação Externa consome o porto detido pelo Armazém.
- As confirmações de recolha/retorno executam efeitos físicos através desse porto.
- Os efeitos ocorrem no mesmo unit of work da alteração do estado do ciclo.
- Nenhum efeito físico é inferido.

---

## 10. Relação com Ferramentas

- O master CM/MF pertence às Ferramentas.
- Inclui:
  - identidade;
  - referência;
  - lotes;
  - peças;
  - características.
- A Reparação Externa não possui o master de ferramentas.
- A Reparação Externa não possui o general lifecycle.
- A Reparação Externa lê do domínio Ferramentas a identidade das peças CM/MF:
  - referência;
  - lote;
  - número individual.
- Leitura via porto:
  - `IFerramentasPieceLookup`;
  - `IToolPieceResolver`.
- Posição/estado vêm dos domínios respetivos.
- Brief §4: “estado e localização vêm dos domínios respetivos”.
- Não modifica o master.
- “Nenhuma vista cria cópias divergentes das ferramentas”.
- A saída programada referencia IDs estáveis de CM/MF.

---

## 11. Relação com Job On

Comportamento documentado, restrito:

- O batch é preparado para uma produção futura planeada.
- Mostra a data prevista de início.
- A associação a uma produção prevista aparece como campo compacto dentro do formulário do batch.
- Não é leitura automática da produção ativa.
- A página não carrega a produção atualmente ativa.

Job On:

- O Job On não cria registos de reparação externa.
- O Job On não seleciona ferramentas reparadas para produção futura.
- A relação é contexto de produção/prazo.
- Apenas quando existe relação explícita.
- Brief §11: “Job On: contexto de produção, apenas quando existe relação explícita”.

Evidência técnica:

- A Reparação Interna sim é aberta a partir do Job On, “Ver reparações”.
- Na Reparação Externa não há lookup vivo do Job On documentado.
- Os snapshots imutáveis são o padrão histórico transversal.

Contexto derivado:

- O Job On pode exibir, read-only, o último reparador relevante da ferramenta/lote.
- Contexto derivado dos factos.
- Sem edição.
- Modelo aceite Ferramentas/Job On.
- Não é a Reparação Externa que alimenta isso diretamente.

---

## 12. Relação com Reparação Interna

Reparação Interna, módulo 34, vs Reparação Externa, módulo 35.

Diferenças funcionais concretas:

| Dimensão | Reparação Interna | Reparação Externa |
|---|---|---|
| Objetivo | Intervenções internas durante a produção, registos rápidos de turno | Ciclos de envio a reparadores externos preparados com antecedência |
| Atores | Reparador de turno, utilizador autenticado | Responsável, dono do módulo, decisão D1; operador de Armazém confirma física |
| Identidade do reparador | Sempre o utilizador autenticado; nunca selecionado manualmente | Diretório canónico; selecionado manualmente no batch, com default do último usado |
| Tipos | Apenas CM e MF; BQ nunca reparação interna, regra fechada | Apenas CM e MF, como tipos separados; BQ fora do módulo, decisão D2 |
| Unidade operacional | Registo individual, tipo + nº individual, com contexto de produção opcional | Batch/lista de itens, peças individuais, para uma produção futura |
| Movimento físico | Sem movimentos de Armazém; intervenção em produção | Movimento físico sim, via porto do Armazém, recolha/retorno |
| Edição | — | Batch sempre editável pelo Responsável, em qualquer fase, decisão D3 |
| Validações | Sem bloqueios operacionais | Validações de consistência: duplicado; retorno sem saída; mínimo 1 item; tipos fora de âmbito; o estado nunca bloqueia edição |
| Fronteira registada | Aggregate `InternalRepairRecord` | Aggregate `RepairExit`; entidades e raízes distintas, evidência técnica |

---

## 13. Relação com Boquilhas

- BOQUILHAS = fluxo de movimentos de reparação externa de BQ.
- Módulo de topo separado, `31_*`.
- Modelo aprovado.
- Inclui:
  - saída/retorno;
  - quantidades;
  - saldos;
  - reparador;
  - histórico;
  - discrepância 20→25 não-bloqueante;
  - snapshot de fecho.

Fronteira:

- A Reparação Externa NÃO possui nem executa o fluxo BQ.
- O BQ não participa na gestão de batches desta autoridade.
- Não há tab BQ aqui.
- O BQ nunca é misturado com CM/MF.

Master e regras:

- O master do BQ, identidade, pertence às Ferramentas.
- As Boquilhas registam apenas os movimentos de reparação.
- Nunca reparação interna para BQ.
- Regra fechada: BQ NUNCA REPARAÇÃO INTERNA.

Não inferir participação BQ:

- Não inferir participação BQ em batches/listas CM/MF.
- A única menção de BQ em listas, design V1, é histórica / superseded.

---

## 14. Dados Introduzidos pelo Utilizador

Preparação do batch:

- tipo do batch:
  - CM;
  - MF;
- Referência;
- lote;
- número individual;
- posição atual, opcional;
- produção prevista, campo compacto, quando aplicável;
- reparador;
- data prevista, “Enviar até”.

Ações:

- editar o batch em qualquer fase;
- adicionar/remover itens e composição relevante a qualquer momento;
- não limitado a antes de criar/enviar;
- Criar lista;
- Disponibilizar a saída;
- confirmações de recolha;
- confirmações de retorno;
- observação opcional no retorno CM;
- fecho quando completo.

Definições:

- criar reparador;
- associar tipos:
  - CM;
  - MF;
- associar Linha/máquina permitida;
- estado ativo/inativo.

Histórico:

- filtros:
  - período;
  - tipo;
  - Referência;
  - lote;
  - reparador;
  - estado;
  - Linha/máquina;
  - operador.

---

## 15. Dados Derivados e Automáticos

Apenas o que está documentado:

- Estado do batch:
  - derivado das confirmações persistidas;
  - nunca de abrir a página.

- Saída/Entrada + operadores + datas por item:
  - registados nas confirmações;
  - facultados pelo sistema;
  - ator autenticado;
  - brief: “Cada item preserva datas e operadores de saída/entrada”;
  - cabeçalho “criado por/data”.

- Snapshot do reparador por envio:
  - preservado no momento do envio;
  - alterações futuras de associação não reescrevem o passado.

- Posição atual / estado / localização:
  - lidos dos domínios respetivos:
    - Ferramentas;
    - Armazém;
  - não inventados.

- Reparador predefinido sugerido:
  - último reparador conhecido;
  - com alteração manual.

- Movimentos físicos no Armazém:
  - criados pelo porto do Armazém;
  - no mesmo UoW;
  - quando há confirmações explícitas.

Fórmulas/quantidades:

- Não existem cálculos documentados.
- CM/MF trabalham por peça individual.
- Sem saldos específicos nesta autoridade.
- O conceito “saldo” é do fluxo BQ/Boquilhas.

Não está documentada qualquer automatização adicional:

- sem aprovações automáticas;
- sem correções automáticas;
- sem reconciliação automática.

---

## 16. Quantidades e Identidade das Peças

- A Reparação Externa, CM/MF, trabalha por peça individual identificada.
- Identidade:
  - tipo;
  - Referência;
  - lote;
  - número individual.
- Não há “quantidade” para CM/MF nos batches.
- Aceitação explícita:
  - “BQ usa quantidades; CM/MF usam números individuais”.
- O conceito de quantidades/saldo pertence ao fluxo BQ, Boquilhas.
- Fora desta autoridade.

---

## 17. Recolha e Retorno

Recolha, pickup, confirmação de retirada:

- Confirmação explícita de que o item saiu fisicamente da posição para o reparador externo.
- Executada item a item pelo Armazém, check.
- Efeitos:
  - posição liberta, quando todas confirmadas;
  - estado avança.
- Vai ao Armazém através do porto.
- No mesmo UoW.

Retorno, return:

- Confirmação explícita de que o item voltou e ocupou a posição indicada.
- Regista entrada e operador.
- Encerra o ciclo ao chegarem todos.
- Fecho item a item.
- Pode incluir observação, CM.
- A observação não altera dados mestres automaticamente.

Vocabulário técnico associado:

- disponibilizar → recolha → retorno.
- Evidência técnica confirma endpoints dedicados e regras:
  - recolha após retorno/ciclo fechado rejeitada;
  - retorno em lista cancelada rejeitado.

Regra funcional:

- Estas são as únicas ações documentadas que mudam estado de ciclo + estado físico.
- Por isso correm atómicas, um único UoW.
- Nenhum efeito físico é inferido.

---

## 18. Casos Parciais e Excecionais

Retorno parcial:

- Documentado como estado “Retorno parcial”.
- O progresso é mostrado explicitamente.
- Apenas fecha com todos os itens de volta.
- Aceitação: “retorno fecha o ciclo item a item”.

Item que não retorna / fecho com itens em aberto:

- As regras de “non-returning-close/destination” e outras GLM-RE-12 estão seguramente adiadas.
- Não há regra ativa documentada para encerrar com itens em falta.

Item duplicado:

- O mesmo item lógico, tipo + ferramenta/lote, não pode figurar duas vezes no mesmo batch/contexto de saída aberta.
- Duplicá-lo criaria dados duplicados.
- Prevenção de duplicidade de dados, identidade.
- Não é um bloqueio de workflow.
- SOT F: “duplicate-item-in-open-exit” é regra de aplicação/domínio.
- Dentro da mesma lista, o construtor evita/não persiste o duplicado.

Cancelamento:

- Funcionalmente adiado.
- O estado Cancelado é compatibilidade de schema.

Retorno sem saída correspondente:

- Registado/mostrado como inconsistência.
- Permitir correção.
- Aviso/alerta do brief §8.
- Não é um bloqueio duro do utilizador.
- Não se inventa um fluxo de aprovação/resolução.

Item sem localização conhecida:

- Aviso.
- Sem localização inventada.
- Não bloqueia.

Falha de persistência:

- Manter a seleção.
- Não mostrar sucesso.

Discrepância/reparador errado:

- Não há regra específica documentada para reparador diferente do planeado.
- A preservação histórica, snapshot, garante que o reparador efetivo fica registado.
- Mismatch não é especificada como bloqueio.

Sucesso/erros:

- “Não inferir transições apenas pela abertura da página”.
- Estados só mudam por confirmações.

Edição em qualquer fase:

- Os casos acima não limitam a edição do batch pelo Responsável.
- Nenhum estado ou situação parcial remove as opções de edição.

---

## 19. Validações de Consistência, Avisos e Limites de Âmbito

Regra do Owner:

- NUNCA BLOQUEAR OU REMOVER OPÇÕES DO UTILIZADOR;
- A MENOS QUE O OWNER ESTABELEÇA EXPLICITAMENTE UMA IMPOSSIBILIDADE DE NEGÓCIO GENUÍNA.

A Reparação Externa não é descrita como tendo “bloqueios operacionais” de workflow.

As validações abaixo protegem:

- identidade/integridade dos dados;
- coerência do ciclo;
- sem remover opções ao utilizador.

Explicitamente NÃO bloqueia / NÃO remove, decisão do Owner D3:

- o estado do batch nunca bloqueia a edição;
- a edição está sempre disponível ao Responsável, em qualquer fase;
- avançar o batch nunca remove ações de edição:
  - disponibilizar;
  - “A retirar”;
  - “Enviado”;
  - retorno parcial;
- sem bloqueios de aprovação inventados;
- sem estados congelados inventados;
- sem restrições de interface baseadas em estado inventadas;
- onde uma fonte antiga conflitua, por exemplo “adicionar/remover itens apenas antes de criar/enviar”, a decisão do Owner vence.

Validações de consistência / limites de âmbito preservados:

1. Duplicação de item.
   - Prevenção de duplicidade de dados, identidade.
   - O mesmo item lógico, tipo + ferramenta/lote, não pode figurar duas vezes no mesmo batch/contexto de saída aberta.
   - Duplicá-lo criaria dados duplicados.
   - “duplicate-item-in-open-exit” é regra de aplicação/domínio.
   - É validação de identidade/integridade.
   - Não é um bloqueio de workflow.
   - Dentro da mesma lista, o construtor evita/não persiste o duplicado.

2. Retorno sem saída correspondente.
   - Inconsistência registada para correção.
   - Não é um bloqueio duro do utilizador.
   - O sistema regista/mostra a inconsistência e permite a correção.
   - Não se inventa um fluxo de aprovação ou de resolução.

3. Mínimo de uma ferramenta.
   - Condição de criação do batch.
   - Um batch sem qualquer ferramenta não tem conteúdo significativo.
   - Criar exige ≥1 ferramenta individual adicionada.
   - É uma condição de criação do batch.
   - Não é um bloqueio de workflow/estado.
   - Não generalizar como filosofia de bloqueio.

4. BQ fora de âmbito.
   - O BQ não é um tipo de batch da Reparação Externa.
   - Simplesmente não aparece como opção.
   - Não é uma opção apresentada e depois rejeitada.
   - Pertence ao módulo Boquilhas.
   - Limite de âmbito que protege a fronteira de tipo.

5. Efeitos físicos só via porto do Armazém, no mesmo UoW.
   - Invariante de execução, integridade física.
   - Não é um bloqueio de utilizador.

Avisos que NÃO bloqueiam:

- Item sem localização conhecida → aviso.
- Sem inventar localização.
- Aplica-se o padrão global do sistema:
  - avisos não bloqueiam produção/operação salvo regra explícita.
- Aqui apenas o item sem localização é aviso documentado.

Sem aprovações documentadas:

- sem fluxos de aprovação;
- sem correções automáticas;
- NÃO INVENTAR esses mecanismos.

---

## 20. Histórico e Auditoria

- Um batch concluído não desaparece.
- Passa para o Histórico.

Campos mínimos do Histórico:

- Lista;
- Tipo;
- Referência;
- Lote;
- Qtd./N.º;
- Reparador;
- Saída;
- Operador saída;
- Entrada;
- Operador entrada;
- Estado.

Preserva:

- saída e entrada com datas e operadores;
- reparador efetivo, snapshot, por envio;
- estado.

Eventos de auditoria técnica:

- A reparação externa escreve registos de auditoria.
- Evidência técnica:
  - `reparacao_externa.lista.criar`;
  - `reparacao_externa.lista.item`;
  - `reparacao_externa.lista.disponibilizar`;
  - `reparacao_externa.item.recolhido`;
  - `reparacao_externa.item.retornado`;
  - `reparacao_externa.reparador.criar`;
  - `reparacao_externa.reparador.editar`;
  - `reparacao_externa.reparador.desativar`;
  - `reparacao_externa.linha.defeito`.
- A História lê transversalmente esses factos.
- Módulo leitura.

Histórico não reescrito:

- Alterações posteriores de associações de reparadores não modificam batches antigos.
- Snapshot preservado.
- A edição do batch pelo Responsável, decisão D3, aplica-se ao batch/ciclo corrente.
- Os factos históricos persistidos não são reescritos.

---

## 21. Documentos, Impressão e Outputs

Impressão da lista programada:

- Passo 3 do ciclo: “A lista fica disponível no Armazém e pode ser impressa”.
- A impressão é suporte V1.

PDF / etiqueta / exportação:

- NÃO ESPECIFICADO na autoridade funcional do módulo.
- Não documentado.

Classificação:

- impressão = suportada no brief, apoio;
- NÃO CONFIRMADA no design atual;
- PDF/etiquetas/exportação = NÃO PRESENTE/NÃO ESPECIFICADO.

---

## 22. Ownership

| Dado / conceito | Dono |
|---|---|
| Master CM/MF: identidade, referência, lotes, peças | Ferramentas |
| Estado físico / stock / posições / movimentos físicos | Armazém, dono único; efeitos só via porto |
| Movimento físico individual de reparação: saída, destino = reparação, reparador, via Armazém | Armazém, fluxo A do Operador, sem exigir batch |
| Batch/lista de reparação externa: plano, ciclo, itens, estado | Reparação Externa, Responsável |
| Diretório de reparadores / associações por tipo e linha, vocabulário canónico partilhado | Reparação, fonte canónica; vocabulário partilhado; reutilizado por Boquilhas e Armazém |
| Eventos/histórico do ciclo de reparação externa, incl. snapshot do reparador | Reparação Externa, factos; leitura transversal pela História |
| Fluxo de movimentos de reparação externa de BQ | Boquilhas, módulo de topo separado |
| Planeamento/produção, contexto de produção futura | Job On, contexto; relação explícita opcional |
| História transversal / auditoria | História, leitura; factos persistidos pelos módulos originais |

---

## 23. Regras Negativas

A Reparação Externa NÃO:

- NÃO escreve tabelas do Armazém.
  - Nem stock.
  - Nem movimentos físicos.

- NÃO infere efeitos físicos.
  - Só confirmações explícitas movem ferramentas.

- NÃO altera o master das Ferramentas.
- NÃO cria cópias divergentes das ferramentas.
- A saída referencia IDs estáveis.

- NÃO parte da ferramenta atualmente em produção.
  - Essa associação é exclusiva da Reparação Interna.

- NÃO carrega a produção atualmente ativa.

- NÃO mistura CM e MF em batches/domínio.
  - Tipos sempre separados.

- NÃO bloqueia nem remove a edição por estado.
  - O batch é sempre editável pelo Responsável, em qualquer fase.
  - Sem estados congelados.
  - Sem locks de aprovação.
  - Sem restrições de UI por estado.

- NÃO aplica bloqueios operacionais de workflow.
  - O módulo protege identidade/integridade dos dados.
  - Duplicados: prevenção de duplicidade.
  - Mostra inconsistências para correção, retorno sem saída.
  - Exige conteúdo mínimo na criação, ≥1 ferramenta.
  - Não bloqueia nem remove opções sem impossibilidade de negócio genuína e explícita do Owner.

- NÃO implica Reparação Interna.
  - Módulos e raízes distintas.

- NÃO duplica os movimentos do Armazém.
  - Acompanha sem duplicar.

- O cancelamento NÃO está funcional.
  - `CancelarLista` adiado.
  - Estado Cancelado só compatibilidade.

- O BQ NÃO participa na Reparação Externa.
  - Módulo de topo Boquilhas.
  - Sem tab BQ.
  - Nunca misturado com CM/MF.
  - BQ simplesmente não aparece como tipo de batch.

- O histórico NÃO é reescrito por alterações de configuração/reparadores.
  - Snapshots.

- NÃO inventa localização/estado/reparador quando desconhecidos.
  - Aviso.
  - Sem invenção.

- O Job On NÃO cria nem seleciona na Reparação Externa.
  - Relação só de contexto explícito.

---

## 24. Atual vs Histórico, Superseded e Deferred

### CURRENT

Regra funcional atual, decisões do Owner D1/D2/D3:

- módulo de topo `reparacao_externa`, para o Responsável;
- gestão de batches CM/MF de reparação externa:
  - preparação;
  - composição;
  - reparador;
  - despacho;
  - acompanhamento;
  - retorno;
  - conclusão;
  - histórico;
- áreas internas:
  - Registo;
  - Ferramentas;
  - Histórico;
  - Definições;
- CM e MF selecionados separadamente:
  - tipos/fluxos separados;
  - nunca misturados;
- batches sempre editáveis pelo Responsável, em qualquer fase;
- o estado nunca bloqueia nem remove a edição;
- o Armazém é dono dos movimentos físicos:
  - efeitos só via porto;
  - mesmo UoW;
  - confirmações físicas do Armazém;
- o Operador pode enviar ferramentas individuais para reparação através do Armazém:
  - saída individual;
  - destino = reparação;
  - reparador associado;
  - fluxo A, independente de batches;
- estados:
  - Preparação;
  - A retirar;
  - Enviado;
  - Retorno parcial;
  - Concluído;
- histórico com snapshots;
- definições de reparadores:
  - diretório canónico;
  - tipos;
  - linha/máquina;
  - ativo/inativo;
- validações de consistência/limites de âmbito preservados.

### DEFERRED

Decidido adiar — não é erro:

- `CancelarLista`, SOT E;
- fecho com itens em aberto / destino / outras GLM-RE-12, SOT G.

### SUPERSEDED

Histórico, decisões do Owner D1/D2/D3:

- navegação combinada “Reparação” com menu Boquilhas/Moldes do mockup v2;
- DO NOT IMPLEMENT, ficheiro `99_DO_NOT_IMPLEMENT`;
- a página intermédia obrigatória “Reparação”;
- já proibida no brief V1;
- a composição:
  - Boquilhas;
  - Contra moldes;
  - Moldes finais;
  - Envios;
  - Histórico;
  - Definições;
- como conjunto de áreas do módulo;
- incluindo “tabs globais”;
- BQ dentro da Reparação Externa;
- qualquer interpretação de que o Operador gere batches da Reparação Externa;
- qualquer regra que remova a edição do batch apenas porque o estado avançou;
- inclui a redação antiga “adicionar/remover itens só antes de criar/enviar”.

### NOT SPECIFIED

Itens preservados como NÃO ESPECIFICADOS:

- PDF / etiqueta / exportação.
- Eliminação de reparadores.
- Superfície atual de impressão/PDF no design atual.
- Regra específica para mismatch de reparador errado como bloqueio.
- Detalhe adicional de transições de estado além do ciclo documentado.

---

## 25. Questões Owner

### 25.1 Questões fechadas

As questões anteriores Q1, Q2 e Q3 foram FECHADAS pelas decisões do Owner D1/D2/D3 nesta revisão.

Q1 — variante Operador vs Responsável:

- Fechada por D1.
- A Reparação Externa é do Responsável.
- O Operador não gere batches na Reparação Externa.
- O Operador usa o Armazém para movimentos físicos individuais de reparação.

Q2 — conjunto de áreas internas:

- Fechada por D2.
- Áreas internas:
  - Registo;
  - Ferramentas;
  - Histórico;
  - Definições.
- CM/MF como tipos separados.
- BQ fora do módulo.
- “Envios” é o ciclo de vida funcional do batch, não um tab novo.

Q3 — edição após disponibilização/envio:

- Fechada por D3.
- O batch é sempre editável pelo Responsável, em qualquer fase.
- O estado nunca bloqueia nem remove a edição.
- A decisão do Owner vence sobre qualquer redação anterior.

### 25.2 Questões abertas

NO OPEN OWNER QUESTIONS.

---

## 26. Resumo Funcional Final

A Reparação Externa é o módulo de gestão de batches de CM/MF do Responsável.

O Responsável:

- prepara um batch de reparação externa;
- escolhe as ferramentas CM ou MF desse batch;
- tipos sempre separados;
- nunca misturados num batch;
- associa o reparador do diretório canónico;
- filtrado por tipo e Linha/máquina;
- define a data prevista;
- despacha.

O batch é sempre editável pelo Responsável, em qualquer fase do ciclo.

- O estado nunca bloqueia nem remove a edição.

O batch fica disponível no Armazém.

- O operador confirma cada retirada e cada retorno.
- Efeitos físicos sempre através do porto do Armazém.
- No mesmo unit of work.
- Quando todos os itens voltam, o ciclo fecha item a item.
- Estado “Concluído”.
- Retornos parciais são estado explícito.

Cada envio preserva o snapshot do reparador.

O histórico guarda:

- saída/entrada;
- operadores;
- datas;
- estado.

O histórico não é reescrito.

Distinção central:

- gestão de batches:
  - preparar;
  - compor;
  - reparador;
  - despachar;
  - acompanhar;
  - retornar;
  - concluir;
  - historiar;
  = REPARAÇÃO EXTERNA, Responsável.

- movimento físico individual de reparação:
  - saída de uma ferramenta individual;
  - destino = reparação;
  - associação do reparador;
  = ARMAZÉM, Operador;
  - sem exigir um batch da Reparação Externa.

Estrutura atual:

- Registo;
- Ferramentas;
- Histórico;
- Definições.

Com CM e MF como seleções de tipo separadas.

“Envios” é o ciclo de vida do batch:

- programado;
- despacho;
- recolha;
- retorno;
- conclusão.

Não é um tab independente.

Validações de consistência:

- o mesmo item não pode figurar duas vezes no mesmo batch;
- prevenção de duplicação de dados;
- retorno sem saída correspondente é registado/mostrado como inconsistência para correção;
- criar batch exige ≥1 ferramenta;
- condição de criação;
- um batch sem ferramentas não tem conteúdo;
- BQ está fora de âmbito;
- simplesmente não aparece como tipo de batch;
- módulo Boquilhas.

BQ não participa na Reparação Externa:

- o fluxo de reparação externa de BQ é o módulo de topo Boquilhas.

Outras fronteiras:

- o master CM/MF pertence às Ferramentas;
- o planeamento é contexto do Job On;
- a Reparação Interna é o fluxo paralelo de turno;
- ator autenticado;
- sem movimento de Armazém.

Cancelamento e fecho com itens em aberto estão adiados.

NO OPEN OWNER QUESTIONS.

As Q1–Q3 foram fechadas pelas decisões do Owner D1–D3.

## Implementation Pointers

### Relevant implementation areas

- Web / Razor: canonical module `reparacao_externa`; single page id `reparacao_externa.listas`; route `/reparacao-externa`.
- Application: batch flow (preparação → criação → reparador → disponibilização → retirada → envio → acompanhamento → retorno parcial/completo → conclusão); states `Preparacao` / `ARetirar` / `Enviado` / `Retorno parcial` / `Concluído`; `Cancelado` = schema compatibility only; `CancelarLista` deferred.
- Infrastructure: Armazém movement port `IArmazemRepairMovementPort` (same UoW; physical effects only through the port, never inferred); lookups `IFerramentasPieceLookup`, `IToolPieceResolver`; aggregates `RepairExit` (externa) vs `InternalRepairRecord` (interna); `repair_events` with scope interna/externa.
- Database: repairer directory `repairers/line_repairer_defaults`, `repairer_repair_types`, `line_defaults`; physical state tables `warehouse_stock` / `warehouse_movements` (owned by Armazém); migrations/tables N08/N20/N25 (implementation evidence — verify names); audit events `reparacao_externa.lista.criar|item|disponibilizar`, `reparacao_externa.item.recolhido|retornado`, `reparacao_externa.reparador.criar|editar|desativar`, `reparacao_externa.linha.defeito`.
- Technical map: `maps\12_REPARACAO_EXTERNA.md` (verify freshness before use).

### Known implementation gaps (verified in this document set)

- Print/PDF: list printing documented in brief V1 (support) but NOT confirmed in the current design; the technical map does not identify a print/PDF surface for this module — verify before implementing outputs (§21).
- Batch cancellation (`CancelarLista`) and close-with-open-items rules are functionally deferred (SOT E/G) — do not implement as active behavior (§24 DEFERRED).

### Design reference

- `AI-CONTEXT\design-coder\35_REPARACAO_EXTERNA_01_VISUAL_AUTHORITY_moldes.html` (file named "moldes").
- LEGACY — do not implement: `reparacao-v2.html` (combined "Reparação" navigation — historical / DO NOT IMPLEMENT).

### Cross-module dependencies

- Armazém (sole owner of physical state; individual repair movements via the port; Operador flow A); Ferramentas (CM/MF master identity, read-only); Job On (explicit optional relation — production context/date, not automatic); Reparação Interna (sibling module, separate roots); Boquilhas (BQ external-repair flow separate, never in RE batches).