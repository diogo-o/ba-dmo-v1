# Ferramentas — criação de Referência e lotes

Estado: base funcional para design e implementação V1  
Âmbito: primeira criação de uma ferramenta e criação posterior de novos lotes

## 1. Objetivo

O mesmo fluxo cobre:

- uma Referência de ferramenta nova que nunca trabalhou;
- uma ferramenta existente fisicamente, mas introduzida no sistema pela primeira vez;
- um novo lote de uma Referência já registada.

Não criar identidades paralelas no Armazém, Job On ou Reparação. Cada integração usa o ID estável criado no domínio da ferramenta.

## 2. Dois níveis de informação

### Referência da ferramenta

Identidade comum aos respetivos lotes:

- Tipo de ferramenta: CM, MF, BQ ou outro tipo confirmado;
- Referência;
- Nome técnico;
- Owner plant, com `MG — Marinha Grande` como valor inicial confirmado.

### Lote da ferramenta

Dados próprios da ocorrência física/operacional:

- Lote;
- Processo: `NNPB` ou `PS` para lotes criados no módulo Peso;
- quantidade;
- Máquinas/Linhas onde pode trabalhar;
- Nome/número do desenho;
- revisão do desenho, quando aplicável;
- restantes características específicas do tipo de ferramenta.

Uma Referência pode possuir vários lotes. Criar um lote não duplica a Referência mestre.

## 3. Nome técnico

O `Nome técnico` é um atributo principal de identificação e ajuda a distinguir ferramentas que não ficam claras apenas pelo código da Referência.

Não confundir:

- `Nome técnico`: designação humana/canónica da ferramenta;
- `Nome/número do desenho`: identificador documental do desenho associado ao lote;
- `Referência`: código operacional da ferramenta.

Regras de UI:

- aparece imediatamente junto da Referência;
- participa na pesquisa;
- aparece nas listas, seletores do Job On e detalhes da ferramenta;
- não fica escondido apenas na ficha expandida;
- não é tratado automaticamente como único sem regra confirmada;
- alterações posteriores devem ser auditáveis.

## 4. Página `Criar novo registo`

É uma página própria dentro do domínio da ferramenta, não um modal pequeno.

Ordem visual:

1. Identificação da Referência;
2. Compatibilidade;
3. Primeiro lote;
4. Informação do desenho;
5. características específicas do tipo;
6. ações.

### Identificação da Referência

| Campo | Componente | Regra visual |
|---|---|---|
| Tipo | seletor/dropdown | pode vir definido pelo módulo atual |
| Referência | texto | largura média |
| Nome técnico | texto | campo largo e visualmente principal |
| Owner plant | dropdown/seleção | `MG — Marinha Grande` predefinido na V1 |

Não inventar outras plantas ou processos até existirem no catálogo real.

### Compatibilidade

`Máquinas/Linhas onde trabalha` usa os cartões de seleção canónicos:

- B1;
- B2;
- B3;
- C1;
- C2;
- C3.

Pode selecionar várias. A relação é guardada explicitamente; não é deduzida a partir da Referência ou desenho.

### Primeiro lote

| Campo | Dimensão esperada |
|---|---|
| Lote | compacta |
| Processo do lote | dropdown `NNPB`/`PS` no módulo Peso |
| Quantidade | compacta, numérica |
| Nome/número do desenho | média |
| Revisão | compacta |

Campos específicos de CM, MF e BQ aparecem depois desta base comum e continuam definidos pelo respetivo domínio.

## 5. Guardar uma Referência nova

Ao guardar:

1. validar campos obrigatórios;
2. verificar correspondências existentes sem fundir resultados parecidos;
3. criar a Referência mestre;
4. criar o primeiro lote associado;
5. persistir ambos como uma operação consistente;
6. só depois apresentar sucesso e abrir a ficha criada.

Se a criação do lote falhar, não deixar uma Referência mestre parcialmente criada sem indicação/recuperação prevista pelo contrato técnico.

Uma pesquisa sem resultados nunca cria automaticamente uma ferramenta. O utilizador entra explicitamente em `Criar novo registo`.

## 6. Criar novo lote por duplicação

Na ficha/lista de lotes de uma Referência existente, selecionar um lote e usar o botão externo `Novo lote a partir deste`.

A lista respeita o padrão global:

- um clique seleciona;
- duplo clique abre o lote;
- o botão de duplicação fica fora da lista.

### Informação herdada e protegida

O novo lote mantém:

- Tipo;
- Referência;
- Nome técnico;
- Processo;
- Owner plant.

Estes campos aparecem read-only. Alterá-los exige editar a Referência mestre através de outro fluxo auditável.

### Informação copiada e editável

O novo rascunho parte do lote escolhido e permite ajustar:

- novo número de lote, obrigatório;
- quantidade;
- adicionar ou remover Máquinas/Linhas permitidas;
- Nome/número do desenho;
- revisão do desenho;
- características específicas do tipo que possam variar por lote.
- configuração da tab `Verificações`: manter, editar, adicionar, remover, desativar ou reativar linhas para o novo lote.

Guardar cria um novo lote e não altera o lote de origem. A ficha identifica `Criado a partir do lote …`.

As regras de verificação são copiadas como configuração do novo lote. Ocorrências, checks, operadores e histórico do lote anterior nunca são copiados.

## 7. Lista de Referências

Pesquisa por:

- Referência;
- Nome técnico;
- lote;
- desenho;
- Máquina/Linha;
- processo do lote;
- Owner plant.

Colunas mínimas:

| Tipo | Referência | Nome técnico | Owner plant | Lotes | Processo do lote | Máquinas/Linhas |
|---|---|---|---|---|---|---|

Interação:

- um clique seleciona;
- duplo clique abre a ficha da Referência;
- `Criar novo registo` e `Novo lote a partir deste` ficam fora da lista;
- resultados ambíguos exigem escolha explícita.

## 8. Ficha da Referência

O topo mostra Tipo, Referência, Nome técnico em destaque e Owner plant. Processo é apresentado no respetivo lote quando esse campo pertence ao fluxo do Peso.

A lista de lotes mostra:

- lote;
- processo do lote, quando aplicável;
- quantidade;
- Máquinas/Linhas permitidas;
- desenho e revisão;
- informação específica relevante;
- estado atual vindo do domínio da ferramenta, quando aplicável.

Cada lote inclui a tab `Verificações` definida em `../20_JOB_ON/05_BRIEF_VERIFICATIONS.md` (a cópia dentro do pacote do contrato de verificações).

Vida útil, `Sucatado`, `Arquivado` e outros estados continuam no domínio da ferramenta e não são copiados para Armazém.

## 9. Integrações

### Job On

- pesquisa por Referência ou Nome técnico;
- resultados mostram Nome técnico, lote e máquinas permitidas;
- filtra lotes pela máquina do Job On usando relações registadas;
- não interpreta o desenho para deduzir compatibilidade;
- associa o ID estável do lote.

### Armazém

- apresenta Referência, Nome técnico e lote como identificação read-only;
- regista apenas posição e movimentos;
- não edita compatibilidade, desenho, processo, vida útil ou estado.

### Reparação

- associa intervenções e listas programadas ao ID estável da ferramenta/lote;
- consulta os dados identificadores;
- mantém o próprio fluxo sem duplicar a Referência mestre.

## 10. Evidência do procedimento de desenhos

O procedimento `OP 99 PMD 02/d — Drawing numbers` confirma:

- estruturas distintas para desenhos de acabamento, artigo, moldes, suplementos e acessórios;
- desenhos de moldes com componentes de modelo, dimensão, tipo, processo, ventilação/material e revisão;
- `MP`, `MF` e `FF` como tipos documentais de molde;
- revisão por sufixo alfabético e códigos de ensaio `E1`, `E2`, etc.;
- desenhos aprovados controlados pelo Product Development;
- cópias impressas não controladas.

Implicação para a UI:

- guardar Nome/número do desenho explicitamente;
- guardar revisão separadamente quando disponível;
- permitir abrir a fonte oficial quando existir integração;
- não gerar, decompor ou validar automaticamente o código sem contrato confirmado.

O documento usa códigos de processo documentais (`PS`, `SS`, `NN`, `P4`), enquanto o requisito operacional atual pede `NNPB/PS`. A correspondência não deve ser inferida automaticamente.

## 11. Estados vazios e conflitos

- Referência não encontrada: oferecer `Criar novo registo`, sem criação automática;
- Referência existente com mesmo código: mostrar resultados e Nome técnico antes de permitir nova criação;
- lote já existente na mesma Referência: bloquear duplicação e indicar conflito;
- nenhuma Máquina/Linha selecionada: validar segundo a obrigatoriedade confirmada;
- desenho não definido: mostrar `Não definido`;
- falha ao guardar: preservar o rascunho e não mostrar sucesso.

## 12. Questões por confirmar

- tipos abrangidos inicialmente além de CM, MF e BQ;
- campos obrigatórios por tipo;
- se Nome técnico precisa de unicidade ou apenas pesquisa/identificação;
- se Máquinas/Linhas pertencem sempre ao lote ou podem ter base na Referência;
- formato e unicidade do lote por Referência;
- catálogo futuro de owner plants;
- relação oficial entre `NNPB/PS` e códigos documentais;
- fonte oficial para abrir o desenho aprovado;
- características editáveis ao duplicar cada tipo.

## 13. Critérios de aceitação do mockup V1

- `Criar novo registo` cria Referência e primeiro lote na mesma página;
- Nome técnico tem destaque e participa nas pesquisas;
- Processo usa `NNPB/PS` e Owner plant começa em `MG — Marinha Grande`;
- Máquinas/Linhas permitem seleção múltipla;
- a lista segue clique/duplo clique canónicos;
- `Novo lote a partir deste` fica fora da lista;
- novo lote mantém identidade mestre e permite alterar dados próprios;
- quantidade, linhas permitidas, desenho e revisão identificam o lote;
- lotes anteriores nunca são alterados pela duplicação;
- Job On, Armazém e Reparação usam IDs estáveis;
- códigos de desenho não são inferidos automaticamente.
