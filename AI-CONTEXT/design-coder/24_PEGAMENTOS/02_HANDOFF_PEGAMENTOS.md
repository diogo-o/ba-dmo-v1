# Pegamentos — handoff funcional e visual

> Regra documental posterior: o PDF dos Pegamentos é guardado no workspace único da produção criado/resolvido pelo Controlo e fica acessível no Job On pelo `job_on_id + job_on_revision_id`. `OWNER_DECISION_SHARED_PRODUCTION_DOCUMENTS.md` prevalece sobre referências abaixo a uma pasta criada no Job On ou subpasta própria do lote.

## Objetivo

A folha mantém os cálculos, medições, tolerâncias, verificação dimensional e impressão do ficheiro original. A alteração é de fluxo e integração: antes de apresentar a folha, a aplicação exige um contexto de trabalho.

## Fluxo aprovado

1. Abrir o tab **Pegamentos**.
2. Selecionar/receber o **Job On** da produção.
3. A aplicação carrega do Job On a **Referência**, **Produção**, **Máquina** e as instâncias exatas de **CM**, **BQ** e **MF**, incluindo os respetivos lotes.
4. **Abrir folha** só prossegue quando o Job On contém todo o contexto obrigatório.
5. O contexto e as ferramentas herdadas permanecem visíveis no topo como dados não editáveis.
6. A folha mantém medições, limites, validações, mapa dimensional e **Imprimir / Guardar PDF**.

## Contrato de dados

Não criar catálogos paralelos. As opções vêm das tabelas já existentes no programa.

| Campo | Origem | Regra |
|---|---|---|
| Job On | Job On/planeamento | identificador estável e obrigatório |
| Referência | Job On | herdada; não voltar a escolher |
| Produção | Job On | herdada; seis dígitos |
| Máquina | Job On | herdada; B1–C3 |
| CM | instância/lote selecionado no Job On, proveniente do Peso | usar exatamente o CM e lote do Job On |
| BQ | instância/lote selecionado no Job On, proveniente de Boquilhas/Reparação | usar exatamente a BQ e lote do Job On |
| MF | instância/lote selecionado no Job On, proveniente do respetivo domínio | usar exatamente o MF e lote do Job On |

O protótipo contém `COMPONENT_CATALOG` apenas como dados demonstrativos. Na implementação, não é um seletor alternativo dentro de Pegamentos: a resolução vem do Job On e é validada contra os catálogos do backend.

## Integração obrigatória com Job On

Payload mínimo esperado:

```json
{
  "jobOnId":"JO-202601-B3",
  "reference":"9389T194",
  "production":"202601",
  "machine":"B3",
  "cm":{"id":"cm-5447-l4","reference":"5447","lot":"4"},
  "bq":{"id":"bq-t194-l12","reference":"T194","lot":"12"},
  "mf":{"id":"mf-9389-l26","reference":"9389","lot":"26"}
}
```

Ao receber o Job On:

- preencher Referência, Produção e Máquina como contexto não editável;
- carregar os IDs, referências e lotes concretos de CM, BQ e MF já escolhidos no Job On;
- validar que essas instâncias ainda existem e são compatíveis com a máquina;
- manter o contexto e as ferramentas visíveis para confirmação;
- gravar `jobOnId` em todo o registo e snapshot de Pegamentos.

Não existe fallback que permita escolher silenciosamente outra ferramenta. Se faltar CM, BQ ou MF obrigatório, ou se um lote estiver inválido, bloquear a folha com uma mensagem acionável: `Corrigir ferramentas no Job On`.

## Persistência e pasta do relatório

- O servidor guarda o registo estruturado de Pegamentos: Job On, Produção, Referência, Máquina, IDs/lotes CM-BQ-MF, medições, resultados, estado, revisão e auditoria.
- O snapshot estruturado persistido no registo de domínio é a fonte oficial. Não usar o HTML, o PDF nem um ficheiro JSON solto como base de dados.
- O PDF é opcional e gerado apenas quando o utilizador escolhe `Imprimir / Exportar PDF` ou quando um fluxo autorizado exige o output físico.
- O PDF é sempre regenerável a partir da revisão do snapshot escolhida.
- O PDF enviado/impresso para Produção é guardado na estrutura documental resolvida automaticamente para o Job On.
- O diretório principal é definido por ano em `Definições`; Controlo e Pegamentos não permitem escolher ou alterar diretórios.
- O caminho de Pegamentos é `diretório anual / Referência / Produção / Produção_Referência_Linha_Pegamentos`.
- Pegamentos apenas apresenta o workspace resolvido. Não permite selecionar uma pasta diferente para o mesmo Job On/revisão.
- O nome do ficheiro usa dados retirados do Job On, incluindo pelo menos Produção, Referência, tipo `Pegamentos` e revisão/data.
- Mostrar separadamente `Dados guardados no servidor` e `PDF guardado localmente`. Uma falha local não apaga o registo numérico.

Exemplo estrutural: `PEGAMENTOS_SNAPSHOT_EXAMPLE.json`.

## Conteúdo do snapshot

Persistir por revisão:

- `job_on_id` e `job_on_revision_id`;
- identidade e revisão do controlo de Pegamentos;
- Referência, Produção e Máquina capturadas do Job On;
- CM, BQ e MF concretos, com IDs, referências e lotes;
- nominal, limite mínimo e limite máximo capturados para cada componente;
- todas as medições individuais: número, Costura, Contra costura, Ovalização e Média;
- médias por componente;
- resultado dentro/fora do corredor por medição e por componente;
- resultado global;
- notas, estado, autor e datas de criação/submissão/aprovação;
- versão do motor de cálculo e regra de arredondamento.

Não persistir o gráfico como imagem nem depender das coordenadas do PDF. O mapa de limites é uma projeção determinística dos limites e medições do snapshot.

## Projeção do relatório/PDF

O exemplo visual fornecido mantém:

1. cabeçalho Pegamentos, Referência, Produção, Máquina, data e revisão;
2. três resumos: CM, BQ e MF, com média medida, nominal e corredor;
3. estado global claro;
4. mapa de limites com fronteiras, corredores, médias e legenda;
5. tabela CM;
6. tabela BQ;
7. tabela MF;
8. rastreabilidade da geração.

Cada tabela usa `N.º`, `Costura`, `Contra costura`, `Ovalização` e `Média`. O PDF aplica o design azul dos documentos de Controlo. A consulta dentro do programa usa os mesmos dados estruturados e pode apresentar o gráfico responsivo; imprimir é uma ação adicional, não requisito para guardar o controlo.

O novo PDF não imprime `file:///...`, caminhos completos do computador nem o nome de um HTML temporário. No cabeçalho mostra a identidade documental, produção, máquina, controlo/revisão e data. As médias individuais (círculos), média consolidada (losango), nominais/fronteiras e corredores do mapa são todos derivados dos valores do snapshot.

Manifesto mínimo do output:

```json
{
  "document_type": "pegamentos",
  "job_on_id": "JO-202602-B2",
  "job_on_revision_id": "JOR-202602-B2-R3",
  "control_revision_id": "PEG-202602-B2-R1",
  "snapshot_hash": "sha256:...",
  "template_version": "pegamentos-pdf-v1",
  "file_name": "202602_9389T194_B2_Pegamentos_R1.pdf",
  "generated_at": "...",
  "generated_by": "..."
}
```

## Histórico de Pegamentos

- Filtros: Job On, referência/produção, máquina, data inicial e data final.
- Um clique seleciona visualmente a linha.
- Duplo clique abre a folha associada.
- Não existe botão adicional para abrir a linha selecionada.
- Não colocar ações de abertura dentro ou abaixo da lista.

## Elementos removidos

- **+ Nova referência** acima dos tabs;
- cartão **Base de dados** em Configurações;
- **Guardar ficheiro para imprimir**;
- **Enviar resumo**.

As funções antigas podem permanecer temporariamente no JavaScript, mas não fazem parte da interface aprovada.

## Design e aceitação

- Usa `dmo-design-system.css` e os tokens canónicos.
- Usa o header canónico com logótipo, título da página, nome e título/função do perfil administrativo.
- Botões preenchidos em repouso e invertidos no hover.
- Campos compactos; cartões claros; estados dessaturados.
- Não abrir sem Job On, Referência, Produção, Máquina e as instâncias/lotes obrigatórios de CM, BQ e MF.
- Alterar o Job On substitui todo o contexto como uma unidade; não mistura ferramentas de produções diferentes.
- Registos antigos preservam valores.
- Adicionar/remover medições e cálculos originais continuam ativos.
- Números apresentam no máximo duas casas decimais.
- O relatório identifica referência, produção, máquina e data.
- O cabeçalho de medição usa **Costura** e **Contra costura**; o nome interno legado `noventa` pode ser migrado posteriormente sem alterar o cálculo.
