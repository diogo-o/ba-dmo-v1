# BOQUILHAS — MODELO FUNCIONAL

OPEN OWNER QUESTIONS: NONE

SCOPE: Boquilhas existe apenas para registar os movimentos relacionados com a **reparação externa de BQ**.

## Índice

1. Objetivo
2. Âmbito Funcional
3. Posição na Aplicação
4. Utilizadores / Perfis / Acesso
5. Estrutura Interna
6. Modelo Funcional Central
7. Identificação BQ / Lote
8. Seleção / Criação
9. Fluxo de Reparação
10. Movimentos Documentados
11. Quantidades / Saldos
12. Reparadores
13. Máquina / Linha
14. Utilização %
15. Data de Abertura
16. Registos Existentes
17. Avisos / Comportamentos Documentados
18. Decisões / Confirmações
19. Histórico / Auditoria
20. Outputs
21. Ownership
22. Relações com Outros Módulos
23. Casos Especiais
24. Regras Negativas
25. Regras Superseded / Refined
26. Questões Funcionais em Aberto
27. Resumo Funcional Final

## 1. Objetivo

Em palavras simples: Boquilhas é o módulo onde a fábrica regista, diariamente e a alta frequência, o fluxo de movimentos de reparação externa das boquilhas (BQ): quais os lotes de BQ que saem para reparação, com que reparador, e quais os que voltam, com as quantidades envolvidas e o histórico desses movimentos.

**Que problema operacional resolve:** nas linhas de produção, as boquilhas desgastam-se e têm de ser enviadas para reparação externa e recebidas de volta de forma contínua. Sem um registo dos movimentos de reparação por quantidade é impossível saber quantas BQ de um lote saíram para reparação, quantas voltaram e se o retorno veio a mais do que o esperado. Boquilhas resolve exatamente isso: o ciclo de envio/retorno de reparação da BQ, em alta frequência.

**O que é rastreado (apenas relacionado com a reparação):**
* O lote de BQ envolvido no movimento de reparação (referência + lote);
* A saída para reparação (lote enviado);
* O reparador selecionado/associado;
* A associação reparador ↔ máquina/linha quando aplicável;
* O retorno/entrada de reparação, com reconciliação (incluindo retorno a mais);
* As quantidades envolvidas nesses movimentos de reparação;
* O histórico desses movimentos de reparação por lote e de forma transversal.

**Natureza do módulo — registo operacional MANUAL:** Boquilhas é fundamentalmente um registo operacional manual. O operador introduz manualmente o movimento; a aplicação guarda a informação no servidor; o movimento permanece no histórico; a aplicação apresenta o saldo/diferença resultante. O módulo não decide operacionalmente por si — regista e mostra o que aconteceu.

**Porquê a quantidade/histórico dos movimentos de reparação importam:** porque o envio/retorno de reparação é de alta frequência e por quantidade. É preciso lançar um envio para reparação, receber de volta, e perceber quando voltou mais do que o esperado (discrepância) sem nunca bloquear a operação nem inventar quantidades.

## 2. Âmbito Funcional

O Owner fechou o âmbito funcional de Boquilhas:
* **FERRAMENTAS** = BQ tool/master, identidade, lote, estado técnico e dados normais de ferramenta.
* **ARMAZÉM** = localização física geral e movimentos normais de armazém.
* **BOQUILHAS** = APENAS o fluxo de movimentos de reparação externa de BQ (diário/contínuo): saída para reparação; lote de BQ enviado; reparador selecionado/associado; associação reparador ↔ máquina/linha quando aplicável; retorno/entrada de reparação; quantidades envolvidas nesses movimentos de reparação; histórico desses movimentos de reparação.

**Porquê o módulo existe de forma separada:** o fluxo de reparação de BQ é diário e contínuo.

**Regra de paragem (não alargar):** Boquilhas não trata de BQ como ferramenta geral (isso é Ferramentas), não trata da localização física geral de BQ (isso é Armazém), não gere o ciclo de vida geral da BQ nem o master de BQ, e não gere movimentos gerais de armazém. BQ comporta-se como CM/MF no modelo normal de ferramenta/master/armazém; a única diferença funcional relevante é o fluxo de reparação.

**Fechado:** `BQ NUNCA REPARAÇÃO INTERNA`.

Não está no âmbito de Boquilhas: a gestão do master de BQ (Ferramentas), a localização/movimento físico geral de BQ (Armazém), nem qualquer ciclo de vida genérico da ferramenta BQ fora do fluxo de reparação.

## 3. Posição na Aplicação

* Boquilhas é um módulo funcional de topo, atribuível individualmente a cada utilizador.
* Quando atribuído, aparece na navegação e pode ser aberto; quando não atribuído, não aparece e não está funcionalmente acessível.
* Os operadores arrancam no Job On (landing operacional) e entram em Boquilhas quando precisam de registar os movimentos de reparação externa de BQ (saída/retorno).
* Boquilhas é o módulo onde se trabalha o fluxo de reparação externa de BQ. O trabalho genérico de BQ como ferramenta (master, localização física) pertence a Ferramentas/Armazém.
* Dentro de Boquilhas há áreas internas (tabs): Registo, Boquilhas, Histórico e Definições, mais um painel lateral fixo de linhas de produção. Nenhuma destas é um módulo separado.

## 4. Utilizadores / Perfis / Acesso

O modelo global tem exatamente três perfis: Admin, Operador / Controlador e Responsável. Não existe quarto perfil.

* **Acesso:** O acesso a Boquilhas é controlado por atribuição individual do módulo ao utilizador, definida no Admin. Sem o módulo → sem acesso ao módulo (não aparece, não funciona).
* **Admin:** O Admin não é um perfil operacional de Boquilhas. Não se inventam ações operacionais para o Admin. O Admin pode atribuir/gerir o módulo (configuração de acesso), mas não opera Boquilhas como tal.
* **Operador / Controlador e Responsável (Q1 fechada — OWNER-CONFIRMED):** Comportamento funcional SEM DIFERENÇA. Em Boquilhas, `OPERADOR / CONTROLADOR = RESPONSÁVEL` quanto às ações operacionais dentro do módulo: se qualquer um dos perfis tiver Boquilhas atribuído, ambos têm exatamente as mesmas ações de Boquilhas.
  * Não existe variante Operador específica de Boquilhas; não existe variante Responsável específica de Boquilhas; não existe workflow de aprovação/revisão apenas por o utilizador ser Responsável; Responsável NÃO é "Operador + ações extra" neste módulo; nenhuma ação funcional deve ficar escondida do Operador mas exposta ao Responsável; o perfil não altera a experiência operacional de Boquilhas.
  * O único gate é: `BOQUILHAS MODULE ASSIGNED?` Se SIM e o perfil for Operador / Controlador ou Responsável → mesmas operações funcionais de Boquilhas.
  * A manutenção de registos BQ/Lote já existentes no Armazém pelo Responsável é uma regra do módulo Armazém (Q4 — ver secções 16 e 21) e não cria qualquer distinção de perfil dentro de Boquilhas.

**Conclusão por perfil:** V1 funcional: módulo único atribuível; divisão Operador/Responsável NÃO EXISTE em Boquilhas (OWNER-CONFIRMED — Q1); perfil não altera as operações. A implementação atual comporta-se sem divisão por perfil — consistente com a regra funcional confirmada pelo Owner.

## 5. Estrutura Interna

| Área | O que o utilizador faz ali | Classificação |
| --- | --- | --- |
| **Registo** | Pesquisa um lote de BQ (por referência/BQ, lote e/ou máquina/linha); se existir, seleciona e abre o fluxo de reparação; se não existir, cria a identificação em falta (referência + lote + máquina(s)/linha(s) registadas) e continua de imediato para o registo; regista os movimentos de reparação (Saída para reparação, Entrada/retorno de reparação, Não reparadas, Corrigir contagem, Editar ficheiro — correção do registo do fluxo de reparação, Fechar); vê o estado atual e os movimentos de reparação desse lote (lista paginada com tipo, quantidade, saldo, data e operador). | Área interna (tab) |
| **Boquilhas** | Vista de grelha/cartões de lotes, com filtros (referência/lote/linha, estado, linhas por página). Estados documentados no design: atuais, ativas, em produção, em reparação, disponível, arquivados, sucata e todos (classificação na secção 25: "arquivados" mantém-se como resultado do fecho do registo de reparação; "sucata" como lifecycle genérico da ferramenta é design superseded). Os cartões mostram quantidade, linha/localização/reparador e percentagem de vida utilizada; a percentagem representa tempo de vida/desgaste, não quantidade; nunca como barra de progresso. Os totais `Na fábrica`, `Em reparação` e `Em produção` não pertencem a esta página — passam para o Histórico. | Área interna (tab) |
| **Histórico** | Vista transversal/agregada dos movimentos de reparação: calendário à esquerda, cartões de resumo à direita, filtros em largura total e tabela de movimentos em largura total (colunas: Referência, Lote, Movimento, Quantidade, Saldo, Reparador, Linha, Data e hora, Operador — sem colunas Detalhe nem Ficheiro). Uma linha selecionada ativa `Corrigir movimento` e `Eliminar movimento`; duplo clique abre o lote no Registo. Serve para pesquisar, agregar e comparar entre referências, lotes, linhas, reparadores e períodos. | Área interna (tab) |
| **Definições** | Configuração de reparadores: tabela compacta com uma linha por B1–C3 (reparador predefinido e reparadores permitidos por linha); o predefinido é sugerido ao criar uma saída, mas pode ser alterado no movimento; `Sem associação` é permitido e visível; secção separada com a lista de reparadores (desativar, não eliminar — preserva histórico); se o predefinido for desativado, a linha exige nova associação | Área interna / aba isolada (configuração) |
| **Linhas de produção (painel lateral)** | Leitura rápida das linhas B1–C3 e do que cada linha está a produzir (cartão com linha, referência, lote, quantidade e hora de início quando aplicável); clicar na referência de produção completa abre o Job On ativo associado àquela referência/linha; clicar no restante cartão abre o lote no Registo; menu `…` com `Substituir`/`Remover` (linha ocupada) e `Adicionar` (linha livre); alerta de conflito quando há referências diferentes na mesma linha; ligação ao Job On | Área interna fixa (contexto) |

* Tab Fabrico → não existe (regra explícita).
* O Registo mostra o lote selecionado e os seus movimentos de reparação; o Histórico é a vista geral. Ambas usam a mesma fonte de dados; muda apenas o âmbito da consulta. Nenhuma área interna é um módulo de topo.
* Nota de âmbito: as tabs/estruturas de apoio (lote, linhas, config de reparadores) existem para suportar o fluxo de movimentos de reparação externa de BQ; não implicam que Boquilhas possua o master de BQ nem a localização física geral de BQ.

## 6. Modelo Funcional Central

O ciclo operacional do fluxo de reparação de BQ, em palavras:
1. O utilizador entra em Boquilhas. Vê o painel lateral com as linhas B1–C3 e, quando relevante, o contexto de produção de cada linha (lido do Job On).
2. Seleciona ou cria o lote de BQ que vai a reparação — comportamento `EXISTE → SELECIONA` / `NÃO EXISTE → CRIA`. A pesquisa encontra o lote por referência/BQ, lote e/ou máquina/linha. Se o lote existir, é selecionado e aberto; se não existir, a identificação em falta é criada (referência + lote + máquina(s)/linha(s)) e o utilizador continua de imediato para o registo. A criação em falta não é bloqueada por o master completo de Ferramentas ainda não estar preenchido, e não transfere a posse do master (Ferramentas continua dono).
3. Regista os movimentos de reparação que vão acontecendo: Saída (envia o lote de BQ para reparação, indicando o reparador), Entrada (recebe de volta, com reconciliação), Não reparadas (declara irrecuperável), Corrigir contagem (acerta a quantidade), Fechar (encerra o registo).
4. Cada movimento de reparação altera o saldo de reparação e fica guardado no histórico para sempre.
5. Quando um retorno de reparação é maior do que o esperado, o utilizador recebe um aviso e o sistema não bloqueia: regista o retorno completo e abre uma discrepância (entrada excecional) para ser tratada depois.
6. Quando o fluxo de reparação do lote termina, o utilizador fecha; o sistema guarda um snapshot final imutável do resumo/estado e o lote passa para o histórico/arquivados.

**Princípio operacional — registo manual (preservado):**
* Boquilhas é um registo manual da operação: o operador introduz manualmente cada movimento; a aplicação guarda a informação no servidor; o movimento permanece no histórico; a aplicação apresenta o saldo/diferença resultante.
* O módulo não toma decisões operacionais automáticas: não bloqueia, não aprova, não corrige automaticamente, não esconde nem reescreve o que foi registado.
* Tudo o que é registado fica visível no lote e no Histórico — incluindo o saldo/diferença de cada movimento (ver secção 11).

**Em todo o tempo:**
* O saldo de reparação é calculado a partir dos movimentos de reparação (não é introduzido à mão).
* Os movimentos históricos não são reescritos; correções criam registos novos.
* O módulo emite confirmações/histórico para o utilizador e para o sistema de História transversal.
* Nenhuma ação de Boquilhas altera a produção do Job On, a localização física do Armazém, nem o master de Ferramentas.

## 7. Identificação BQ / Lote

* **Identidade do lote BQ** = `REFERENCE + LOT` (ex.: `T173` · `Lote 5`).
* **`MACHINE/LINE(S)`** = contexto operacional registado, obrigatório (≥ 1), MULTI-VALOR (B1–C3; seleção múltipla; `B1 + C3` é válido).
* **NÃO existe identidade composta** `REFERENCE + LOT + MACHINE`.
* `T173 · Lot 5 · [B1, C3]` = UM lote BQ registado para trabalhar em B1 e C3 — não são dois lotes, nem dois registos master, nem duas identidades.
* **Referência:** formato `^[A-Z][0-9]{3}$` (ex.: T173). (1 letra + 3 dígitos).
* **Lote:** formato livre compacto; parte da identidade do lote.
* Cada lote BQ deve ter o seu contexto de máquina/linha válido registado (regra de qualidade de dados funcional).

## 8. Seleção / Criação

* **`EXISTE → SELECIONA`** — pesquisa por referência/BQ, lote e/ou máquina/linha registada; se existir, seleciona e abre o fluxo de reparação. A pesquisa não se reduz a apenas referência nem a apenas lote (o contexto de máquina/linha registado no lote pode servir de filtro). Sem resultado: `Nenhuma boquilha encontrada`.
* **`NÃO EXISTE → CRIA → CONTINUA`** — se não existir, o utilizador cria a identificação em falta e continua de imediato para o registo do fluxo de reparação. A criação é inline, na própria página Registo (não abre nova página nem modal); o botão muda de `Criar novo lote` para `Fechar criação` enquanto o painel estiver aberto.
* A ausência de um master completo de Ferramentas não bloqueia o trabalho diário; não existe onboarding, wizard de migração, workflow de reconciliação, aprovação separada nem pré-requisito de importação de master.
* Criar a identificação em falta NÃO transfere a posse do master — Ferramentas permanece dono master.
* **Campos de criação (ordem documentada no design):** 1) Boquilha/Referência (obrigatória) → 2) Lote (obrigatório, compacto) → 3) Máquina(s)/Linha(s) (escolha múltipla B1–C3, pelo menos uma obrigatória; `B1 + C3` válido) → 4) Total do lote (quantidade inicial do registo do fluxo de reparação — não é o stock físico do Armazém) → 5) Utilização inicial (`%`, valor sempre manual) → 6) Data de abertura (campo DATE editável, default hoje) → 7) Observações compactas. Não incluir: `Linha associada`; escolha `Fabricar/Reparar`.
* Depois de criar: fechar o formulário, selecionar o lote e continuar imediatamente para o seu registo (`CREATE → CONTINUE`). Se houver conteúdo introduzido, confirmar antes de o descartar ao fechar/cancelar.
* **`Fechar criação`** (fechar/cancelar o painel de criação) **NÃO é** **`Fechar registo de reparação`** (fechar o trace do fluxo de reparação) — conceitos distintos.
* **Limite (não virar editor master genérico):** a criação quando em falta é validada e limitada à identificação necessária para continuar o fluxo de reparação. Não expor manutenção master genérica em Boquilhas.

## 9. Fluxo de Reparação

* **Abrir/selecionar lote** — ou criar quando em falta.
* **Saída para reparação (envio):** quantidade enviada; reparador; sai de "disponível" → "em reparação".
* **Entrada/retorno de reparação:** quantidade que voltou; reconciliação com o que estava "em reparação".
* **Não reparadas:** quantidade declarada irreparável.
* **Corrigir contagem:** delta de correção; correção é novo movimento (o original permanece no histórico).
* **Discrepância / excesso (regra 20→25 — mecanismo separado do saldo negativo, ver secção 11):** retorno a mais que o esperado é aceite na íntegra; a parte esperada volta a "disponível"; o excesso fica como entrada excecional com discrepância aberta (aviso); nunca bloqueia; nunca soma automaticamente o excesso à quantidade normal; histórico original preservado; a resolução da discrepância — quando o utilizador a fizer — é registada com nota obrigatória, sem reescrever movimentos (a exigência de nota aplica-se apenas no momento da resolução; nunca bloqueia o retorno nem a operação, nem é necessária para registar/guardar a Entrada).
* **Editar ficheiro:** em Boquilhas limita-se à correção do registo do fluxo de reparação (notas/movimentos) — não é uma edição master genérica (referência/lote/linhas permitidas como master); a manutenção de um registo BQ/Lote já existente é feita a partir do Armazém pelo Responsável (Q4).
* **Fechar registo de reparação (trace):** guarda snapshot final imutável (resumo, estado atual, metadados de fecho, data/hora e utilizador que fechou); retira o trace das listas ativas → Histórico/arquivados; os movimentos originais permanecem ligados ao lote; alterações futuras de reparadores, linhas ou configurações não modificam o snapshot fechado; fecho falhado = trace permanece ativo, sem snapshot parcial apresentado como válido.
* **Reabrir (trace):** apenas o último registo fechado e apenas se não houver outro trace ativo para o mesmo lote; a reabertura fica registada no histórico.

**Resumo do lote ativo (preservado):** ao selecionar um lote ativo, o Registo apresenta três blocos:
1. Resumo de abertura/configuração: referência, lote, estado, total do lote, data de entrada/abertura, número de registos e linhas permitidas.
2. Estado atual calculado: na produção, em reparação, não reparadas, saídas excecionais, entradas excecionais e linha atual.
3. Movimentos do lote: lista completa e paginada com tipo, quantidade, **saldo**, data e operador, com acesso a impressão/PDF (ver secção 20).

O resumo de abertura e o estado atual não são totais independentes introduzidos manualmente: resultam do lote e dos seus movimentos. Na lista de movimentos, distinção cromática subtil — `Saída`: fundo laranja muito claro e rótulo laranja; `Entrada`: fundo verde muito claro e rótulo verde — sem depender apenas da cor: o texto `Entrada`/`Saída` está sempre presente. Não apresentar o indicador `Contagem reconciliada`.

## 10. Movimentos Documentados

| Movimento | Efeito no balanço / Descrição |
| --- | --- |
| **Início** (abrir registo do lote) | aumenta "disponível" pela quantidade inicial |
| **Saída** (envio para reparação) | tira de "disponível" e põe em "em reparação" |
| **Entrada** (retorno de reparação) | devolve de "em reparação" para "disponível" (até ao esperado); o excesso vai para "entrada excecional" |
| **Não reparadas** | tira de "em reparação" e põe em "não reparadas" |
| **Registo/contexto de linha** (mudança de linha do repair trace) | sem efeito nas quantidades |
| **Corrigir contagem** | ajusta "disponível" pela variação (nunca negativo) |
| **Fecho** | marcador de fim, sem efeito nas quantidades |

**Formulário Entrada/Saída (comportamento preservado):**
* Modal compacto; cabeçalho em destaque com o tipo de movimento e `Referência · Lote · Linha`; sem campo `Material/trabalho`.
* Primeira linha: **Data**, **Quantidade**, **Motivo**; segunda linha: **Detalhe** e **Observações** (Observações começa com uma linha e pode crescer).
* Motivos: `Movimento normal`, `Movimento anterior não registado`, `Correção operacional`, `Outro`.
* O placeholder de Detalhe depende do Motivo: Normal → `Opcional`; Movimento anterior → `Ex.: saída de 12 BQ em 12/08`; Correção → `Ex.: quantidade registada incorretamente`; Outro → `Indique brevemente a razão`.
* O aviso de correspondência fica escondido em `Movimento normal` e aparece nos restantes motivos relevantes (ver secção 17).
* Este formulário é a superfície de introdução manual do movimento: aquilo que o operador guarda fica registado no servidor, permanece no histórico e é apresentado no lote e no Histórico com o saldo/diferença resultante (ver secção 11).

## 11. Quantidades / Saldos

* **O que a quantidade representa:** O número de boquilhas (BQ) de um lote envolvidas no movimento de reparação (ou a variação sobre ele). Nunca representa "tempo de vida". A quantidade é introduzida em cada movimento (exceto no registo/contexto de linha do repair trace), e o total inicial no momento de abrir o registo do lote.
* **O que o saldo/balanço significa:** O resultado dos movimentos de reparação, calculado pelo sistema — nunca introduzido por mão.
  * **Disponível** — quantas boquilhas do lote estão disponíveis (não saíram para reparação / já voltaram).
  * **Em reparação** — quantas saíram e ainda não voltaram.
  * **Não reparadas** — quantas foram declaradas irreparáveis.
  * **Entrada excecional** — quantas voltaram a mais do que o esperado (registo separado).
* `Linha atual` NÃO é um componente de saldo/quantidade. O saldo é apenas de quantidades.
* **`Total` / quantidade inicial do Registo:** É a quantidade inicial do registo do fluxo de reparação (movimento "Início" do trace). NÃO é a "verdade física total" do stock do Armazém, nem é o inventário físico. Uma operação de Boquilhas não move stock físico nem muda a verdade física do Armazém.
* **Inconsistências / excessos:** Voltou mais do que o esperado? Aceite na íntegra (nunca bloqueado), com aviso e discrepância.

### 11.1 Registo manual Entrada/Saída e coluna `Saldo` — comportamento preservado (nunca superseded)

* Boquilhas é um registo operacional **manual** de movimentos: o operador introduz manualmente o movimento (através do formulário Entrada/Saída — secção 10); a aplicação guarda a informação no servidor; o movimento permanece no histórico; a aplicação apresenta o **saldo/diferença** resultante.
* A lista de movimentos do lote (Registo) e a tabela do Histórico apresentam uma coluna **`Saldo`** por movimento — comportamento pré-existente do design Boquilhas, nunca discutido nem alterado pelo Owner, portanto **PRESERVADO**.
* O `Saldo` é a diferença resultante dos movimentos de reparação (o design documenta "saldo de movimentos" = **saídas menos entradas**), apresentada pelo sistema a partir dos movimentos registados — nunca introduzida à mão.
* **Exemplo documentado:** SAÍDA = 10; ENTRADA = 15 → na linha da ENTRADA correspondente, **SALDO = -5**.
* **O valor negativo é apresentado a VERMELHO** — é a representação visual/histórica da diferença na coluna `Saldo`.
* **Semântica do saldo negativo:** significa que o retorno superou a saída nesse movimento (voltou mais do que saiu). Nessa situação o sistema:
  * regista o movimento na mesma (a ENTRADA é registada por inteiro);
  * preserva a quantidade real introduzida;
  * preserva no histórico o que aconteceu;
  * mostra o saldo negativo na coluna `Saldo`;
  * destaca visualmente o saldo negativo a vermelho.
* **O saldo negativo NÃO é** (não reinterpretar): um bloqueio; uma rejeição; um workflow obrigatório de resolução de discrepância; uma correção automática; uma exceção de permissão; uma aprovação; um processo de reconciliação oculto.
* **O objetivo é REGISTAR E MOSTRAR O QUE ACONTECEU.** Não existe qualquer comportamento de paragem, validação de consistência obrigatória ou gate derivado do saldo negativo.
* **Relação com o balanço por categorias:** o balanço por categorias (Disponível / Em reparação / Não reparadas / Entrada excecional — acima) e a coluna `Saldo` são apresentações complementares da mesma realidade de movimentos: o primeiro agrega o estado por categoria; a segunda mostra a diferença (saídas − entradas) por linha de movimento. A forma exata de cálculo/apresentação de cada uma na implementação é item técnico; a regra funcional de ambas é: derivar dos movimentos registados, nunca introduzir à mão, nunca bloquear.
* **Saldo negativo vs discrepância — mecanismos separados (distinção explícita):** o saldo negativo na coluna `Saldo` é apenas o resultado visual/histórico da diferença dos movimentos Entrada/Saída registados (ex.: Saída 10 → Entrada 15 → Saldo -5 a vermelho). Por si só, NÃO cria uma discrepância, NÃO a exige e NÃO desencadeia qualquer workflow obrigatório de resolução. A discrepância / entrada excecional é um mecanismo funcional SEPARADO (regra 20→25 — retorno a mais do que o esperado; secções 9 e 23): o retorno é registado na íntegra com aviso e a discrepância fica registada para ser tratada depois, se o utilizador a tratar; nunca bloqueia; nunca é obrigatória para registar/guardar a Entrada; e a nota aplica-se apenas no momento em que o utilizador resolve.

## 12. Reparadores

* **Reparador:** selecionado/associado ao movimento de reparação a partir do diretório canónico de reparadores.
* **Associação reparador ↔ máquina/linha configurável:** reparador predefinido/permitidos por linha (tabela compacta, uma linha por B1, B2, B3, C1, C2 e C3); "Sem associação" permitido e visível; desativar, não eliminar.
* **Comportamento preservado:** o reparador predefinido é sugerido automaticamente ao criar uma saída, mas pode ser alterado no movimento; se o predefinido for desativado, a linha exige nova associação.
* O reparador efetivo é preservado no histórico — alterações posteriores de configuração não reescrevem movimentos passados.
* **Configurar reparadores (Definições):** criar reparador; desativar reparador (não eliminar — preserva histórico); associar reparador predefinido e permitidos por linha; "Sem associação" permitido.

## 13. Máquina / Linha

* Máquina/linha é escolha múltipla (MULTI-SELECT): o utilizador pode selecionar uma ou várias linhas. `B1 + C3` é válido. Pelo menos uma máquina/linha deve ser selecionada ao criar um lote BQ em falta.
* A informação de máquina/linha registada existe para que a aplicação filtre corretamente as opções de ferramenta (agora e no futuro).
* Registado `T173 · Lot 5 · [B1, C3]`: para `B1` → elegível; para `C3` → elegível; para `B2` → não elegível a menos que `B2` esteja também registado.
* Nunca inferir compatibilidade de máquina a partir do código da referência — usar as máquinas/linhas explicitamente registadas.

**Distinção (conceitos A/B/C, não colapsar):**
* **A** — Máquinas/Linhas registadas do BQ/Lot master: contexto multi-valor registado para filtragem; dono Ferramentas; alterar esta configuração é manutenção master (não Boquilhas).
* **B** — Máquina/Linha associada a um movimento/trace de reparação: contexto válido de Boquilhas (ex.: o movimento regista-se relativamente à linha B1).
* **C** — Movimento físico geral da BQ de uma linha/local para outro: dono Armazém / modelo de movimento físico, onde aplicável (não Boquilhas).

* **Registo/contexto de linha do repair trace (change line context):** regista/altera o contexto de linha do repair trace (sem quantidade). Efeito no saldo: nenhuma alteração de quantidade. NÃO acontece: não altera as Máquinas/Linhas registadas do master da ferramenta; não é uma relocação física da BQ.

## 14. Utilização %

* **`% UTILIZAÇÃO = MANUAL VALUE` (Q2 fechada — OWNER-CONFIRMED):** o valor é introduzido/atualizado manualmente por um utilizador.
* **O sistema deve NUNCA:** calcular `% utilização`; incrementar `% utilização`; derivar `% utilização` automaticamente; atualizar `% utilização` automaticamente; sincronizá-la silenciosamente para outro valor; modificá-la automaticamente por causa de um movimento.
* Não é `CALCULATED VALUE`; não é `AUTOMATICALLY UPDATED VALUE`.
* O campo `Utilização %` no Registo de Boquilhas não é read-only apenas porque Ferramentas é dono do master BQ: `MASTER OWNERSHIP ≠ AUTOMATIC VALUE` e `MASTER OWNERSHIP ≠ READ-ONLY IN EVERY OPERATIONAL SURFACE`. O utilizador pode introduzir/atualizar manualmente `% utilização` onde o workflow confirmado expõe o campo.
* A alternativa rejeitada pelo Owner — "Boquilhas lê automaticamente o % atual do Ferramentas e expõe read-only" — não é regra funcional.
* Ferramentas permanece dono do master / informação master de `% utilização`.
* **Reminder / alarme (regra confirmada):** quando a ferramenta sai de Produção e entra no Armazém, o sistema apresenta um alarme/reminder para atualizar `% utilização`: diz ao utilizador que `% utilização` deve ser atualizada; não calcula um valor novo; não modifica o valor guardado; não infere a percentagem correta; não bloqueia por não conseguir calcular; existe apenas para promover a atualização manual.
* **Distinção funcional:** `REMINDER ≠ AUTOMATIC UPDATE`. O trigger operacional ocorre na transição `PRODUÇÃO → ARMAZÉM`.
* **Atualização manual na superfície de manutenção (Q2+Q4 combinadas — OWNER-CONFIRMED):** a atualização **manual** de `% utilização` pode ser realizada pelo perfil **RESPONSÁVEL** na superfície de manutenção do **Armazém** (registo existente), apenas onde a característica estiver exposta/confirmada como editável — sem qualquer automatização de escrita, sem cálculo automático e sem transferência de posse (Ferramentas permanece o domínio master).
* **Valores de abertura/fecho no repair trace** (`vida/utilização inicial`, `vida final (%)`): são valores introduzidos manualmente — nunca "calculados", "derivados automaticamente" nem "sincronizados automaticamente".
* Vida/utilização NÃO é quantidade; não se mostra como barra de progresso. Valor perto do limite é aviso, não bloqueio.

## 15. Data de Abertura

* **`Data de abertura`** é um campo de data normal e EDITÁVEL na UI de criação do Registo (Q3 fechada — OWNER-CONFIRMED).
* **O utilizador pode:** digitar/preencher a data manualmente; selecionar a data no date picker/calendário; alterar o valor antes de guardar.
* **Default = hoje** (pré-preenchimento permitido) — mas: `DEFAULT = TODAY` ≠ `FIXED = TODAY`; `DEFAULT = TODAY` ≠ "hidden system timestamp only".
* **Semântica:** `Data de abertura` = a data de negócio/abertura do registo do fluxo de reparação de Boquilhas. Não é: data de criação do master BQ; timestamp automático de auditoria; timestamp imutável criado pelo sistema.
* O sistema pode manter tecnicamente `created_at`, data/hora de auditoria e timestamps de modificação — esses timestamps técnicos não substituem a `Data de abertura` funcional editável.
* Campo DATE (não se inventa seletor de hora sem autoridade funcional separada).

## 16. Registos Existentes

**Q4 fechada (clarificação adicional pré-merge — OWNER-CONFIRMED):**
* **`EXISTE → SELECIONA`** — BQ/Lote existente: em Boquilhas é selecionada/aberta para o fluxo de reparação (e continua).
* **`MISSING → CREATE IN BOQUILHAS → CONTINUE`** — BQ/Lote em falta no registo operacional ferramenta/armazém: pode ser criada diretamente de Boquilhas (Referência; Lote; Máquina(s)/Linha(s); restantes campos de criação correntemente confirmados); a criação existe especificamente para o fluxo diário de reparação não ser bloqueado; após criar, o utilizador continua imediatamente no fluxo de reparação.
* **`EXISTING RECORD → VIEW / MAINTAIN CONFIRMED EDITABLE CHARACTERISTICS IN ARMAZÉM → RESPONSÁVEL`** — Boquilhas não é a superfície normal de edição/master de um registo existente: ver o registo BQ/Lote existente e as suas características de ferramenta → ARMAZÉM; manter/editar as características do BQ/Lote existente que estejam funcionalmente confirmadas como editáveis → a partir do ARMAZÉM; a manutenção é realizada pelo perfil RESPONSÁVEL.

**Distinção crítica — posse vs superfície operacional (`FUNCTIONAL OWNERSHIP` ≠ `WHERE THE USER EDITS THE RECORD`):**
* **Ferramentas** — a BQ continua a ser uma ferramenta; o Ferramentas permanece o domínio master / classificação funcional da ferramenta.
* **Armazém** — superfície operacional onde o registo BQ/Lote existente é visto/aberto/mantido pelo Responsável.
* **Boquilhas** — pode criar a BQ/Lote em falta para iniciar/continuar a reparação; não se torna a superfície normal de edição/manutenção do existente; possui o fluxo de movimentos de reparação externa.

**Editabilidade campo-a-campo (Q4 NÃO decide):** a Q4 determina onde ocorre a manutenção operacional e quem a executa — não é um catálogo de editabilidade.
* `EDITABLE` — já confirmado por autoridade funcional (ex.: `% utilização`).
* `READ-ONLY / FIXED` — já confirmado por autoridade funcional (ex.: identidade do lote = `Referência + Lote`).
* `NOT SPECIFIED` — Q4 NÃO DECIDE A EDITABILIDADE DO CAMPO (ex.: Referência, identidade do Lote, Máquinas/Linhas registadas de um registo existente).

**Continuidade de dados (regra crítica):** não podem existir dois registos BQ independentes. A BQ/Lote criada em Boquilhas tem de ser, mais tarde, o MESMO registo lógico BQ/Lote visto/editado no Armazém. Sem master duplicado, sem cópia manual, sem segunda identidade.

## 17. Avisos / Comportamentos Documentados

* **Aviso (informa, não bloqueia por si):**
  * Retorno que excede o esperado (ex. voltou 25, esperado 20): aviso; o retorno é registado na íntegra e a discrepância fica registada (a nota aplica-se apenas na resolução, se o utilizador a fizer). Não bloqueia.
  * Vida/utilização perto do limite: aviso informativo. Não bloqueia.
  * Referências diferentes na mesma linha (painel lateral): alerta de conflito (informa que há referências diferentes na mesma linha) — o cartão em conflito não abre lote; a correção é feita pelo menu `…` (remover ou substituir uma referência) e o alerta desaparece quando resta apenas uma referência distinta. Vários lotes da mesma referência na mesma linha são permitidos e não geram alerta.
  * **Aviso de correspondência no formulário Entrada/Saída (comportamento preservado):** oculto no motivo `Movimento normal`, apresentado nos motivos relevantes; informa que qualquer parte sem correspondência fica assinalada no histórico e não altera a quantidade física do lote. Nunca bloqueia o movimento.
* **Regra global de ouro:** Aviso ≠ decisão automática. Um aviso não bloqueia.

## 18. Decisões / Confirmações

* **Aprovações/rejeições:** NOT APPLICABLE em Boquilhas — não há fluxo de aprovação/rejeição de registos.
* **Confirmações:** existem confirmações de ações destrutivas ou de perda de dados (fechar registo, eliminar, descartar criação preenchida). São confirmações de interface, não aprovações de negócio por um perfil superior.
* **Decisões operacionais do utilizador:** escolher reparador, resolver discrepância, reabrir o último trace fechado, fechar o trace. Estas são feitas por quem opera o módulo; não pertencem a um perfil específico (Q1 fechada).
* **Feedback e acessibilidade (comportamentos preservados):** todos os elementos clicáveis funcionam por teclado; foco visível em botões, tabs, cartões e linhas selecionáveis; menus e modais fecham com `Escape`; mensagens de sucesso são discretas e não bloqueantes; confirma-se apenas ações destrutivas ou perda de dados preenchidos; botões com dois estados visuais (repouso e hover/foco), sem `brightness`/transparência intermédia; ações destrutivas usam vermelho nos dois estados; botões desativados são cinzentos e não reagem ao hover.

## 19. Histórico / Auditoria

* **Histórico operacional de Boquilhas:**
  * **Movimentos de reparação:** todos os movimentos (início, saída, entrada, não reparadas, linha, correção, fecho) ficam no histórico do lote e na vista Histórico transversal, com referência, lote, tipo, quantidade, saldo após o movimento, reparador, linha, data/hora e operador.
  * **Balanço:** o histórico de saldo é reconstruível a partir dos movimentos.
  * **Correções:** a correção de contagem é um novo movimento; o valor original fica visível antes; nada é apagado.
  * **Movimentos anteriores:** não são reescritos; uma eventual "eliminação" é um registo de anulação à parte, nunca uma remoção física.
  * **Fecho/reabertura:** o fecho guarda um snapshot final imutável; as reaberturas ficam registadas.
  * **Ciclo do repair trace:** o estado ativo/fechado do trace e as reaberturas ficam registados (quem/quando/motivo). Não existe histórico de lifecycle de ferramenta em Boquilhas.
  * **Discrepâncias:** entradas excecionais e respetivas resoluções ficam registadas (quem/quando/nota).
* **Página Histórico (comportamentos preservados):**
  * **Responsabilidade:** o Registo apresenta apenas o lote atualmente selecionado (incluindo todos os seus movimentos); o Histórico é a visão geral e transversal do sistema; a mesma fonte de dados alimenta ambas as páginas — muda apenas o âmbito da consulta. Não obrigar o utilizador a usar o Histórico para consultar os movimentos de um lote já aberto no Registo.
  * **Organização:** calendário à esquerda e cartões de resumo à direita; filtros em largura total; tabela de movimentos em largura total; os cartões respondem ao período e aos filtros aplicados.
  * **Filtros:** referência, lote ou linha; data/período; tipo de movimento (Inícios, Saídas, Entradas, Não reparadas, Mudanças de linha, Correções, Fechos); reparador; estado do ficheiro; linhas por página; `Mostrar todos os dias` remove a seleção de data; `Limpar filtros` restaura os valores por defeito.
  * **Tabela — colunas:** Referência · Lote · Movimento (`Entrada` ou `Saída`, sem texto redundante) · **Quantidade** · **Saldo** · Reparador · Linha · Data e hora · Operador. Não incluir `Detalhe` nem `Ficheiro` como colunas.
  * **Interações:** um clique seleciona a linha e ativa `Corrigir movimento` e `Eliminar movimento`; a seleção fica visualmente marcada; duplo clique abre o lote correspondente no tab Registo; alterar filtros limpa a seleção e desativa as ações; eliminar exige confirmação.
  * **Coluna `Saldo`:** diferença resultante dos movimentos (saídas menos entradas), apresentada por linha; saldos negativos mostrados a vermelho; regista e mostra o que aconteceu, sem qualquer bloqueio (ver secção 11).
  * **Corrigir/Eliminar movimento:** corrigir cria um novo movimento (o original permanece no histórico); eliminar é uma anulação registada (com confirmação), nunca uma remoção física — o histórico não é reescrito.
* **História transversal:** A História é uma superfície transversal de leitura dos eventos de auditoria. Mostra eventos dos módulos concedidos ao utilizador (incluindo Boquilhas). História NÃO é dono dos registos operacionais de Boquilhas; apenas os apresenta.
* **Auditoria:** cada operação fica registada com quem/quando; a escrita e o registo de auditoria ocorrem na mesma operação atómica.

## 20. Outputs

* **PDF / impressão de movimentos do lote:** o design documenta acesso a impressão/PDF na lista de movimentos do lote (Registo) e no Histórico. Na implementação atual não há produção de PDF/impressão/exportação de Boquilhas; a divergência design/implementação fica em reconciliação técnica.
* **Documento de envio/retorno de reparação, labels, CSV, relatórios de stock:** NOT PRESENT como outputs correntes.
* **O que existe hoje:** o Registo mostra a lista de movimentos de reparação do lote e o Histórico mostra a vista agregada. Os "totais" surgem no mockup; na implementação atual vê-se um resumo agregado (pills de saídas/entradas) — saldo funcional consultável, não um documento exportável.
* **Conclusão:** sem output documental correntemente confirmado.

## 21. Ownership

| Domínio | Dono funcional |
| --- | --- |
| **BQ master** (identidade master da ferramenta BQ) | Ferramentas |
| **Modelo normal de ferramenta BQ** (referência, lote, estado técnico, dados normais de ferramenta) | Ferramentas |
| **Localização física / movimentos físicos gerais de BQ** | Armazém |
| **Movimentos de reparação externa de BQ** (saída/retorno de reparação) | Boquilhas |
| **Quantidades/balanço do fluxo de reparação de BQ** | Boquilhas (derivado dos movimentos de reparação) |
| **Reparador / associação reparador ↔ linha** (no fluxo de reparação) | Boquilhas (vocabulário partilhado) |
| **Produção / planeamento / revisões / contexto de produção** | Job On |
| **Seleção de BQ+lote para uma produção** | Job On (decisão do Responsável) |
| **Registo/resultados/decisões de controlo** (Peso/Pegamentos/Resumo) | Controlo |
| **Reparação interna** | Reparação Interna (CM/MF only; BQ nunca) |
| **Reparação externa** (batch CM/MF) | Reparação Externa (BQ fluxo adiado) |
| **História transversal** | História lê; não é dono de eventos |

**Regras de distinção (nunca confundir):** usar dados ≠ possuir dados; editar através de uma superfície ≠ possuir o domínio master. Uma superfície operacional pode expor edição de uma característica confirmada como editável sem que isso transfira a posse do domínio master.

**Superfície operacional vs posse master (Q4 — OWNER-CONFIRMED):**
* BQ é uma ferramenta / conceito master → Ferramentas.
* Superfície operacional de consulta/manutenção do registo BQ/Lote existente (características confirmadas como editáveis) → Armazém.
* Quem mantém as características existentes (confirmadas como editáveis) → Responsável.
* Criar BQ/Lote em falta durante a reparação → Boquilhas.
* Movimentos de reparação externa → Boquilhas.
* Localização física / movimento de armazém → Armazém.

## 22. Relações com Outros Módulos

* **Ferramentas:** Fronteira: FERRAMENTAS = BQ tool/master · BOQUILHAS = BQ external-repair movements. Boquilhas mostra/utiliza a identificação do lote de BQ cujo master pertence ao Ferramentas. Boquilhas não é dono do master; uma operação de Boquilhas não altera o master do Ferramentas.
* **Armazém:** Fronteira: ARMAZÉM = PHYSICAL LOCATION / normal warehouse movements · BOQUILHAS = BQ external-repair movements. BQ comporta-se como CM/MF no modelo normal de armazém. Um movimento de reparação de BQ em Boquilhas não é um movimento físico de Armazém. O saldo "disponível"/"em reparação" de Boquilhas é o balanço do fluxo de reparação; não é "localização física". Reminder de `% utilização` na transição Produção → Armazém (secção 14). Registo BQ/Lote existente — superfície operacional (Q4, secção 16).
* **Job On:** Job On é dono da produção/planeamento/revisões/contexto e da seleção de ferramenta. BQ é uma ferramenta principal selecionada pelo RESPONSÁVEL no Job On. Boquilhas NÃO altera o Job On. Selecionar BQ+lote no Job On NÃO cria automaticamente um movimento de Boquilhas. Boquilhas NÃO decide qual BQ vai à produção. Filtragem por máquina/linha registada. Não existe consulta ao vivo do Job On a partir de Boquilhas: a integração usa snapshots imutáveis do Job On/BQ (decisão Owner D2). Consumo de contexto: Boquilhas mostra no painel lateral o estado das linhas. Snapshot: o lote BQ usado numa produção é guardado como snapshot/contexto histórico.
* **Controlo:** O Controlo usa o lote de BQ exato que foi selecionado no Job On para a produção, como contexto/snapshot. Boquilhas não fornece ao Controlo valores técnicos/volume. Um resultado de Controlo (ex. NOK/aviso) NÃO altera automaticamente o estado ou saldo de Boquilhas.
* **Reparação Externa:** Boquilhas é o fluxo de movimentos de reparação externa de BQ. A Reparação Externa (módulo) gere os batches de reparação externa CM/MF. O fluxo de reparação externa desenhado para BQ está adiado (não ativo no modelo atual — Owner D1): a BQ não entra no processo de batch externo CM/MF da Reparação Externa.
* **Reparação Interna:** `BQ NUNCA REPARAÇÃO INTERNA`. A Reparação Interna repara CM e MF apenas. BQ nunca é reparada, selecionada ou processada na Reparação Interna.
* **História:** História é leitura transversal dos eventos de auditoria. Não é dona dos registos operacionais de Boquilhas.

## 23. Casos Especiais

* **Voltou mais do que o esperado (o caso "20→25"):** é a exceção central do fluxo de reparação. O retorno é aceite na íntegra, reconciliado com o esperado, e o excesso é registado como entrada excecional com discrepância aberta. Nunca bloqueia; nunca soma automaticamente o excesso. É um mecanismo separado do saldo negativo: o saldo negativo por si só não cria nem exige discrepância (ver secção 11).
* **Saldo negativo (ex.: SAÍDA 10; ENTRADA 15 → SALDO -5 a vermelho):** diferença resultante apresentada na coluna `Saldo` do histórico; o movimento é registado, a quantidade real é preservada, o histórico mostra o que aconteceu e o valor negativo aparece a vermelho — sem bloqueio, sem rejeição, sem workflow obrigatório e sem discrepância obrigatória (a discrepância/entrada excecional é um mecanismo separado — ver secções 9 e 11).
* **Referências diferentes na mesma linha:** alerta de conflito no painel lateral (requer remover/substituir uma referência); vários lotes da mesma referência na mesma linha são permitidos sem alerta; o cartão em conflito não abre lote.
* **Fecho falhado:** o registo permanece ativo, sem snapshot parcial apresentado como válido.
* **Reabrir:** só o último fechado e sem outro ativo.
* **Vida/desgaste:** é tempo de vida, não quantidade; valor perto do limite é aviso, não bloqueio.
* **Sem integração SAP automática:** a vida é manual; não há leitura/escrita SAP automática.

## 24. Regras Negativas

* Boquilhas NÃO é dono do master BQ (Ferramentas é).
* Boquilhas NÃO altera o master da ferramenta ao registar movimentos; a consulta do registo BQ/Lote existente e a manutenção das características confirmadas como editáveis são feitas a partir do Armazém pelo Responsável (Q4).
* Boquilhas NÃO é o dono do ciclo de vida geral da BQ como ferramenta.
* Movimento de reparação de Boquilhas NÃO é movimento físico de Armazém; Boquilhas não gere localização física nem movimenta stock de Armazém.
* Boquilhas NÃO gere movimentos gerais/operacionais de BQ fora do fluxo de reparação.
* Boquilhas NÃO altera a produção/planeamento do Job On nem reescreve revisões/snapshots históricos.
* Selecionar BQ+lote no Job On NÃO cria automaticamente um movimento de Boquilhas.
* Controlo NOK/aviso NÃO altera automaticamente o estado/saldo de Boquilhas.
* Retorno a mais NÃO bloqueia e NÃO é somado automaticamente (é aviso + discrepância).
* **Saldo negativo na coluna `Saldo` NÃO é bloqueio, rejeição, correção automática, exceção de permissão, workflow obrigatório nem discrepância obrigatória (preservado — secção 11).**
* **O aviso de correspondência (formulário Entrada/Saída) NÃO bloqueia e NÃO altera a quantidade física do lote (preservado).**
* **Eliminar movimento no Histórico = anulação registada (com confirmação), nunca remoção física (preservado).**
* **Não apresentar o indicador `Contagem reconciliada` (design: não apresentar — preservado).**
* Aviso ≠ bloqueio.
* Histórico NÃO é reescrito.
* `BQ NUNCA REPARAÇÃO INTERNA`.
* História NÃO é dono dos registos operacionais de Boquilhas; apenas lê eventos.
* Boquilhas não usa ao vivo o Job On para decidir saldos (usa contexto/snapshot — Owner D2).
* Não existe módulo separado "Boquilhas Operador/Responsável/BQ master".
* Sem tab `Fabrico`.
* Vida/utilização NÃO é quantidade; não se mostra como barra de progresso.
* Admin não é operacional de Boquilhas.
* Não se bloqueia a criação em falta apenas porque o master de Ferramentas não está totalmente preenchido.
* `Referência + Lote` é a identidade do lote; Máquina/Linha é contexto registado, NÃO identidade composta.
* Não reduzir a pesquisa a apenas referência ou apenas lote.
* Máquina/linha é escolha múltipla; pelo menos uma deve ser selecionada na criação em falta.
* Não inferir compatibilidade de máquina a partir da referência.
* `Fechar criação` NÃO é `Fechar registo de reparação`.
* Criar a identificação em falta NÃO torna Boquilhas dono do master de BQ.
* Boquilhas NÃO é a superfície normal de manutenção de uma BQ/Lote já existente.
* O `Total`/quantidade inicial do Registo é a quantidade do registo do fluxo de reparação, NÃO a verdade física total do stock do Armazém.
* Em Boquilhas, Operador / Controlador e Responsável têm as MESMAS ações funcionais (Q1).
* `% UTILIZAÇÃO = MANUAL VALUE` (Q2). O sistema nunca calcula, incrementa, deriva, sincroniza nem atualiza `% utilização` automaticamente.
* `MASTER OWNERSHIP ≠ AUTOMATIC VALUE` e `MASTER OWNERSHIP ≠ READ-ONLY IN EVERY OPERATIONAL SURFACE` (Q2).
* O reminder/alarme da transição Produção → Armazém NÃO altera o valor de `% utilização` (Q2).
* O trigger do reminder não transfere posse (Q2).
* `Data de abertura` é um campo de data EDITÁVEL (Q3).
* Existente → consultar/manter no Armazém (Q4).

## 25. Regras Superseded / Refined

| Item | Classificação |
| --- | --- |
| Regra do retorno em excesso (aceitar na íntegra, matched/unmatched/entrada excecional, nunca bloquear) | CURRENT FUNCTIONAL RULE |
| Registro manual Entrada/Saída com coluna `Saldo` (saídas menos entradas; negativos a vermelho; regista e mostra o que aconteceu) | PRESERVED (nunca superseded) |
| BQ NUNCA REPARAÇÃO INTERNA | CURRENT FUNCTIONAL RULE |
| Ciclo de vida geral / domínio BQ genérico / movimentos operacionais gerais de BQ associados a Boquilhas | SUPERSEDED BY LATEST OWNER CLARIFICATION |
| Boquilhas como "BQ OPERATIONAL FLOW" genérico (entradas/saídas diárias gerais, balanço de produção) | SUPERSEDED BY LATEST OWNER CLARIFICATION |
| Boquilhas = dono do master BQ | SUPERSEDED BY LATEST OWNER CLARIFICATION |
| Antiga lógica "bloquear retorno sem correspondência" + permissão "entrada excecional" como autorização | HISTORICAL / SUPERSEDED |
| Bug "entrada excecional mostrada em tipos errados" | HISTORICAL / SUPERSEDED |
| Mockups com divisão "Responsável · Metrologia" / perfil separado | HISTORICAL / SUPERSEDED |
| Percentagem de utilização (vida) | CURRENT FUNCTIONAL RULE |
| Fluxo completo de reparação externa de BQ (batch) | HISTORICAL / ADIADO (Owner D1) |
| Registo — criar/select (EXISTE → SELECIONA / NÃO EXISTE → CRIA) | CURRENT FUNCTIONAL RULE |
| Máquina/linha multi-select + não identidade composta + filtragem no Job On | CURRENT FUNCTIONAL RULE |
| Total = quantidade inicial do fluxo de reparação, não stock físico do Armazém | CURRENT FUNCTIONAL RULE |
| Fechar criação (painel) ≠ Fechar registo de reparação (trace) | CURRENT FUNCTIONAL RULE |
| Ciclo de vida genérico do BQ tool/master em Boquilhas: arquivar/sucatar/restaurar | SUPERSEDED BY LATEST OWNER CLARIFICATION |
| Filtros/estados de "ficheiro" ligados ao lifecycle genérico (ex.: estado "sucata" da BQ como lifecycle master em Boquilhas; filtros "arquivados/sucata" nessa aceção) | SUPERSEDED — no modelo atual "arquivados" corresponde ao resultado do fecho do registo de reparação (trace fechado → Histórico/arquivados); a declaração corrente de irreparável é o movimento "Não reparadas" (secção 10) |
| "Editar ficheiro" genérico (editar referência/lote/linhas permitidas como master) | SUPERSEDED BY LATEST OWNER CLARIFICATION — "Editar ficheiro" em Boquilhas limita-se à correção do registo do fluxo de reparação (notas/movimentos) |
| Lifecycle do repair trace (arquivar/sucatar o trace como a ferramenta) | SUPERSEDED / IMPLEMENTATION OR OLD DESIGN EVIDENCE |
| Perfil em Boquilhas — UNKNOWN Operador vs Responsável | PREVIOUSLY OPEN (Q1) — NOW CLOSED BY OWNER |
| Utilização % no Registo — snapshot introduzível vs read-only do Ferramentas | PREVIOUSLY OPEN (Q2) — NOW CLOSED BY OWNER |
| Data de abertura — automática vs editável | PREVIOUSLY OPEN (Q3) — NOW CLOSED BY OWNER |
| "Armazém does not own or edit the utilisation record" | SUPERSEDED / REFINED BY LATER OWNER CLARIFICATION Q2 + Q4 (ver secção 14: sem automatização; atualização manual possível pelo Responsável na superfície de manutenção do Armazém) |

Classificação adicional aplicada a este documento (nada desapareceu em silêncio):

| Comportamento pré-existente do design | Classificação |
| --- | --- |
| Formulário Entrada/Saída (Data, Quantidade, Motivo, Detalhe, Observações; motivos Normal/Movimento anterior/Correção/Outro) | PRESERVED (nunca superseded) |
| Aviso de correspondência no formulário (oculto em "Movimento normal"; nunca bloqueia; não altera quantidade física) | PRESERVED (nunca superseded) |
| Coluna `Saldo` na lista de movimentos do lote e na tabela do Histórico; "saldo de movimentos" = saídas menos entradas; negativos a vermelho | PRESERVED (nunca superseded) |
| Corrigir movimento / Eliminar movimento no Histórico (correção = novo movimento; eliminação = anulação registada com confirmação) | PRESERVED (nunca superseded) |
| Resumo do lote ativo em três blocos; estado atual calculado; movimentos do lote paginados com saldo | PRESERVED (nunca superseded) |
| Snapshot final imutável ao fechar; fecho falhado mantém trace ativo sem snapshot parcial | PRESERVED (nunca superseded) |
| Painel lateral: cartões por linha, navegação para Job On/Registo, menu `…` (Substituir/Remover/Adicionar), alerta de conflito | PRESERVED (nunca superseded) |
| Definições: reparadores predefinidos/permitidos por linha B1–C3; "Sem associação"; sugerido na saída; desativar não eliminar | PRESERVED (nunca superseded) |
| Não apresentar o indicador `Contagem reconciliada` | PRESERVED (manda não apresentar — design) |
| Reparação externa batch da BQ (fluxo completo) | HISTORICAL / ADIADO (Owner D1) |

## 26. Questões Funcionais em Aberto

* **GENUINE FUNCTIONAL OWNER QUESTIONS REMAINING: NONE.**
* Q1, Q2, Q3, Q4 estão FECHADAS PELO OWNER.
* O âmbito de Boquilhas está fechado; o lifecycle da ferramenta BQ e a edição master estão fechados (Ferramentas).
* Divergências de implementação não reabrem questões funcionais — ficam em reconciliação técnica.

## 27. Resumo Funcional Final

Em uma frase: Boquilhas é o módulo onde a fábrica regista, por referência + lote, o fluxo de movimentos de reparação externa de BQ — quantas BQ de um lote saem para reparação, com que reparador, quantas voltam e qual o balanço de reparação — num fluxo diário/contínuo com histórico permanente e sem nunca bloquear quando um retorno vem a mais.

**O utilizador:** abre Boquilhas → vê as linhas e o contexto de produção; seleciona ou cria o lote de BQ; regista manualmente cada movimento de reparação; vê o saldo de reparação atualizado, a diferença de cada movimento (coluna `Saldo` — saídas menos entradas, negativos a vermelho) e o histórico; trata discrepâncias de retorno a mais; configura reparadores por linha.

**O sistema:** guarda no servidor cada movimento introduzido manualmente; apresenta o saldo/diferença resultante de cada movimento (coluna `Saldo` — negativos a vermelho; regista e mostra o que aconteceu); preserva todo o histórico, com quem/quando; aplica apenas os cálculos já explicitamente documentados (saldo de reparação derivado dos movimentos registados; snapshot final imutável ao fechar); nunca bloqueia um retorno a mais (aviso + discrepância — mecanismo separado, secções 9 e 23); não toma decisões operacionais automáticas.

**Fronteiras (confirmadas e preservadas):** Ferramentas = BQ tool/master; Boquilhas = movimentos de reparação externa de BQ; Armazém = localização física / movimentos normais de armazém. BQ comporta-se como CM/MF no modelo normal de ferramenta/master/armazém. Boquilhas não altera produção, não altera o master, não gere stock físico. BQ nunca em Reparação Interna. História apenas lê.

**Fechado pelo Owner:** Boquilhas é um módulo (sem sub-módulos por perfil); Operador e Responsável têm as MESMAS operações; `% utilização` é sempre manual; a transição Produção → Armazém produz um reminder; `Data de abertura` é um campo de data editável; BQ/Lote existente é mantido no Armazém pelo Responsável.

## Implementation Pointers

### Relevant implementation areas

- Domain: BQ lot identity = `REFERENCE + LOT` (ex.: `T173` · `Lote 5`); Entrada/Saída with Motivos (`Movimento normal`, `Movimento anterior não registado`, `Correção operacional`, `Outro`) and Motivo-dependent placeholder rules; `Saldo` column = saídas − entradas, negatives in red, derived from movements (never hand-entered) — PRESERVED (never superseded).
- Application: return-overage discrepancy (20→25) recorded with warning, non-blocking, separate mechanism from negative `Saldo`; final close snapshot imutável (failed close keeps the trace active without partial snapshot); repair trace lifecycle (fechar trace → Histórico/arquivados); `% utilização` always manual; repairers per line B1–C3 (predefined/allowed, suggested on saída, deactivate ≠ delete, "Sem associação"); do NOT present the `Contagem reconciliada` indicator (§10).
- Technical map: `maps\10_BOQUILHAS.md` (verify freshness before use).

### Known implementation gaps

- Divergências de implementação ficam em reconciliação técnica e não reabrem questões funcionais (§26); none specific verified in this document set.

### Design reference

- `AI-CONTEXT\design-coder\31_BOQUILHAS_01_VISUAL_AUTHORITY_boquilhas.html`

### Cross-module dependencies

- Ferramentas (dono do master BQ); Job On (contexto de produção, linhas B1–C3, ligação ao Job On ativo); Armazém (BQ comporta-se como CM/MF no modelo normal; Boquilhas não move stock físico nem muda a verdade física); Reparação Interna (BQ nunca RI); Reparação Externa (fluxo BQ é deste módulo; BQ fora da RE); História (apenas lê).