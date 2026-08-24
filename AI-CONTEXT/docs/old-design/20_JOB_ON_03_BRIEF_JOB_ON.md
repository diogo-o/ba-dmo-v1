# Job On — brief de design extraído dos exemplos

Estado: base para discussão e mockup  
Fonte: cinco exemplos visuais de `JOB-ON MOLDES - MG`, procedimento de numeração de desenhos e discussões funcionais existentes.

## 0. Definição funcional

O **Job On é o contexto central de produção/planeamento e a folha onde a equipa consulta toda a informação necessária para executar uma produção**. Não é uma página de gestão de ferramentas nem um formulário que deva parecer permanentemente editável.

O Job On é simultaneamente:

- **contexto central de produção/planeamento** — identifica a produção concreta (Referência, Produção, Máquina/Linha), a revisão exata e o snapshot do que foi decidido;
- **folha operacional** — a representação dessa produção para consulta/edição;
- **hub central de consulta operacional** — para cada produção, é o ponto onde se compreende a produção **como um todo**: planeamento/calendário; Job On e revisão exatos; Referência/Produção/Máquina; ferramentas/lotes exatos; verificações; histórico; impressão/documentos; e os registos associados de **Controlo** e **Reparação Interna**.

**A produção é planeada por Máquina/Linha.** Cada Job On representa uma produção concreta numa **Máquina/Linha** concreta (ex.: `B1`, `B2`, `B3`, `C1`, `C2`, `C3`). Para essa produção, o RESPONSÁVEL escolhe a configuração exata de ferramentas principais — **CM**, **MF** e **BQ** — cada uma com Referência, Lote e Máquina/Linha (todos os campos que façam parte do registo da opção). O Job On persiste exatamente essa configuração escolhida.

O Job On **integra/liga aos registos de Controlo e Reparação Interna associados à produção**, para consulta, **sem se tornar dono desses registos**. Não duplica nem assume a posse dos dados desses módulos:

```
JOB ON            = contexto central de produção + hub de consulta operacional
CONTROLO          = é dono dos registos/resultados de controlo
REPARAÇÃO INTERNA = é dono dos registos de reparação interna
```

Regras base:

- agrega o contexto da produção e as ferramentas escolhidas nos respetivos módulos de domínio;
- não cria nem altera os registos mestres de CM/MP, MF, BQ ou restantes ferramentas;
- abre por defeito em **Modo consulta**, com leitura rápida e hierarquia semelhante a uma folha técnica;
- `Editar folha` ativa campos editáveis apenas para utilizadores autorizados;
- `Guardar alterações` fecha a edição e devolve a folha ao modo de consulta;
- criar ou duplicar prepara um novo rascunho de Job On; não duplica nem cria ferramentas;
- o calendário serve para localizar, planear e abrir produções, não substitui a folha.
- depois de guardado, o Job On é a fonte operacional da produção concreta para Peso e Pegamentos: identifica a Referência, Produção, Máquina e os CM/MP, MF e BQ exatos, incluindo os respetivos lotes.

**Regra de permissões (papéis):**

- **RESPONSÁVEL** — é o ÚNICO perfil/account autorizado a modificar o Job On: pode criar, editar, duplicar, alterar campos de produção/ferramentas, alterar associações CM/MF/BQ, guardar novas revisões e executar as ações de gestão do Job On definidas pelo design.
- **OPERADOR** — pode consultar o Job On, mas não pode editar campos de produção, nem alterar ferramentas/lotes, nem guardar revisões, nem entrar em Modo edição. Pode apenas **confirmar manualmente** os checks de verificação do Job On onde autorizado.

O controlo técnico de acesso é aplicado através de templates/capabilities, mas isso não enfraquece a regra de negócio: **a edição do Job On pertence exclusivamente ao RESPONSÁVEL**.

### Informação que tem de ser interpretada imediatamente

1. Data de início e data de fim;
2. máquina/linha onde a produção trabalha;
3. referência e número de produção;
4. ferramentas principais `MP/CM`, `MF` e `BQ`, com referência e lote;
5. imagem do artigo, quando existir.

Os restantes parâmetros técnicos continuam disponíveis na mesma folha, com contraste secundário, para não competir com os dados operacionais críticos.

## 1. O que os exemplos confirmam visualmente

### Contexto principal da produção

Os cinco exemplos repetem no topo:

- Referência;
- Produção;
- Linha/Máquina;
- Secções;
- Gota;
- Tipo;
- Data de início;
- Data de fim;
- Processo (`NNPB`/`PS` nos exemplos);
- Peso;
- campo curto de paragem.

Referência, Produção e Linha formam o contexto mínimo que deve permanecer visível durante toda a edição.

Quando o Job On apresenta `Processo` (`NNPB`/`PS`), o valor vem do lote criado no módulo Peso. O Job On apenas o mostra no contexto da produção; o operador não redefine o processo nesta folha.

### Ações existentes na folha antiga

- Novo;
- Gravar;
- Replicar;
- Eliminar;
- Exportar;
- Comparar;
- pesquisar Referência;
- percorrer Produções anteriores da Referência.

Estas ações são evidência funcional, não uma aprovação da posição, ícones ou implementação antiga.

### Grupos de informação apresentados

| Código visual | Informação observada nos exemplos |
|---|---|
| MP | referência, lote, tipo, diâmetros, tolerâncias/folgas, stock/máquina, adaptador, inversão, parafuso, 3.ª almofada, reparador, utilização e notas |
| MF | referência, lote, tipo, diâmetros, tolerâncias/folgas, stock/máquina, fundo final, adaptador, inversão, parafuso, reparador, utilização e notas |
| BQ | referência, lote, stock/máquina, utilização e notas |
| PU | referência, versão, stock/máquina e notas |
| CAL | conjunto de medidas/valores operacionais, pinças e quantidade em máquina |
| AN | referência e notas |
| ARR | referência e notas |
| PI | pinças/material, diâmetro e notas |
| CS | referência, furos, tubo, stock/máquina e notas |
| TP | diâmetro PS, referência, bacia PS, stock/máquina e notas |
| FO | tipo, stock/máquina e notas |

Os códigos são mantidos como terminologia visível da fonte. A expansão oficial de cada sigla e a lista final de campos devem ser confirmadas antes do freeze.

### Informação complementar

- notas gerais extensas;
- imagem/desenho do artigo;
- notas específicas por grupo;
- valores de quantidade em stock e quantidade necessária/em máquina;
- referência a reparador e utilização em MP/MF/BQ;
- produções anteriores da mesma referência para consulta/comparação.

## 2. Problemas de UI observados

- todos os campos competem simultaneamente pela atenção;
- hierarquia depende sobretudo do tamanho enorme das siglas;
- labels e inputs são demasiado pequenos;
- elevada densidade dificulta leitura e utilização por toque;
- ações dependem de ícones pouco explícitos;
- pesquisa e histórico estão misturados no topo da edição;
- notas gerais ficam afastadas do contexto que as originou;
- estado atual e informação copiada de uma produção anterior não estão visualmente separados;
- não é claro quais campos são editáveis, calculados ou provenientes de outras áreas;
- scroll horizontal faz parte da página em vez de ficar contido.

## 3. Estrutura proposta

### Tab Planeamento

Landing operacional com o calendário canónico e uma lista associada ao dia selecionado.

O calendário é deliberadamente compacto, com largura aproximada de `300px` em desktop. Não deve dominar a página nem usar a altura disponível como área vazia. O cartão da lista ocupa o restante espaço.

`Criar Job On` pertence ao cabeçalho do cartão da lista/dia selecionado. Não fica isolado no cabeçalho geral da página. Ao clicar, expande o formulário de criação dentro desse mesmo cartão.

#### Comportamento do calendário

- um clique num dia passado mostra as Referências com movimentos/registos de entrada ou saída nesse dia;
- um clique num dia presente mostra os registos desse dia;
- um clique num dia futuro mostra a lista vazia quando ainda não existe planeamento e disponibiliza `Criar Job On para este dia`;
- mudar de mês não escolhe automaticamente um dia;
- o dia selecionado permanece visível no cabeçalho da lista e no formulário de criação;
- dias com atividade usam o indicador discreto do calendário canónico;
- o calendário consulta factos registados; não deduz entradas ou saídas a partir da ausência de um Job On.
- depois de um Job On ser criado e persistido, aparece automaticamente no calendário;
- o calendário referencia o mesmo ID estável do Job On; uma alteração de data atualiza o evento existente e não cria uma cópia.

Cada linha/cartão mostra:

- data;
- Referência;
- Produção;
- Máquina;
- resumo de atenção/preparação quando existir um facto registado.

Interação canónica:

- um clique seleciona;
- duplo clique abre o Job On;
- filtros por período, Referência e Máquina;
- nenhuma seleção automática quando existirem resultados ambíguos.

Ao criar a partir de um dia futuro, a data do novo Job On recebe o dia selecionado. O utilizador pode depois escolher entre:

- `Duplicar anterior` da Referência;
- procurar e duplicar um Job On histórico específico;
- `Novo em branco` quando não existir histórico ou quando o Manager pretender começar sem base.

### Tab Job On

Ordem da página:

1. Contexto fixo da produção.
2. Toolbar de ações.
3. Avisos e tarefas de preparação.
4. Grelha de famílias de ferramentas.
5. Notas gerais.
6. Desenho/visualização técnica.
7. Histórico e comparação.

A folha completa deve estar efetivamente presente no mockup e na implementação. Cartões-resumo não substituem a folha operacional. Para o exemplo atual, a folha apresenta as famílias confirmadas nos ficheiros de origem: `MP`, `MF`, `BQ`, `PU`, `CAL`, `AN`, `ARR`, `PI`, `CS`, `TP` e `FO`, incluindo os respetivos campos, quantidades e notas. Famílias futuras só são adicionadas com evidência do domínio.

No Planeamento, um clique seleciona a linha e um duplo clique abre a vista separada `Folha Job On`. A folha nunca aparece por baixo do calendário ou da lista. Um novo rascunho também abre imediatamente essa vista, preenchida a partir da origem escolhida ou vazia quando é usado `Novo em branco`.

### Imagem do artigo

A Folha Job On disponibiliza um bloco compacto `Imagem do artigo`, próximo do contexto prioritário. O utilizador pode carregar uma imagem e recebe pré-visualização imediata. Quando existir persistência, a interface deve permitir substituir ou remover a imagem com confirmação e auditoria adequada.

A propriedade da imagem já está resolvida em `20_JOB_ON_08_OWNER_DECISION_ARTICLE_IMAGE.md`: a imagem pertence ao artigo/referência mestre, é selecionada pelo utilizador a partir do diretório de imagens do servidor da empresa, a associação pertence à Referência e cada revisão do Job On consome essa imagem na impressão. Não é uma imagem independente por revisão do Job On. A implementação não deve copiar nem substituir imagens entre produções sem uma regra explícita da Referência.

### Hierarquia visual prioritária

O operador deve interpretar a folha em poucos segundos. A ordem visual confirmada é:

1. Data de início e Data de fim.
2. Máquina/Linha onde o Job On trabalha.
3. CM, MF e BQ.
4. Referência, Lote, Quantidade e alertas dentro de CM/MF/BQ.
5. Restantes ferramentas e medidas técnicas.

No cabeçalho da folha, `Máquina`, `Referência` e `Produção` formam um único bloco central de contexto, com valores maiores do que os respetivos rótulos. O título `Folha Job On` e a identificação do módulo usam tipografia reforçada. Este bloco mantém-se centrado em desktop/tablet e reorganiza-se sem perder a associação no mobile.

Os cartões técnicos usam altura determinada pelo conteúdo. A grelha alinha os cartões ao início; um cartão curto nunca é esticado para acompanhar a altura da imagem ou de outro cartão. Cabeçalhos, gaps, campos e notas curtas usam espaçamento compacto, preservando expansão apenas para notas longas e detalhe solicitado.

MP, MF e BQ ficam destacados, respeitando a nomenclatura da folha de origem. `PU`, `CAL`, `AN`, `ARR`, `PI`, `CS`, `TP` e `FO` permanecem sempre visíveis, apenas com contraste secundário. Referência geral, Produção, Secções, Gota e Processo continuam presentes, mas não competem visualmente com Data, Máquina, MP, MF e BQ. A entrada direta do mockup abre a tab `Job On`; Planeamento e calendário ficam numa tab separada.

### Tab Histórico

- pesquisa por Referência, Produção e Máquina;
- intervalo de datas;
- lista canónica;
- duplo clique abre o Job On histórico;
- filtros mantêm resultados explícitos, sem inferir equivalência entre máquinas.

### Definições

Fica alinhada à direita e contém apenas configuração real autorizada, não ações operacionais.

## 4. Contexto fixo da produção

Cartão compacto no topo:

| Grupo largo | Campos compactos |
|---|---|
| Referência | Produção, Máquina, Secções, Gota, Processo, Peso |
| Datas | Início, Fim, Paragem quando aplicável |

Regras:

- Referência é larga;
- Produção, Máquina, Secções e Gota são compactas;
- datas ficam no final da linha;
- o contexto permanece visível ao percorrer os grupos;
- depois de iniciar/finalizar, campos protegidos usam modo de correção auditável em vez de edição silenciosa.

### Data de fim e atualização do calendário

Enquanto o Job On estiver em fabrico, o Manager autorizado pode usar `Alterar data de fim`.

Fluxo:

1. abre um cartão inline compacto;
2. apresenta a data de fim atual;
3. o Manager escolhe a nova data;
4. guarda a alteração;
5. apenas após persistência, o Job On e o calendário são atualizados;
6. a interface mantém o utilizador no mesmo contexto.

Exemplo confirmado: o fabrico estava previsto terminar no dia 6 e passa para o dia 8. O mesmo Job On prolonga/atualiza a sua presença no calendário até ao dia 8.

Auditoria mínima:

- data de fim anterior;
- nova data de fim;
- Manager que alterou;
- data/hora da alteração.

Regras:

- alterar a data de fim não muda a data inicial;
- não cria um novo Job On;
- não reescreve alterações anteriores;
- uma falha mantém a data anterior no Job On e no calendário;
- quando o fabrico terminar, o último valor guardado em `Data de fim` fica como data final do registo;
- depois de fechado, a Data de fim deixa de usar esta edição operacional; qualquer correção posterior segue o fluxo auditável aplicável.

## 5. Cartões das famílias de ferramentas

Cada família usa o mesmo componente visual, mas recebe campos e ações próprias.

### Estado fechado

Mostra apenas:

- código e nome confirmado da família;
- referência;
- lote quando aplicável;
- quantidade em stock / necessária;
- localização ou disponibilidade live quando proveniente da fonte autoritativa;
- utilização quando existir como indicador registado;
- estado curto: completo, atenção ou informação em falta.

### Estado expandido

Ao clicar no cartão ou em `Editar detalhes`:

- expande inline;
- mostra os campos específicos da família;
- apenas um cartão principal fica expandido de cada vez;
- primeiro campo editável recebe foco;
- Guardar valida e persiste antes de fechar;
- Cancelar não altera o estado guardado;
- apenas informação **live** de disponibilidade/localização é read-only e identifica a origem. Depois de um valor ser guardado como parte do snapshot do Job On, o seu campo de produção é editável em `Modo edição` sem alterar a fonte mestre.

Não criar um formulário genérico que obrigue BQ, MP, MF, PU ou acessórios a ter os mesmos campos.

## 6. Toolbar de ações

| Ação | Comportamento proposto |
|---|---|
| Novo Job On | expande cartão de criação do contexto; não cria até guardar |
| Guardar | ação primária; feedback apenas depois de persistir |
| Duplicar anterior | para a Referência do novo fabrico, procura o Job On imediatamente anterior e abre um novo rascunho com essa base |
| Duplicar histórico selecionado | copia o Job On escolhido na pesquisa/histórico da Referência |
| Novo em branco | cria um template vazio para o Manager decidir e preencher as ferramentas quando a Referência nunca teve Job On |
| Comparar | expande seletor de produção anterior, priorizando mesma Referência + mesma Máquina |
| Exportar | disponível após existir registo guardado |

| Eliminar/Arquivar | ação destrutiva autorizada, confirmada e auditável |

Evitar uma fila de ícones sem texto. Usar botões compactos com labels claros.

### Regra funcional de `Duplicar anterior`

Exemplo confirmado: ao preparar para amanhã um Job On da Referência `5774T173`, `Duplicar anterior` usa o Job On anterior dessa mesma Referência.

Regras de interface:

1. O utilizador inicia o novo Job On e identifica a Referência/Produção.
2. `Duplicar anterior` pesquisa o Job On imediatamente anterior da mesma Referência.
3. A aplicação mostra a origem da cópia: Referência, Produção, Máquina e data do Job On anterior.
4. O utilizador confirma e é criado apenas um rascunho; o registo histórico original nunca é alterado.
5. Todos os valores copiados da folha ficam editáveis no novo rascunho para o utilizador com permissão de editar Job On; a origem do valor não bloqueia a edição do snapshot.
6. A cópia só fica guardada depois da ação explícita `Guardar`.

Se não existir Job On anterior, `Duplicar anterior` fica indisponível e a interface explica `Não existe um Job On anterior para esta referência`. A ação `Novo em branco` permanece disponível. Não escolher outra Referência nem inventar uma base alternativa.

### Regra funcional de `Novo em branco`

Usar quando a Referência nunca trabalhou ou não possui Job On anterior.

1. O utilizador informa o contexto confirmado da nova produção.
2. `Novo em branco` cria um rascunho com a estrutura das famílias aplicáveis, mas sem Referências ou lotes de ferramentas escolhidos.
3. O Manager decide o que associar em cada família.
4. A aplicação não copia silenciosamente dados de outra Referência, Máquina ou produção semelhante.
5. O rascunho só se torna registo persistido após `Guardar` com as validações do domínio.

Os campos obrigatórios e as famílias que devem aparecer neste template continuam dependentes das regras confirmadas do programa.

O critério técnico exato de “anterior” deve usar a ordenação canónica do domínio (data/produção e desempate estável), a confirmar com o programador e a fonte de dados.

### Regra funcional de `Duplicar histórico selecionado`

Este fluxo permite usar um Job On mais antigo, mesmo quando não é o imediatamente anterior.

1. O utilizador pesquisa pela Referência.
2. A aplicação apresenta os Job On históricos dessa Referência numa lista canónica, com data, produção e máquina suficientes para distinguir os registos.
3. Um clique seleciona o Job On histórico.
4. Duplo clique abre o Job On histórico para consulta, sem o alterar.
5. O botão externo `Duplicar selecionado` cria um novo rascunho a partir da seleção.

Na duplicação:

- a data do Job On de origem é o único valor que não é reutilizado;
- quando o fluxo começou num dia futuro do calendário, a nova data recebe esse dia;
- sem dia previamente selecionado, a nova data fica por preencher;
- todo o restante conteúdo vem preenchido exatamente a partir do Job On escolhido;
- o registo de origem permanece imutável;
- o novo rascunho identifica qual Job On lhe deu origem;
- nenhuma ferramenta, lote, nota ou valor copiado é atualizado silenciosamente com dados live.

Depois de criar o rascunho, a interface pode apresentar separadamente o estado atual das ferramentas para o Manager decidir o que manter ou substituir. Essa consulta live não reescreve os valores copiados.

## 7. Seleção de lotes em CM, MF e BQ

Ao editar uma família CM, MF ou BQ, a Referência da ferramenta permanece visível e o campo `Lote` funciona como seletor contextual.

**Identidade da opção = Tipo + Referência + Lote + Máquina/Linha.** Referência + Lote **não basta** para distinguir todas as opções registadas: a mesma Referência de ferramenta pode existir para várias Máquinas/Linhas, e o **mesmo número de lote pode existir para a mesma Referência em Máquinas/Linhas diferentes**. Exemplo:

```
CM | Referência 5447 | Lote 3 | B1
CM | Referência 5447 | Lote 3 | C3
```

Estas são **opções de ferramenta distinguíveis**, mesmo partilhando Referência e Lote. Isto **não** deve ser reformulado como «máquina diferente ⇒ lote diferente» — é falso; o lote pode ser igual ou diferente. Para efeitos de registo/consulta operacional, o contexto da opção preserva **Tipo + Referência + Lote + Máquina/Linha**.

**A escolha pertence ao RESPONSÁVEL — sem decisão automática.** Se o Job On estiver a ser preparado para uma Máquina/Linha (ex.: `B1`), o RESPONSÁVEL vê as opções registadas e escolhe a configuração correta para `B1`. A aplicação **não infere** a ferramenta correta a partir da Máquina/Linha, da Referência ou de outro valor; a Máquina/Linha é **contexto visível / informação distintiva / ajuda de filtragem onde já estiver definida**, nunca um seletor automático. O sistema **apoia a decisão do RESPONSÁVEL; não a substitui**. O Job On **nunca substitui silenciosamente** a escolha do utilizador por outra ferramenta.

### Pesquisa da Referência da ferramenta

O campo `Referência da ferramenta` aceita pesquisa direta no registo autoritativo da respetiva família. Exemplo: ao escrever `6185`, a interface consulta as ferramentas CM, MF ou BQ já registadas, conforme o cartão em edição.

- resultados aparecem progressivamente e mostram contexto suficiente para distinguir registos;
- resultados apresentam o Nome técnico junto da Referência;
- escolher uma Referência não escolhe automaticamente o lote;
- depois da escolha, o campo `Lote` carrega os lotes compatíveis com a Referência e a Máquina/Linha do Job On;
- sem correspondência, mostrar `Referência de ferramenta não encontrada`;
- uma pesquisa sem resultado não cria um novo registo;
- a criação da ferramenta continua a pertencer ao respetivo módulo autoritativo;
- a aplicação deve distinguir `não existe no registo` de `existe, mas não possui lote compatível com esta máquina`, quando os dados permitirem essa distinção.

### Origem das opções

As opções do lote são obtidas dos registos já existentes da respetiva ferramenta e filtradas por:

- tipo de ferramenta: CM, MF ou BQ;
- Referência associada à família no Job On;
- Máquina/Linha do novo fabrico.

CM, MF e BQ mantêm fontes, identidade e histórico próprios. O Job On apenas consulta e associa o lote escolhido; não cria nem altera lotes desses módulos.

Cada opção apresentada deve preservar **Tipo + Referência + Lote + Máquina/Linha** (os campos que façam parte do registo da opção), para que a mesma Referência e Lote possam aparecer em Máquinas/Linhas diferentes como **opções distinguíveis** e o RESPONSÁVEL reconheça a correta. A Máquina/Linha pode ser usada como contexto/filtro, mas nunca decide a escolha por si.

### Contrato com Peso e Pegamentos

- A escolha acontece nesta folha, durante a preparação/edição autorizada do Job On.
- **A configuração da produção não é redefinida a jusante:** depois de selecionadas as ferramentas no Job On, os módulos a jusante (Peso, Pegamentos, Controlo, Reparação Interna) **não redefinem de forma independente o ferramental planeado para a produção** — registam os seus próprios dados usando a configuração de produção definida pelo Job On. O Job On é a configuração operacional de produção autoritativa. Identificar **outro lote válido como sujeito da própria regra de domínio** (ex.: o Controlo regista/controla um lote recém-chegado) não configura nem altera o plano de produção do Job On; voltar a escolher o ferramental de produção continua a ser uma ação do RESPONSÁVEL no Job On.
- Cada ferramenta guardada usa um ID estável e um snapshot legível de Referência, Lote **e Máquina/Linha** (quando a Máquina/Linha fizer parte da opção selecionada).
- Exemplo: o Job On da Produção `202601` pode indicar `CM 5447 · Lote 4`; o Novo controlo de Peso dessa produção usa exatamente esse CM/lote.
- **Peso — ferramentas funcionais:** a produção como um todo tem **CM + MF + BQ** selecionados no Job On; porém, o **domínio Peso usa funcionalmente apenas CM + Lote** para o registo de peso (o registo de peso é o peso associado ao CM usado naquela Referência/produção). Distinguir **ferramentas globais da produção (CM + MF + BQ)** de **ferramentas funcionais do Peso (CM + Lote)**. O Peso não volta a selecionar CM, não seleciona MF, não seleciona BQ e não reconstrói as ferramentas da produção.
- Pegamentos usa exatamente os CM/MP, MF e BQ guardados no mesmo Job On.
- Peso e Pegamentos mostram estas ferramentas como contexto herdado, sem oferecer uma segunda seleção.
- Se uma ferramenta obrigatória estiver em falta, eliminada ou incompatível, os módulos consumidores bloqueiam e disponibilizam `Corrigir ferramentas no Job On`.
- Alterar posteriormente uma ferramenta no Job On não reescreve snapshots de controlos históricos já aprovados. Um novo registo usa o contexto válido da revisão corrente do Job On.

### Acessos da produção — Controlo e Reparação Interna

Na produção atual e em qualquer produção histórica aberta, mostrar ações persistentes `Ver Controlo` e `Ver reparações` junto da informação principal do Job On.

- `Ver Controlo` abre a folha correspondente a `job_on_id + job_on_revision_id`;
- `Ver reparações` abre a consulta de Reparação Interna filtrada pelo `job_on_id`, Produção e Linha abertos;
- um perfil de chefia autorizado consulta todos os registos dos turnos dessa Produção em modo read-only;
- o reparador continua limitado aos próprios registos na entrada operacional normal;
- a Referência isolada nunca é usada para conciliar, porque pode repetir-se em várias Produções;
- regressar ao Job On mantém a mesma Produção/revisão selecionada.

Estes acessos concretizam o papel do Job On como **hub central de consulta operacional**: o Job On **integra/liga** os registos de Controlo e de Reparação Interna associados à produção, para que a produção seja compreendida como um todo. **Não os assume nem os duplica** — Controlo continua a ser o dono dos registos/resultados de controlo e a Reparação Interna continua a ser a dona dos registos de reparação; o Job On apenas liga a respetiva associação à produção.

**Herança, sem reconstrução, com distinção entre sujeito-de-controlo e ferramental-de-produção.** O **Controlo** recebe/conserva/apresenta o **resumo de ferramentas já selecionado no Job On** (`CM + Lote`, `MF + Lote`, `BQ + Lote`) e **não pede** ao utilizador para reconstruir manualmente o contexto de produção; a Máquina/Linha é preservada onde fizer parte da opção. O Controlo não reconfigura a produção. Porém, o Controlo pode **selecionar/identificar outro lote de ferramenta válido como sujeito de um registo de controlo** quando isso for exigido pela sua própria regra de domínio (ex.: controlo de um lote recém-chegado que ainda não é o lote planeado no Job On); essa identificação **não altera o Job On** e não é uma seleção do ferramental de produção — alterar o ferramental planeado é uma ação do RESPONSÁVEL no Job On. A **Reparação Interna** associa os seus registos ao **Job On / contexto de produção exato** e **não reconstitui nem decide independentemente** a configuração de ferramentas da produção (repara apenas CM e MF; BQ nunca).

### Controlo da produção — documentos

Na produção atual e em qualquer produção anterior aberta, apresentar `Ver Peso`, `Ver Pegamentos` e `Ver Resumo`. Os acessos resolvem o manifesto documental pelo `job_on_id + job_on_revision_id` aberto e usam o workspace único criado pelo Controlo. Nunca procurar por texto da Referência ou nome aproximado. Estados: `Disponível`, `Ainda não gerado`, `A aguardar aprovação`, `Workspace indisponível`, `Ficheiro em falta` e `Versões disponíveis`. Cada ação abre primeiro o snapshot/página da aplicação; daí pode `Imprimir / Exportar PDF` ou abrir o PDF físico já gerado. Ver `OWNER_DECISION_SHARED_PRODUCTION_DOCUMENTS.md`.

### Consulta de disponibilidade durante a edição

O Job On possui dois modos com fronteira explícita:

| Modo | Objetivo | Ferramentas |
|---|---|---|
| `Modo consulta` | ler a folha necessária à produção | não permite adicionar, retirar, substituir, duplicar ou editar campos |
| `Modo edição` | preparar/corrigir o Job On | permite editar todos os campos da folha, duplicar e substituir associações de ferramentas |

O indicador de modo deve ser imediatamente distinguível sem usar cores agressivas: azul-cinza suave em `Modo consulta` e âmbar/castanho suave em `Modo edição`, sempre com contraste de texto suficiente.

Job On é a landing page de todos os utilizadores autenticados operacionais. O Administrador puro é a exceção: entra diretamente em `/admin` e não recebe Job On nem módulos operacionais. Todos os utilizadores operacionais podem abrir a folha em `Modo consulta`; apenas o papel/template técnico Responsável recebe a capability de entrar em `Modo edição`, criar/duplicar Job Ons e gerir Definições. O título livre do perfil não concede esta capability. Confirmar verificações é uma ação operacional separada e não abre os restantes campos para edição.

- A informação live de disponibilidade não ocupa nem altera a folha em `Modo consulta`.
- Em `Modo edição`, todos os campos visíveis do Job On ficam editáveis, incluindo contexto da produção, quantidades, notas e todas as famílias secundárias: PU, CAL, AN, ARR, PI, CS, TP e FO.
- O contexto editável inclui Referência, Produção, Máquina/Linha, Secções, Gota, Tipo, Data início, Paragem, Data fim, Processo e Peso. Guardar cria uma nova revisão; não altera a revisão anterior.
- Em CAL, editar valores e quantidades por elemento; em PI, editar Pinças, Diâmetro e Notas; a mesma regra aplica-se aos campos equivalentes das restantes famílias.
- Opções evolutivas de dropdown, como o material das Pinças de PI, não ficam hardcoded. Definições permite adicionar, editar, ordenar e desativar opções para cada Família/Campo. A regra aplica-se aos campos equivalentes de todos os cartões.
- Desativar uma opção impede novas escolhas, mas preserva o valor em Job Ons e revisões antigas.
- Em `Modo edição`, usar `Alterar` no cartão CM/MP, MF ou BQ abre uma lista de seleção já filtrada pela Referência da ferramenta e pela Máquina do Job On.
- O utilizador pode refinar os filtros por lote, localização/contexto operacional, estado técnico e disponibilidade.
- Cada resultado mostra pelo menos: Referência, Lote, Nome técnico quando existir, Máquina compatível, Posição atual, Localização/contexto (`Armazém`, `Produção`, `Reparação` ou não registada), Estado técnico (`Novo`, `Reparado`, `Por reparar`), `% de uso` e disponibilidade.
- Exemplo legível: `CM 5447 · Lote 3 · Posição 2421 · Por reparar · 38% uso` ou `Fora — em reparação`.
- `Posição` e localização vêm do Armazém; estado técnico e `% de uso` vêm do domínio da ferramenta. O Job On agrega estes dados em leitura e não os copia como propriedades próprias.
- Um clique seleciona uma opção; duplo clique abre a ficha/histórico da ferramenta no módulo autoritativo; `Associar lote selecionado` confirma a substituição no rascunho do Job On.
- Pesquisar ou selecionar não cria movimento de Armazém e não reserva fisicamente a ferramenta. Qualquer saída continua a ser registada pelo fluxo próprio do Armazém.
- **Finalidade operacional do Armazém no planeamento:** o Armazém diz ao Job On **onde está fisicamente a ferramenta** necessária (CM/MF/BQ onde suportado) e o Job On usa essa informação para **planear a produção**. Ao preparar uma produção futura, o RESPONSÁVEL precisa de saber, para o lote exato: onde está a ferramenta; posição/localização no armazém; se está presente; se está em produção; se está fora para reparação; se já regressou; disponibilidade; e se há algo que exija atenção antes do início. Selecionar/associar uma ferramenta no Job On não cria movimento de Armazém nem a reserva. Os movimentos físicos continuam a ser operações do Armazém.
- Ao guardar o Job On, persistir o ID/lote escolhido e o snapshot completo da revisão. A localização/estado live continuam consultáveis e podem mudar sem reescrever o Job On histórico.
- Localização, estado, compatibilidade ou `% de uso` geram informação/aviso, mas não bloqueiam a associação nem a gravação do rascunho. A decisão final pertence ao utilizador autorizado.

## 7.1 Ownership e persistência do snapshot

### Base de dados do Job On

Cada Job On/revisão guarda uma fotografia completa e autónoma da produção:

- `jobOnId`, Referência, Produção, Máquina, datas, Secções, Gota, Processo apresentado e restantes campos de contexto;
- origem da cópia (`copiedFromJobOnId`) quando foi duplicado;
- para CM/MP, MF e BQ: ID estável da ferramenta/lote associado **e** snapshot dos valores apresentados/usados naquela produção;
- conteúdo completo de PU, CAL, AN, ARR, PI, CS, TP e FO;
- linhas de CAL, incluindo Elemento, Valor e Quantidade em máquina;
- PI, incluindo tipo/material das Pinças, Diâmetro e Notas;
- quantidades, parâmetros, notas gerais, verificações/ocorrências e imagem/ligação conforme o contrato final;
- revisão, autor, datas de criação/alteração e auditoria.

Exemplo obrigatório: se `Job On 202601 · Referência 5447T173` usa uma configuração específica de PI, essa configuração fica gravada no snapshot do Job On 202601. Não depende de voltar a consultar o valor atual de PI para reconstruir a folha.

### Bases de dados das ferramentas e do Armazém

Permanecem fora do Job On:

- identidade mestre da ferramenta, Nome técnico, desenho, lotes e máquinas permitidas;
- estado técnico atual, `% de uso`/vida e histórico de reparações;
- posição/localização atual e movimentos do Armazém;
- restantes dados mestre que pertencem ao domínio CM, MF, BQ ou outro módulo autoritativo.

Editar um campo do snapshot do Job On não altera a ficha mestre, o estado técnico, a vida, a posição nem o histórico da ferramenta. Para mudar a associação concreta de CM/MF/BQ, o utilizador escolhe outra ferramenta/lote na lista live; para mudar valores de produção como PI, CAL, pinças ou calibres, edita diretamente o snapshot.

### Duplicação sem bloqueios

Ao duplicar `Job On 202601` para `202602`:

1. copiar a fotografia completa de todos os grupos e linhas, não apenas IDs de ferramentas;
2. atribuir novo `jobOnId`, Produção/datas novas e `copiedFromJobOnId`;
3. abrir imediatamente em `Modo edição`;
4. permitir alterar qualquer campo, incluindo Pinças, PI, CAL/calibres, quantidades, notas e associações CM/MF/BQ;
5. mostrar disponibilidade atual como ajuda, sem substituir valores nem bloquear a gravação;
6. guardar um novo snapshot independente. O Job On 202601 permanece imutável.

Não recalcular nem “atualizar” automaticamente os valores copiados a partir das bases mestre. O utilizador decide o que mantém e o que altera.

Mesmo quando se corrige o próprio Job On original, não substituir a revisão anterior: `Guardar alterações` cria uma nova revisão. A consulta do histórico deve conseguir abrir qualquer revisão e mostrar os valores exatos então guardados. Registos e documentos emitidos mantêm ligação ao `job_on_revision_id` usado.

O histórico principal organiza os Job Ons pela Referência e apresenta as respetivas Produções. Selecionar uma Referência deve permitir percorrer `202601`, `202602`, etc.; um clique seleciona a Produção e duplo clique abre a folha. O histórico de revisões dessa Produção fica dentro da folha/histórico detalhado e não se mistura com a lista de Produções.

O esquema técnico canónico está em `JOB_ON_DATA_MODEL.md`. Usa um cabeçalho de Job On, revisões imutáveis, componentes, campos tipados e linhas repetíveis para CAL. Não depende de reproduzir fotograficamente a folha antiga nem de guardar os detalhes num bloco opaco.

View model ilustrativo da lista de edição — não é uma imposição de esquema de base de dados:

```json
{
  "toolId": "cm-5447-l3",
  "family": "CM",
  "reference": "5447",
  "lot": "3",
  "technicalName": "Contra-molde 5447",
  "compatibleMachines": ["B1", "B3"],
  "location": {
    "context": "warehouse",
    "position": "2421",
    "source": "warehouse"
  },
  "technicalState": {
    "condition": "Por reparar",
    "usagePercent": 38,
    "source": "tool-domain"
  }
}
```

O cliente deve mostrar loading, vazio e erro por fonte. Se o domínio da ferramenta responder mas o Armazém falhar, pode mostrar os dados técnicos e `Localização indisponível`; nunca converter falha de consulta em `Não está no Armazém`.

### Dois níveis de interação

1. **Dropdown rápido:** clicar no campo `Lote` mostra diretamente os lotes compatíveis.
2. **Lista completa:** clicar no ícone/área de pesquisa do seletor, ou em `Ver todos os lotes compatíveis`, abre uma lista com mais contexto.

A lista completa segue o padrão global:

- um clique seleciona a linha;
- duplo clique abre o registo completo desse lote no respetivo módulo;
- o botão externo `Associar lote selecionado` confirma a seleção e regressa ao Job On;
- pesquisa e filtros não selecionam automaticamente um resultado;
- a linha apresenta pelo menos Referência, Lote e Máquina/Linha; outros campos só entram quando existirem na fonte autoritativa;
- a linha apresenta também o Nome técnico para distinguir ferramentas semelhantes;
- a seleção atual fica visualmente marcada.

### Estados do seletor

- **Um ou mais lotes compatíveis:** mostrar as opções, sem escolher automaticamente.
- **Nenhum lote compatível:** mostrar `Nenhum lote registado para esta referência e máquina` e ligação para abrir o módulo de origem, se o utilizador tiver permissão.
- **Valor copiado continua compatível:** manter selecionado e marcar como proveniente do Job On anterior.
- **Valor copiado deixou de ser compatível:** manter visível como valor anterior e mostrar atenção; permitir guardar se o utilizador autorizado decidir mantê-lo.
- **Erro de carregamento:** não apresentar lista vazia como se fosse um resultado válido; mostrar erro e permitir tentar novamente.

### Informação no cartão fechado

Depois da escolha, o resumo da família mostra:

- Referência;
- Lote selecionado;
- Máquina/Linha associada;
- origem `Copiado do anterior` ou `Alterado neste Job On`, quando aplicável.

Não usar um campo de texto livre para o lote quando o registo já existe no módulo autoritativo.

## 8. Comparação com produção anterior

A comparação deve separar:

### Snapshot da produção anterior

- ferramentas/lotes usados;
- máquina;
- valores e notas registados naquele momento;
- problemas e instruções aplicáveis naquele momento.

### Estado atual

- disponibilidade/localização atual;
- reparações posteriores;
- utilização atual quando disponível;
- informação atual de Controlo/Reparação relevante;
- valores atualmente propostos para o novo Job On.

Valores anteriores são sugestões/candidatos. O Responsável decide reutilizar, substituir ou verificar; copiar não transforma o histórico em verdade atual.

## 9. Notas, instruções e alertas

- Notas específicas permanecem no cartão da respetiva família.
- Observações verificáveis seguem o contrato `20_JOB_ON_05_BRIEF_VERIFICATIONS.md` (neste mesmo módulo).
- As regras são configuradas na tab `Verificações` da ficha da ferramenta/lote.
- As frequências V1 são `Uma vez no lote` e `Por fabrico`.
- O Job On apresenta as ocorrências e permite ao operador fazer o check; não edita a configuração da ferramenta.
- Confirmar esconde das pendentes, preserva o histórico e guarda operador/data.
- Notas gerais ficam num cartão próprio, com área de texto ampla.
- Instruções recorrentes aparecem apenas quando foram explicitamente registadas para o contexto aplicável.
- Alertas distinguem informação, atenção e bloqueio real.
- Todo o alerta explica a ação necessária.
- Percentagem de utilização é contexto; não bloqueia nem diagnostica automaticamente a ferramenta.

## 10. Desenho/visual técnico

- Imagem do artigo fica num cartão lateral no desktop e abaixo do contexto em ecrãs menores.
- Clique abre visualização maior.
- O desenho deve indicar versão/origem quando disponível.
- A UI não tenta interpretar automaticamente códigos de desenho para criar relações operacionais.
- O PDF de numeração é referência documental; não deve ser usado como regra automática sem contrato confirmado.

## 11. Tarefas de acompanhamento

Padrão candidato vindo das discussões:

- Responsável cria ação/verificação;
- escolhe operador registado;
- descreve a tarefa;
- associa ao Job On quando aplicável;
- operador vê a tarefa em destaque;
- operador marca como concluída;
- Job On mostra autor e data/hora de atribuição e conclusão.

Estados exatos, comentário de conclusão e possibilidade de reatribuição continuam por confirmar.

## 12. Responsividade

Desktop:

- contexto numa linha;
- famílias numa grelha de 3 ou 4 colunas de resumos;
- cartão expandido ocupa a largura disponível;
- desenho/notas podem usar coluna lateral.

Tablet:

- grelha de 2 colunas;
- contexto divide em duas linhas;
- toolbar pode quebrar linha.

Mobile/PWA:

- uma coluna;
- contexto essencial sempre primeiro;
- cartões fechados compactos;
- edição de uma família de cada vez;
- sem scroll horizontal na página.

## 13. Questões que precisam de confirmação

- significado oficial das siglas visuais e respetivos nomes apresentados;
- campos obrigatórios por família;
- autoridade/origem de cada campo;
- quem cria, edita, replica, finaliza e elimina;
- estados reais do Job On;
- diferença exata entre stock e quantidade em máquina/necessária;
- relação de `Tipo` com processo;
- que ferramentas são obrigatórias por produção;
- regras oficiais de compatibilidade/apertos;
- se e como o desenho é obtido;
- quais ações do acompanhamento são obrigatórias.
- ordenação canónica usada para determinar o Job On imediatamente anterior;
- campos mínimos que identificam de forma inequívoca um Job On histórico na lista de duplicação;
- origem autoritativa dos movimentos de entrada/saída apresentados pelo calendário;
- estado dos lotes que os torna elegíveis no seletor (por exemplo, ativos, disponíveis ou também históricos);
- campos adicionais necessários na lista completa de lotes compatíveis.
- se as verificações precisam de prioridade, comentário de conclusão ou anulação.

Até confirmação, o mockup pode mostrar estes elementos como estrutura visual, mas não deve implementar regras automáticas.

## 14. Critérios de aceitação do futuro mockup

- preserva toda a informação relevante dos exemplos sem repetir a grelha antiga;
- Referência, Produção e Máquina permanecem visíveis;
- famílias são identificáveis no estado fechado;
- editar expande inline;
- lista/histórico segue clique e duplo clique canónicos;
- comparação separa snapshot e live;
- notas gerais e específicas não se confundem;
- nenhum código técnico é inferido automaticamente;
- estados e alertas explicam ações;
- funciona sem scroll horizontal da página.
- `Duplicar anterior` nunca altera o Job On de origem;
- duplicar um Job On histórico substitui apenas a data; todos os outros valores partem da cópia escolhida;
- selecionar um dia futuro aplica essa data ao novo rascunho;
- um dia passado apresenta os registos de entrada/saída associados sem alterar dados;
- `Novo em branco` nunca copia dados de outra Referência por aproximação;
- pesquisar `6185` ou outro código consulta o módulo autoritativo e nunca cria uma ferramenta implicitamente;
- CM, MF e BQ só apresentam lotes fornecidos pelo respetivo módulo e compatíveis com Referência + Máquina/Linha;
- um lote copiado incompatível nunca é substituído silenciosamente.
- verificações por lote reaparecem apenas para um ID de lote novo e nunca perdem o histórico confirmado.
