# Decisão do proprietário — Reparação interna

Estado: decisão funcional posterior a R009/R015. Prevalece sobre texto anterior em conflito.

## Âmbito da intervenção

- A Reparação interna regista exclusivamente ferramentas do tipo `CM` e `MF`.
- `BQ` não é um tipo de Reparação interna e nunca aparece como opção de registo.
- A Referência/produção tem sempre três componentes visíveis no contexto: `CM`, `BQ` e `MF`.
- A BQ é contexto read-only; serve para identificar integralmente o conjunto montado nessa produção.
- A associação automática ao Job On/produção é preferida, mas nunca é condição para guardar.

## Fluxo do operador

1. Escolher a Linha, por exemplo `B1`.
2. O sistema carrega o Job On aplicável e mostra Produção, Referência, revisão e o conjunto `CM | BQ | MF`.
3. Escolher `CM` ou `MF`.
4. Introduzir um número individual, por exemplo `45`.
5. Premir `OK`; o sistema cria imediatamente uma linha na tabela inferior.
6. O campo do número fica vazio e com foco; Linha e Tipo permanecem selecionados.
7. Introduzir `54` e premir `OK`; é criada outra linha independente.

Repetir o mesmo número continua a criar ocorrências distintas quando a operação realmente aconteceu mais de uma vez.

O reparador nunca é escolhido manualmente: a identidade vem da sessão autenticada. Cada criação, correção e anulação guarda o `user_id`/ator canónico, nome legível no snapshot, data/hora, módulo, ação, entidade e resultado.

## Visibilidade e âmbito da correção/anulação

- Ao entrar, o reparador vê na Reparação interna exclusivamente os registos cujo `repairer_user_id` corresponde ao utilizador autenticado.
- O reparador só pode corrigir ou anular os seus próprios registos.
- Esta regra é aplicada no backend em todas as consultas e comandos; esconder linhas ou botões no frontend não é autorização suficiente.
- Não existe seleção manual de reparador nem forma de consultar ou agir sobre registos operacionais de outro reparador nesta interface.
- O Admin mantém a visão global no diário de Auditoria e pode consultar ações de todos os utilizadores, sem transformar essa consulta numa pontuação automática.

## Mudança de produção da Linha — regra das 09:00

- A mudança/preparação física da produção é feita às `06:00`.
- Entre `06:00` e `08:59`, a Reparação interna mantém como contexto a produção anterior dessa Linha.
- Este intervalo permite registar e reparar CM/MF retirados da produção anterior antes de iniciar o novo contexto operacional.
- Às `09:00`, a Linha passa automaticamente para a nova produção indicada pelo Job On dessa data.
- A produção permanece como contexto até à ativação, às `09:00`, da produção seguinte da mesma Linha; a data final não provoca uma troca isolada.
- A regra é exclusiva da projeção de contexto da Reparação interna. Não altera datas, estados ou calendário do Job On.
- Se a associação resultante não corresponder ao facto real, o registo continua permitido e pode ser corrigido pelo reparador sem bloqueio.

## Sem bloqueios operacionais

- Qualquer número individual não vazio é aceite como facto introduzido pelo reparador.
- Erros de digitação são corrigidos depois; não existe validação bloqueante nem aviso de “número não encontrado”.
- Sem Job On, produção, revisão, lote ou ferramenta confirmados, o registo continua permitido.
- Nesses casos, Linha, Tipo, número, reparador e data/hora ficam sempre guardados; o contexto indisponível permanece sem associação.
- O sistema nunca inventa uma associação para preencher os campos em falta.
- O reparador autenticado pode corrigir diretamente os seus próprios registos, sem capability adicional, bloqueio, aviso ou motivo obrigatório.
- Corrigir preserva o original e cria a respetiva evidência de auditoria.

## Tabela

- Um clique seleciona uma linha.
- Duplo clique pode abrir o detalhe read-only conforme a regra universal.
- `Corrigir registo` e `Apagar registo` ficam fora da tabela.
- Corrigir cria uma nova versão/registo e preserva o original.
- Apagar remove da lista operacional ativa, mas persiste como anulação auditável; nunca executa hard delete do facto histórico.

## Histórico anual e avaliação

- As ações de Reparação interna integram o diário anual global consultável no Admin.
- O Admin pode filtrar por ano, reparador, módulo, ação, resultado, Linha, Produção e período.
- O registo serve como evidência factual para a avaliação anual de desempenho.
- O sistema não calcula pontuação, ranking, produtividade ou avaliação automática.
- Quantidade de registos sem contexto não é interpretada pelo sistema.

## Contexto e histórico

- Quando disponíveis, o registo guarda Job On, revisão exata, Produção, Referência e lote. Guarda sempre Linha, componente CM/MF, número individual, reparador autenticado e data/hora.
- Uma revisão posterior do Job On não reinterpreta o registo.
- Mudar a Linha numa correção recalcula o contexto para a nova Linha; o Job On original nunca é alterado.

## Correção de autoridade

Esta decisão substitui apenas os pontos incompatíveis de R009:

- `BQ recordable` passa a **falso**;
- tipos permitidos passam a `CM | MF`;
- o conjunto `CM | BQ | MF` permanece obrigatório como contexto da produção.

As garantias de ativação por Linha, associação à revisão exata, ocorrências independentes e correções auditáveis permanecem.
