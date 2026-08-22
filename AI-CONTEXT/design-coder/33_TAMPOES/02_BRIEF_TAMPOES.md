# Tampões — brief funcional V1

Estado: contrato funcional para design e implementação  
Âmbito: consulta, movimentos de quantidade, transformação técnica e planeamento de tampões

## 1. Objetivo

O módulo permite ao operador consultar rapidamente, sobretudo no telemóvel, quantos tampões existem para uma configuração técnica e planear a necessidade antes da produção.

Exemplo de contexto: `Ø 28,95 mm · Calote 4 mm`, com `28 enchidos` e `5 por encher`.

Na V1, Tampões é um controlo agregado de quantidades. Não existem números individuais por tampão. Uma parte da quantidade pode ser alterada de uma configuração técnica para outra sem apagar a configuração ou o histórico de origem.

## 2. Modelo funcional confirmado

A entidade consultada é uma configuração técnica estável, identificada por ID próprio. As características comparáveis são configuradas nas opções do módulo e incluem inicialmente:

- diâmetro, em milímetros;
- profundidade/calote, em milímetros.

As características são campos numéricos comparáveis. A aplicação deve guardar a definição do campo separada dos seus valores: nome, unidade, precisão e estado ativo. Não codificar `Diâmetro` ou `Calote` numa string composta nem criar colunas novas por cada configuração.

Cada configuração tem dois saldos confirmados:

- `Enchidos`;
- `Por encher`.

O enchimento com Colmonoy e a maquinação fazem parte do processo real, mas `Maquinado` não é criado como terceiro estado na V1 sem confirmação funcional.

## 3. Estrutura do módulo

Tabs V1:

1. `Registo`
2. `Consulta`
3. `Planeamento`
4. `Histórico`

`Opções` fica alinhado à direita. O Operador tem acesso total e pode criar, editar, ordenar, ativar ou desativar os campos técnicos comparáveis.

O header segue a shell global. Nome e título/função vêm do perfil gerido na Administração.

## 4. Consulta mobile-first

No topo, mostrar filtros compactos para as características ativas, inicialmente:

- `Diâmetro (mm)`;
- `Profundidade/Calote (mm)`.

Diâmetro e Calote não são introduzidos como texto livre no Registo, Consulta, Planeamento ou transformação. Os valores possíveis são preenchidos e mantidos numa tabela em `Opções`; depois aparecem como dropdowns em todas as páginas operacionais. Esta normalização impede variantes equivalentes como `4`, `4.0` e `4,00`.

Os resultados aparecem numa lista canónica. Um clique seleciona; duplo clique abre o detalhe da configuração. Não existe botão `Abrir`.

O cartão selecionado apresenta de forma imediata:

| Configuração | Enchidos | Por encher |
|---|---:|---:|
| Ø 28,95 · Calote 4 mm | 28 | 5 |

No telemóvel, os dois saldos usam blocos lado a lado e algarismos dominantes. A página não pode criar scroll horizontal.

## 5. Registar quantidades

As ações `Adicionar quantidade` e `Remover quantidade` ficam fora da lista e abrem o mesmo cartão inline.

Campos:

1. configuração selecionada, read-only;
2. saldo: `Enchidos` ou `Por encher`;
3. quantidade, inteiro positivo e campo curto.

Regras:

- adicionar incrementa apenas o saldo escolhido;
- remover decrementa apenas o saldo escolhido;
- nunca permitir saldo negativo;
- consulta, seleção e abertura não alteram quantidades;
- o novo saldo só aparece depois da confirmação persistida pelo servidor;
- falha ao guardar preserva os valores introduzidos e não mostra sucesso.

Não adicionar campos de descrição técnica, vida, estado de sucata ou arquivo neste módulo. Esses dados pertencem ao domínio mestre da ferramenta quando existirem.

### Alterar estado

O botão `Alterar estado` abre um cartão inline com um seletor:

- `Enchidos`;
- `Por encher`.

O Operador escolhe o novo estado e a quantidade. O sistema determina o saldo de origem como o estado oposto e apresenta a transferência antes de confirmar.

Exemplo: selecionar `Enchidos` e quantidade `5` retira 5 de `Por encher` e adiciona 5 a `Enchidos`.

A transferência é atómica e cria um único movimento `Alterar estado`, com origem, destino, quantidade, saldos anteriores/novos, operador e data/hora. Não implementar como dois movimentos independentes. Impedir a operação quando a origem não tiver quantidade suficiente.

## 6. Alterar configuração

`Alterar configuração` transforma uma quantidade existente. Não edita a configuração original nem altera todos os tampões desse saldo.

Exemplo confirmado:

1. origem: `Ø 28,95 mm · Calote 4 mm`;
2. quantidade: `25`;
3. destino: `Ø 28,95 mm · Calote 7 mm`;
4. confirmar retira 25 unidades da origem e adiciona 25 unidades ao destino;
5. a aplicação passa a contar essas 25 unidades na configuração de 7 mm.

O cartão inline apresenta:

- configuração e saldo de origem, read-only;
- quantidade a transformar;
- características atuais e novos valores lado a lado;
- pré-visualização das diferenças;
- configuração de destino encontrada ou indicação de que será criada;
- saldos previstos antes da confirmação.

Regras:

- a quantidade é inteira, positiva e não pode exceder o saldo de origem;
- pelo menos uma característica tem de mudar;
- características não alteradas mantêm o valor de origem;
- origem e destino usam IDs diferentes, mesmo que os valores sejam visualmente semelhantes;
- se já existir uma configuração com os valores de destino, reutilizar o seu ID;
- se não existir, criar uma nova configuração apenas após validação e confirmação;
- a operação de retirar da origem e adicionar ao destino é atómica: ou ambas persistem ou nenhuma persiste;
- nunca implementar esta operação como edição direta de `4 mm` para `7 mm`;
- falha mantém o formulário e os saldos originais.

O movimento de transformação guarda origem, destino, quantidade, valores anteriores/novos, saldos antes/depois, operador e data/hora.

## 7. Opções e campos comparáveis

O Operador tem liberdade total para criar e gerir os campos usados para descrever e comparar configurações. Também pode criar uma configuração nova diretamente ou durante uma transformação.

Para cada campo guardar:

- nome visível;
- unidade;
- número máximo de casas decimais;
- ordem de apresentação;
- ativo/inativo.

Para cada valor disponível guardar:

- campo a que pertence (`Diâmetro` ou `Calote`);
- valor numérico normalizado;
- unidade herdada/apresentada;
- ordem;
- ativo/inativo.

`Opções` apresenta a tabela `Valores disponíveis`. `Adicionar valor` cria uma opção nova; desativar remove-a dos dropdowns para novos registos, mas não elimina configurações nem histórico existentes. Os dropdowns devem carregar apenas valores ativos, ordenados numericamente.

Na V1, os campos são numéricos. Desativar um campo não elimina valores nem histórico. Alterar nome, unidade ou precisão não pode reinterpretar silenciosamente valores já guardados; uma mudança incompatível exige migração explícita.

Os campos ativos aparecem de forma consistente na Consulta, na alteração de configuração e nos filtros. Esta gestão não fica reservada ao Administrador ou ao Responsável.

A liberdade operacional não elimina as regras de integridade: todas as alterações guardam operador e data/hora, nunca apagam movimentos anteriores e não podem produzir saldos negativos ou transferências parciais.

## 8. Planeamento

`Planear` cria uma necessidade prevista; não adiciona, remove nem reserva stock físico.

Cartão inline mínimo:

- configuração selecionada;
- quantidade necessária;
- data prevista;
- produção/Job On, apenas quando existir relação inequívoca no sistema.

Depois de guardar, mostrar:

- quantidade necessária;
- saldos atuais `Enchidos` e `Por encher`;
- diferença entre a necessidade e os `Enchidos` disponíveis.

A diferença é informativa. A V1 não deduz automaticamente quantidades nem converte tampões `Por encher` em `Enchidos`.

## 9. Histórico de movimentos

Cada alteração de quantidade cria um movimento imutável com:

| Data/hora | Origem/configuração | Destino | Movimento | Saldo | Quantidade | Antes | Depois | Operador |
|---|---|---|---|---|---:|---:|---:|---|

`Movimento` é `Adicionar`, `Remover`, `Alterar estado` ou `Alterar configuração`. `Destino` é preenchido nas transferências de estado e de configuração. `Saldo` é `Enchidos` ou `Por encher`.

Filtros:

- intervalo de datas;
- diâmetro;
- calote;
- movimento;
- saldo;
- valor anterior/novo das características configuradas;
- operador.

Comportamento canónico:

- um clique seleciona;
- duplo clique abre o detalhe;
- `Corrigir movimento` fica fora da lista;
- filtros não selecionam automaticamente uma linha.

Uma correção preserva movimento original, valores anteriores, valores novos, autor, data/hora e justificação. Não existe edição silenciosa do saldo.

## 10. Histórico de planeamento

O planeamento mantém registo separado dos movimentos físicos. Deve ser possível consultar necessidade, data prevista, produção/Job On associado, autor e estado do plano.

Cancelar ou alterar um plano não altera os saldos. O conjunto exato de estados do plano depende da integração futura com Job On e deve ser confirmado antes da implementação.

## 11. Estados e mensagens

- sem configuração: `Selecione o diâmetro e a calote.`
- sem resultado: `Não foi encontrada uma configuração com estes valores.`
- resultado ambíguo: apresentar a lista e exigir seleção explícita;
- quantidade insuficiente: impedir remoção e indicar saldo disponível;
- destino igual à origem: impedir confirmação e indicar que nenhuma característica mudou;
- transformação concorrente: recarregar saldos e exigir nova confirmação;
- sem movimentos: estado vazio, não erro;
- falha de carregamento: mensagem de erro com ação `Tentar novamente`.

## 12. Regras visuais e mobile

- aplicar tokens do `DMO_DESIGN_SYSTEM.md`;
- botões preenchidos em repouso e invertidos no hover/foco;
- alvos táteis com pelo menos 44 × 44 px;
- campos de diâmetro, calote e quantidade dimensionados para valores curtos;
- teclado numérico em dispositivos móveis;
- no máximo duas casas decimais na apresentação de diâmetro e calote;
- quantidades são inteiros;
- manter a configuração ativa visível enquanto o operador adiciona, remove ou planeia.
- na transformação, apresentar `Atual` e `Novo` em colunas comparáveis; no telemóvel, empilhar por característica sem perder os rótulos.

## 13. Integrações e ownership

- Tampões é autoridade dos seus saldos e movimentos agregados;
- definições de características, configurações e movimentos são entidades distintas;
- uma transformação transfere quantidade entre configurações e não reescreve dados históricos;
- o Operador pode gerir campos, configurações, movimentos e planeamento dentro do próprio módulo;
- Job On pode referenciar configuração, necessidade e saldo consultado, mas não altera stock por simples abertura ou planeamento;
- produção e máquina só são preenchidas a partir de relações existentes;
- não deduzir IDs a partir do texto `28,95 / 4`;
- operador autenticado e data/hora vêm do servidor.

## 14. Questões por confirmar

1. `Enchido` significa já pronto para produção ou ainda pode faltar maquinação?
2. A maquinação precisa de um saldo próprio (`Maquinados`) ou apenas de histórico de processo?
3. O planeamento deverá reservar stock numa versão posterior?
4. Quais são os limites e incrementos válidos de diâmetro e calote?
5. Onde é criada uma configuração técnica nova: neste módulo ou na ficha mestre da ferramenta?
6. Que estados finais deverá ter um plano (`Aberto`, `Cumprido`, `Cancelado`, outros)?
7. Além de diâmetro e profundidade/calote, que campos comparáveis entram inicialmente?

Até estas respostas existirem, não acrescentar estados nem automatismos por inferência.

## 15. Critérios de aceitação V1

- pesquisar por diâmetro e calote encontra a configuração correta;
- diâmetro e calote são selecionados em dropdowns alimentados pela tabela de valores disponíveis;
- a vista mobile mostra claramente `Enchidos` e `Por encher`;
- adicionar e remover alteram apenas o saldo escolhido;
- alterar estado transfere a quantidade entre `Por encher` e `Enchidos` num único movimento;
- transformar 25 unidades de calote 4 mm para 7 mm reduz 25 na origem e acrescenta 25 no destino;
- uma transformação nunca edita retroativamente a configuração de origem;
- origem e destino são atualizados na mesma transação;
- opções permitem definir campos numéricos comparáveis sem apagar histórico;
- o Operador consegue criar e gerir campos/configurações sem depender do Administrador;
- nenhum saldo pode ficar negativo;
- planear não altera nem reserva stock;
- cada movimento guarda saldos anterior/novo, operador e data/hora;
- clique seleciona e duplo clique abre;
- correção é auditável e não apaga o original;
- valores técnicos têm no máximo duas casas e quantidades são inteiros;
- `Maquinado` não existe como estado sem decisão funcional explícita.
