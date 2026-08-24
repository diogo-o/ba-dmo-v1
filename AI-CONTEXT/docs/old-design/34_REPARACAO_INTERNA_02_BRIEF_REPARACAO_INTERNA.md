# Reparação interna/turno — brief funcional V1

Estado: contrato funcional para design e implementação  
Âmbito: registo rápido de reparações de CM e MF durante a produção

## 1. Objetivo

Os reparadores de turno registam ferramentas CM e MF que vão saindo da produção para intervenção interna.

O fluxo exige apenas:

1. Linha;
2. Tipo `CM` ou `MF`;
3. número individual da ferramenta.

Referência, lote, produção e contexto são associados automaticamente quando disponíveis. A falta de contexto nunca bloqueia o registo operacional.

O reparador é sempre o utilizador autenticado. A interface lista apenas os registos desse utilizador; ele pode corrigir ou anular os próprios registos sem aviso, motivo obrigatório ou capability adicional.

## 2. Estrutura da página

Tabs V1:

1. `Registo`
2. `Consulta`

## 3. Tab Registo

### Seleção da produção por Linha

Usar cartões compactos B1, B2, B3, C1, C2 e C3. Cada cartão mostra:

- a Linha em primeiro nível;
- a Referência completa atualmente associada a essa Linha;
- `Sem Job On ativo` quando não existe contexto utilizável.

A Referência é uma variável read-only obtida do Job On ativo. Não é escrita, deduzida ou mantida localmente pela Reparação interna.

Ao escolher um cartão, consultar a projeção de contexto da Reparação interna nessa Linha para a data/hora do registo. A mudança física ocorre às `06:00`, mas entre `06:00` e `08:59` esta projeção mantém a produção anterior; às `09:00` passa para o novo Job On. A data final não provoca uma troca isolada. Os cartões devem ser grandes, legíveis e fáceis de selecionar por operadores com pouca familiaridade informática.

#### Regra obrigatória de layout

A implementação antiga com seis botões numa única fila está rejeitada: provoca overflow e coloca C2/C3 por baixo do painel de contexto.

O seletor ocupa um cartão horizontal com toda a largura útil no topo. O painel de contexto e registo ocupa outra linha, imediatamente por baixo. Estes dois cartões nunca aparecem lado a lado.

Em desktop e larguras intermédias, usar três colunas para dar dimensão suficiente aos seis cartões. Em mobile, usar uma coluna. Não comprimir a informação para manter seis Linhas numa só fila.

```css
.flow {
  grid-template-columns: minmax(0, 1fr);
}
.flow > * {
  min-width: 0;
}
.line-choice {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 12px;
}
.line-card {
  width: 100%;
  min-width: 0;
}
```

Não colocar o seletor num painel lateral estreito, não aumentar a largura da página para esconder o problema e não aplicar scroll horizontal. Nos breakpoints, reduzir a grelha para três e depois duas colunas; os cartões nunca podem invadir outro painel.

### Contexto automático

Depois de escolher a Linha, mostrar apenas um resumo compacto e legível com `Referência`, `Produção` e `Linha`. IDs técnicos, lote, Job On interno, auditoria e componentes de contexto não ocupam o ecrã de Registo; são resolvidos e guardados pelo sistema.

O ecrã operacional deve comunicar apenas quatro passos: escolher Linha, escolher CM/MF, introduzir número e confirmar. Não apresentar painéis vazios antes da seleção nem blocos explicativos que não sejam necessários para completar estes passos.

Regras:

- usar relações e IDs registados, mesmo quando não são mostrados ao operador;
- não deduzir produção pelo código da Referência;
- não inventar uma associação quando o contexto estiver ausente ou ambíguo;
- não criar um Job On ausente;
- uma consulta não altera dados.

## 4. Registar a ferramenta

Depois de carregar o contexto:

1. escolher `CM` ou `MF`;
2. introduzir o número individual;
3. confirmar `OK · Registar`.

Qualquer número individual não vazio é aceite. Não existe bloqueio nem aviso de “número não encontrado”; erros de introdução são corrigidos depois.

Não voltar a pedir Referência, lote, produção, Linha, operador ou data/hora. Operador autenticado e data/hora são capturados automaticamente.

## 5. Guardar

Ao confirmar:

1. criar imediatamente um registo independente de Reparação interna;
2. associar IDs estáveis da ferramenta/lote, Job On e produção apenas quando existirem, sem inventar relações;
3. guardar sempre Linha, Tipo, número individual, reparador autenticado e data/hora;
4. mostrar sucesso apenas após persistência;
5. acrescentar a linha à tabela inferior;
6. limpar o número individual e devolver-lhe o foco.

Linha e Tipo podem permanecer selecionados para registos consecutivos. Mudar de Linha recarrega obrigatoriamente o contexto.

O registo não altera automaticamente posição no Armazém, vida útil, estado técnico, Job On ou dados mestres.

## 6. Estados excecionais

### Sem produção ativa ou contexto inequívoco

O cartão da Linha mostra `Sem Job On ativo`. O registo continua permitido e fica associado ao reparador, Linha, Tipo, número e data/hora; os campos de Job On/produção permanecem nulos e a interface mostra `Sem associação`.

### Número não encontrado ou tipo divergente

O registo continua permitido como facto introduzido pelo reparador. O sistema não cria automaticamente uma ferramenta, não muda CM para MF e não inventa lote ou associação.

### Falha ao guardar

Manter Linha, Tipo e número introduzido; não limpar nem mostrar sucesso.

## 7. Tab Consulta

### Filtros

- intervalo de datas;
- Linha;
- Produção/Job On;
- Referência/lote;
- Tipo CM/MF;
- número individual;
- apenas corrigidos.

Na entrada operacional normal, o reparador consulta apenas os próprios registos. Quando a Consulta é aberta a partir de um Job On por um perfil de chefia autorizado, apresenta todas as reparações dos turnos associadas àquela Produção, independentemente do reparador, em modo de leitura.

### Lista

| Data/hora | Linha | Produção | Referência | Lote | Tipo | N.º individual | Operador | Estado |
|---|---|---|---|---|---|---|---|---|

- um clique seleciona;
- duplo clique abre o detalhe;
- `Corrigir registo` fica fora da tabela, na mesma barra da paginação e imediatamente antes das setas;
- `Corrigir registo` usa o botão standard do design system: altura mínima de `36px` e padding `7px 12px`; não criar uma variante maior para esta ação;
- filtros não selecionam automaticamente;
- correções usam indicador textual `Corrigido`.

## 8. Detalhe

Mostra todos os valores, contexto relacionado, operador/data original e histórico de correções. Abrir é apenas consulta.

## 9. Corrigir um engano

O reparador seleciona um dos seus próprios registos e escolhe `Corrigir registo`.

O cartão inline apresenta os valores atuais e permite corrigir:

- Linha/contexto associado;
- Tipo CM/MF;
- número individual.

Data/hora e operador originais permanecem read-only.

Ao alterar a Linha, resolver novamente o contexto para a data/hora original. Se não existir resultado inequívoco, guardar a correção sem associação, preservando sempre o facto operacional.

## 10. Auditoria

Uma correção guarda:

- registo afetado;
- valores anteriores;
- valores novos;
- utilizador que corrigiu;
- data/hora da correção.

O original nunca desaparece. A lista apresenta a versão válida mais recente e o detalhe toda a sequência. Corrigir não altera Job On, Armazém ou ferramenta de origem. `Apagar registo` é uma anulação auditável: retira a linha da lista operacional ativa, mas não executa hard delete do histórico.

## 11. Integração Job On, Produção e Controlo

Cada registo associado guarda obrigatoriamente, quando o contexto existir:

- `repair_record_id` estável;
- `job_on_id`;
- `job_on_revision_id` ativo no momento do registo;
- ID/código da Produção;
- Referência e Linha como snapshots legíveis;
- ID da ferramenta/lote quando resolvido;
- tipo CM/MF, número individual, turno, reparador e data/hora.

O mesmo registo é consultado na Reparação Interna, ficha da ferramenta e Job On; as vistas referenciam o mesmo `repair_record_id` e não criam cópias.

Ao abrir qualquer Produção atual ou histórica no Job On, apresentar junto da informação da folha:

1. `Ver Controlo` — abre o Controlo usando exatamente `job_on_id + job_on_revision_id`;
2. `Ver reparações` — abre a Consulta de Reparação Interna filtrada por esse Job On/Produção e mostra os registos de todos os turnos que o utilizador tem autorização para consultar.

Não procurar reparações apenas por texto da Referência: a mesma Referência pode ter várias Produções. A associação primária é ao Job On/Produção. A Referência é contexto legível.

O Controlo e a Reparação Interna permanecem domínios independentes. O Job On é o ponto comum de navegação e contexto; não copia nem altera os respetivos registos.

## 12. Permissões

- registar: Reparador de turno autorizado;
- consultar a própria atividade: registos do utilizador autenticado;
- consultar a partir do Job On: chefia/perfil autorizado pode ver, em leitura, todos os turnos dessa Produção;
- corrigir/anular: apenas os próprios registos, sem capability adicional nem motivo obrigatório;
- consultar todos os utilizadores: apenas no Admin/Auditoria.

Autorizações são validadas no comando. Nome/título do header vem do perfil gerido na Administração.

## 13. Questões por confirmar

- formato/intervalo do número individual de CM e MF;
- se futuramente exige observação ou motivo;
- se pode existir mais de um lote do mesmo tipo ativo na Linha;

## 14. Critérios de aceitação V1

- cada cartão grande mostra Linha e Referência do Job On ativo;
- escolher o cartão carrega a produção ativa do dia;
- uma Linha sem Job On ativo continua a permitir registar, ficando `Sem associação`;
- utilizador só escolhe CM/MF e número individual;
- Referência, lote, produção e operador não são reintroduzidos;
- nenhuma ambiguidade é resolvida automaticamente;
- guardar captura operador/data;
- sucesso limpa apenas o número;
- falha preserva dados;
- Consulta operacional mostra os registos do reparador autenticado;
- Job On atual e histórico apresenta `Ver Controlo` e `Ver reparações`;
- `Ver reparações` resolve por `job_on_id + Produção`, não apenas por Referência, e permite à chefia consultar todos os turnos autorizados;
- clique seleciona e duplo clique abre;
- Correção preserva original, alterações, autor e data/hora;
- nenhuma correção altera silenciosamente outros domínios;
- o reparador só consegue selecionar, corrigir ou anular os próprios registos;
- mudança física às 06:00 e ativação do novo contexto da Reparação interna às 09:00.
