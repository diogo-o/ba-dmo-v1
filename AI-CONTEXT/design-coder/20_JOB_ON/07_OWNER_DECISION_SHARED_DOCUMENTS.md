# Decisão do proprietário — documentos partilhados da produção

Estado: decisão funcional definitiva posterior aos Plans consolidados. A estrutura detalhada em `docs/DOCUMENT_DIRECTORY_STRUCTURE_HANDOFF.md` prevalece sobre qualquer convenção anterior.

## Regra simples

O Controlo é o ponto de associação documental da produção. Os snapshots de `Resumo`, `Peso` e `Pegamentos` ficam no programa como fonte oficial. Os PDFs são outputs derivados, imprimíveis e regeneráveis, guardados na estrutura criada automaticamente a partir do Job On. O utilizador configura apenas o diretório principal atual; o Job On não pede nomes de pastas nem duplica dados.

O mesmo princípio aplica-se ao conjunto de impressão do próprio Job On: a revisão estruturada é a fonte oficial; as quatro páginas A4 são uma projeção opcional e regenerável dessa revisão. HTML e PDF nunca substituem o snapshot.

## Identidade antes do caminho

O contexto é resolvido a partir de um Job On existente e validado:

- `job_on_id`;
- `job_on_revision_id`;
- Produção;
- Referência;
- Máquina/Linha.

O caminho textual e o nome do ficheiro são metadados. Nunca substituem estes identificadores e nunca são aceites como forma de associar manualmente um controlo a uma produção.

## Estrutura automática por Referência e Produção

- O utilizador autorizado escolhe em `Definições` apenas um diretório principal atual.
- O sistema cria/reutiliza primeiro a pasta da Referência e, dentro dela, a pasta da Produção.
- Dentro da Produção cria pastas independentes para `Peso`, `Pegamentos` e `Resume`.
- Referência, Produção e Máquina/Linha vêm do snapshot do Job On; o utilizador não escreve os nomes.
- A operação é idempotente: a mesma Referência e Produção reutilizam as mesmas pastas.
- Uma nova Produção da mesma Referência reutiliza a pasta da Referência e cria uma nova subpasta de Produção.

Exemplo:

```text
Diretório principal/
└── 5447T173/
    ├── 202601/
    │   ├── 202601_5447T173_C3_Peso/
    │   ├── 202601_5447T173_C3_Pegamentos/
    │   └── 202601_5447T173_C3_Resume/
    └── 202602/
        ├── 202602_5447T173_C3_Peso/
        ├── 202602_5447T173_C3_Pegamentos/
        └── 202602_5447T173_C3_Resume/
```

Decisão confirmada: o Resumo é uma página consolidada da aplicação, construída a partir do snapshot do Controlo, com opção `Imprimir / Exportar PDF`. Peso e Pegamentos seguem a mesma regra de consulta no programa; os respetivos PDFs continuam disponíveis como outputs finais.

## Registo dos documentos

Cada output guarda no servidor, pelo menos:

- `document_id`;
- tipo `Resumo | Peso | Pegamentos`;
- `job_on_id` e `job_on_revision_id` usados;
- ID e revisão do controlo de origem;
- nome físico;
- pasta da Referência, pasta da Produção e pasta do tipo documental;
- estado de gravação local;
- data/hora e utilizador responsável.

Os dados estruturados e snapshots imutáveis continuam no servidor. O PDF é um output derivado, não a fonte primária do controlo. Não usar um ficheiro JSON solto como base de dados; quando existir um snapshot JSON, ele é persistido e versionado no registo de domínio correspondente.

## Consulta no Job On

Na produção aberta, o Job On apresenta a área `Controlo da produção` com `Ver Peso`, `Ver Pegamentos` e `Ver Resumo`. Cada ação abre primeiro a página/snapshot da aplicação. Dentro dessa vista, `Imprimir / Exportar PDF` gera novamente o documento da revisão aberta e `Abrir PDF guardado` usa o output físico quando disponível. O mesmo bloco existe quando se abre uma produção anterior.

Cada acesso apresenta um estado real:

- `Disponível`;
- `Ainda não gerado`;
- `A aguardar aprovação`;
- `Workspace indisponível`;
- `Ficheiro em falta`;
- `Versões disponíveis`.

Um acesso só fica ativo quando pode concluir a operação correspondente. Não termina num erro genérico.

O Job On consulta o mesmo manifesto documental do Controlo pelo `job_on_id + job_on_revision_id`. Não procura ficheiros por texto livre ou por nome aproximado, não cria uma cópia e não mantém uma segunda configuração de diretório.

Se o ficheiro não estiver acessível naquele computador, o registo e os metadados continuam visíveis; a interface indica `Ficheiro indisponível` e permite voltar a autorizar o diretório partilhado. Uma falha local não desfaz a aprovação nem elimina o histórico.

## Revisões

O documento fica historicamente associado à revisão do Job On usada na sua geração. Uma nova revisão do Job On não reatribui documentos antigos. A página pode destacar os documentos da revisão corrente e permitir consultar os de revisões anteriores no histórico.

Quando existirem várias versões válidas, o Job On abre primeiro a versão atual e apresenta as anteriores antes de trocar de versão.

## Permissões e abertura

- Só abre quem tiver acesso ao Job On e ao tipo de documento correspondente.
- A falta de autorização local ao workspace é diferente de falta de permissão funcional.
- A interface não expõe caminhos locais completos sem necessidade.
- Consultar um documento nunca altera a produção, o Job On ou o Controlo.

## Regra substituída

Fica substituída a arquitetura anterior em que:

- o Job On criava a pasta durante a sua própria criação;
- Peso/Pegamentos escolhiam subpastas separadamente;
- o Job On mantinha uma diretoria documental paralela.

Existe uma pasta estável por Referência; dentro dela, uma pasta por Produção; e dentro de cada Produção, pastas próprias para Peso, Pegamentos e Resume. Toda a estrutura abaixo do diretório principal é criada automaticamente pelo programa.
