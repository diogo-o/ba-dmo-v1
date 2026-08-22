# Peso/Controlo — handoff da passagem de design

> Regra documental posterior: o PDF do Peso é guardado no workspace único da produção criado/resolvido pelo Controlo e fica acessível no Job On pelo `job_on_id + job_on_revision_id`. `OWNER_DECISION_SHARED_PRODUCTION_DOCUMENTS.md` prevalece sobre referências abaixo a uma subpasta própria do lote.

## Âmbito aplicado

- Operador mantém a estrutura funcional principal.
- Removido o botão manual `Atualizar` do cabeçalho.
- Sincronização é apresentada como estado automático, não como ação.
- Removidos os cartões `Preencher`, `Guardar`, `Aprovação`, `Produção`.
- `Gerar documento para produção` e `Enviar para produção` saem do Novo controlo.
- Estas ações passam para o Histórico e para a folha de uma revisão aprovada.
- Resultados deixam de ser texto corrido e passam a resumo + tabela por CM.
- Responsável usa a mesma fonte de resultados com menos colunas.

## Novo controlo

- Existem dois tipos de registo no módulo Peso: `Novo controlo` e `Comparação`. Ambos ficam ligados ao **Job On da produção** que lhes dá contexto.
- `Novo controlo` começa a partir do Job On selecionado/ativo para aquela produção e Referência.
- O Job On fornece o seu identificador, Referência, Produção, Máquina e o **CM/lote exatos** escolhidos para fabricar, por exemplo `Produção 202601 · CM 5447 · Lote 4`.
- Os dados técnicos do lote CM já referenciado pelo Job On fornecem Processo NNPB/PS e máquinas permitidas.
- O identificador/contexto do Job On permanece visível na folha e é guardado com o controlo para permitir abrir a produção correta mais tarde.

| Tipo de registo | Momento | Contexto apresentado | Base usada |
|---|---|---|---|
| Novo controlo | preparação/entrada da nova produção definida no Job On | Job On, Referência, Produção, Máquina, CM e lote escolhidos no Job On, Processo do lote | leituras introduzidas para essa produção |
| Comparação | depois de a produção estar a trabalhar | o mesmo Job On, Referência, Produção, Máquina, lote e Processo | Novo controlo aprovado desse Job On + leituras dos CM atualmente em produção |

O vínculo técnico deve usar o identificador estável do Job On/produção devolvido pela aplicação. O texto da Referência, Produção ou Máquina é apenas apresentação e filtro.
- `Calcular` e `Enviar para aprovação` permanecem ações explícitas.
- Guardar pode ser automático; mostrar `A guardar`, `Guardado` ou erro.
- Enviar para aprovação nunca é automático.
- A máquina atual deve pertencer às máquinas permitidas da referência.
- Referência, Produção, Máquina, CM e Lote são herdados do Job On; o Processo é herdado do lote CM referenciado. Não devem ser novamente pedidos ao Operador.
- Estado do molde, temperatura e leituras continuam editáveis quando aplicável.
- Campos devem respeitar o tamanho real dos valores: máquina e temperatura são compactos; lote comporta três caracteres; data e estado ocupam apenas o necessário.
- `Fim da produção anterior (SAP)` e `Peso médio anterior (SAP)` são compactos; `Notas` recebe o espaço restante.
- Todos os valores decimais introduzidos ou calculados são normalizados para, no máximo, duas casas decimais na interface e nos documentos.
- `Adicionar leitura` cria efetivamente um novo par CM/Peso e integra-o no cálculo.
- `Remover leitura` remove a última leitura; nunca permite ficar sem pelo menos uma leitura.
- Novo controlo e Comparação usam os mesmos componentes e os mesmos textos: `Remover leitura`, `Adicionar leitura`, `Calcular` e `Enviar para aprovação`.
- Por baixo de cada par CM/Peso existe um resultado compacto `Peso do vidro`, atualizado em tempo real através do motor de cálculo existente.

## Referências

- Ao criar o primeiro lote, ou ao duplicar/criar outro lote no Peso, pedir `Processo` obrigatório: NNPB ou PS.
- NNPB/PS pertence ao lote do Peso, não ao formulário do Novo controlo e não à Referência mestre isoladamente.
- Substituir a linha única por cartões de `Máquinas permitidas`: B1–C3.
- Pelo menos uma máquina é obrigatória.
- Os cartões B1–C3 representam a associação funcional entre o lote e as máquinas/linhas onde esse lote pode trabalhar; não são apenas uma preferência visual ou um filtro.
- Ao guardar, a seleção deve usar e atualizar a associação que já existe no programa, sem criar uma segunda estrutura paralela.
- Um lote pode ficar associado a várias máquinas/linhas permitidas, mas a máquina atual de cada controlo continua a ser uma única máquina.
- Novo controlo e Comparação só podem usar uma máquina incluída nessa associação.
- O Novo controlo e a Comparação mostram sempre o Job On/Produção a que pertencem.
- O CM e lote usados são os que já estão explícitos no Job On. O módulo Peso não apresenta uma segunda escolha de CM/lote para esse controlo.
- Se o Job On não tiver CM/lote válido, bloquear a abertura do controlo e encaminhar a correção para o Job On; não escolher automaticamente outro CM.
- A base normal da Comparação é o Novo controlo aprovado associado ao mesmo Job On da produção.

## Controlos da referência ativa

- Tabela sem botões dentro das linhas.
- Um clique seleciona e ativa `Editar controlo`.
- Duplo clique abre a folha.
- `Novo controlo para esta referência` fica fora da lista e permanece disponível.
- `Editar controlo` fica fora da lista e exige seleção.
- Editar revisão aprovada exige justificação, cria nova revisão e exige nova aprovação.
- A revisão aprovada anterior permanece imutável.

## Lista de referências

- Usa o mesmo contrato canónico das restantes listas.
- Um clique seleciona a referência e atualiza a área de detalhe e os controlos associados.
- Duplo clique encaminha para `Histórico` com o filtro da referência já aplicado.
- Se a abertura partir de um lote específico, aplicar simultaneamente referência e lote.
- Não criar uma segunda página dedicada aos registos do lote; o Histórico é a vista canónica desses registos.
- Ao entrar no Histórico por este atalho, manter disponíveis os restantes filtros e indicar claramente quais foram pré-aplicados.

## Histórico

- Contém apenas controlos enviados para aprovação.
- Um clique seleciona; duplo clique abre a folha.
- Não existe botão `Abrir folha`; a abertura é sempre feita por duplo clique na linha selecionada.
- `Gerar folha de produção` e `Enviar email para produção` ficam fora da tabela.
- Só ficam ativos para a revisão aprovada selecionada.
- Documento/email usam o snapshot aprovado, nunca valores entretanto alterados.
- Assim que o Responsável confirma `Aprovar`, a mesma folha apresenta imediatamente `Enviar para produção`; não obriga a navegar primeiro para o Histórico.
- A máquina/linha do snapshot aprovado escolhe automaticamente o grupo de destinatários: B1–B3 usam `Linha B` e C1–C3 usam `Linha C`.
- Antes do envio, mostrar máquina, destinatários resolvidos, assunto, mensagem e anexo. O envio exige confirmação explícita e nunca acontece automaticamente com a aprovação.
- Se não existir configuração de destinatários para a máquina, bloquear apenas o envio e indicar que a configuração está em falta; a aprovação mantém-se válida.
- Destinatários e template do email pertencem às Definições da aplicação. A interface apresenta a resolução devolvida pelo serviço e não altera o snapshot aprovado.
- Filtros mínimos: Job On, Referência, Produção, Tipo (`Novo controlo`/`Comparação`), Estado e intervalo de datas.

## Persistência dos valores e dos documentos

O módulo separa deliberadamente o **registo estruturado** do **documento enviado à Produção**:

| Conteúdo | Persistência | Regra |
|---|---|---|
| Job On, Produção, Referência, CM, lote CM, leituras, cálculos, estados, revisões, decisões e auditoria | servidor | constitui o histórico pesquisável e comparável do Peso |
| PDF aprovado/enviado para Produção | computador/local configurado | é um artefacto documental gerado a partir do snapshot aprovado |

- O servidor guarda os números e a ligação estável ao Job On. O PDF não substitui estes dados.
- Em `Definições`, o utilizador autorizado escolhe o diretório principal de cada ano, por exemplo `Documentos DMO / 2026`.
- Peso não apresenta campo de diretório ou subpasta.
- A aplicação resolve `diretório anual / Referência / Produção / Produção_Referência_Linha_Peso`.
- Se a pasta da Referência ou Produção não existir, é criada automaticamente; se existir, é reutilizada.
- Uma nova Produção da mesma Referência reutiliza a pasta da Referência e cria outra pasta de Produção, por exemplo `202602` depois de `202601`.
- O nome do PDF é gerado com dados do Job On/snapshot aprovado e deve incluir contexto suficiente para evitar colisões, pelo menos Produção, Referência, tipo de documento e revisão/data. A convenção final do nome é configuração técnica, não texto livre do operador.
- A interface distingue os estados `Dados guardados no servidor` e `PDF guardado localmente`. Uma falha ao escrever o PDF não apaga nem reverte o registo estruturado aprovado.
- Noutro computador, o utilizador continua a consultar o histórico numérico do servidor; abrir o PDF depende de esse computador ter acesso à pasta local/partilhada configurada.
- A permissão do diretório é local ao browser/computador e pode precisar de ser renovada. Nunca apresentar uma pasta como disponível antes de confirmar a permissão.

## Resultados

Resumo: densidade, capacidade média, peso médio estimado, peso nominal, diferença absoluta e percentual.

Tabela do Operador: CM, peso em água, capacidade, desvio cm³, desvio %, peso estimado e diferença anterior.

Tabela do Responsável: CM, capacidade, desvio %, peso estimado e diferença anterior.

As duas vistas usam os mesmos dados calculados; a vista do Responsável apenas esconde detalhe operacional.

## Origem do processo NNPB/PS

- O processo operacional é escolhido ao **criar o lote no módulo Peso**.
- NNPB ou PS é guardado no lote do Peso e pode variar apenas segundo as regras funcionais permitidas para os lotes; não é pedido no Novo controlo.
- Ao resolver o lote do Peso associado ao Job On, o Novo controlo e a Comparação herdam automaticamente esse processo.
- O Operador não escolhe novamente o processo no Novo controlo nem numa Comparação.
- O valor mostrado em `Referência ativa` é informativo e deve ser apresentado como não editável.
- O lote do Peso é a fonte de verdade para o processo apresentado no contexto do Job On, nos controlos e nas comparações.
- Registos anteriores mantêm no seu snapshot o processo e o lote usados naquele Job On.

## Comparações operacionais

> `OWNER_DECISION_PESO_GLASS_COMPARISON.md` substitui as regras antigas desta secção que comparem água, capacidade ou uma média global.

- A `Comparação` é o segundo tipo de registo do Peso. O Operador cria-a na própria folha `Novo controlo`, imediatamente depois de `Resultados`, no contexto do Job On/produção atual.
- O objetivo visual é registar os CM que **já estão em produção** e compará-los com os valores aprovados no Novo controlo associado ao mesmo Job On.
- Novo controlo, Comparação e Job On partilham e apresentam o mesmo identificador de contexto da produção; não se relacionam apenas pelo texto da Referência.
- `Comparações` deixa de ser um separador principal. Uma vista separada serve apenas para consultar ou reabrir registos já guardados.
- Depois dos Resultados, a ação `Escolher produção anterior` mostra apenas produções aprovadas da mesma Referência. A seleção é confirmada com `job_on_id + job_on_revision_id` antes da criação da tabela.
- O fluxo é `Escolher produção anterior → Confirmar produção → Criar tabela de comparação → associar CM atual a CM anterior → Enviar para aprovação`.
- `Enviar para aprovação` fica no fim deste fluxo e permanece desativado enquanto não existir uma tabela atualizada.
- Não mostrar `Diferença anterior` nos Resultados antes de existir uma produção anterior confirmada; isso produziria um número sem origem rastreável.
- A folha de Comparação mantém no topo o bloco `Referência ativa`: Referência, CM, Boquilha, Processo e Máquina atual.
- Este bloco inclui também Job On e Produção; a secção seguinte identifica separadamente o Novo controlo aprovado usado como base.
- A comparação usa esse controlo aprovado como base imutável.
- Job On, Referência, Produção e Máquina identificam a produção atual; Lote e Processo vêm do lote do Peso associado.
- Os CM atualmente medidos na Comparação pertencem à produção identificada pelo mesmo Job On; a base continua a ser o Novo controlo aprovado desse Job On.
- Os números de CM introduzidos na Comparação identificam elementos individuais do CM/lote já associado ao Job On; não permitem mudar para outro lote de ferramenta.
- O Operador associa cada CM atual a um CM concreto da produção anterior.
- Os CM atuais e os respetivos valores `Peso do vidro` são reutilizados das leituras já preenchidas na mesma folha; não são novamente introduzidos.
- O peso do vidro é calculado por `CM + BQ − PU` pelo motor de backend.
- O resultado final aprovado de cada CM é a unidade comparável. Não usar a média global da produção como base.
- `Fim da produção anterior (SAP)` é substituído por `Data do registo da comparação`, preenchida com a data efetiva do novo registo.
- `Calcular` atualiza peso do vidro, diferença e variação percentual para cada par `CM atual → CM anterior`.
- Água e capacidade não fazem parte desta Comparação.
- Guardar cria um registo complementar e envia-o ao Responsável; não altera a aprovação original.
- Se uma leitura atual for alterada depois da criação da tabela, o envio volta a ficar indisponível até a tabela ser recriada.
- A ação final chama-se `Enviar para aprovação`, com o mesmo componente e estado visual do Novo controlo.
- O cálculo do mockup é apenas demonstrativo; a implementação deve chamar a função/motor de cálculo já existente no programa. Não duplicar nem reimplementar a fórmula no componente visual.

## Decisão da comparação pelo Responsável

- A comparação aparece na mesma lista diária dos controlos, identificada como `Comparação`.
- Cada CM recebe uma decisão independente: `Manter` ou `Colocar de parte`.
- Cada linha mostra CM atual, CM anterior, peso do vidro atual, peso do vidro anterior, diferença e variação.
- A decisão compara diretamente os resultados finais dos dois CM; não usa médias globais de peso/capacidade.
- A confirmação fica bloqueada enquanto existir algum CM sem decisão.
- Se pelo menos um CM for colocado de parte, a justificação é obrigatória.
- O resumo mostra quantos CM foram mantidos, colocados de parte e permanecem sem decisão.
- A confirmação cria decisões por CM, com operador, responsável, data/hora e referência à revisão aprovada.
- O controlo aprovado original permanece imutável e mantém o respetivo estado.

## Estados e filtros de tipo

- Registo de peso e Comparação usam os mesmos estados canónicos: `Pendente`, `Aprovado` e `Não aprovado`.
- `Comparação` é um tipo de registo, nunca um estado como `Por decidir`.
- As listas do Responsável e do Histórico incluem o filtro `Tipo`: `Todos`, `Registo de peso` e `Comparação`.
- O estado usa tons suaves derivados da paleta base: azul/cinza para pendente, verde acinzentado para aprovado e terracota discreto para não aprovado.
- Os botões `Manter` e `Colocar de parte` usam os mesmos tons discretos; a decisão escolhida mantém destaque e a alternativa perde intensidade.

## Densidade das tabelas

- Tabelas compactas usam padding reduzido sem diminuir a área mínima de seleção.
- Colunas curtas, como máquina, lote, processo e revisão, não recebem largura elástica.
- A lista de referências deve caber no cartão em desktop sem scroll horizontal.
- Em ecrãs pequenos, o scroll horizontal continua permitido para não ocultar dados.

## Página do Responsável

- É uma única página de aprovação; não existe uma segunda vista de Comparações.
- O calendário escolhe o dia e a lista apresenta os controlos desse dia.
- A área direita mostra apenas o controlo selecionado e a comparação necessária à decisão.
- `Temperatura` deixa de aparecer na identificação principal e é substituída por `Operador`, que acrescenta rastreabilidade à decisão.
- A temperatura permanece no registo técnico e pode ser consultada na folha completa quando necessária.

## Identidade apresentada no cabeçalho

- O nome vem do utilizador autenticado.
- O texto por baixo do nome usa o campo de título/função do perfil, representado no mockup por `data-user-profile-title`.
- Esse título é texto livre gerido pelo Administrador na página de gestão de utilizadores, por exemplo `Metrologia`, `Chefe`, `Engenheiro` ou `Responsável de qualidade`.
- O título visual não concede permissões e não substitui o papel/template de acesso do utilizador.
- Não escrever o título diretamente em cada página; a shell partilhada carrega o valor canónico do perfil.

## Contrato comum de listas

O ficheiro `dmo-interactions.js` estabelece o comportamento canónico:

- O contentor usa `data-dmo-list`.
- Cada linha/cartão usa `data-dmo-row` e um `data-id` estável.
- Um clique seleciona uma única linha e emite `dmo:list-select`.
- Duplo clique abre o registo e emite `dmo:list-open`.
- `Enter` seleciona; `Ctrl+Enter` abre, garantindo uso por teclado.
- A seleção usa sempre a classe `selected` e `aria-selected`.
- Os botões de ação ficam fora da lista e respondem ao registo selecionado.
- Filtros nunca alteram ou eliminam seleção silenciosamente; se a linha deixar de estar visível, a seleção é limpa e as ações ficam desativadas.

## Contrato comum de calendários

- O contentor usa `data-dmo-calendar`.
- Cada dia usa `data-date="AAAA-MM-DD"`.
- Um clique seleciona uma única data e emite `dmo:date-select`.
- Dias com registos usam `has-record`; o ponto é apenas indicador, não outro botão.
- A data selecionada usa `selected` e `aria-pressed`.
- Setas alteram o mês sem selecionar automaticamente um dia.
- `Mostrar todas as datas` limpa o filtro de data e mantém os restantes filtros.
- Calendário e lista são sincronizados pela mesma data ISO, não pelo texto visível.
