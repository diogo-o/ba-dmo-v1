# CONTROLO — MODELO FUNCIONAL

OPEN OWNER QUESTIONS: NONE

## Índice

1. [Visão Geral](#1-visão-geral)
2. [Acesso e Variantes](#2-acesso-e-variantes)
3. [Relação com Job On](#3-relação-com-job-on)
4. [Controlo Operador / Controlador](#4-controlo-operador--controlador)
5. [Controlo Responsável](#5-controlo-responsável)
6. [Peso](#6-peso)
7. [Pegamentos](#7-pegamentos)
8. [Resumo / Folha de Controlo](#8-resumo--folha-de-controlo)
9. [Decisões e Aprovações](#9-decisões-e-aprovações)
10. [Histórico](#10-histórico)
11. [Documentos](#11-documentos)
12. [Regras Não-Bloqueantes](#12-regras-não-bloqueantes)
13. [Ownership e Relações Cross-Module](#13-ownership-e-relações-cross-module)

## 1. Visão Geral

O CONTROLO é um módulo lógico único. Ele concentra as atividades de medição, verificação, registo, revisão e decisão funcional relacionadas com o controlo de produção. Não é um conjunto de módulos independentes, mas sim uma área funcional coerente, organizada em áreas internas especializadas.

As áreas internas do CONTROLO são:

*   **Peso** — controlo de Capacidade/Volume e peso do vidro por CM;
*   **Pegamentos** — controlo dimensional de componentes;
*   **Resumo / Folha de Controlo** — visão consolidada das peças controladas, avaliação técnica e decisão;
*   **Histórico** — preservação dos registos, decisões, eventos e contexto de controlo.

A **Comparação** não é um módulo separado. Ela é um fluxo de trabalho e um tipo de registo dentro do Peso. Funcionalmente, pertence ao Peso, mas representa uma utilização durante produção, complementar ao controlo inicial.

O CONTROLO existe para apoiar a avaliação técnica e a decisão humana. O sistema calcula, compara, alerta, organiza e preserva histórico, mas não substitui a decisão final do Responsável. Resultados técnicos, médias, alertas dimensionais ou validações automáticas não podem ser interpretados como autorização automática de produção.

## 2. Acesso e Variantes

O acesso ao CONTROLO é determinado pela atribuição funcional do módulo. Um utilizador só entra no CONTROLO se tiver acesso ao módulo.

A experiência dentro do CONTROLO varia conforme o perfil/função do utilizador. Estas variantes não são módulos diferentes; são formas diferentes de usar o mesmo módulo.

### Operador / Controlador

O Operador / Controlador utiliza sobretudo a experiência de medição e registo. A sua função principal é preparar e preencher os dados técnicos:

*   preparar e preencher o controlo;
*   registar medições;
*   editar o Resumo / Folha de Controlo onde permitido;
*   registar OK/NOK técnico;
*   adicionar observações/comentários;
*   adicionar/atualizar/abrir ligação MCaliper onde aplicável;
*   submeter explicitamente o Resumo / Folha de Controlo para revisão.

O Controlador não é a autoridade final de produção. O seu OK/NOK é um resultado técnico.

### Responsável

O Responsável utiliza sobretudo a experiência de revisão, aprovação e decisão. A sua função principal é analisar o que foi preparado pelo Controlador e tomar a decisão funcional adequada.

O Responsável não é simplesmente um Operador com botões adicionais. É uma variante com responsabilidade própria:

*   revê a Folha de Controlo;
*   aprova ou rejeita a folha submetida;
*   decide individualmente CMs medidos em Comparação;
*   aprova ou rejeita o controlo inicial antes de produção;
*   toma a decisão final de produção no âmbito do controlo.

A distinção entre as duas variantes é funcional. O Controlador prepara e regista; o Responsável decide.

## 3. Relação com Job On

O contexto de produção/revisão usado pelo CONTROLO provém do Job On. Esta relação é de consumo, não de duplicação ou substituição.

O Job On é proprietário do contexto de produção: planeamento, revisão, configuração produtiva e contexto herdado de ferramentas/componentes. O CONTROLO consome esse contexto para criar e ancorar os seus próprios registos de controlo.

### Entrada no Controlo

Selecionar ou abrir um Job On não abre automaticamente o CONTROLO. O contexto do Job On só é usado pelo CONTROLO quando um utilizador com acesso ao CONTROLO entra efetivamente no módulo.

Isto significa que:

*   o Job On pode estar selecionado noutro contexto sem que o CONTROLO esteja ativo;
*   o CONTROLO não assume automaticamente o contexto apenas porque existe um Job On aberto;
*   quando o utilizador entra no CONTROLO, o módulo passa a usar o contexto exato do Job On;
*   Peso, Pegamentos, Resumo / Folha de Controlo e Histórico partilham esse mesmo contexto.

O CONTROLO não tem um seletor independente de Job On. O contexto vem da relação funcional com o Job On.

### Estado “Nenhum Job On carregado”

Quando não existe um contexto de produção válido disponível para o CONTROLO, o módulo pode apresentar “Nenhum Job On carregado”.

Este estado é um estado de contexto do CONTROLO. Não é um estado global da aplicação.

Ele indica que, dentro do CONTROLO, não existe atualmente um contexto de Job On disponível para controlo. Não significa necessariamente que não exista nenhum Job On aberto noutra área da aplicação.

### Planeamento e contexto produtivo

O Job On mantém a autoridade do planeamento de produção. O CONTROLO não reconstrói, não redefine e não substitui o planeamento do Job On.

O CONTROLO também não cria um sistema paralelo de planeamento. Ele apenas regista controlos sobre o contexto que lhe é apresentado.

### Contexto herdado de ferramentas/componentes

O contexto produtivo pode incluir ferramentas/componentes previstos, como CM, MF, BQ, PU, CS e outros elementos relacionados com a produção. O CONTROLO usa esse contexto herdado para identificar o que está a ser controlado.

Existem duas situações funcionais importantes.

### Caso A — controlar as ferramentas previstas

No Caso A, o utilizador controla as ferramentas/componentes previstos no contexto de produção. Esta é a situação normal: o controlo é feito sobre o conjunto produtivo esperado pelo Job On.

### Caso B — controlar outro lote válido sem alterar o Job On

No Caso B, o utilizador pode controlar outro lote válido. Esta operação é um ato de controlo, não uma alteração do Job On.

Controlar outro lote não significa selecionar esse lote como ferramenta oficial de produção do Job On. Também não altera o planeamento, a configuração produtiva ou a revisão do Job On.

O Controlo pode registar que foi controlado outro lote válido, mas o Job On permanece com o seu contexto próprio. Não existe inferência automática CM↔MF, nem substituição silenciosa do lote previsto.

### Regras negativas importantes da relação com Job On

*   O CONTROLO não redefine o planeamento do Job On.
*   O CONTROLO não cria um planeamento paralelo.
*   O CONTROLO não transforma controlo de lote em seleção de ferramentas de produção.
*   O CONTROLO não infere CM↔MF automaticamente.
*   O CONTROLO não altera a configuração produtiva do Job On.
*   O CONTROLO não é proprietário de PU/CS/TP/Pinças/Calibres enquanto configuração de produção.

## 4. Controlo Operador / Controlador

O Operador / Controlador é responsável pela preparação técnica dos registos de controlo. A sua atuação ocorre antes da decisão do Responsável.

No âmbito do Peso, o Operador / Controlador regista as medições necessárias para calcular Capacidade/Volume e peso do vidro. No âmbito dos Pegamentos, regista ou apresenta as medições dimensionais relevantes. No âmbito do Resumo / Folha de Controlo, preenche a avaliação técnica por peça.

A função do Controlador inclui:

*   preparar e preencher o controlo;
*   registar medições;
*   editar o Resumo / Folha de Controlo onde permitido;
*   registar OK/NOK técnico;
*   adicionar observações/comentários;
*   adicionar/atualizar/abrir ligação MCaliper onde aplicável;
*   submeter explicitamente o Resumo / Folha de Controlo para revisão.

A submissão é um ato explícito. A folha não é considerada submetida apenas por ter dados preenchidos. Enquanto estiver em Rascunho, pode ser editada. Depois de submetida, entra no circuito de decisão do Responsável.

## 5. Controlo Responsável

O Responsável é a variante de decisão dentro do CONTROLO. A sua função não é apenas visualizar resultados, mas analisar, aprovar, rejeitar ou reabrir conforme o estado funcional do controlo.

No controlo inicial de Peso, o Responsável aprecia o conjunto controlado antes de produção. A aprovação inicial é geral para o conjunto controlado. Essa aprovação não transforma automaticamente qualquer medição em decisão final permanente, mas estabelece a aceitação funcional inicial.

Em Comparação, o Responsável decide individualmente cada CM medido. Cada CM selecionado/medido precisa de uma decisão explícita antes da confirmação. Se um CM for posto de parte, deve existir justificação funcional.

No Resumo / Folha de Controlo, o Responsável apenas decide folhas submetidas. Uma folha em Rascunho não está pronta para aprovação ou rejeição. A decisão ocorre após submissão explícita do Controlador.

O Responsável pode:

*   aprovar a folha submetida;
*   rejeitar a folha submetida;
*   reabrir uma folha submetida ou já decidida;
*   devolver a folha a Rascunho para edição;
*   preservar o histórico das ações anteriores.

A decisão do Responsável é final no contexto funcional do controlo, mas continua a ser uma decisão humana. O sistema suporta a decisão; não a substitui.

## 6. Peso

O Peso é a área do CONTROLO responsável pelo controlo de Capacidade/Volume e peso do vidro por CM. O seu propósito é fornecer valores técnicos fiáveis, individuais e comparáveis, para apoiar o controlo antes e durante a produção.

O Peso distingue dois momentos principais:

*   **Controlo inicial**, antes de produção;
*   **Comparação**, durante produção.

Ambos usam o mesmo modelo de cálculo, mas têm finalidades funcionais diferentes.

### Entradas do Peso

O cálculo e o registo do Peso dependem de várias entradas funcionais. Estas entradas devem ser preservadas como informação relevante do controlo.

Entradas principais:

*   peso de água;
*   estado do molde;
*   temperatura da água;
*   peso nominal;
*   dados anteriores de SAP ou de produção final anterior, quando estabelecido;
*   notas/observações;
*   valores relacionados com o processo aplicável;
*   valores de referência/desenho técnico.

Estas entradas não têm todas o mesmo papel. Algumas alimentam diretamente os cálculos, outras servem para contextualizar ou complementar o registo funcional.

### Temperatura

A temperatura da água é usada no cálculo da Capacidade/Volume. A tabela de temperatura tem um intervalo canónico suportado de 5–35 °C.

O valor da tabela correspondente à temperatura é usado como divisor no cálculo da Capacidade/Volume.

### Emparelhamento posicional

O número do CM é um identificador. Não é a chave de emparelhamento.

As leituras de água do mesmo controlo são emparelhadas/relacionadas pela sua posição na linha/tabela de leitura. Esta regra usa a posição da leitura/tabela; não infere o emparelhamento automaticamente pelo número do CM.

Esta regra não deve ser confundida com a associação entre CM atual e CM anteriormente aprovado em Comparação. Em Comparação, essa associação é explícita/validada. Não é inferida por emparelhamento posicional, nem simplesmente pelo número do CM.

### Fórmula de Capacidade / Volume

    Capacidade / Volume do CM = Peso de água ÷ valor da tabela de temperatura

A Capacidade/Volume do CM é um valor de primeira classe por CM. Não é apenas um passo intermédio. Deve permanecer visível e relevante como resultado do controlo.

### Fórmula do peso do vidro

    Peso do vidro = (Capacidade do CM + Volume da Marisa/BQ − Volume do Punção/PU) × Densidade do vidro

O peso do vidro também é um valor de primeira classe por CM. A regra antiga baseada apenas em comparação por peso do vidro foi substituída. Atualmente, tanto Capacidade/Volume como peso do vidro são valores relevantes.

### Origem dos volumes e da densidade

O Volume da Marisa/BQ e o Volume do Punção/PU provêm de dados técnicos de referência, como desenho técnico ou dados de referência aplicáveis. Eles não são inventados no ato do controlo.

A densidade do vidro depende do processo aplicável, por exemplo NNPB ou PS. O processo aplicável determina o valor de densidade funcionalmente adequado.

O CONTROLO consome estes valores de referência. Não é proprietário dos dados técnicos de origem, mas usa-os para calcular o resultado do Peso.

### Apresentação decimal

Onde estabelecido, aplica-se a regra de apresentação com máximo de duas casas decimais. Esta regra não altera o cálculo funcional, mas define a apresentação do resultado quando aplicável.

### Controlo inicial

O controlo inicial acontece antes da produção. O seu objetivo é avaliar o conjunto controlado antes de este ser considerado funcionalmente aceitável para produção.

No controlo inicial:

*   existe Capacidade/Volume individual por CM;
*   existe peso do vidro individual por CM;
*   existe uma média global do peso do vidro como informação adicional de comparação;
*   a média não substitui os valores individuais;
*   o conjunto é enviado para apreciação do Responsável;
*   o Responsável aprova ou rejeita de forma geral antes de produção.

A média é informativa e comparativa. Ela não pode ocultar um resultado individual problemático. A aprovação inicial é geral para o conjunto controlado, mas não elimina a necessidade de decisões individuais onde o fluxo funcional as exige, como em Comparação.

### Comparação

A Comparação ocorre durante produção. Ela é complementar ao controlo inicial e não substitui o controlo anteriormente aprovado.

Na Comparação:

*   podem ser medidos um ou mais CMs;
*   não é obrigatório medir todos os CMs;
*   o processo de cálculo é o mesmo do Peso;
*   cada CM selecionado/medido exige uma decisão explícita;
*   todos os CMs medidos precisam de decisão antes da confirmação;
*   se pelo menos um CM for posto de parte, é necessária justificação;
*   o registo de Comparação preserva rastreabilidade histórica.

A Comparação não altera o controlo base previamente aprovado. O histórico permanece separado e rastreável. Se um registo de Comparação revelar um problema, esse problema fica registado e pode ser decidido pelo Responsável, mas não reinterpreta ou apaga o controlo anterior.

### Tampão / Calote

O Tampão/calote pertence ao contexto de referência do Peso. É uma terceira dimensão técnica informativa, distinta do Punção/PU.

A sua fórmula é:

    Volume do Tampão = π × s² × (3r − s) / 3

onde:

*   s = sagitta/profundidade;
*   r = raio.

O resultado do Tampão/calote pode ser apresentado para consulta, com a regra de apresentação aplicável quando estabelecida.

Este valor existe para apoio técnico e consulta. Ele não é parte do cálculo principal do peso do vidro e não altera os resultados funcionais do Peso.

O Tampão/calote não altera:

*   o resultado do peso do vidro do CM;
*   o resultado individual do CM;
*   a média geral;
*   a Capacidade/Volume;
*   o cálculo de aprovação;
*   o cálculo de Comparação.

Também não deve ser confundido com o Punção/PU. O Punção/PU entra na fórmula do peso do vidro; o Tampão/calote é apenas informativo.

O Tampão/calote do Peso não deve ser confundido com TP/Tampão do Job On. TP/Tampão no Job On é configuração produtiva/ferramenta específica da produção. O Tampão/calote no Peso é um valor técnico informativo calculado.

## 7. Pegamentos

Os Pegamentos são a área do CONTROLO responsável pelo controlo dimensional de componentes. A lógica funcional parte de uma secção circular, com medições em dois eixos perpendiculares.

### Eixos e medições

*   Costura = 0°
*   Contra costura = 90°

Os dois eixos são perpendiculares. As medições são registadas por linha/componente, preservando a identificação do componente medido.

### Fórmula da Ovalização

    Ovalização = Costura − Contra costura

O sinal da Ovalização é preservado. O sinal pode ser funcionalmente relevante e não deve ser descartado ou normalizado silenciosamente.

### Fórmula da Média

    Média = (Costura + Contra costura) / 2

A Média representa o valor médio entre as duas medições. Ela é uma informação útil, mas não substitui as medições individuais.

### Aplicação independente

O controlo aplica-se independentemente a:

*   CM;
*   BQ;
*   MF.

Cada componente tem o seu próprio nominal e as suas próprias medições. Não se deve misturar componentes diferentes como se partilhassem automaticamente o mesmo nominal ou os mesmos limites.

Costura, Contra costura e Média são verificadas independentemente. A Média não pode ocultar uma medição individual má.

O uso de BQ em Pegamentos não altera o registo mestre da ferramenta. O controlo dimensional regista resultados operacionais; a identidade mestre da ferramenta pertence a FERRAMENTAS.

### Corredor de tolerância

O corredor de tolerância é definido por:

    Nominal − 0.20 até Nominal + 0.20

As regras de fronteira são importantes:

*   atingir o limite cria alerta;
*   cruzar o limite cria alerta;
*   igualdade no limite conta como alerta.

Isto significa que um valor exatamente sobre o limite não é tratado como silenciosamente aceitável. Ele entra na condição de alerta segundo a regra funcional.

### Visualização e autoridade

O mapa/visualização é uma projeção ou apresentação do modelo dimensional. Ele ajuda a interpretar, mas não é a autoridade de validação. A autoridade funcional permanece nas regras escritas, nos valores nominais, nas medições e nos estados definidos.

Os alertas dimensionais não bloqueiam automaticamente a produção. Eles avisam, destacam e suportam a análise humana.

### Artefactos visuais e regras de negócio

O contrato escrito de Pegamentos contém nominais por componente, limites por componente e dados por medição: Costura, Contra costura, Ovalização, Média e estado.

Alguns artefactos visuais podem sugerir regras adicionais, como espaçamentos, folgas ou limites específicos derivados da apresentação. Esses artefactos não devem ser promovidos a regras de negócio sem autoridade explícita.

Não devem ser assumidos como regras funcionais independentes:

*   um limiar separado de aceitação de Ovalização;
*   um valor do tipo ovalMax = 0.20 como regra autónoma;
*   uma tolerância do tipo gapTol = 0.05 como regra de negócio;
*   um espaçamento esperado entre componentes como regra independente;
*   uma regra autónoma de montagem/folga;
*   o nominal de um componente vizinho como nova fronteira de negócio.

Estes elementos podem ser úteis para apresentação ou leitura visual, mas não substituem o contrato escrito. A razão para não os promover é que a regra funcional confirmada se baseia nos nominais e limites por componente e nas medições registadas, não em derivações visuais de espaçamento ou montagem.

## 8. Resumo / Folha de Controlo

O Resumo é a apresentação consolidada da Folha de Controlo. Ele reúne a informação das peças controladas e serve de base à avaliação técnica e à decisão do Responsável.

O Resumo / Folha de Controlo cobre exatamente:

*   CM;
*   BQ;
*   MF;
*   PU;
*   CS.

Não deve ser expandida para outras entidades sem autoridade funcional explícita.

### Informação por peça

Por peça, o Resumo / Folha de Controlo contém funcionalmente:

*   resultado técnico OK/NOK;
*   observação/comentário;
*   ligação MCaliper, quando aplicável.

A ligação MCaliper pode ser adicionada, atualizada ou aberta pelo utilizador onde aplicável. Não é automaticamente importada.

### Origem de PU/CS

PU e CS apresentados no Resumo / Folha de Controlo vêm do contexto exato de produção/revisão do Job On. Atualmente, não são obtidos do Armazém.

O CONTROLO consome PU/CS, mas não possui nem mantém a sua configuração de produção. Esta fronteira é importante: o Resumo / Folha de Controlo mostra e usa PU/CS no contexto do controlo, mas não os redefine.

### Quem edita e quem revê

O Controlador edita e prepara o Resumo / Folha de Controlo. O Responsável revê e decide.

O acesso ao módulo determina quem pode entrar no CONTROLO. A variante funcional determina o tipo de experiência: preparação técnica para Operador / Controlador, revisão/decisão para Responsável.

### Relação com o contexto do Job On

Quando existe contexto de produção válido, o Resumo / Folha de Controlo está ancorado ao contexto exato do Job On e da revisão aplicável. Esta ancoragem permite que o histórico preserve corretamente o que foi controlado, em que contexto e sob que revisão.

O Resumo não reinterpreta o Job On. Ele apresenta a avaliação de controlo dentro do contexto recebido.

## 9. Decisões e Aprovações

O fluxo de decisão do Resumo / Folha de Controlo é explícito e baseado em estados funcionais.

### Estados principais

    Rascunho
    → Submetida
    → Aprovada / Rejeitada

### Rascunho

Em Rascunho, a folha está em preparação. O Controlador pode editar, preencher, corrigir e completar a informação técnica.

Uma folha em Rascunho não está pronta para decisão. O Responsável não deve aprovar ou rejeitar uma folha que ainda não foi submetida.

### Submetida

A folha passa a Submetida por ação explícita do Controlador. A submissão não é automática. Ela indica que a folha entra no circuito de revisão/decisão.

Uma folha submetida entra no âmbito de decisão do Responsável.

### Aprovada

A folha é Aprovada quando o Responsável aceita funcionalmente o controlo apresentado. A aprovação é uma decisão do Responsável sobre a folha submetida.

### Rejeitada

A folha é Rejeitada quando o Responsável não aceita o controlo apresentado. A rejeição não apaga o histórico. O registo da decisão permanece preservado.

### Reabertura

Uma folha Submetida ou já decidida pode ser reaberta para Rascunho.

    Submetida ou decidida
    → Rascunho

Após rejeição, o fluxo funcional esperado é:

    reabrir → editar → voltar a submeter

A folha pode ser corrigida e novamente submetida. Os eventos anteriores não são apagados silenciosamente. A reabertura serve para corrigir sem destruir histórico.

### Resultado técnico vs decisão final

É fundamental distinguir resultado técnico de decisão final.

O resultado técnico é o OK/NOK indicado pelo Controlador. Ele representa a avaliação técnica preparada.

A decisão final de produção é do Responsável. O Responsável pode considerar o resultado técnico, mas a decisão final é humana e funcional.

Consequências importantes:

*   um NOK técnico não para automaticamente a produção;
*   um OK técnico não autoriza automaticamente a produção;
*   a decisão final pode não coincidir mecanicamente com o resultado técnico;
*   o sistema apoia a decisão, mas não decide sozinho.

## 10. Histórico

O Histórico interno do CONTROLO preserva a memória funcional dos controlos. Ele não é apenas uma lista de eventos soltos; é a continuidade do que foi controlado, decidido e alterado.

### Contexto exato

Os registos históricos de controlo preservam o contexto exato do Job On e da revisão quando esse contexto é usado. Isto permite saber em que contexto o controlo foi realizado.

Revisões posteriores não reinterpretam o histórico anterior. Um registo aprovado ou histórico permanece com o contexto que tinha quando foi produzido.

### Correções e continuidade

Correções não apagam o passado. Quando algo é corrigido, o histórico preserva a sequência funcional. A correção cria continuidade histórica, não substituição silenciosa.

### Eventos append-only

Onde estabelecido, eventos e histórico são append-only. Isto significa que novos eventos são adicionados sem apagar indevidamente eventos anteriores.

### PDF e fonte de verdade

O PDF é derivado do registo estruturado. Ele é imprimível e regenerável, mas não é a fonte de verdade.

A fonte oficial é o registo estruturado/snapshot funcional. Se houver diferença entre o PDF e o registo estruturado, o registo estruturado prevalece funcionalmente.

O Histórico interno do CONTROLO não se confunde com uma vista histórica/transversal de eventos, apenas leitura, designada HISTÓRIA quando definida.

## 11. Documentos

O CONTROLO pode produzir ou usar documentos associados aos controlos. A regra central é que o documento oficial é o registo estruturado/snapshot, não um ficheiro solto sem contexto.

### PDF

O PDF é derivado. Ele pode ser gerado novamente a partir do registo estruturado. A sua função é apresentação, impressão ou distribuição, não autoridade de dados.

### Envio de documentos

O envio de documentos é explícito e confirmado. O Operador / Controlador pode enviar o documento relevante quando o fluxo funcional o permite, mas esse envio não acontece automaticamente apenas porque o controlo foi concluído ou aprovado.

Antes do envio, deve existir confirmação funcional adequada. O destino depende da Máquina/Linha quando aplicável. Por exemplo, Line B e Line C podem ter grupos destinatários diferentes.

A localização concreta da configuração dos destinatários não é uma regra funcional do CONTROLO. O CONTROLO define que o envio é explícito, confirmado e orientado por contexto de Máquina/Linha; a configuração externa dos destinatários pertence ao local funcional adequado.

### Diretórios

A estrutura de diretórios segue um princípio de referência antes de produção.

    Root/
    └── Reference/
        └── Production/
            ├── Peso/
            ├── Pegamentos/
            └── Resumo/

Princípios funcionais:

*   Reference precede Production;
*   uma Reference pode ter muitas Productions;
*   apenas a raiz é configurada manualmente pelo utilizador;
*   as pastas inferiores são criadas ou reutilizadas automaticamente;
*   a criação/reutilização deve ser idempotente;
*   o Job On acede à mesma relação exata de documento de produção/revisão;
*   não existe uma árvore de documentos duplicada propriedade do Job On.

O CONTROLO não deve criar uma estrutura documental paralela para o Job On. O documento está relacionado com o contexto de produção/revisão e deve ser acedido de forma coerente pelos módulos autorizados.

## 12. Regras Não-Bloqueantes

O princípio não-bloqueante é central no CONTROLO. Ele separa validação estrutural/dados, resultados técnicos e decisão automática de negócio.

### Validação estrutural/dados

A validação estrutural ou de dados pode rejeitar uma operação objetivamente inválida quando uma regra funcional confirmada exige dados válidos.

Exemplos:

*   campo obrigatório estruturalmente em falta;
*   formato de valor não suportado;
*   transição de estado impossível;
*   requisito explícito de fluxo não satisfeito.

Esta validação protege a integridade dos dados e do fluxo. Não é uma decisão automática de produção.

### Resultados técnicos, avisos e valores de controlo

Resultados técnicos, avisos e valores de controlo não podem automaticamente:

*   parar produção;
*   remover um CM de produção;
*   decidir se a produção continua;
*   converter OK/NOK técnico em estado operacional automático;
*   rejeitar produção automaticamente por causa de uma tolerância;
*   substituir silenciosamente ferramenta/contexto;
*   tomar a decisão de negócio do Responsável.

### Validação / aviso vs decisão automática de negócio

A relação funcional é:

*   validação / aviso = permitido;
*   decisão automática de negócio / bloqueio duro de produção = não permitido, salvo regra de negócio explícita.

O sistema pode:

*   avisar;
*   destacar;
*   validar;
*   mostrar tolerâncias;
*   mostrar informações de comparação;
*   mostrar médias;
*   mostrar valores calculados;
*   mostrar alertas dimensionais;
*   pedir correção;
*   guiar o utilizador;
*   evidenciar contexto em falta ou inconsistente.

### Não-bloqueante não significa aceitar dados inválidos

O princípio não-bloqueante não significa que dados inválidos sejam aceites silenciosamente. O sistema deve destacar, pedir correção e impedir operações inválidas quando a regra funcional assim o determinar.

A diferença é que o sistema não transforma automaticamente um alerta técnico ou resultado de controlo numa decisão de negócio. A decisão permanece humana, suportada pelo sistema.

## 13. Ownership e Relações Cross-Module

O **CONTROLO** é proprietário dos seus próprios registos e resultados de controlo, incluindo **Peso**, **Pegamentos**, **Resumo / Folha de Controlo**, avaliações técnicas, comentários, ligações MCaliper, decisões e histórico interno.

Os restantes módulos apenas fornecem ou recebem contexto relacionado:

- **Job On** fornece o contexto exato de produção/revisão e a configuração produtiva usada pelo CONTROLO.
- **Ferramentas** mantém o registo mestre das ferramentas; o CONTROLO apenas as usa no contexto do controlo.
- **Boquilhas** regista os movimentos operacionais e os movimentos associados à reparação externa das BQ; o CONTROLO apenas consome a BQ necessária ao controlo.
- **Armazém** mantém a localização e os movimentos físicos; o CONTROLO não gere stock nem movimentos.
- **História** é uma vista transversal de eventos e não se confunde com o Histórico interno do CONTROLO.

O CONTROLO não altera o planeamento do Job On, o registo mestre das ferramentas, os movimentos do Armazém ou os históricos operacionais de outros módulos.

## Implementation Pointers

### Relevant implementation areas

- Application: Peso formulas (Capacidade/Volume, peso do vidro, comparação inicial), emparelhamento posicional, apresentação decimal; Pegamentos (eixos/medições, ovalização, média, corredor de tolerância); Resumo / Folha de Controlo (quem edita vs revê; origem PU/CS); decisões/aprovações (Rascunho → Submetida → Aprovada/Rejeitada, reabertura); Histórico append-only; PDF e envio de documentos.
- Web / Razor: entradas a partir do contexto de Job On (Caso A vs Caso B), estados "Nenhum Job On carregado", regras negativas da relação com Job On.
- Technical map: `maps\07_CONTROLO.md` (verify freshness before use).

### Known implementation gaps

- None verified in this document set.

### Design reference

- `AI-CONTEXT\design-coder\21_CONTROLO_01_VISUAL_AUTHORITY_controlo.html`
- Peso Operador: `AI-CONTEXT\design-coder\22_PESO_OPERADOR_01_VISUAL_AUTHORITY_peso-operador.html`; Peso print: `AI-CONTEXT\design-coder\22_PESO_OPERADOR_02_VISUAL_AUTHORITY_PRINT_peso.html`
- Peso Responsável: `AI-CONTEXT\design-coder\23_PESO_RESPONSAVEL_01_VISUAL_AUTHORITY_peso-responsavel.html`
- Pegamentos: `AI-CONTEXT\design-coder\24_PEGAMENTOS_01_VISUAL_AUTHORITY_pegamentos.html`

### Cross-module dependencies

- Job On (entrada/planeamento, contexto exato de produção/revisão, configuração produtiva); Ferramentas (registo mestre das ferramentas usadas no contexto do controlo); Armazém (localização/movimentos físicos — o Controlo não gere stock); Boquilhas (consome apenas a BQ necessária ao controlo); História (vista transversal de eventos, distinta do Histórico interno).
