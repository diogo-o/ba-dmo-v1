# Controlo - Histórico com calendário e documentos

Estado: contrato funcional para implementação  
Mockup: `controlo.html`, tab `Histórico`

## Separação de módulos

Esta funcionalidade pertence à tab `Histórico` dentro do Controlo. Não pertence à página transversal `História`.

- `Histórico do Controlo`: documentos e eventos de Resumo, Peso e Pegamentos por Produção/data;
- `História`: consulta transversal de evolução operacional entre módulos, sem substituir o Histórico do Controlo.

## Estrutura

A tab Histórico contém:

1. calendário mensal;
2. filtro de Produção;
3. filtros combináveis `Resumo`, `Peso` e `Pegamentos`;
4. filtro de estado documental;
5. lista de snapshots/PDFs;
6. eventos append-only da folha.

Os filtros de tipo são multi-seleção. O utilizador pode consultar apenas Peso, apenas Pegamentos, Peso + Pegamentos ou qualquer outra combinação.

## Comportamento do calendário

- dias com documentos/eventos apresentam indicador discreto;
- clicar num dia seleciona a data e atualiza documentos/eventos;
- mudar de mês não seleciona um dia automaticamente;
- a data funciona em conjunto com Produção e tipos de documento;
- um dia sem resultados mostra estado vazio explícito;
- o calendário consulta factos persistidos, não deduz atividade pela existência do Job On.

## Associação dos documentos

Cada registo resolve por IDs estáveis:

```text
job_on_id
job_on_revision_id
control_sheet_id
document_type: summary | weight | gluing
document_revision_id
snapshot_id
pdf_file_id (opcional)
created_at / approved_at
```

Não pesquisar documentos por nome aproximado de ficheiro, texto da Referência ou pasta local.

## Ações

- `Abrir página`: abre o snapshot estruturado do Resumo;
- `Abrir controlo`: abre o snapshot/aplicação de Peso ou Pegamentos;
- `Abrir PDF`: abre um PDF já gerado;
- `Exportar PDF`: gera/regenera a partir do snapshot autorizado.

O snapshot estruturado é a fonte oficial. O PDF é derivado e regenerável.

## Estados

- Disponível;
- Ainda não gerado;
- A aguardar aprovação;
- Workspace indisponível;
- Ficheiro em falta;
- Versões disponíveis.

Uma falha ao consultar não aparece como lista vazia. Deve ser distinguida de `Nenhum documento corresponde aos filtros`.

## Critérios de aceitação

- os outputs deixam de aparecer na tab Resumo;
- aparecem apenas na tab Histórico do Controlo;
- calendário e Produção filtram os mesmos resultados;
- Resumo, Peso e Pegamentos são filtros independentes e combináveis;
- selecionar Produção diferente não reutiliza documentos da Produção anterior;
- ações resolvem pelo ID e revisão corretos;
- eventos mantêm ordem cronológica e são append-only;
- a página transversal História permanece separada;
- permissões são validadas no servidor;
- mobile reorganiza calendário sobre resultados sem scroll horizontal da página.

## Limite do mockup

O HTML demonstra agosto de 2026 e documentos da Produção 202602. O coder deve ligar calendário, filtros, paginação e estados aos serviços reais, sem manter datas ou ficheiros fixos no cliente.
