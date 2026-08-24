# Registo de Boquilhas — especificação de interface

## 1. Objetivo

Este documento é o handoff funcional e visual para implementação. O mockup define a apresentação; este ficheiro define os comportamentos observáveis, regras de negócio, estados e critérios de aceitação. Não inventar nomes de campos, RPCs ou persistência a partir deste documento.

## 2. Estrutura global

- Tabs operacionais à esquerda: `Registo`, `Boquilhas`, `Histórico`.
- `Definições` fica isolado à direita.
- O painel lateral de linhas está fixo e presente em todas as páginas.
- O tab `Fabrico` não existe.
- O cabeçalho não contém ações duplicadas pelo painel lateral.
- **Sem divisão de perfil dentro de Boquilhas (OWNER-CONFIRMED):** Operador/Controlador e Responsável têm **as mesmas ações** quando o módulo está atribuído; o único gate é o módulo atribuído; não existem variantes por perfil nem aprovação por ser Responsável. (A manutenção de registos BQ existentes no Armazém pelo Responsável é uma regra do módulo Armazém — ver `32_ARMAZEM_00_README.md` — não cria distinção em Boquilhas.)

## 3. Sistema de botões

Todos os botões têm dois estados visuais:

1. Repouso: fundo preenchido com a cor da ação e texto branco.
2. Hover ou foco visível: fundo branco, contorno e texto na cor original.

Regras:

- Não usar `brightness`, transparência ou tom médio no hover.
- Ações destrutivas usam vermelho nos dois estados.
- Botões desativados são cinzentos e não reagem ao hover.
- Um botão selecionável pode manter o estado invertido enquanto estiver ativo.
- Foco por teclado deve permanecer visível.

## 4. Painel lateral de linhas

O painel mantém a leitura rápida das BQ em produção, mas também funciona como ligação ao contexto de produção:

- cada cartão mostra BQ, lote e quantidade;
- mostra a referência de produção completa, separada do identificador da BQ;
- clicar especificamente na referência completa abre o Job On ativo associado àquela referência/linha;
- clicar no restante cartão abre o registo da BQ;
- a navegação usa os IDs relacionados devolvidos pelo sistema e não reconstrói a ligação a partir do texto visível.

### Conteúdo

- Mostrar linha, referência, lote, quantidade e hora de início quando aplicável.
- Não mostrar o total global de BQ na fábrica.
- Não mostrar o selo `Em produção`.
- Linha ocupada, menu `…`: `Substituir` e `Remover`.
- Linha livre, menu `…`: `Adicionar`.

### Navegação

- Clicar no corpo de um cartão ocupado abre o respetivo lote no tab `Registo`.
- Clicar no menu `…` não abre o lote.
- Uma linha livre não abre registo; informa que deve ser usada a ação `Adicionar`.
- Se existirem vários lotes da mesma referência, pedir escolha do lote antes de abrir.

### Alerta de conflito

- Vários lotes da mesma referência na mesma linha são permitidos e não geram alerta.
- Referências diferentes na mesma linha geram alerta.
- O cartão em conflito usa contorno/indicador de atenção e o texto `Referências diferentes na mesma linha`.
- O cartão apresenta referências e lotes em conflito.
- Clicar num cartão em conflito não abre nenhum lote nem apresenta uma escolha.
- Mostrar apenas `Referências diferentes nesta linha. Remova uma referência para continuar.`
- A correção é feita pelo menu `…`, removendo ou substituindo uma referência.
- O alerta desaparece quando resta apenas uma referência distinta.

## 5. Registo

### Pesquisa

- Pesquisar por **referência / BQ, lote e/ou máquina/linha registada** (o contexto de máquina/linha registado no lote pode servir de filtro; a superfície pode mostrar o rótulo `Procurar boquilha ou lote` / `Referência ou lote`, mas não reduzir o modelo de pesquisa a apenas referência nem a apenas lote).
- Um resultado selecionado abre resumo, estado e ações do lote.
- Sem resultado, mostrar `Nenhuma boquilha encontrada`.
- Comportamento central: **`EXISTE → SELECIONA`**; **`NÃO EXISTE → CRIA`** (ver "Criar novo lote"). O utilizador **não** é bloqueado só porque o master completo de Ferramentas ainda não está preenchido; não há onboarding separado.

### Criar novo lote

- Não abrir nova página nem modal.
- O formulário expande na própria página `Registo`.
- O botão muda de `Criar novo lote` para `Fechar criação` enquanto o painel estiver aberto.
- `Fechar criação` e `Cancelar` fecham o painel (**fecham/cancelam o modo de criação**). **`Fechar criação` NÃO é o mesmo que `Fechar registo de reparação`** (fechar o registo/trace do fluxo de reparação) — conceitos distintos.
- Se houver conteúdo introduzido, confirmar antes de o descartar.
- Depois de criar, fechar o formulário, selecionar o lote e **continuar imediatamente** para o seu registo (`CREATE → CONTINUE`). Se o lote já existir, **selecionar** e abrir.
- **A criação em falta é válida** e não transfere a posse do master (Ferramentas continua dono do master BQ). Criar aqui é criar a **identificação em falta** necessária para continuar o fluxo de reparação — **não** é tornar Boquilhas dono do master, nem um editor master genérico de Ferramentas.
- **Continuidade do registo:** a BQ/Lote criada aqui (quando em falta) é o **mesmo registo lógico** que depois é **consultado/mantido no Armazém** — sem master duplicado, sem cópia manual, sem segunda identidade.
- **Registo existente:** Boquilhas **não** é a superfície normal de manutenção de uma BQ/Lote já existente. Quando a BQ/Lote **já existe**, a consulta e a manutenção das **características confirmadas como editáveis** são feitas a partir do **ARMAZÉM**, pelo perfil **RESPONSÁVEL**; a Q4 não torna automaticamente todos os campos editáveis (a editabilidade campo-a-campo de Referência/identidade do Lote/Máquinas-Linhas não está estabelecida por esta clarificação).

Ordem dos campos (hierarquia visual: identificação primária em destaque; campos secundários de contexto abaixo):

1. **Boquilha / Referência** (obrigatória; contribui para a identidade master de Ferramentas).
2. **Lote** (obrigatório, compacto).
3. **Máquina(s)/Linha(s)** — **escolha múltipla** B1–C3; **pelo menos uma obrigatória**. `B1 + C3` é válido. O controlo deve comunicar **multi-seleção** (seleção visível de vários valores em simultâneo), não um comportamento de rádio/seleção única.
4. **Total do lote** (compacta, até três dígitos no mockup) — é a **quantidade inicial do registo do fluxo de reparação**, **não** a verdade física total do stock do Armazém.
5. **Utilização inicial** (compacta, percentagem de vida útil) — **contexto/snapshot de abertura do fluxo de reparação**; valor **sempre manual** (o sistema nunca calcula, incrementa, deriva nem atualiza automaticamente; nenhum movimento altera o valor por si); a `% uso` master da ferramenta pertence ao Ferramentas. Quando a ferramenta sai de Produção e entra no Armazém, o sistema apresenta apenas um **reminder/alarme para atualizar `% utilização`** — não calcula, não infere, não modifica o valor, não bloqueia.
6. **Data de abertura** — campo **DATE editável** (preenchível manualmente ou por date picker; **default = hoje**, alterável antes de guardar); representa a **abertura/início do registo do fluxo de reparação**, não a criação do master BQ; timestamps técnicos de auditoria (`created_at`, data/hora de sistema) **não substituem** este campo funcional.
7. **Observações compactas** — notas associadas a este registo do fluxo de reparação.

Não incluir:

- Linha associada.
- Escolha `Fabricar/Reparar`.

> **Identidade (não criar identidade composta):** a identidade do lote é **`Referência + Lote`**; as **Máquina(s)/Linha(s)** são **contexto operacional registado** (pesquisa/filtragem), **não** fazem parte da identidade. `Referência + Lote + Máquina` **não** é uma identidade de negócio nova. Uma referência+lote pode estar registada para várias máquinas (ex.: `T173 · Lote 5 · [B1, C3]` = **um único** lote, não vários).

### Ações do lote

- `Saída`, `Entrada`, `Não reparadas`, `Corrigir contagem`, `Editar ficheiro`, `Fechar`.
- Não apresentar o indicador `Contagem reconciliada`.
- O botão **`Editar ficheiro`** não é uma edição master genérica: em Boquilhas limita-se à **correção do registo do fluxo de reparação** (notas/movimentos). A **manutenção de um registo BQ/Lote já existente** (características confirmadas como editáveis) é feita a partir do **Armazém**, pelo perfil **Responsável** — não em Boquilhas.

### Resumo do lote ativo e fecho

Ao selecionar um lote ativo, apresentar três blocos:

1. Resumo de abertura/configuração: referência, lote, estado, total do lote, data de entrada/abertura, número de registos e linhas permitidas.
2. Estado atual calculado: na produção, em reparação, não reparadas, saídas excecionais, entradas excecionais e linha atual.
3. Movimentos do lote: lista completa e paginada com tipo, quantidade, saldo, data e operador, com acesso a impressão/PDF. Esta lista evita obrigar o utilizador a abrir e filtrar o Histórico global para consultar um único lote.

Na lista de movimentos do lote, usar distinção cromática subtil:

- `Saída`: fundo laranja muito claro e rótulo laranja.
- `Entrada`: fundo verde muito claro e rótulo verde.
- Não depender apenas da cor; manter sempre o texto `Entrada` ou `Saída`.

O resumo de abertura e o estado atual não são totais independentes introduzidos manualmente: devem resultar do lote e dos seus movimentos.

Ao fechar:

- Pedir confirmação explícita.
- Calcular e guardar um snapshot final imutável do resumo, estado atual e metadados de fecho.
- Guardar data/hora e utilizador que fechou o lote.
- Manter os movimentos originais ligados ao lote.
- Retirar o lote das listas de ativos e disponibilizá-lo no Histórico/arquivados.
- Alterações futuras de reparadores, linhas ou configurações não podem modificar o snapshot fechado.
- Se o fecho falhar, o lote permanece ativo e nenhum snapshot parcial é apresentado como válido.

## 6. Formulário de Entrada/Saída

- Modal compacto.
- Cabeçalho em destaque: tipo de movimento e `Referência · Lote · Linha`.
- Remover `Material/trabalho`.
- Primeira linha: Data, Quantidade, Motivo.
- Segunda linha: Detalhe e Observações.
- Quantidade e Data são campos compactos.
- Observações começa com uma linha e pode crescer.
- O placeholder de Detalhe depende do Motivo:
  - Normal: `Opcional`.
  - Movimento anterior: `Ex.: saída de 12 BQ em 12/08`.
  - Correção: `Ex.: quantidade registada incorretamente`.
  - Outro: `Indique brevemente a razão`.
- O aviso de correspondência fica escondido em `Movimento normal` e aparece nos restantes motivos relevantes.

## 7. Boquilhas

- Mostrar filtros: referência/lote/linha, estado e linhas por página.
- Estados: atuais, ativas, em produção, em reparação, disponível, arquivados, sucata e todos.
- Os cartões mostram quantidade, linha/localização/reparador e percentagem de vida utilizada.
- A percentagem representa tempo de vida/desgaste, não quantidade.
- Não usar barra de progresso para a percentagem.
- Os totais `Na fábrica`, `Em reparação` e `Em produção` não pertencem a esta página; passam para o Histórico.

## 8. Histórico

### Responsabilidade da página

- O `Registo` apresenta apenas o lote atualmente selecionado, incluindo todos os seus movimentos.
- O `Histórico` é a visão geral e transversal do sistema.
- O Histórico serve para pesquisar, agregar e comparar movimentos entre referências, lotes, linhas, reparadores e períodos.
- Não obrigar o utilizador a usar o Histórico para consultar os movimentos de um único lote já aberto no Registo.
- A mesma fonte de dados alimenta ambas as páginas; muda apenas o âmbito da consulta.

### Organização

- Topo: calendário à esquerda e cartões de resumo à direita.
- Abaixo: filtros em largura total.
- Depois: tabela de movimentos em largura total.
- Os cartões respondem ao período e aos filtros aplicados.

### Filtros

- Referência, lote ou linha.
- Data/período.
- Tipo de movimento.
- Reparador.
- Estado do ficheiro.
- Linhas por página.
- `Mostrar todos os dias` remove a seleção de data.
- `Limpar filtros` restaura os valores por defeito.

### Tabela

Colunas:

- Referência.
- Lote.
- Movimento (`Entrada` ou `Saída`, sem texto redundante).
- Quantidade.
- Saldo.
- Reparador.
- Linha.
- Data e hora.
- Operador.

Não incluir `Detalhe` nem `Ficheiro` como colunas.

Interações:

- Um clique seleciona a linha e ativa `Corrigir movimento` e `Eliminar movimento`.
- A seleção fica visualmente marcada.
- Duplo clique abre o lote correspondente no tab `Registo`.
- Alterar filtros limpa a seleção e desativa as ações.
- Eliminar exige confirmação.

## 9. Definições — reparadores

- Usar uma tabela compacta com uma linha por B1, B2, B3, C1, C2 e C3.
- Cada linha configura um reparador predefinido e reparadores permitidos.
- O predefinido é sugerido ao criar uma saída, mas pode ser alterado no movimento.
- `Sem associação` é permitido e visível.
- Uma secção separada gere a lista de reparadores.
- Reparadores antigos são desativados, não eliminados, para preservar o Histórico.
- Se o predefinido for desativado, a linha exige nova associação.
- O movimento guarda o reparador efetivamente escolhido; mudanças posteriores de configuração não alteram o histórico.

## 10. Acessibilidade e feedback

- Todos os elementos clicáveis funcionam por teclado.
- Foco visível em botões, tabs, cartões e linhas selecionáveis.
- Menus e modais fecham com `Escape` na implementação final.
- Mensagens de sucesso são discretas e não bloqueantes.
- Confirmar apenas ações destrutivas ou perda de dados preenchidos.

## 11. Critérios mínimos de aceitação

- Não existe tab `Fabrico`.
- Criar lote é inline e pode ser fechado com proteção contra perda de dados.
- **Registo: `EXISTE → SELECIONA` / `NÃO EXISTE → CRIA`** — o lote em falta é criado e o fluxo continua de imediato; não é bloqueado pelo master não preenchido; a criação não transfere a posse do master.
- **Pesquisa por referência/BQ, lote e/ou máquina/linha registada** (não reduzida a apenas referência nem apenas lote).
- **Máquina(s)/Linha(s) é escolha múltipla** (B1–C3); pelo menos uma obrigatória; `B1 + C3` válido; não se comporta como seleção única; seleção simultânea visível.
- **`Referência + Lote` é a identidade; Máquina/Linha é contexto registado** — não há identidade composta; uma referência+lote pode estar registada para várias máquinas.
- **`Fechar criação` ≠ `Fechar registo de reparação`** — conceitos distintos.
- Cartões laterais abrem o lote; o menu não propaga o clique.
- Só referências distintas na mesma linha geram alerta.
- Um clique na tabela seleciona; duplo clique abre o lote.
- Histórico filtra por reparador e lote e recalcula os resumos.
- Botões usam apenas os dois estados definidos.
- Percentagem de utilização nunca é representada como quantidade.
- Configuração de reparadores preserva movimentos históricos.
- **Perfil (Q1):** Operador e Responsável têm as mesmas ações em Boquilhas (sem variantes por perfil; sem aprovação/revisão por ser Responsável).
- **`% utilização` (Q2):** sempre manual; nunca calculada nem atualizada automaticamente; a transição Produção → Armazém gera apenas reminder (não muta o valor).
- **`Data de abertura` (Q3):** campo DATE editável com default hoje, alterável antes de guardar.
- **Registo existente (Q4):** criar em falta em Boquilhas e continuar; registo já existente é consultado/mantido no **Armazém** pelo **Responsável** (características confirmadas como editáveis); sem registos BQ duplicados; Boquilhas não é superfície normal de manutenção; Armazém não é dono do fluxo de reparação.
