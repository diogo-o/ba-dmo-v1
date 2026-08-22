# Job On — modelo de dados do snapshot editável

Este documento é o contrato técnico de persistência da folha Job On. A prioridade não é reproduzir fotograficamente uma folha antiga: é garantir que **todos os dados necessários à produção ficam acessíveis, editáveis, versionados e duplicáveis**.

## 1. Princípio

- A base das ferramentas descreve a ferramenta real e o seu estado atual.
- O Armazém descreve a localização e os movimentos atuais.
- O Job On descreve **o que foi decidido para uma produção concreta**.
- Cada gravação do Job On cria uma revisão com um snapshot completo.
- A folha em `Modo consulta` lê esse snapshot.
- Um utilizador autorizado entra em `Modo edição` e pode alterar qualquer valor do snapshot, incluindo CAL, PI, pinças, calibres, quantidades, notas, contexto e associações de ferramentas.
- Alterar o snapshot nunca altera automaticamente a ficha mestre, o estado técnico, a percentagem de uso ou a posição de uma ferramenta.
- Estado, localização, compatibilidade e percentagem de uso podem gerar avisos, mas não bloqueiam a gravação de uma revisão do Job On.
- Uma revisão guardada é imutável. `Guardar alterações` insere uma nova revisão e novos registos filhos; nunca executa um `UPDATE` destrutivo sobre os valores históricos da revisão anterior.

## 2. Tabelas recomendadas

Os nomes são orientadores e podem ser adaptados às convenções do backend. A separação e as responsabilidades são obrigatórias.

### `job_on`

Identidade estável do fabrico planeado.

| Campo | Tipo indicativo | Regra |
|---|---|---|
| `id` | UUID/ID | chave estável do Job On |
| `production_code` | texto | exemplo `202601`; indexado |
| `article_reference_id` | FK anulável | ligação à Referência mestre, quando existir |
| `article_reference_snapshot` | texto | referência legível usada na produção, exemplo `5447T173` |
| `machine_code` | texto/FK | B1, B2, B3, C1, C2 ou C3 |
| `planned_start_at` | data/hora | data planeada, editável |
| `planned_end_at` | data/hora anulável | data final planeada, editável |
| `status` | enum/texto | rascunho, planeado, em fabrico, fechado, cancelado |
| `current_revision_id` | FK | revisão atualmente apresentada |
| `copied_from_job_on_id` | FK anulável | origem da duplicação |
| `created_by`, `created_at` | ator/data | auditoria de criação |

Recomendação de unicidade: usar a identidade real do negócio. Se o programa permitir mais de um Job On para a mesma Produção/Referência/Máquina, não impor uma unicidade que elimine esse caso.

O calendário consulta diretamente `planned_start_at` e `planned_end_at`; não existe uma segunda cópia de datas exclusiva do calendário. Ao guardar uma revisão com novas datas, atualizar estes campos do Job On e a projeção no calendário na mesma transação/evento de domínio. As datas antigas continuam preservadas na revisão anterior e no evento de auditoria.

### `job_on_revision`

Cabeçalho imutável de cada gravação.

| Campo | Tipo indicativo | Regra |
|---|---|---|
| `id` | UUID/ID | chave da revisão |
| `job_on_id` | FK | Job On lógico |
| `revision_number` | inteiro | crescente por Job On |
| `production_code_snapshot` | texto | valor efetivamente guardado nesta revisão |
| `article_reference_snapshot` | texto | valor efetivamente guardado nesta revisão |
| `machine_code_snapshot` | texto | máquina efetivamente guardada |
| `start_at_snapshot`, `end_at_snapshot` | data/hora | datas desta revisão |
| `sections` | inteiro anulável | secções |
| `drop_count` | inteiro anulável | gota |
| `type_snapshot` | texto anulável | Tipo usado nesta produção |
| `stop_snapshot` | texto anulável | Paragem; preserva texto/código inserido pelo utilizador |
| `weight_snapshot` | decimal anulável | Peso decidido/apresentado no Job On |
| `process_snapshot` | texto anulável | NNPB/PS recebido do lote do Peso; pode ser corrigido apenas nesta revisão |
| `general_notes` | texto anulável | notas gerais |
| `image_asset_id` | FK/URI anulável | imagem de apoio, sem obrigar fotografia |
| `change_reason` | texto | obrigatório quando se altera uma revisão já fechada/aprovada, conforme permissões |
| `saved_by`, `saved_at` | ator/data | auditoria |

`job_on.current_revision_id` aponta para a revisão mais recente, mas históricos, aprovações, PDF de Peso, Pegamentos e outros consumidores devem guardar o `job_on_revision_id` exato que utilizaram. Atualizar o Job On não reescreve um PDF nem o contexto de um registo histórico já emitido.

### `job_on_component`

Um registo por cartão/grupo da folha em cada revisão: MP/CM, MF, BQ, PU, CAL, AN, ARR, PI, CS, TP, FO ou uma futura família.

| Campo | Tipo indicativo | Regra |
|---|---|---|
| `id` | UUID/ID | chave do componente no snapshot |
| `job_on_revision_id` | FK | revisão proprietária |
| `family_code` | texto | MP, MF, BQ, PU, CAL, AN, ARR, PI, CS, TP, FO… |
| `source_tool_id` | FK anulável | ferramenta mestre selecionada; não é o dono dos valores do snapshot |
| `source_lot_id` | FK anulável | lote mestre selecionado |
| `reference_snapshot` | texto anulável | referência apresentada nessa produção |
| `lot_snapshot` | texto anulável | lote apresentado nessa produção |
| `technical_name_snapshot` | texto anulável | nome legível no momento da associação |
| `planned_quantity` | decimal/inteiro anulável | quantidade decidida para a produção |
| `stock_snapshot` | decimal/inteiro anulável | valor informativo copiado quando necessário; não substitui stock live |
| `usage_snapshot` | decimal anulável | apenas se a equipa decidir preservar o valor visto; o valor atual continua no domínio da ferramenta |
| `notes` | texto anulável | notas deste componente |
| `display_order` | inteiro | ordem na folha |

Uma associação pode manter `source_tool_id` e, simultaneamente, valores de snapshot diferentes. Isto é intencional: o ID explica a origem; o snapshot explica o que foi usado/decidido nessa produção.

### `job_on_component_field`

Campos editáveis de cada componente. Evita criar uma coluna nova na tabela principal por cada futuro detalhe, mas mantém os valores pesquisáveis e tipados.

| Campo | Tipo indicativo | Regra |
|---|---|---|
| `id` | UUID/ID | chave |
| `job_on_component_id` | FK | componente proprietário |
| `field_code` | texto | código estável, por exemplo `pi_clamp_material`, `pi_diameter`, `cs_holes` |
| `label_snapshot` | texto | rótulo apresentado naquela revisão |
| `value_type` | enum | text, integer, decimal, boolean, date, select |
| `value_text` | texto anulável | textos, referências e opções |
| `value_number` | decimal anulável | números pesquisáveis/calculáveis |
| `value_boolean` | boolean anulável | checks |
| `value_date` | data/hora anulável | datas |
| `unit` | texto anulável | mm, %, unidades… |
| `display_order` | inteiro | ordem no cartão |

Regra: apenas a coluna compatível com `value_type` deve conter valor. Não guardar números exclusivamente dentro de JSON ou texto quando forem usados em cálculo/filtro.

Exemplo PI da Produção `202601`:

| `field_code` | `value_type` | valor | unidade |
|---|---|---|---|
| `pi_clamp_material` | text/select | `Latão` | — |
| `pi_diameter` | decimal | `44.00` | `mm` |
| `pi_notes` | text | `Boleadas.` | — |

### `job_on_component_row`

Linhas repetíveis, nomeadamente a tabela CAL. É preferível a oito colunas rígidas no cabeçalho do Job On.

| Campo | Tipo indicativo | Regra |
|---|---|---|
| `id` | UUID/ID | chave da linha |
| `job_on_component_id` | FK | componente CAL ou equivalente |
| `row_code` | texto anulável | código estável quando existe |
| `element_label` | texto | exemplo `Bucha marcada`, `Pinças`, `Nível` |
| `value_text` | texto anulável | valor livre quando não é um único número |
| `value_number` | decimal anulável | valor numérico quando aplicável |
| `unit` | texto anulável | mm, unidades… |
| `machine_quantity` | decimal/inteiro anulável | quantidade em máquina |
| `display_order` | inteiro | ordem das linhas |

As linhas podem ser adicionadas, removidas e reordenadas em edição. Nenhuma lista fixa de CAL impede guardar um elemento novo.

Todos os valores da tabela CAL são editáveis na nova revisão, incluindo `element_label`, valor, unidade e quantidade em máquina. A linha equivalente da revisão anterior permanece intacta.

### `job_on_verification_occurrence`

Materializa as verificações desta produção.

| Campo | Regra |
|---|---|
| `job_on_revision_id` / `job_on_id` | contexto do Job On |
| `source_rule_id` | regra de origem, quando existir |
| `component_id` | cartão/ferramenta associado |
| `description_snapshot` | instrução apresentada |
| `frequency_snapshot` | uma vez no lote / por fabrico |
| `status` | pendente, concluída, reposta, desativada |
| `completed_by`, `completed_at` | quem confirmou e quando |
| `completion_source` | `manual_job_on`; não inferir confirmação a partir de Armazém, Reparação, estado da ferramenta ou simples leitura |

A regra nasce na ficha da ferramenta/lote e é materializada como ocorrência no Job On. O estado só passa a confirmado quando um utilizador autorizado executa o check e a operação é persistida com sucesso. Marcar visualmente a checkbox antes da resposta do servidor não é confirmação. Em caso de erro, a ocorrência permanece `pendente`.

### `job_on_audit_event`

Regista criação, duplicação, abertura de edição, gravação, alteração de ferramenta, alteração de datas e checks. Guardar `before`/`after` apenas para auditoria; esses blocos não substituem as tabelas de snapshot.

### `job_on_field_option`

Catálogo configurável para dropdowns de negócio que podem crescer. Não deixar materiais, tipos, versões ou opções equivalentes presos ao HTML/código.

| Campo | Tipo indicativo | Regra |
|---|---|---|
| `id` | UUID/ID | chave estável da opção |
| `family_code` | texto | PI, MP, MF, BQ, PU, CS, TP, FO… |
| `field_code` | texto | exemplo `clamp_material` |
| `value_code` | texto | código estável, independente do rótulo |
| `display_label` | texto | exemplo `Latão`, `Grafite` |
| `sort_order` | inteiro | ordem no dropdown |
| `is_active` | boolean | ativa para novas escolhas |
| `created_by`, `created_at` | ator/data | auditoria |
| `updated_by`, `updated_at` | ator/data | última alteração |

Regra global: dropdowns de **dados de negócio evolutivos** usam o catálogo do módulo proprietário e são geridos em Definições. Máquinas, paginação e controlos puramente técnicos usam os respetivos catálogos/regras canónicas e não são misturados nesta tabela.

Desativar uma opção remove-a de novas escolhas, mas não elimina nem altera o `value_text`/rótulo guardado nas revisões antigas. Se uma revisão histórica for aberta, continua a mostrar exatamente a opção usada na altura.

## 3. Limite entre bases de dados

| Informação | Proprietário autoritativo | O que o Job On guarda |
|---|---|---|
| ID, nome técnico, desenho e lotes da ferramenta | BD CM/MF/BQ/… | FK de origem + texto legível do snapshot |
| Máquinas permitidas da ferramenta | BD da ferramenta | associação escolhida; pode mostrar aviso se divergir |
| Estado técnico, reparado/por reparar, % de uso | BD da ferramenta | consulta live na seleção; não é sobrescrito pelo Job On |
| Posição e movimentos | BD Armazém | consulta live; não cria reserva ou saída |
| Produção, máquina e datas decididas | BD Job On | valor integral por revisão |
| PI, pinças, CAL/calibres e restantes detalhes da folha | BD Job On | linhas/campos integrais e editáveis por revisão |
| Quantidades e notas decididas para este fabrico | BD Job On | snapshot integral |
| Controlo de Peso e Pegamentos | bases desses módulos | ligação pelo `job_on_id` e revisão usada |
| Reparação interna dos turnos | base de Reparação Interna | relação por `job_on_id`, revisão ativa no registo e Produção; Referência/Linha apenas como snapshots legíveis |

## 4. Duplicação

Duplicar `202601 · 5447T173` para `202602` executa uma cópia transacional:

1. criar novo `job_on` com `copied_from_job_on_id`;
2. copiar a revisão atual, componentes, campos, linhas CAL e regras/ocorrências que devam nascer no novo fabrico;
3. atribuir nova Produção, datas e estado `rascunho`;
4. abrir em edição;
5. permitir mudar livremente PI, pinças, CAL, calibres, quantidades, notas e qualquer associação de ferramenta;
6. guardar como snapshot independente, sem alterar `202601` e sem escrever na BD mestre das ferramentas.

Não atualizar automaticamente os valores copiados a partir do estado atual das ferramentas. A interface pode propor ou avisar; o utilizador decide.

## 5. API/UI mínima

- `GET JobOn/{id}` devolve a revisão corrente completa com componentes, campos e linhas.
- `GET JobOn/{id}/revisions` devolve o histórico legível.
- `POST JobOn/{id}/duplicate` cria rascunho completo e devolve o novo ID.
- `PUT JobOn/{id}/draft` grava todos os grupos do rascunho numa operação transacional.
- `POST JobOn/{id}/revisions` fecha uma revisão e atualiza `current_revision_id`.
- A resposta de leitura pode agregar estado/localização live, claramente marcado como `liveContext`; esses dados não devem ser confundidos com o snapshot.

## 6. Critérios de aceitação

- Abrir `202601` sem consultar ferramentas externas continua a mostrar a configuração PI/CAL guardada.
- Duplicar para `202602` copia todos os campos e linhas.
- Alterar uma pinça ou calibre em `202602` não altera `202601` nem a ferramenta mestre.
- Corrigir CAL/PI no próprio Job On `202601` cria a revisão seguinte; consultar a revisão anterior continua a apresentar exatamente os valores antigos.
- É possível adicionar uma linha CAL não existente no modelo anterior.
- Uma ferramenta por reparar ou fora do Armazém gera aviso, mas não impede guardar o Job On por um utilizador autorizado.
- Peso e Pegamentos conseguem identificar o `job_on_id` e a revisão usada.
- O histórico identifica autor, data, motivo e valores alterados.
- Alterar Data início/fim atualiza o intervalo apresentado no calendário depois de guardar, sem apagar as datas da revisão anterior.

## 7. Histórico por Referência e Produção

A navegação histórica tem dois níveis diferentes:

1. **Produções da Referência**: ao selecionar uma Referência, listar todos os seus Job Ons por Produção, por exemplo `202601`, `202602`, com datas, máquina e estado. Um clique seleciona; duplo clique abre a produção.
2. **Revisões da Produção**: dentro de `202601`, listar as revisões imutáveis dessa produção. Abrir uma revisão antiga mostra exatamente o snapshot então guardado.

O filtro principal usa `article_reference_id` quando existir e mantém `article_reference_snapshot` para legibilidade histórica. Nunca agrupar apenas pelo texto se houver um ID mestre estável. A lista de Produções não substitui o histórico de revisões.
