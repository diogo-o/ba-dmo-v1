# Ferramentas e Job On — verificações configuráveis

Estado: contrato funcional V1  
Objetivo: configurar verificações na ferramenta e executá-las no Job On

## 1. Local onde são configuradas

O Chefe abre a ficha da ferramenta/lote e entra na tab `Verificações`.

As regras não são criadas dentro do Job On. O Job On apenas apresenta as verificações geradas para o lote associado à produção e recebe os checks dos operadores.

Exemplo de configuração:

| Verificação | Frequência | Estado |
|---|---|---|
| Verificar gargalo | Por fabrico | Ativa |
| Verificar rebaixos | Por fabrico | Ativa |
| Meter pernos nos fundos | Uma vez no lote | Ativa |

## 2. Regra e ocorrência

### Regra da ferramenta/lote

Configuração editável pelo Chefe:

- texto da verificação;
- frequência;
- lote a que pertence;
- estado ativa ou desativada;
- autor e data/hora de criação/alteração;
- origem quando foi copiada de outro lote.

### Ocorrência no Job On

Verificação concreta criada a partir da regra:

- regra de origem;
- ferramenta e lote;
- Job On/Produção;
- estado pendente ou confirmada;
- operador que confirmou;
- data/hora da confirmação;
- eventos de reset, quando existirem.

Confirmar uma ocorrência não elimina nem altera a regra.

## 3. Frequências V1

O campo `Frequência` é um dropdown canónico com:

| Opção | Comportamento |
|---|---|
| Uma vez no lote | aparece enquanto não existir um primeiro check válido para esse lote; depois deixa de aparecer, salvo reset pelo Chefe |
| Por fabrico | cria uma nova ocorrência em cada novo Job On/Produção onde esse lote esteja associado |

As opções são modulares e podem crescer no futuro, mas não criar na V1 um construtor livre de condições por percentagem de vida, data, estado técnico ou texto da Referência.

## 4. Tab `Verificações` da ficha da ferramenta

### Cabeçalho

- título `Verificações`;
- descrição curta;
- botão `Adicionar verificação`;
- botão `Histórico de verificações`.

### Lista de configuração

| Verificação | Frequência | Estado | Última confirmação |
|---|---|---|---|

Comportamento:

- um clique seleciona a linha;
- duplo clique abre o detalhe/histórico da regra;
- ações ficam fora da lista;
- o dropdown de frequência usa o mesmo componente do design system;
- a seleção apresenta contexto do lote atual.

Ações externas dependentes da seleção:

- `Editar`;
- `Desativar` ou `Ativar novamente`;
- `Resetar verificação`;
- `Apagar`.

## 5. Adicionar e editar

`Adicionar verificação` expande um cartão inline com:

- `Verificação` — texto largo;
- `Frequência` — Uma vez no lote ou Por fabrico;
- lote atual como contexto read-only;
- `Guardar` e `Cancelar`.

Guardar só fecha depois da persistência. Cancelar não altera dados.

Editar altera a configuração futura. Não reescreve ocorrências nem Job On históricos.

## 6. Desativar, reativar e apagar

### Desativar

- deixa de gerar novas ocorrências;
- pendências existentes devem manter estado histórico/operacional segundo o comando confirmado;
- histórico permanece consultável.

### Ativar novamente

- volta a gerar ocorrências segundo a frequência;
- não duplica uma ocorrência já existente para o mesmo Job On;
- regista autor e data/hora da reativação.

### Apagar

O Chefe pode usar `Apagar` para retirar a regra da configuração do lote.

Regras técnicas:

- deixa de aparecer na lista ativa e não é copiada para futuros lotes;
- ocorrências e confirmações históricas permanecem imutáveis;
- o evento de remoção fica auditado;
- apagar não significa destruir histórico já utilizado.

## 7. Resetar uma verificação

Uma regra `Uma vez no lote` deixa de aparecer após o primeiro check. Se o Chefe quiser verificá-la novamente, seleciona-a e usa `Resetar verificação`.

O reset:

1. preserva a confirmação anterior;
2. regista Chefe e data/hora do reset;
3. cria uma nova pendência para o mesmo lote;
4. apresenta-a imediatamente se existir Job On ativo com esse lote;
5. caso contrário, apresenta-a na próxima utilização relevante do lote;
6. mantém no histórico confirmação anterior, reset e confirmação seguinte.

Resetar não altera a frequência da regra.

## 8. Duplicar um lote novo

Ao usar `Novo lote a partir deste`, a configuração de verificações do lote de origem é copiada para o rascunho do novo lote.

No rascunho, o Chefe pode:

- manter as linhas;
- editar texto ou frequência;
- adicionar verificações;
- remover verificações;
- desativar ou reativar verificações antes de guardar.

Ao guardar:

- o novo lote recebe a própria configuração;
- nenhuma ocorrência/check do lote anterior é copiada;
- uma regra `Uma vez no lote` começa sem confirmação no novo lote;
- alterações no novo lote não alteram a configuração do lote de origem;
- a origem `Copiada do lote …` fica registada.

## 9. Apresentação no Job On

Quando o Job On associa um lote, carrega as regras ativas desse lote.

O cartão da família, por exemplo MF, apresenta:

- contador `Verificações pendentes`;
- lista curta das pendentes;
- ação `Ver verificações`;
- para o Chefe autorizado, ligação `Gerir na ficha da ferramenta`.

Não existe `Adicionar verificação` dentro do Job On.

### Pendentes

- checkbox;
- texto;
- frequência;
- lote/contexto.

### Confirmadas

- fechadas por defeito em `Mostrar confirmadas`;
- apresentam operador e data/hora;
- permanecem consultáveis;
- não ocupam o cartão fechado.

## 10. Confirmar no Job On

Ao marcar o checkbox:

1. mostrar processamento;
2. persistir a confirmação;
3. guardar operador e data/hora;
4. remover das pendentes apenas após sucesso;
5. atualizar o contador;
6. manter em `Confirmadas`.

Falha ao guardar mantém a ocorrência pendente e visível.

Abrir ou ler a lista não conta como confirmação. Apenas o check persistido responde a `Já foi verificado?`.

Na V1 a confirmação é exclusivamente manual no Job On. Não a inferir de movimentos do Armazém, Reparação, estado técnico, percentagem de uso ou passagem do tempo. A UI deve apresentar explicitamente `Confirmada manualmente por {utilizador} · {data/hora}`.

## 11. Geração por frequência

### Uma vez no lote

- permanece pendente até ao primeiro check desse lote;
- reutilizar o lote não cria nova ocorrência depois da confirmação;
- um reset explícito cria nova pendência;
- um lote novo recebe regra copiada, mas sem a confirmação anterior.

### Por fabrico

- usa o ID estável do Job On/Produção;
- cria uma ocorrência por cada novo Job On onde o lote é necessário;
- confirmar num fabrico não confirma os seguintes;
- duplicar um Job On gera as ocorrências do novo Job On, não copia checks antigos.

## 12. Histórico para o Manager

Na ficha da ferramenta, `Histórico de verificações` apresenta:

| Verificação | Frequência | Referência | Lote | Job On/Produção | Estado | Confirmada em | Confirmada por |
|---|---|---|---|---|---|---|---|

Regras:

- `Pendente` não apresenta operador/data de confirmação;
- `Confirmada` apresenta dia/hora e operador;
- um clique seleciona;
- duplo clique abre o Job On/ocorrência;
- filtros: Referência/lote, verificação, estado e datas;
- regras apagadas/desativadas continuam visíveis no histórico;
- resets aparecem como eventos auditáveis;
- consultar não reativa nem confirma.

Exemplo: `Verificar gargalo — lote 25 — Produção 202608 — Confirmada em 18/08/2026 09:42 por Ana Martins`.

## 13. Alterar o lote no Job On

Ao substituir o lote associado:

1. preservar o snapshot das ocorrências do lote anterior;
2. carregar a configuração ativa do novo lote;
3. gerar apenas as ocorrências aplicáveis;
4. mostrar o que entrou/saiu da lista;
5. nunca reutilizar checks de outro lote.

## 14. Permissões

- criar, editar, desativar, reativar, resetar e apagar regras: Chefe/Responsável autorizado;
- confirmar ocorrências: operador autorizado do Job On;
- consultar histórico: segundo o template de acesso.

Autorizações são validadas no comando, não apenas pela visibilidade dos botões.

## 15. Estados vazios e erros

- sem configuração: `Este lote não tem verificações configuradas`;
- sem pendentes: `Sem verificações pendentes para este lote`;
- regra desativada: não gera novas ocorrências;
- falha ao carregar: mostrar erro, não estado vazio;
- falha ao confirmar: manter pendente;
- falha ao resetar: manter último estado válido;
- falha ao apagar/desativar: manter regra visível e ativa conforme o último estado persistido.

## 16. Questões por confirmar

- se a lista do novo lote copia também regras desativadas ou apenas as ativas;
- se `Apagar` exige motivo;
- se a confirmação exige comentário;
- comportamento das pendências já criadas quando a regra é desativada;
- reset/correção num Job On já fechado;
- nomenclatura final das capacidades do Chefe.

## 17. Critérios de aceitação V1

- regras são configuradas na tab Verificações da ficha da ferramenta/lote;
- linhas mostram texto, dropdown de frequência e estado;
- frequências V1 são Uma vez no lote e Por fabrico;
- Job On apenas apresenta e confirma ocorrências;
- check só esconde após persistência;
- operador e data/hora ficam registados;
- novo lote copia configuração, mas nunca checks/histórico;
- o Chefe pode editar, desativar, reativar, resetar e apagar;
- apagar/desativar não destrói histórico;
- reset cria nova pendência e preserva confirmações anteriores;
- Manager consulta quem verificou e quando;
- duplicar Job On não copia checks antigos.
