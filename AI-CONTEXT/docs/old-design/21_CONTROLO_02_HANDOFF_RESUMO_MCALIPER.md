# Controlo Resumo - ligação MCaliper por peça

Estado: contrato funcional e técnico para implementação  
Mockup: `controlo.html`, tab `Resumo`

## Objetivo

Cada resultado de peça/ferramenta apresentado no Resumo pode guardar uma ligação para o respetivo controlo detalhado no MCaliper. A ligação permite sair do resumo consolidado e abrir diretamente o registo técnico que fundamenta a decisão `OK`, `NOK` ou `Por decidir`.

## Entrada e contexto obrigatório

O Controlo não apresenta um seletor nem uma ação `Carregar Job On atual`.

- todos os utilizadores operacionais têm acesso ao Job On;
- o utilizador abre o Controlo a partir do Job On/revisão em que está a trabalhar;
- `job_on_id` e `job_on_revision_id` são recebidos automaticamente;
- Resumo, Peso, Comparação e Pegamentos reutilizam esse contexto;
- o Controlo nunca escolhe automaticamente outra produção;
- o Admin puro não tem acesso ao Job On nem ao Controlo operacional e entra apenas na área Admin.

Se o contexto estiver ausente, inválido ou não autorizado, a página mostra erro e regressa ao Job On. Não disponibiliza um botão para procurar/carregar outra produção dentro do Controlo.

### Seleção no Planeamento do Job On

Ao abrir o Planeamento, o cartão de contexto ao lado do calendário começa vazio.

1. O utilizador seleciona um dia no calendário.
2. A lista mostra os Job Ons desse dia.
3. Um clique numa Referência/Produção carrega o cartão de contexto com Referência, Produção, Máquina e revisão.
4. Apenas esse Job On selecionado passa a ser o contexto corrente.
5. `Abrir Folha Job On` e `Abrir Controlo` usam exatamente esse ID e revisão.
6. Mudar de dia limpa o cartão; não seleciona automaticamente o primeiro resultado.
7. Um duplo clique pode abrir diretamente a Folha Job On, mantendo o mesmo contexto carregado.

## Âmbito visual

O Resumo é deliberadamente curto e contém apenas:

- estado do Peso com ligação para abrir o respetivo controlo;
- estado de Pegamentos com ligação para abrir o respetivo controlo;
- peças/ferramentas com decisão `OK` ou `NOK`;
- comentário por peça;
- ligação MCaliper por peça.

Folha, Verificações, manifestos de PDFs, versões e eventos não aparecem no Resumo. Documentos e eventos pertencem exclusivamente à tab `Histórico` do Controlo.

Cada cartão de resultado apresenta:

- família da peça;
- referência e lote/versão;
- decisão e comentário resumido;
- campo editável de comentário;
- campo `Ligação MCaliper`;
- ação `Adicionar ligação` quando vazio;
- ação `Atualizar ligação` quando preenchido;
- ação `Abrir MCaliper`, desativada enquanto não existir ligação guardada;
- confirmação ou erro de persistência junto ao campo.

O mockup demonstra CM, MF, BQ, PU e CS. A implementação deve aplicar o mesmo componente a qualquer peça incluída no Resumo, sem limitar a lista a estas cinco famílias.

## Associação correta

A ligação pertence ao resultado de uma peça concreta dentro de uma folha de Controlo concreta:

```text
control_sheet_id
control_result_id
job_on_id
job_on_revision_id
job_on_component_id
mcaliper_url
```

Não guardar a ligação apenas por texto de Referência, sigla da família ou posição do cartão. Duas peças com a mesma Referência podem ter controlos MCaliper diferentes.

## Persistência sugerida

```json
{
  "controlResultId": "control-result-mf-7080-01",
  "jobOnComponentId": "mf-7080-01",
  "family": "MF",
  "reference": "7080",
  "lot": "01",
  "mcaliperUrl": "mcaliper://MF-7080-01",
  "updatedBy": "user-id",
  "updatedAt": "2026-08-22T10:30:00Z"
}
```

O formato real do URL é definido pela integração MCaliper. O cliente não deve construir um URL por concatenação de família, Referência e lote.

## Comportamento

1. O utilizador cola ou introduz a ligação fornecida pelo MCaliper.
2. `Adicionar ligação`/`Atualizar ligação` envia o comando para o servidor.
3. O botão fica em processamento e não fecha/limpa o campo.
4. Apenas após sucesso a UI confirma `Ligação guardada` e ativa `Abrir MCaliper`.
5. `Abrir MCaliper` usa a última ligação persistida, não texto ainda não guardado.
6. Uma falha mantém o último valor persistido e permite tentar novamente.

## Validação e segurança

- aceitar apenas esquemas/origens autorizados pela integração MCaliper;
- remover espaços exteriores, sem reescrever o conteúdo interno;
- impedir `javascript:`, `data:` e outros esquemas não autorizados;
- abrir em novo contexto com proteção equivalente a `noopener`;
- validar autorização para alterar a folha de Controlo;
- texto e URL devolvidos pelo servidor são escapados antes de renderizar;
- a aplicação não autentica automaticamente o utilizador no MCaliper nem inclui credenciais no URL.

## Auditoria e histórico

Adicionar, alterar ou remover uma ligação cria evento append-only com:

- folha e resultado afetados;
- peça/componente;
- utilizador;
- data/hora;
- ação (`added`, `updated`, `removed`);
- valor anterior e novo, segundo a política de auditoria e sensibilidade definida.

Reabrir ou rever uma folha não apaga a ligação anterior. Uma correção cria novo evento e mantém rastreabilidade.

## Estados

- vazio: `Nenhuma ligação adicionada`;
- edição local: `Alterações ainda não guardadas`;
- processamento: `A guardar ligação...`;
- sucesso: `Ligação guardada para esta peça`;
- inválida: explicar o formato/origem aceite;
- erro de rede/servidor: manter valor anterior e `Tentar novamente`;
- sem permissão: campo read-only, mantendo `Abrir MCaliper` quando autorizado para consulta.

## Critérios de aceitação

- todas as peças do Resumo podem receber uma ligação independente;
- PU e CS têm a mesma capacidade que CM, MF e BQ;
- uma ligação nunca é copiada automaticamente para outra peça;
- guardar associa o URL ao `control_result_id` e `job_on_component_id` corretos;
- `Abrir MCaliper` só fica ativo depois de existir uma ligação persistida;
- alterar o texto sem guardar não muda o destino do botão;
- permissões são validadas no servidor;
- URLs não autorizados são rejeitados;
- adicionar/alterar/remover fica auditado;
- o Resumo exportado pode indicar que existe controlo detalhado, mas não deve imprimir credenciais ou parâmetros sensíveis do URL.

## Limite do mockup

O mockup simula a alteração no browser. O coder deve substituir esta simulação pelos comandos de persistência e pelo esquema/origem oficial fornecido pela integração MCaliper.

## Limpeza informacional do Resumo

O Resumo é uma página operacional e mostra apenas informação necessária à decisão. Não mostrar `job_on_id`, `job_on_revision_id`, outras chaves internas ou avisos que expliquem como o contexto foi transportado entre páginas. Produção, referência e máquina são suficientes para confirmar o contexto ao utilizador.
