# BA DMO — GLOBAL MODULE / USER ROLE MODEL

OPEN OWNER QUESTIONS: NONE

## Índice

1. [Visão Geral](#sec-1)
2. [Os Três Perfis Funcionais](#sec-2)
3. [Atribuição Individual de Módulos](#sec-3)
4. [O Que É um Módulo](#sec-4)
5. [Módulos Funcionais Atuais](#sec-5)
6. [Áreas Internas, Workflows e Variantes](#sec-6)
7. [Relação entre Perfil e Módulo](#sec-7)
8. [Matriz Global Módulo × Perfil](#sec-8)
9. [Resumo Funcional dos Módulos](#sec-9)
   - [9.1 Job On](#sec-9-1)
   - [9.2 Controlo](#sec-9-2)
   - [9.3 Ferramentas](#sec-9-3)
   - [9.4 Armazém](#sec-9-4)
   - [9.5 Boquilhas](#sec-9-5)
   - [9.6 Reparação Interna](#sec-9-6)
   - [9.7 Reparação Externa](#sec-9-7)
   - [9.8 Tampões](#sec-9-8)
   - [9.9 Admin](#sec-9-9)
10. [Ownership e Relações Cross-Module](#sec-10)
11. [Navegação, Visibilidade e Start Page](#sec-11)
12. [Admin / Users / Access](#sec-12)
13. [Modelos Históricos / Superados](#sec-13)
14. [Questões Globais em Aberto](#sec-14)
15. [Resumo Funcional Final](#sec-15)

---

## <a id="sec-1"></a>1. Visão Geral

Este documento descreve o modelo funcional global do BA DMO ao nível dos módulos e dos perfis de utilizador.

O seu objetivo é tornar claro:

- quais são os módulos funcionais atuais de topo;
- o que cada módulo representa funcionalmente;
- quais áreas são internas e não módulos;
- quais itens são workflows ou tipos de registo;
- como os três perfis funcionais se relacionam com os módulos;
- como a atribuição individual de módulos determina acesso;
- como o perfil determina a experiência dentro de um módulo atribuído;
- quais regras são transversais;
- quais diferenças são apenas técnicas e precisam de reconciliação;
- quais modelos históricos foram superados.

A regra funcional central de atribuição individual de módulos está confirmada: o Admin atribui módulos individualmente por utilizador; o perfil seleciona a variante dentro de um módulo atribuído; um perfil nunca implica automaticamente todos os módulos operacionais.

---

## <a id="sec-2"></a>2. Os Três Perfis Funcionais

Existem exatamente três perfis funcionais no BA DMO:

1. Admin
2. Operador / Controlador
3. Responsável

Não existe um quarto perfil.

Não existe perfil read-only.

Não existe perfil management / metrology / consultation.

Não deve ser criado ou implícito qualquer perfil adicional.

### Admin

O Admin é o perfil administrativo.

A sua função está associada à administração do portal, incluindo utilizadores, templates, aplicações e auditoria, conforme concedido funcionalmente.

O Admin não é implicitamente um utilizador operacional.

O privilégio administrativo não concede automaticamente acesso a módulos operacionais.

Um Admin puro não deve ser convertido automaticamente em Operador / Controlador ou Responsável.

### Operador / Controlador

O Operador / Controlador é o perfil operacional de execução, medição, registo e consulta, onde esse comportamento estiver definido.

O acesso a módulos continua a depender da atribuição individual de cada módulo.

O Operador / Controlador não recebe automaticamente todos os módulos operacionais.

### Responsável

O Responsável é o perfil de revisão, aprovação, decisão, configuração ou validação, onde esse comportamento estiver definido.

O acesso a módulos continua a depender da atribuição individual de cada módulo.

O Responsável não recebe automaticamente todos os módulos.

---

## <a id="sec-3"></a>3. Atribuição Individual de Módulos

A atribuição de módulos é feita individualmente por utilizador.

O Admin cria ou edita um utilizador e escolhe quais módulos da aplicação esse utilizador pode aceder.

A regra funcional é:

- módulos atribuídos determinam quais módulos o utilizador pode usar;
- perfil determina como o utilizador experiencia um módulo atribuído, quando existir variante dependente de perfil.

Um perfil nunca implica todos os módulos operacionais.

Um módulo não atribuído:

- não aparece na navegação normal do utilizador;
- não pode ser usado funcionalmente;
- não deve ser diretamente acessível para contornar a restrição.

A atribuição de módulos é um conceito de controlo de acesso, não apenas visibilidade de interface.

Assim:

- atribuir um módulo determina se o utilizador pode entrar e trabalhar nessa área funcional;
- não atribuir um módulo torna essa área funcional ausente e inutilizável para o utilizador.

---

## <a id="sec-4"></a>4. O Que É um Módulo

O modelo funcional distingue claramente os seguintes conceitos.

### TOP-LEVEL MODULE

Um módulo de topo é uma unidade funcional lógica de acesso que pode ser atribuída a um utilizador.

Exemplos atuais:

- Job On;
- Controlo;
- Ferramentas;
- Armazém;
- Boquilhas;
- Reparação Interna;
- Reparação Externa;
- Tampões;
- Admin.

### INTERNAL AREA

Uma área interna é uma área funcional contida dentro de um módulo.

Exemplo:

- Peso é área interna do Controlo;
- Pegamentos é área interna do Controlo;
- Resumo / Folha de Controlo é área interna do Controlo;
- Histórico do Controlo é área interna do Controlo.

Uma área interna não é um módulo de topo atribuível separadamente.

### WORKFLOW / RECORD TYPE

Um workflow ou tipo de registo é um processo ou operação dentro de um módulo ou área interna.

Exemplo:

- Comparação é workflow / tipo de registo dentro de Peso, que é área interna do Controlo.

### ROLE-DEPENDENT VARIANT

Uma variante dependente de perfil ocorre quando o mesmo módulo atribuído apresenta experiências diferentes conforme o perfil funcional.

Exemplo:

- Job On pode ter experiência de consulta/execução para Operador / Controlador e experiência de configuração/edição para Responsável;
- Controlo pode ter experiência de medição/registo para Operador / Controlador e experiência de revisão/decisão para Responsável.

Uma variante dependente de perfil não cria um módulo separado.

### TRANSVERSAL SYSTEM AREA

Uma área transversal de sistema é uma área necessária ao funcionamento global, mas não é um módulo operacional atribuível nos mesmos termos.

Exemplos:

- Login / Auth;
- Users / Access como domínio transversal;
- História como superfície transversal de leitura de eventos de auditoria.

As tabs/áreas **Histórico** dentro dos módulos são áreas internas de leitura dos registos respetivos; não são módulos de topo.

### NOT A CURRENT MODULE

Alguns itens existem como páginas, tabs, labels ou superfícies de design, mas não são módulos funcionais atuais.

Exemplos:

- Definições, quando aparece como tab interno;
- Design Laboratório, como superfície de design/demonstração, salvo comportamento funcional próprio que exija validação.

### HISTORICAL / SUPERSEDED

Estruturas antigas ou recuperadas que foram substituídas pelo modelo atual devem permanecer classificadas como históricas/superadas.

Não devem contaminar o modelo funcional atual.

### Regra central

Não classificar algo como módulo apenas porque tem:

- página própria;
- tab própria;
- namespace técnico;
- serviço próprio;
- workflow próprio.

Uma página, tab, namespace, serviço ou workflow pode existir dentro de um módulo sem ser um módulo.

---

## <a id="sec-5"></a>5. Módulos Funcionais Atuais

Os módulos funcionais atuais de topo são:

| Módulo | Classificação funcional | Atribuição | Observação |
|---|---|---|---|
| Job On | Módulo de topo / hub operacional | Atribuível | Landing para utilizadores operacionais |
| Controlo | Módulo de topo | Atribuível | Contém áreas internas Peso, Pegamentos, Resumo / Folha e Histórico |
| Ferramentas | Módulo de topo | Atribuível | Owner do master record das ferramentas, incluindo BQ master |
| Armazém | Módulo de topo | Atribuível | Localização física e movimentos físicos |
| Boquilhas | Módulo de topo | Atribuível | Movimentos de reparação externa de BQ (saída/retorno, quantidades, reparador, histórico) |
| Reparação Interna | Módulo de topo | Atribuível | Registos de reparação interna, CM/MF only |
| Reparação Externa | Módulo de topo | Atribuível | Batches de reparação externa |
| Tampões | Módulo de topo | Atribuível | Saldos/movimentos/config de tampões |
| Admin | Módulo de topo / área transversal de sistema | Atribuível | Administração |

Não existem módulos funcionais atuais separados para:

- Peso;
- Pegamentos;
- Resumo / Folha de Controlo;
- Histórico do Controlo;
- Comparação;
- História;
- Users / Access;
- Login / Auth;
- Definições;
- Design Laboratório.

História é uma superfície transversal de leitura de eventos de auditoria, não um módulo atribuível. Os Históricos internos dos módulos são áreas/tabs internas de leitura.

Esses itens são classificados como áreas internas, workflows, áreas transversais ou não módulos, conforme detalhado na secção seguinte.

---

## <a id="sec-6"></a>6. Áreas Internas, Workflows e Variantes

### CONTROLO como módulo único

CONTROLO é um único módulo funcional de topo.

A sua estrutura interna é:

- Peso — área interna;
- Pegamentos — área interna;
- Resumo / Folha de Controlo — área interna;
- Histórico do Controlo — área interna.

Dentro de Peso:

- Controlo inicial — workflow;
- Comparação — workflow / tipo de registo.

Peso e Pegamentos não são módulos funcionais de topo separados.

Resumo / Folha de Controlo não é um módulo separado.

Histórico do Controlo não é um módulo separado.

Comparação não é um módulo separado.

A eventual representação técnica de Peso e Pegamentos como entradas separadas no catálogo técnico é apenas representação técnica e não altera a classificação funcional.

Não é:

- uma questão de owner;
- um conflito funcional;
- uma classificação funcional por resolver.

### Outros itens não modulares

| Candidato | Classificação |
|---|---|
| Peso | Área interna do Controlo |
| Pegamentos | Área interna do Controlo |
| Resumo / Folha de Controlo | Área interna do Controlo |
| Histórico do Controlo | Área interna do Controlo |
| Comparação | Workflow / tipo de registo dentro de Peso |
| Users / Access | Área transversal de sistema, não módulo operacional atribuível |
| Login / Auth | Área transversal de sistema, não módulo |
| Definições | Tab/área interna dentro de módulos, não módulo de topo |
| Design Laboratório | Não é módulo funcional atual; superfície de design/demonstração |
| Per-lot Verificações | Subárea interna / tipo de registo em Ferramentas, espelhando ocorrências do Job On |

### Variantes dependentes de perfil

Uma variante dependente de perfil é uma experiência diferente do mesmo módulo atribuído.

Exemplos confirmados ou parcialmente suportados:

- Job On;
- Controlo;
- Ferramentas;
- Armazém.

Não se deve inventar variantes simétricas para todos os módulos.

Alguns módulos podem ter apenas uma experiência relevante documentada ou podem ainda ter comportamento por perfil não estabelecido.

---

## <a id="sec-7"></a>7. Relação entre Perfil e Módulo

Existem dois conceitos funcionais independentes:

1. Módulos atribuídos  
   Determinam QUAIS módulos o utilizador pode aceder.

2. Perfil funcional  
   Determina COMO o utilizador experiencia um módulo atribuído, quando existir variante dependente de perfil.

Assim:

- módulo atribuído = possibilidade de entrar e usar o módulo;
- perfil = comportamento/experiência dentro desse módulo, onde houver diferença por perfil.

Os dois conceitos não devem ser confundidos.

Um utilizador pode ter um módulo atribuído, mas a experiência dentro desse módulo pode variar conforme seja Operador / Controlador ou Responsável.

Um utilizador não perde acesso a um módulo apenas porque não tem um contexto de produção carregado.

Por exemplo:

"Nenhum Job On carregado" é uma condição de contexto, não uma falta de permissão de módulo.

Esta condição pertence a áreas dependentes de produção, como Controlo, e não significa que o utilizador não tenha o módulo atribuído.

---

## <a id="sec-8"></a>8. Matriz Global Módulo × Perfil

Legenda de valores:

- YES = acesso/comportamento relevante confirmado;
- NO = não aplicável/negado por regra funcional conhecida;
- UNKNOWN = não estabelecido na fonte atual;
- SAME = comportamento igual para os perfis relevantes;
- DIFFERENT = comportamento diferente conforme perfil;
- ONLY ADMIN = módulo/área apenas Admin;
- UNKNOWN no comportamento de perfil = não inferir.

Nota: a coluna Admin representa o perfil funcional Admin. No modelo funcional atual, o perfil Admin não recebe acesso aos módulos operacionais. Mecanismos técnicos que combinam permissões não criam um perfil funcional novo nem alteram esta matriz funcional.

| Módulo | Admin | Operador / Controlador | Responsável | Role behavior | Status |
|---|---:|---:|---:|---|---|
| Job On | NO | YES | YES | DIFFERENT | Confirmado |
| Controlo | NO | YES | YES | DIFFERENT | Confirmado |
| Ferramentas | NO | YES | YES | DIFFERENT | Parcial; comportamento por perfil ainda em revisão |
| Armazém | NO | YES | YES | DIFFERENT | Variante confirmada; detalhe pendente |
| Boquilhas | NO | UNKNOWN | UNKNOWN | UNKNOWN | Módulo confirmado; comportamento por perfil não documentado |
| Reparação Interna | NO | UNKNOWN | UNKNOWN | UNKNOWN | Módulo confirmado; sem divisão funcional documentada |
| Reparação Externa | NO | UNKNOWN | UNKNOWN | UNKNOWN | Módulo confirmado; sem divisão funcional documentada |
| Tampões | NO | YES | UNKNOWN | UNKNOWN | Operador documentado; Responsável não estabelecido |
| Admin | YES | NO | NO | ONLY ADMIN | Confirmado |

Peso e Pegamentos não aparecem nesta matriz global porque não são módulos funcionais atuais de topo. Eles são áreas internas do Controlo.

História também não aparece nesta matriz porque não é um módulo funcional atribuível; é uma superfície transversal de leitura de eventos de auditoria, visível dentro dos módulos concedidos ao utilizador, com eventos administrativos sujeitos a permissão específica.

---

## <a id="sec-9"></a>9. Resumo Funcional dos Módulos

Este resumo é global e proporcional. Ele não substitui a explicação detalhada de cada módulo.

---

### <a id="sec-9-1"></a>9.1 Job On

Finalidade:

- contexto central de produção/planeamento;
- folha operacional;
- hub para outros módulos;
- calendário e lista de produções;
- fornece contexto exato de produção/revisão/ferramentas a módulos operacionais.

Classificação:

- módulo de topo;
- atribuível;
- landing para utilizadores operacionais.

Admin:

- Admin puro não recebe acesso ao Job On;
- Job On é negado a administrador puro.

Operador / Controlador:

- consulta;
- pode confirmar manualmente verification checks onde definido.

Responsável:

- cria/edita/duplica/configura;
- seleciona tooling;
- guarda revisões;
- gere configuração de produção.

Variante de perfil:

- DIFFERENT;
- Operador / Controlador consulta/confirma;
- Responsável edita/configura.

Fronteira de ownership:

- Job On é owner do contexto de produção/revisão;
- módulos downstream consomem esse contexto;
- Job On não duplica os módulos downstream.

---

### <a id="sec-9-2"></a>9.2 Controlo

Finalidade:

- módulo único para controlo de produção;
- medição/verificação/registo;
- revisão/decisão.

Classificação:

- módulo de topo;
- atribuível;
- contém áreas internas: Peso, Pegamentos, Resumo / Folha de Controlo e Histórico do Controlo.

Admin:

- não aplicável como acesso automático;
- Admin puro não recebe Controlo implicitamente.

Operador / Controlador:

- mede/registra;
- trabalha nas áreas internas conforme permitido;
- pode editar/submeter registos de controlo onde permitido.

Responsável:

- revê;
- aprova/rejeita;
- toma decisões funcionais onde definido.

Variante de perfil:

- DIFFERENT;
- Operador / Controlador está associado a medição/registo;
- Responsável está associado a revisão/decisão.

Fronteira de ownership:

- Controlo é owner dos registos/resultados de controlo;
- consome contexto exato do Job On;
- não reconstrói produção/ferramentas.

Nota:

- Peso, Pegamentos, Resumo / Folha de Controlo e Histórico do Controlo são áreas internas, não módulos separados.

---

### <a id="sec-9-3"></a>9.3 Ferramentas

Finalidade:

- owner do MASTER RECORD das ferramentas;
- identidade/master data das ferramentas;
- inclui referência/lot/máquina/estado técnico/%usage/configuração de verificação.

Classificação:

- módulo de topo;
- atribuível.

Admin:

- não aplicável como acesso automático;
- Admin puro não recebe Ferramentas implicitamente.

Operador / Controlador:

- pesquisa/consulta;
- movimentos de Entrada/Saída onde definido;
- correção operacional onde definida.

Responsável:

- edições de master data;
- alterações de estado técnico onde definido.

Variante de perfil:

- DIFFERENT;
- backbones de recuperação indicam divisão Operador/Responsável;
- o detalhe ainda permanece com estatuto de revisão.

Fronteira de ownership:

- Ferramentas possui o master record das ferramentas;
- inclui o master de BQ;
- outros módulos podem registar movimentos, controlos, reparações ou uso em produção, mas não se tornam owners do master das ferramentas.

---

### <a id="sec-9-4"></a>9.4 Armazém

Finalidade:

- localização física;
- movimentos físicos;
- entrada/saída/reposição/substituição, conforme definido.

Classificação:

- módulo de topo;
- atribuível;
- módulo com variante de perfil.

Admin:

- não aplicável como acesso automático.

Operador / Controlador:

- experiência Operador mencionada;
- detalhe funcional ainda pendente.

Responsável:

- experiência Responsável mencionada;
- detalhe funcional ainda pendente.

Variante de perfil:

- conceito de variante confirmado;
- workflow detalhado permanece desconhecido/pendente.

Fronteira de ownership:

- Armazém é owner da localização física e dos movimentos físicos.

---

### <a id="sec-9-5"></a>9.5 Boquilhas

Finalidade:

- regista movimentos/fluxo operacional específicos de BQ;
- inclui movimentos associados a reparação externa;
- trabalho operacional por referência + lote, quantidade e fluxo diário/alta frequência, conforme definido.

Classificação:

- módulo de topo;
- atribuível.

Admin:

- não aplicável como acesso automático.

Operador / Controlador:

- comportamento por perfil não documentado.

Responsável:

- comportamento por perfil não documentado.

Variante de perfil:

- UNKNOWN.

Fronteira de ownership:

- Boquilhas regista os **movimentos de reparação externa de BQ** (saída/retorno de reparação, quantidades, reparador, histórico);
- Boquilhas não possui o master record de BQ;
- o master de BQ permanece em Ferramentas;
- BQ comporta-se como CM/MF no modelo normal de ferramenta/master/armazém; a diferença funcional é o fluxo de reparação.

---

### <a id="sec-9-6"></a>9.6 Reparação Interna

Finalidade:

- registos de reparação interna.

Classificação:

- módulo de topo;
- atribuível;
- CM/MF only.

Admin:

- não aplicável como acesso automático.

Operador / Controlador:

- comportamento por perfil não documentado.

Responsável:

- comportamento por perfil não documentado.

Variante de perfil:

- UNKNOWN.

Fronteira de ownership:

- Reparação Interna possui os seus registos/workflows de reparação interna.

Nota histórica:

- BQ em Reparação Interna é histórico/superado;
- o modelo atual é CM/MF only.

---

### <a id="sec-9-7"></a>9.7 Reparação Externa

Finalidade:

- batches de reparação externa.

Classificação:

- módulo de topo;
- atribuível.

Admin:

- não aplicável como acesso automático.

Operador / Controlador:

- comportamento por perfil não documentado.

Responsável:

- comportamento por perfil não documentado.

Variante de perfil:

- UNKNOWN.

Fronteira de ownership:

- Reparação Externa possui os seus registos/workflows de reparação externa.

Nota:

- BQ repair permanece adiado conforme fonte atual.

---

### <a id="sec-9-8"></a>9.8 Tampões

Finalidade:

- saldos de tampões;
- movimentos de tampões;
- config/settings de tampões.

Classificação:

- módulo de topo;
- atribuível.

Admin:

- não aplicável como acesso automático.

Operador / Controlador:

- acesso pleno do Operador documentado.

Responsável:

- comportamento Responsável não estabelecido.

Variante de perfil:

- UNKNOWN.

Fronteira de ownership:

- Tampões é owner dos saldos/movimentos/config de tampões.

---

### <a id="sec-9-9"></a>9.9 Admin

Finalidade:

- administração do portal;
- utilizadores;
- templates;
- aplicações;
- auditoria.

Classificação:

- módulo de topo / área transversal de sistema;
- atribuível.

Admin:

- YES

Operador / Controlador:

- NO

Responsável:

- NO

Variante de perfil:

- ONLY ADMIN.

Fronteira de ownership:

- Admin gere utilizadores/templates/auditoria;
- Admin não é acesso operacional implícito.

## <a id="sec-10"></a>10. Ownership e Relações Cross-Module

### Regras de ownership

| Domínio | Owner funcional | Não confundir |
|---|---|---|
| Produção / planeamento / revisão | Job On | Job On não duplica módulos downstream |
| Registos/resultados de controlo | Controlo | Controlo consome contexto, não possui master de ferramentas |
| Master record / identidade das ferramentas | Ferramentas | Ferramentas possui master, incluindo BQ master |
| Movimentos de reparação externa de BQ | Boquilhas | Boquilhas regista movimentos de reparação externa de BQ, não possui BQ master |
| Localização física / movimentos físicos | Armazém | Armazém não possui master de ferramentas |
| Reparação interna | Reparação Interna | Reparação Interna possui registos de reparação interna |
| Reparação externa | Reparação Externa | Reparação Externa possui registos de reparação externa |
| Leitura transversal de eventos | História | História lê auditoria, não é owner dos eventos |
| Administração / utilizadores / templates / auditoria | Admin | Admin não é acesso operacional implícito |

### Relações principais

#### Job On → módulos operacionais

Job On é o hub central.

Módulos downstream consomem o contexto exato do Job On e não devem reconstruir produção ou tooling.

#### Controlo → Job On

Controlo consome o contexto exato de produção/revisão fornecido pelo Job On.

A ausência de Job On carregado é condição de contexto, não falta de permissão de módulo.

#### Controlo → Ferramentas / Armazém / Boquilhas

Controlo pode consumir identidade de ferramentas, contexto BQ e outra informação funcional.

Contudo:

- Ferramentas mantém o master record;
- Armazém mantém localização/movimentos físicos;
- Boquilhas mantém os movimentos de reparação externa de BQ;
- Controlo mantém registos/resultados de controlo.

#### Boquilhas → Ferramentas

Boquilhas regista os movimentos de reparação externa de BQ.

Ferramentas possui o master record de BQ.

Registar movimento BQ não é possuir master BQ.

#### Controlo ↔ Reparação Interna

Não existe relação direta entre Controlo e Reparação Interna.

Ambos são independentes e downstream do Job On.

#### Admin ↔ módulos operacionais

Admin não concede implicitamente módulos operacionais.

Admin puro é funcionalmente separado dos utilizadores operacionais.

O perfil Admin não recebe módulos operacionais como parte do seu comportamento funcional.

Job On permanece negado ao perfil Admin conforme já documentado.

#### História ↔ módulos concedidos

História mostra apenas eventos dos módulos concedidos ao utilizador.

Eventos administrativos exigem permissão específica de auditoria.

---

## <a id="sec-11"></a>11. Navegação, Visibilidade e Start Page

### Navegação reflete módulos atribuídos

Regra transversal:

- módulo atribuído aparece na navegação e é acessível conforme perfil;
- módulo não atribuído não aparece na navegação normal e não é funcionalmente acessível.

A navegação principal é derivada dos módulos atribuídos.

Entradas não autorizadas não devem ser apresentadas.

Dentro de um módulo atribuído, tabs/áreas internas podem variar conforme perfil.

### Administração

A área de Administração aparece apenas quando a permissão administrativa adequada está concedida.

### Controlo na navegação

Controlo é um módulo único.

As suas áreas internas podem ser apresentadas como áreas/tabs dentro do módulo, mas não como módulos de topo separados.

### Start page

Comportamento funcional confirmado:

- utilizadores operacionais iniciam no Job On;
- Operador / Controlador inicia no Job On;
- Responsável inicia no Job On;
- Admin puro inicia no Admin.

O Job On é o landing universal para utilizadores funcionais operacionais.

Admin puro não recebe acesso ao Job On e cai para Admin.

### Contexto de produção

"Nenhum Job On carregado" não é ausência de módulo.

É uma condição de contexto para áreas dependentes de produção.

---

## <a id="sec-12"></a>12. Admin / Users / Access

O Admin cria e edita utilizadores.

Ao criar/editar um utilizador, o Admin seleciona individualmente os módulos que o utilizador pode aceder.

Regras funcionais:

- módulos são atribuídos individualmente;
- perfil e módulos atribuídos são conceitos independentes;
- um perfil nunca implica todos os módulos operacionais;
- um módulo não atribuído permanece ausente/inutilizável;
- Admin puro não é implicitamente um utilizador operacional.

Users / Access é um domínio transversal de sistema.

Não é um módulo operacional atribuível como Job On, Controlo ou Ferramentas.

Login / Auth também é área transversal.

Não existe escolha de perfil no login.

O encaminhamento inicial é determinado pelo servidor:

- utilizador operacional vai para Job On;
- Admin puro vai para Admin.

---

## <a id="sec-13"></a>13. Modelos Históricos / Superados

### Peso / Pegamentos como módulos separados

Historicamente, Peso e Pegamentos apareceram como módulos/aplicações separados.

Estado atual:

- superado;
- Peso e Pegamentos são áreas internas do Controlo.

### Controlo como página de redirecionamento

Antes do modelo unificado, tabs do Controlo podiam redirecionar para outros módulos.

Estado atual:

- histórico/superado;
- Controlo é workspace unificado ligado à produção.

### Reparação Interna com BQ

Historicamente, Reparação Interna chegou a considerar CM/MF/BQ.

Estado atual:

- superado;
- Reparação Interna é CM/MF only;
- BQ nunca RI.

### Templates tab visível

Desenhos antigos podem mostrar Templates visível na navegação Admin.

Estado atual:

- Templates é uma área funcional atual do Admin — visível e gerível (Owner Decision 1; ver `90_ADMIN_FUNCTIONAL.md` §17–§18);
- tratar a visibilidade/manutenção de Templates como item histórico/apresentação está superado.

### Comentários "no per-user override"

Comentários antigos dizendo que não haveria override por utilizador em V1 estão stale.

Estado:

- histórico/superado pela funcionalidade viva de override.

### Ferramentas com tipos antigos

Estruturas antigas podem conter tipos de ferramentas ou combinações históricas.

Estado atual:

- Ferramentas possui master de ferramentas, incluindo BQ master e master CM/MF;
- Boquilhas regista os movimentos de reparação externa de BQ;
- configurações específicas de produção permanecem no Job On onde funcionalmente definido.

### Artefactos recuperados

Mockups antigos, labels antigos, documentos legacy e marcadores históricos são artefactos superados.

Eles não redefinem o modelo atual.

---

## <a id="sec-14"></a>14. Questões Globais em Aberto

GENUINE GLOBAL OWNER QUESTIONS:
NONE

O modelo global de módulos e perfis permanece fechado nos seus princípios essenciais:

- exatamente três perfis funcionais;
- módulos atuais de topo definidos;
- Controlo é um único módulo;
- Peso e Pegamentos são áreas internas;
- atribuição individual de módulos;
- separação entre módulo atribuído e perfil;
- Admin puro separado de utilizadores operacionais.

Os pontos ainda não definidos permanecem como comportamento por perfil UNKNOWN, apenas dentro dos três perfis existentes; a sua clarificação não cria quarto perfil, perfil read-only, perfil de consulta ou módulo adicional.

Não devem ser reintroduzidas questões sobre:

- perfis adicionais;
- perfil read-only;
- novos módulos atuais;
- se Peso ou Pegamentos são módulos de topo.

---

## <a id="sec-15"></a>15. Resumo Funcional Final

O modelo funcional global é:

- existem exatamente três perfis funcionais: Admin, Operador / Controlador e Responsável;
- módulos são atribuídos individualmente pelo Admin;
- módulo atribuído determina acesso;
- perfil determina experiência dentro do módulo, quando existir variante;
- Admin puro não é utilizador operacional implícito;
- Job On é o hub operacional e landing para utilizadores operacionais;
- Controlo é um único módulo de topo;
- Peso, Pegamentos, Resumo / Folha de Controlo e Histórico do Controlo são áreas internas do Controlo;
- Comparação é workflow/tipo de registo dentro de Peso;
- Ferramentas é owner do master record das ferramentas, incluindo BQ master;
- Boquilhas regista os movimentos de reparação externa de BQ, não o master BQ;
- Armazém é owner de localização/movimentos físicos;
- Reparação Interna e Reparação Externa possuem os seus registos de reparação;
- Tampões possui saldos/movimentos/config de tampões;
- História é superfície transversal de leitura de eventos de auditoria, não um módulo atribuível;
- Admin gere administração e não concede operacionalidade implicitamente;
- modelos históricos permanecem históricos/superados;
- células UNKNOWN permanecem UNKNOWN e não são inventadas.

## Implementation Pointers

### Relevant implementation areas

- Web / Razor: navigation and start page derive from **assigned modules** + effective access (Job On for operational users, Admin for pure Admin); module/capability gates (`admin.gerir`, `audit.view`, `audit.export`, `jobon.view`) control visibility/access.
- Application: the module/profile model is enforced through Admin user creation/editing (individual module assignment) — see `03_USERS_ACCESS_OPERATIONAL.md` and `90_ADMIN_FUNCTIONAL.md` for current implementation notes; a per-user `modules_override` exists in the implementation (technical detail — see `03_USERS_ACCESS_OPERATIONAL.md` and `90_ADMIN_FUNCTIONAL.md`).
- Technical map: `maps\16_USERS_ACCESS.md`, `maps\15_ADMIN.md`, `maps\19_APPLICATION.md` (verify freshness before use).

### Known implementation gaps

- None verified for the global model itself in this document set. The per-user `modules_override` vs the template-association model is a technical reconciliation item (functional model unchanged — see `03_USERS_ACCESS_OPERATIONAL.md`).

### Design reference

- Transversal shell/navigation/tabs behaviour: `99_DESIGN_LABORATORIO.md`.
- Admin visual authority: `AI-CONTEXT\design-coder\13_ADMIN_01_VISUAL_AUTHORITY_admin.html`.

### Cross-module dependencies

- Transversal model consumed by every module; Admin is the enforcement surface (assignments/templates); História is a transversal read surface over audit events, not a module.