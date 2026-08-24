# Tampões — brief funcional V2 (Owner-confirmed)

Estado: contrato funcional Owner-confirmed para design e implementação
Âmbito: disponibilidade de quantidade por configuração técnica / máquina (consulta, movimentos de quantidade, manutenção de configurações e histórico)

> **Autoridade:** este ficheiro segue a clarificação funcional do Owner. As regras aqui descritas são a autoridade mais alta para o módulo e **substituem** qualquer texto prévio do design de Tampões em contradição (nomeadamente o modelo de planeamento, a ligação a Job On e o vínculo a Reference/Production das versões anteriores).

## 1. Objetivo

O módulo ajuda o operador a saber **quantas TP / tampões existem disponíveis para cada configuração técnica / máquina**.

Tampões é um módulo SIMPLES e AUTÓNOMO de nível superior (top-level). A superfície operacional principal é **uma única tabela simples de configurações e quantidades**.

A pergunta que o operador deve conseguir responder num relance é:

> "Quantos tampões tenho disponíveis desta configuração para esta máquina?"

## 2. Modelo funcional confirmado

Tampões é um **controlo agregado de quantidades**. Não existem números individuais por tampão.

Cada linha da tabela principal representa **uma configuração de Tampões** e contém a configuração principal e a informação de quantidade atual:

| Diâmetro | Calote | Máquina / Máquinas | Quantidade / saldos |
|----------|--------|--------------------|---------------------|

As características essenciais atuais da configuração são:

1. **Diâmetro** (mm);
2. **Calote** (mm);
3. **Máquina / Máquinas**.

Uma configuração pode aplicar-se a **uma máquina ou a múltiplas máquinas**. A Máquina é **parte da configuração funcional**, não um campo de exibição incidental.

> **Nota:** o campo existente designado por "Linhas" (valores 20/40/60) **não** representa Máquina. Esse campo é estilo de paginação/tamanho de página e não é a característica de máquina confirmada.

O módulo **não** é planeamento de produção, **não** é integração com Job On, **não** é seguimento de referência, **não** é seguimento de produção, **não** é seguimento tool-a-tool e **não** é um motor de workflow complexo.

## 3. Sem Job On / sem Reference / sem Production

**REGRA DO OWNER — EXPLÍCITA:**

Tampões **não tem qualquer relação funcional com Job On**.

- Sem ligação opcional a Job On;
- Sem campo Job On;
- Sem campo Production;
- Sem campo Reference;
- Sem planeamento de produção através de Tampões;
- Sem integração Job On read-only;
- Sem implicação de que Job On precise de informação de Tampões;
- Sem implicação de que Tampões precise de informação de Job On.

Não existe qualquer fluxo funcional:

```
Tampões → Job On
```
nem
```
Job On → Tampões
```

Tampões **não está associado** a Reference, Production ou Job On.

**Razão:** os tampões têm alta rotação e são reutilizados/adaptados em muitas referências. Associá-los a uma referência ou produção única seria funcionalmente errado.

## 4. Estrutura do módulo

Manter o módulo simples. Estrutura canónica preferida:

1. **REGISTO / tabela principal**;
2. **HISTÓRICO**;
3. **OPÇÕES / CONFIGURAÇÃO**.

Um separador **Consulta** separado é opcional apenas se trouxer valor claro sem duplicar a tabela principal. Preferir reduzir duplicação.

A tabela principal deve já suportar: consulta, seleção, ações de quantidade e edição por duplo clique.

O header segue a shell global. Nome e título/função vêm do perfil gerido na Administração.

## 5. Tabela principal

A tabela principal suporta:

- filtro/pesquisa por **Máquina**;
- filtro/pesquisa por **Diâmetro**;
- filtro/pesquisa por **Calote**;
- apresentação da quantidade / saldos de categoria opcionais;
- **um clique = selecionar**;
- **duplo clique = editar configuração**;
- disponibilidade atual clara;
- comportamento responsivo (telemóvel/tablet).

Sem contexto de produção, sem Reference, sem Job On.

### Un clique — ações rápidas de quantidade

**UM CLIQUE** numa linha da tabela:

- seleciona a configuração;
- expõe ações rápidas de quantidade.

O operador pode:

- **adicionar** quantidade;
- **remover** quantidade;
- escolher a **categoria/saldo de quantidade** quando relevante.

A operação deve ser simples e inline. Não é necessário navegar para outro módulo ou produção.

### Duplo clique — editar essa configuração

**DUPLO CLIQUE** numa linha abre a configuração/definições **daquela linha**.

A partir desse editor de configuração, o operador pode alterar:

- Diâmetro;
- Calote;
- Máquina / Máquinas;
- outras características configuradas, se disponíveis.

Após guardar:

- a configuração atualizada é mostrada na tabela principal.

Isto é manutenção direta da configuração. **Não** forçar o operador a simular uma edição de configuração através de planeamento de produção.

### Criar nova configuração

O operador pode também **criar uma nova configuração / nova linha da tabela**.

Campos mínimos atuais:

- Diâmetro;
- Calote;
- Máquina / Máquinas.

A nova configuração passa a aparecer na tabela principal.

## 6. Distinguir editar configuração vs transformar quantidade

O comportamento importante é:

1. **EDITAR METADADOS DE CONFIGURAÇÃO** — editar a definição da própria configuração (Diâmetro / Calote / Máquina(s)).
2. **MOVER / TRANSFORMAR QUANTIDADE** — mover alguma quantidade de uma configuração para outra.

**Não confundir estas duas ações.**

A lógica de "transformar quantidade entre origem e destino" pode continuar útil **apenas** quando o operador quer intencionalmente mover uma quantidade de uma configuração para outra. Mas **não pode impedir** a manutenção/edição simples da própria linha de configuração.

## 7. Modelo de quantidade

O requisito central é:

> **QUANTIDADE DISPONÍVEL POR CONFIGURAÇÃO.**

Não são necessários números individuais de tampão. O módulo é um controlo agregado de quantidades.

Preservar:

- quantidades inteiras;
- sem saldos negativos;
- persistência confirmada pelo servidor antes de mostrar sucesso;
- movimentos/histórico append-only sempre que a quantidade muda;
- atribuição de operador e data/hora;
- correções auditáveis.

### Enchidos / Por encher — opcional

**CLARIFICAÇÃO DO OWNER:**

`Enchidos` / `Por encher` continuam a ser **opções disponíveis** para o operador.

- Não são um ciclo de vida obrigatório;
- Não são obrigatórias para todos os registos/configurações;
- Existem para o operador separar quantidades **se útil**.

O operador pode escolher distinguir quantidades, por exemplo:

- Enchidos / Por encher

ou equivalentemente segundo a redação operacional estabelecida na UI final.

A regra-chave é: **ESTA SEPARAÇÃO É OPCIONAL.**

O módulo deve continuar a responder fundamentalmente a:

> "Quantos tampões existem para esta configuração?"

**Não** modelar todo tampão como forçado por: `Por encher → Enchido → outro estado obrigatório`. Sem ciclo de vida obrigatório.

### Maquinados / Por maquinar — opcional

**CLARIFICAÇÃO DO OWNER:**

O operador pode, se útil, separar tampões quanto à disponibilidade de maquinação, por exemplo:

- Maquinados
- Por maquinar

Isto é **classificação opcional** de quantidade.

- Não torná-lo um ciclo de vida obrigatório;
- Não forçar o módulo a conter uma máquina de estados rígida;
- O design deve suportar categorias/saldos de quantidade opcionais em vez de assumir um processo universal obrigatório.

O resultado de negócio importante continua a ser **a disponibilidade total por configuração/máquina**.

## 8. Histórico

Preservar um histórico **simples e auditável** das alterações de quantidade.

Campos úteis do histórico:

- data/hora;
- configuração;
- ação/movimento;
- categoria/saldo quando relevante;
- quantidade;
- antes;
- depois;
- operador.

Se a própria configuração for editada, preservar histórico/auditoria suficiente para saber:

- o que mudou;
- valor anterior;
- valor novo;
- quem mudou;
- quando.

**Nunca sobrescrever silenciosamente factos históricos.**

## 9. Opções / gestão de configuração

O operador pode gerir as características configuráveis usadas por Tampões.

No mínimo:

- Máquina / Máquinas;
- Diâmetro;
- Calote.

Campos e valores devem ser editáveis.

**REGRA DO OWNER:** o operador pode editar os campos de configuração, incluindo Diâmetro, Calote e Máquina / Máquinas, bem como futuros campos de configuração que sejam adicionados. O sistema de configuração permanece flexível/editável. Não fixar o design como se Diâmetro e Calote fossem permanentemente as únicas características possíveis. No entanto, a configuração canónica atual deve incluir visivelmente **Máquina/Máquinas, Diâmetro e Calote**.

O operador pode gerir os valores usados por estes campos.

O módulo pode suportar campos futuros sem invenção de schema-por-UI.

Preservar valores normalizados onde útil.

O operador é o dono/utilizador operacional. **Não** exigir Admin/Responsável para esta gestão normal de configuração de Tampões.

## 10. Estados e mensagens

- sem configuração: indicar que é necessário selecionar uma configuração;
- sem resultados: mensagem de "não foi encontrada uma configuração com estes valores";
- resultado ambíguo: apresentar a lista e exigir seleção explícita;
- quantidade insuficiente: impedir a remoção e indicar o saldo disponível;
- atualização concorrente: recarregar saldos e exigir nova confirmação;
- sem histórico: estado vazio, não erro;
- falha de carregamento: mensagem de erro com ação `Tentar novamente`.

## 11. Regras visuais e mobile

- aplicar tokens do `DMO_DESIGN_SYSTEM.md`;
- botões preenchidos em repouso e invertidos no hover/foco;
- alvos táteis com pelo menos 44 × 44 px;
- campos de diâmetro, calote e quantidade dimensionados para valores curtos;
- teclado numérico em dispositivos móveis;
- no máximo duas casas decimais na apresentação de diâmetro e calote;
- quantidades são inteiros;
- a tabela principal não pode criar scroll horizontal ao nível da página (o contentor da tabela pode rolar internamente onde necessário);
- manter a configuração ativa visível enquanto o operador adiciona ou remove quantidade.

## 12. Integrações e ownership

- Tampões é a autoridade dos seus **saldos, movimentos, configurações e definições**;
- Tampões é dono de: registos de configuração, associação Máquina/Máquinas dentro dessas configurações, Diâmetro, Calote, quantidades, classificações/saldos opcionais, histórico/movimentos de Tampões e definições de configuração de Tampões;
- Tampões **não** é dono de: Job On, Production, Reference, registos-mestre de ferramentas não relacionados, nem registos de negócio de outros módulos;
- definições de campos, configurações e movimentos são entidades distintas;
- operador autenticado e data/hora vêm do servidor;
- cada alteração de quantidade é um movimento append-only atómico, com saldos derivados do servidor (evitar rewrites absolutos do saldo no cliente — risco histórico de lost-update).

## 13. Regras negativas — explícitas

Tampões **NÃO**:

- associa tampões a uma Reference;
- associa tampões a uma Production;
- integra funcionalmente com Job On;
- envia dados para Job On;
- consome contexto de Job On;
- planeia produção;
- reserva stock para produção;
- segue números individuais de tampão;
- exige um ciclo de vida de estado rígido;
- exige que todo tampão use Enchidos/Por encher;
- exige que todo tampão use Maquinado/Por maquinar;
- infere Máquina a partir de Reference;
- infere Reference a partir de Máquina;
- altera outros módulos.

## 14. Questões / detalhes remanescentes

As anteriores questões em aberto do design antigo foram resolvidas pelo Owner:

- ~~"Enchido significa pronto para produção?"~~ → **Substituída** pela classificação de quantidade flexível opcional; sem ciclo de vida obrigatório.
- ~~"A maquinação precisa de saldo próprio (Maquinados)?"~~ → **Fechada:** a classificação opcional de quantidade pode distinguir Maquinados/Por maquinar quando útil; não é obrigatória.
- ~~"O planeamento deve reservar stock depois?"** → **Removida** do modelo funcional atual. Planeamento fora do âmbito.
- ~~"Onde é criada uma nova configuração técnica?"** → **Fechada:** o operador pode criar/editar a configuração no próprio Tampões.
- ~~"Que estados de plano?"** → **Removida.** Sem modelo de planeamento.
- ~~"Que campos comparáveis adicionais?"** → **Fechada** ao nível do modelo: os campos são configuráveis/editáveis; os campos essenciais atuais são Máquina/Máquinas + Diâmetro + Calote.

**Sem perguntas de negócio genuínas em aberto.**

Detalhes menores podem ficar como detalhe de implementação/UI (não bloqueadores de negócio):

- min/max numérico exato;
- controlo exato da UI para seleção de múltiplas máquinas;
- nomeação exata das categorias de quantidade opcionais.

## 15. Critérios de aceitação V1

- a tabela principal mostra **Máquina/Máquinas, Diâmetro e Calote** por configuração;
- a quantidade está visível por configuração;
- **um clique** seleciona a linha e expõe ações rápidas de quantidade;
- a quantidade pode ser **adicionada/removida**;
- pode ser selecionada uma **categoria de quantidade opcional**;
- **duplo clique** abre a edição da configuração;
- Máquina/Máquinas, Diâmetro e Calote podem ser **editados**;
- guardar atualiza a tabela;
- uma nova configuração pode ser **criada**;
- sem Reference;
- sem Production;
- sem Job On;
- sem Planeamento;
- sem ciclo de vida obrigatório;
- o histórico permanece auditável;
- nenhum saldo pode ficar negativo;
- comportamento responsivo/mobile.
