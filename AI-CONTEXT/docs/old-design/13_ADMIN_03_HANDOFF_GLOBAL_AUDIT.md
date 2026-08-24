# Auditoria global de ações — contrato funcional e técnico

## 1. Objetivo

Todos os utilizadores autenticados geram um histórico de ações relevantes. Cada ação é registada como um evento próprio, associado ao utilizador, ao módulo e ao registo de negócio afetado. O Admin permite consultar esse histórico por ano.

Este histórico **não calcula pontuação, ranking, produtividade ou avaliação automática**. Limita-se a preservar factos operacionais para revisão autorizada com o respetivo contexto.

## 2. Unidade de registo

Uma ação de negócio concluída corresponde a um evento de auditoria. Não registar cada clique, hover ou navegação sem consequência. Exemplos de ações auditáveis:

- criar, duplicar, guardar, corrigir, confirmar, aprovar, rejeitar, enviar, fechar ou reabrir um registo;
- alterar uma data, ferramenta, lote, quantidade, localização ou configuração;
- confirmar ou repor uma verificação;
- iniciar ou concluir uma saída programada;
- criar/editar/desativar um utilizador ou opção administrável;
- tentar uma operação protegida que termine em falha ou acesso negado, quando relevante para segurança ou rastreabilidade.

## 3. Campos mínimos do evento

| Campo | Regra |
|---|---|
| `id` | identificador imutável do evento |
| `occurredAtUtc` | data/hora UTC gerada no servidor |
| `year` | derivado de `occurredAtUtc`; usado no filtro/índice anual |
| `actorUserId` | ID estável do utilizador autenticado |
| `actorNameSnapshot` | nome apresentado no momento da ação |
| `moduleId` | módulo que recebeu o comando, por exemplo `jobon`, `peso`, `boquilhas`, `armazem` |
| `actionCode` | código estável e pesquisável, por exemplo `jobon.revision.saved` |
| `entityType` | tipo de entidade afetada |
| `entityId` | ID da entidade afetada |
| `entityLabelSnapshot` | referência legível no momento do evento |
| `result` | `succeeded`, `failed`, `denied`, `corrected` ou outro estado controlado |
| `reason` | justificação quando obrigatória; nulo nas ações normais |
| `correlationId` | liga eventos do mesmo comando/transação |
| `jobOnId` | obrigatório quando a ação está associada a uma produção/Job On |
| `revisionId` | revisão/snapshot quando aplicável |
| `beforeSummary` / `afterSummary` | apenas campos necessários para compreender uma alteração auditável |

O registo é append-only. Uma correção gera um novo evento que referencia o anterior; nunca reescreve nem elimina o evento original.

## 4. Responsabilidade de implementação

- O backend é a fonte autoritativa do evento; um registo criado apenas no browser não é auditoria.
- Sempre que possível, a alteração de negócio e a criação do evento ocorrem na mesma transação. Se a arquitetura exigir eventos assíncronos, usar outbox/correlação para não perder ações.
- A data/hora e o utilizador são obtidos da sessão no servidor, não de campos editáveis enviados pelo cliente.
- A tabela é única e canónica. Pode ser particionada/indexada por ano, mas não se criam tabelas incompatíveis por módulo ou por ano.
- A retenção e o acesso seguem a política interna definida pela organização.

## 5. O que não deve ser guardado

Não incluir palavras-passe, tokens, cookies, credenciais, conteúdo integral de emails, PDFs, imagens ou cargas arbitrárias. O evento guarda IDs, metadados e resumos mínimos necessários. Dados sensíveis permanecem no respetivo domínio com as suas próprias regras de acesso.

## 6. Consulta no Admin

A tab `Auditoria` apresenta o registo anual com:

- filtros por ano, utilizador, módulo, ação, resultado e intervalo de datas;
- paginação canónica com 20, 40 ou 60 linhas;
- um clique para selecionar;
- duplo clique para abrir o detalhe;
- exportação anual autorizada;
- data/hora, utilizador, módulo, ação, registo associado e resultado sempre visíveis.

O detalhe mostra apenas informação factual do evento. Não existe coluna de pontos, nota, ranking ou classificação automática.

## 7. Catálogo inicial de ações por módulo

| Módulo | Exemplos de `actionCode` |
|---|---|
| Job On | `jobon.created`, `jobon.duplicated`, `jobon.revision.saved`, `jobon.tool.replaced`, `jobon.date.changed`, `jobon.verification.confirmed`, `jobon.verification.reset` |
| Peso | `weight.lot.created`, `weight.control.calculated`, `weight.control.submitted`, `weight.control.approved`, `weight.control.rejected`, `weight.comparison.decided`, `weight.pdf.generated`, `weight.email.prepared` |
| Pegamentos | `gluing.record.created`, `gluing.record.saved`, `gluing.pdf.generated` |
| Boquilhas | `bq.lot.created`, `bq.movement.created`, `bq.movement.corrected`, `bq.lot.closed` |
| Armazém | `warehouse.entry.created`, `warehouse.exit.created`, `warehouse.location.corrected`, `warehouse.scheduled_exit.completed` |
| Reparação | `repair.list.created`, `repair.exit.confirmed`, `repair.entry.confirmed`, `repair.internal.created`, `repair.internal.corrected` |
| Tampões | `stopper.quantity.added`, `stopper.quantity.removed`, `stopper.configuration.changed`, `stopper.plan.updated` |
| Administração | `admin.user.created`, `admin.user.updated`, `admin.user.deactivated`, `admin.password_reset.requested`, `admin.option.updated`, `admin.access_template.updated` |

O catálogo deve ser versionado. Novas ações usam códigos novos e estáveis; não reutilizar um código antigo com significado diferente.

## 8. Permissões

- `audit.view`: consultar eventos no Admin;
- `audit.export`: exportar o registo anual;
- apenas utilizadores administradores autorizados recebem estas capacidades na V1;
- o título livre apresentado no cabeçalho nunca concede acesso à auditoria.

## 9. Critérios de aceitação

1. Cada comando relevante concluído cria exatamente um evento principal, com correlação para eventos auxiliares quando necessário.
2. O evento identifica utilizador, módulo, ação, entidade, data/hora e resultado.
3. Alterar ou corrigir um registo não remove o evento anterior.
4. O Admin filtra por ano, utilizador, módulo, ação e período.
5. As listas seguem o comportamento global: clique seleciona; duplo clique abre detalhe.
6. A paginação oferece 20, 40 e 60 linhas.
7. A auditoria não apresenta nem calcula pontuações, rankings ou avaliações automáticas.
8. Apenas capacidades autorizadas permitem consulta e exportação.
9. Segredos e documentos integrais não são copiados para o evento.
