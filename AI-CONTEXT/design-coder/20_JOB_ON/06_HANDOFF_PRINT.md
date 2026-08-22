# Job On - handoff das quatro folhas de impressão

Estado: contrato de implementação para coder  
Artefacto visual: `job-on-impressao-4-folhas.html`

## 1. Objetivo

O Job On imprime um pacote único de quatro páginas A4. As imagens históricas fornecidas são referência de conteúdo e familiaridade operacional. Não são um esquema de base de dados nem uma obrigação de reproduzir Excel, controlos de folha de cálculo ou erros gráficos.

O comando `Imprimir 4 folhas` abre uma pré-visualização separada. A impressão deve produzir exatamente quatro páginas, pela ordem descrita neste documento, sem cabeçalhos/rodapés do browser, botões ou partes da interface principal.

## 2. Fonte dos dados

Todas as páginas são projeções do mesmo snapshot imutável:

- `job_on_id`;
- `job_on_revision_id`;
- cabeçalho da produção;
- componentes/ferramentas guardados nessa revisão;
- linhas CAL guardadas nessa revisão;
- notas gerais e específicas;
- imagem do artigo associada à Referência (ver `08_OWNER_DECISION_ARTICLE_IMAGE.md`).

A impressão nunca consulta novamente dados mestre para substituir valores do snapshot. Informação live de Armazém, reparação, localização ou utilização atual não altera um documento histórico.

### Regra obrigatória da imagem

A imagem impressa é a imagem associada à Referência. As folhas de impressão não têm upload, seletor ou imagem própria.

- a origem é a imagem associada à Referência no diretório do servidor da empresa (ver `08_OWNER_DECISION_ARTICLE_IMAGE.md` — a imagem pertence ao artigo/referência mestre, não a cada revisão do Job On);
- selecionar/substituir/remover a imagem acontece apenas junto da Referência, não por revisão do Job On;
- uma revisão histórica consome a imagem da Referência; não tem uma imagem própria da revisão;
- as páginas 1, 2 e 4 reutilizam o mesmo recurso e a mesma versão;
- apenas a folha obrigatória do Job On apresenta a imagem; não a duplicar em todas as folhas;
- se não existir imagem associada à Referência, todas as áreas de artigo mostram `Sem imagem do artigo`;
- a geração não pode procurar uma imagem semelhante nem usar um fallback de outra produção.

## 3. Ordem e responsabilidade de cada página

### Página 1 - Ficha de artigo completa

Objetivo: documento técnico completo de referência.

Conteúdo obrigatório:

- Referência, Produção, Linha/Máquina, Secções, Gota, Peso e Processo;
- datas de entrada e saída;
- CM/MP, MF, TP, BQ, AN, PU, ARR, CS e PI;
- referência, lote, medidas, tolerâncias, utilização e notas quando existirem;
- notas gerais do Job On;
- imagem do artigo;
- resumo de calibres.

### Página 2 - Job On Moldes operacional

Objetivo: leitura rápida das ferramentas e instruções de preparação.

Mantém a estrutura familiar `Ferramenta | Dados | Notas Job-On`, com menos detalhe dimensional do que a página 1. Não pode omitir ferramentas associadas ao fabrico.

### Página 3 - Folha de equipa

Objetivo: preparação física por lado da máquina.

Grupos obrigatórios:

- Lado do Contra-Molde: CM/MP, TP, PU e ARR;
- Lado do Molde Final: MF, fundo final, BQ, AN, CS e PI;
- calibres e quantidades;
- área de observações da equipa.

Esta página reorganiza dados; não cria novas relações de domínio.

### Página 4 - Ficha de artigo com imagem

Objetivo: versão visual próxima da ficha histórica, com artigo destacado.

Inclui ferramentas, notas gerais, imagem grande do artigo e faixa de calibres. A imagem usa `object-fit: contain`; nunca é cortada.

## 4. Mapeamento canónico

| Impresso | Família no Job On | Campos mínimos |
|---|---|---|
| Contra-Moldes | `CM` ou `MP`, conforme nomenclatura configurada | referência, lote, tipo, diâmetros, folgas, utilização, notas |
| Moldes Finais | `MF` | referência, lote, fundo final, diâmetros, folgas, utilização, notas |
| Tampões | `TP` | referência, diâmetro PS, bacia PS, quantidade, notas |
| Boquilhas | `BQ` | referência, lote, utilização, quantidade, notas |
| Anel/Anilha | `AN` | referência, notas e relação visual com BQ |
| Punções/Porta-unidades | `PU` | referência, versão, quantidade, notas |
| Arrefecedores | `ARR` | referência, quantidade, notas e relação visual com PU |
| Cabeça de sopro | `CS` | referência, furos, tubo, quantidade, notas |
| Pinças | `PI` | diâmetro, material/tipo, quantidade, notas |
| Forro | `FO` | tipo, quantidade e notas, quando aplicável |
| Calibres | `CAL[]` | elemento, valor e quantidade em máquina |

Os nomes históricos podem ser apresentados no papel por familiaridade. O código persistido continua a usar as famílias canónicas.

## 5. Campos vazios e conteúdo variável

- Um campo opcional vazio imprime `-` apenas quando a ausência precisa de ficar explícita.
- Uma família não aplicável pode imprimir uma linha curta `Não aplicável`; não deve desaparecer silenciosamente se fizer parte do template autorizado.
- Notas longas podem reduzir ligeiramente a tipografia até ao mínimo definido, mas nunca podem ser cortadas.
- Se o conteúdo exceder a área, a geração falha com mensagem explícita e pede revisão; não cria uma quinta página silenciosamente.
- A imagem ausente mostra `Sem imagem do artigo` dentro da área reservada.

## 6. Regras visuais e de impressão

- formato A4 vertical;
- quatro páginas exatas;
- margens aproximadas de 9 mm;
- preto/cinza como base e azul apenas como identidade discreta;
- bordas finas e legíveis em impressão monocromática;
- tipografia mínima recomendada: 8 pt em detalhe, 10 pt em valores, 14 pt em famílias;
- cada página inclui Produção, revisão e `Página n/4`;
- `@media print` remove toolbar, sombras e fundo da pré-visualização;
- cada `.print-page` usa quebra de página obrigatória;
- não depender de escala manual do utilizador.

## 7. Fluxo do comando

1. O utilizador abre uma revisão concreta do Job On.
2. Seleciona `Imprimir 4 folhas`.
3. O cliente envia `job_on_id + job_on_revision_id` ao endpoint de impressão.
4. O servidor carrega o snapshot exato e valida os dados mínimos.
5. O template é renderizado com as quatro páginas.
6. A pré-visualização identifica a revisão.
7. O utilizador imprime ou exporta PDF.

Não imprimir a partir de valores não guardados no DOM. Em modo edição, o comando deve exigir primeiro `Guardar nova revisão` ou imprimir explicitamente uma pré-visualização marcada como `Rascunho não guardado`, conforme decisão do produto.

## 8. Contrato técnico sugerido

Endpoint ilustrativo:

```text
GET /job-ons/{jobOnId}/revisions/{revisionId}/print
Accept: text/html ou application/pdf
```

View model mínimo:

```json
{
  "jobOnId": "job-c3-202602",
  "revisionId": "3",
  "reference": "7080C002",
  "production": "202602",
  "machine": "C3",
  "dates": { "start": "2026-08-17", "end": "2026-08-20" },
  "sections": 12,
  "drop": 3,
  "weight": { "value": 145, "unit": "g" },
  "process": "NNPB",
  "components": [],
  "calibrationRows": [],
  "generalNotes": "...",
  "articleImage": { "url": "...", "version": "..." }
}
```

## 9. Auditoria e segurança

- gerar/imprimir cria evento de auditoria com utilizador, Job On, revisão e data/hora;
- o servidor valida autorização de consulta/impressão;
- URLs de imagem não aceitam origens arbitrárias fornecidas pelo cliente;
- texto é escapado antes de entrar no HTML;
- documentos históricos mantêm a revisão usada, mesmo após revisões posteriores.

## 10. Critérios de aceitação

- o botão abre o pacote referente à revisão atualmente selecionada;
- o pacote contém exatamente quatro páginas A4;
- as quatro páginas usam os mesmos IDs e valores do snapshot;
- nenhuma família obrigatória desaparece;
- BQ/AN e PU/ARR mantêm proximidade sem alterar a independência dos componentes;
- página 3 separa corretamente os dois lados operacionais;
- a imagem é a da Referência associada ao Job On e nunca é cortada;
- notas não sobrepõem tabelas;
- impressão monocromática permanece legível;
- não existem botões, sombras ou navegação no PDF;
- cabeçalho e rodapé identificam Produção, revisão e página;
- falhas de dados ou overflow são explícitas e não produzem documentos parciais.

## 11. Limites do mockup

O HTML atual usa dados demonstrativos fixos para validar composição e impressão. O coder deve substituir esses valores pelo view model da revisão, mantendo o contrato acima. O mockup não decide nomes definitivos de endpoints, motor de PDF nem política final para rascunhos não guardados. A propriedade da imagem já está resolvida em `08_OWNER_DECISION_ARTICLE_IMAGE.md` e não é decidida aqui.
