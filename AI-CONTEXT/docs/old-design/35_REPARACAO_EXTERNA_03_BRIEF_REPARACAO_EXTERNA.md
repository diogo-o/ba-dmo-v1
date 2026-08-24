# Reparação externa — brief funcional V1

Estado: contrato funcional para design e implementação  
Âmbito: preparação, envio, acompanhamento e retorno de BQ, CM e MF enviados para reparadores externos

## 1. Estrutura

Arquitetura de navegação revista:

1. `Boquilhas` permanece um módulo/tab principal existente (`boquilhas.html`);
2. `Moldes` é um novo módulo/tab principal (`moldes.html`);
3. dentro de Moldes, `Contra moldes` e `Moldes finais` usam o mesmo padrão visual de Boquilhas, mas mantêm dados e fluxos separados.

Não criar uma página intermédia obrigatória `Reparação` nem recriar Boquilhas. O menu principal encaminha diretamente para Boquilhas ou Moldes.

Moldes partilha componentes visuais e o ciclo externo, mas CM e MF não partilham entidades nem movimentos. CM e MF nunca são fundidos num único tipo.

Tabs adicionais:

- `Envios` — listas programadas e respetivo progresso físico no Armazém;
- `Histórico` — consulta transversal;
- `Definições` — reparadores permitidos por tipo e Linha.

## 2. Ciclo externo

Este fluxo não parte da ferramenta atualmente em produção. Essa associação pertence exclusivamente à Reparação interna de turno.

Na Reparação externa de Moldes, o responsável seleciona uma produção futura planeada e prepara a lista vários dias antes do início previsto do fabrico.

1. O responsável cria uma saída programada na Reparação.
2. Seleciona ferramentas/lotes e um reparador permitido.
3. A lista fica disponível no Armazém e pode ser impressa.
4. O operador do Armazém confirma cada retirada com um check.
5. Quando todos os itens estiverem confirmados, a saída é concluída e as posições ficam livres.
6. A Reparação acompanha o envio sem duplicar os movimentos do Armazém.
7. No retorno, o Armazém confirma cada entrada e posição.
8. Quando todos os itens regressarem, o ciclo fecha.

A página não apresenta cartões de Produções ativas ou futuras. A preparação começa diretamente pela seleção CM/MF e pesquisa das ferramentas. Quando a lista precisar de associação a uma produção prevista, essa escolha aparece como um campo compacto dentro do formulário da própria lista.

Cada item preserva datas e operadores de saída/entrada. Uma lista concluída não desaparece; passa para Histórico.

### Adicionar ferramentas à lista

O formulário de criação/edição da lista tem obrigatoriamente uma área `Ferramentas da lista`. Não é permitido mostrar apenas uma contagem read-only como `15 selecionadas` sem existir uma forma visível de escolher os itens.

Para listas CM/MF, o responsável:

1. escolhe o tipo da lista (`CM` ou `MF`);
2. seleciona Referência e lote;
3. introduz/seleciona o número individual da ferramenta;
4. confirma `Adicionar CM` ou `Adicionar MF`;
5. revê a tabela de ferramentas adicionadas e pode remover qualquer item antes de criar/enviar a lista.

A tabela mostra `Tipo | Referência | Lote | N.º individual | Posição atual | Ação`. O botão `Criar lista` fica desativado enquanto não existir pelo menos uma ferramenta. Impedir duplicados do mesmo tipo + ferramenta/lote dentro da lista.

CM e MF usam listas e coleções temporárias separadas. Mudar o seletor de CM para MF nunca converte nem mistura ferramentas já adicionadas; ao regressar ao tipo anterior, o respetivo rascunho é preservado.

## 3. Boquilhas

Reutilizar o fluxo e o detalhe definidos em `BOQUILHAS_INTERFACE_BEHAVIOR.md`.

- unidade operacional: Referência + lote;
- movimentos por quantidade;
- saldo em fábrica, em reparação e não reparadas;
- reparador associado ao envio;
- registo local do lote continua acessível;
- clicar uma vez seleciona; duplo clique abre o lote.

O módulo BQ existente é aberto pela opção `Boquilhas` do menu da Reparação. Não existe migração, cópia ou redesenho da sua interface. A navegação não altera IDs, saldos ou histórico.

## 4. Contra moldes

- unidade operacional: ferramenta CM individual;
- seleção por Referência, lote, máquina permitida e número individual;
- estado e localização vêm dos domínios respetivos;
- saída programada referencia IDs estáveis de CM;
- retorno pode incluir observação, mas não altera automaticamente dados mestres.
- a lista é preparada para uma produção futura, antes do início do fabrico;

## 5. Moldes finais

Segue o mesmo ciclo externo do CM, usando exclusivamente ferramentas MF e respetivos IDs, campos, reparadores e histórico. Partilhar UI não autoriza combinar CM e MF no backend.

## 6. Lista programada

Cabeçalho:

- código da lista;
- tipo BQ/CM/MF;
- reparador;
- data prevista;
- criado por/data;
- estado.

Itens BQ mostram Referência, lote e quantidade. Itens CM/MF mostram Referência, lote, número individual, máquina/linha e posição atual quando conhecida.

Estados visuais V1:

- `Preparação`;
- `A retirar`;
- `Enviado`;
- `Retorno parcial`;
- `Concluído`;
- `Cancelado`.

Não inferir transições apenas pela abertura da página. Cada transição corresponde a confirmações persistidas.

## 7. Listas e ações

- um clique seleciona;
- duplo clique abre o detalhe;
- ações ficam fora da tabela;
- botões de ação usam 36px e ficam junto da paginação quando pertencem à seleção;
- paginação oferece 20, 40 e 60 linhas;
- filtros não selecionam automaticamente.

## 8. Alertas

- item sem localização conhecida: aviso, não localização inventada;
- item já incluído noutra saída aberta: bloquear duplicação;
- confirmação parcial: mostrar progresso explícito;
- retorno sem saída correspondente: bloquear e encaminhar para correção;
- falha de persistência: manter seleção e não mostrar sucesso.

## 9. Histórico

Campos mínimos:

| Lista | Tipo | Referência | Lote | Qtd./N.º | Reparador | Saída | Operador saída | Entrada | Operador entrada | Estado |
|---|---|---|---|---|---|---|---|---|---|---|

Filtros: período, tipo, Referência, lote, reparador, estado, Linha/máquina e operador.

## 10. Definições

Gerir reparadores e associações por:

- tipo BQ/CM/MF;
- Linha/máquina permitida;
- ativo/inativo.

Alterar uma associação não reescreve listas ou movimentos antigos. Cada envio guarda snapshot do reparador usado.

## 11. Ownership

- Reparação: plano, reparador, acompanhamento e ciclo externo;
- Armazém: posição e confirmação física de entrada/saída;
- domínio BQ/CM/MF: identidade, lote e características da ferramenta;
- Job On: contexto de produção, apenas quando existe relação explícita.

Nenhuma vista cria cópias divergentes das ferramentas.

## 12. Critérios de aceitação

- BQ, CM e MF estão separados dentro do mesmo módulo;
- saída programada criada na Reparação aparece no Armazém;
- checks parciais mostram progresso;
- concluir saída liberta posições confirmadas;
- retorno fecha o ciclo item a item;
- BQ usa quantidades; CM/MF usam números individuais;
- listas seguem clique/duplo clique e paginação canónica;
- histórico preserva saída, entrada e operadores;
- reparadores são filtrados pelo tipo e Linha/máquina;
- CM e MF nunca são combinados no domínio.
- criar uma lista CM/MF exige pelo menos uma ferramenta individual adicionada;
- o utilizador consegue adicionar, rever e remover CM/MF antes de guardar a lista;
- Reparação externa não carrega a produção atualmente ativa;
- a lista parte de uma produção futura e mostra a data prevista de início;
