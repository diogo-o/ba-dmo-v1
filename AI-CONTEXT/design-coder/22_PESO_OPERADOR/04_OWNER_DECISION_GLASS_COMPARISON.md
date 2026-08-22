# Decisão do proprietário — Peso e comparação por CM

Estado: decisão funcional posterior aos Plans consolidados. Prevalece sobre comparações antigas baseadas em água, capacidade ou média global da produção.

## Folha do Peso

- Mantém a informação operacional e a rastreabilidade da folha histórica fornecida.
- O design passa para a linguagem azul da aplicação nova, com maior consistência entre ecrã e impressão.
- O snapshot estruturado aprovado é a fonte oficial; o PDF é gerado para impressão/partilha e pode ser regenerado.

## Peso do vidro

O valor comparável é o peso do vidro:

```text
Peso do vidro = CM + BQ − PU
```

O motor de cálculo do backend é a única implementação da fórmula. A UI apresenta componentes, resultado e arredondamento, mas não mantém uma fórmula paralela em JavaScript.

No Novo Controlo, o resultado aparece imediatamente por baixo do par `CM + Peso`, como no exemplo `CM 12 · Peso 152,43 → Peso do vidro 231,41 g`. É esse resultado persistido por CM que a Comparação consome.

## Comparação

- Sai a comparação de peso em água e capacidade.
- Sai a média global da produção como base de decisão.
- A criação da Comparação pertence à própria folha `Novo controlo`, imediatamente depois de `Resultados` e antes de `Enviar para aprovação`; não obriga o Operador a mudar para uma vista separada.
- O fluxo obrigatório é `Escolher produção anterior aprovada → confirmar Job On/revisão → criar tabela → associar os CM → rever → enviar para aprovação`.
- A produção anterior é resolvida por `job_on_id + job_on_revision_id`. O texto da referência, produção ou nome de ficheiro nunca é suficiente para criar a associação.
- A tabela só é criada depois da confirmação explícita da produção anterior. Se as leituras atuais mudarem, a tabela fica desatualizada e deve ser recriada antes do envio.
- Antes dessa confirmação, a zona de Resultados não apresenta qualquer `Diferença anterior`; diferenças e variações só existem na tabela ligada à produção anterior escolhida.
- Cada linha associa explicitamente um `CM atual` a um `CM da produção anterior`.
- O valor atual é exatamente o `Peso do vidro` mostrado por baixo da leitura desse CM no Novo Controlo. A Comparação não volta a pedir os componentes nem recalcula localmente o valor.
- A base é o resultado final aprovado do CM anterior selecionado, pertencente ao snapshot da produção anterior.
- A comparação mostra peso atual, peso anterior, diferença absoluta e variação percentual.
- Não existe correspondência automática por posição da linha ou pelo número do CM. A relação entre os dois CM é explícita, validada e persistida.
- O snapshot da produção anterior permanece imutável.
- Uma vista separada de Comparação pode existir para consultar/reabrir registos já guardados, mas não substitui o fluxo integrado de criação na folha atual.

## Identidade persistida

Cada par comparado guarda:

- Job On/revisão da produção atual;
- controlo/revisão atual;
- CM atual;
- Job On/revisão da produção anterior;
- controlo/revisão aprovado anterior;
- CM anterior;
- snapshot/cálculo de origem do `Peso do vidro`;
- peso do vidro atual e anterior;
- diferença e variação;
- autor e data/hora.

## PDF e Job On

O Job On abre primeiro o snapshot/página do Peso. A partir daí, o utilizador pode imprimir/exportar o PDF no workspace documental da produção. Produções anteriores abrem os seus próprios snapshots e documentos.
