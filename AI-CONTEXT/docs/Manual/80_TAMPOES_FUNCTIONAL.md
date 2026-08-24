# TAMPÕES — MODELO FUNCIONAL

## Índice

- [1. Objetivo](#1-objetivo)
- [2. Âmbito e Classificação](#2-âmbito-e-classificação)
- [3. Utilizadores e Acesso](#3-utilizadores-e-acesso)
- [4. Configuração Funcional](#4-configuração-funcional)
- [5. Tabela Principal e Interação](#5-tabela-principal-e-interação)
- [6. Gestão de Quantidades](#6-gestão-de-quantidades)
- [7. Categorias Opcionais de Quantidade](#7-categorias-opcionais-de-quantidade)
- [8. Edição de Configuração](#8-edição-de-configuração)
- [9. Movimentos / Transformação de Quantidade](#9-movimentos--transformação-de-quantidade)
- [10. Histórico e Auditoria](#10-histórico-e-auditoria)
- [11. Opções e Gestão de Campos](#11-opções-e-gestão-de-campos)
- [12. Fronteiras e Ownership](#12-fronteiras-e-ownership)
- [13. Regras Negativas](#13-regras-negativas)
- [14. Material Superseded](#14-material-superseded)
- [15. Detalhes de Implementação/UI Não Bloqueantes](#15-detalhes-de-implementaçãoui-não-bloqueantes)
- [16. Clarificações do Owner Fechadas](#16-clarificações-do-owner-fechadas)
- [17. Resumo Funcional Final](#17-resumo-funcional-final)

## 1. Objetivo

Ajudar o Operador / Controlador a saber quantos TP/tampões estão disponíveis para cada configuração técnica / máquina.

## 2. Âmbito e Classificação

O módulo Tampões é um módulo simples, autónomo e de nível superior (top-level).
O seu modelo fundamental é uma tabela simples de configurações + quantidades.

Não é:
- Planeamento de produção;
- Integração com Job On;
- Rastreamento de referências;
- Rastreamento de produção;
- Rastreamento individual de tampões;
- Um módulo rígido de ciclo de vida/estado.

## 3. Utilizadores e Acesso

- **Utilizador operacional confirmado:** Operador / Controlador.
- **Admin:** Não operacional por defeito.
- **Responsável:** Sem comportamento operacional específico confirmado para Tampões.

O módulo é de nível superior e atribuível por utilizador no Admin. Se não atribuído, não é mostrado na navegação normal e não há acesso funcional.

## 4. Configuração Funcional

As características essenciais de configuração atuais são:
- Máquina / Máquinas
- Diâmetro
- Calote

Uma configuração pode aplicar-se a uma ou várias máquinas. A(s) máquina(s) faz(em) parte da própria configuração. Não tratar a Máquina como metadados incidentais.

Os campos de configuração são editáveis. O operador pode criar/editar/gerir campos e valores de configuração. O modelo deve permanecer extensível para campos futuros. Não apresentar Diâmetro e Calote como os únicos campos permanentemente hard-coded.

## 5. Tabela Principal e Interação

A UI central é uma tabela simples. Cada linha representa uma configuração.

Colunas conceptuais esperadas:

| Máquina/Máquinas | Diâmetro | Calote | Quantidade / categorias |

O utilizador deve perceber imediatamente: "Quantos tampões tenho disponíveis desta configuração para esta máquina?"

### Modelo de Interação

**Um clique na linha:**
- Seleciona a configuração;
- Expõe ações rápidas de quantidade (Adicionar quantidade, Remover quantidade, escolher categoria/saldo opcional quando relevante).

**Duplo clique na linha:**
- Abre o editor de configuração para essa linha;
- O operador pode editar Máquina(s), Diâmetro, Calote, etc.;
- Após guardar, a configuração atualizada aparece na tabela principal.

## 6. Gestão de Quantidades

O módulo é controlo de quantidade agregada. Não existem números individuais de tampões.

Preservar:
- Quantidades inteiras;
- Sem saldo negativo;
- Alterações apenas confirmadas após persistência;
- Atribuição de operador;
- Atribuição de data/hora;
- Histórico de movimentos apenas para acrescentar (append-only);
- Correção auditável.

## 7. Categorias Opcionais de Quantidade

As classificações de quantidade são opcionais (ex: Enchidos / Por encher, Maquinados / Por maquinar).
Existem apenas para ajudar o operador a separar quantidades quando útil.

Não são:
- Obrigatórias;
- Necessárias para todas as configurações;
- Um ciclo de vida rígido;
- Uma máquina de estados obrigatória.

A informação essencial permanece: QUANTIDADE TOTAL DISPONÍVEL POR CONFIGURAÇÃO / MÁQUINA.

## 8. Edição de Configuração

Duplo clique na linha → alterar metadados de configuração (Máquina(s), Diâmetro, Calote, etc.) → guardar → mesma linha de configuração atualizada.

O Operador / Controlador pode também criar uma **nova configuração**, definindo pelo menos Máquina/Máquinas, Diâmetro e Calote; depois de guardar, a nova configuração passa a aparecer como uma nova linha da tabela principal.

Não confundir edição de configuração com transformação de quantidade.

## 9. Movimentos / Transformação de Quantidade

Quando se move intencionalmente alguma quantidade de uma configuração para outra:
→ movimento de quantidade;
→ origem e destino preservados;
→ histórico append-only.

## 10. Histórico e Auditoria

Preservar histórico auditável.

Histórico de movimento de quantidade deve preservar:
- Data/hora;
- Configuração;
- Movimento/ação;
- Categoria/saldo opcional;
- Quantidade;
- Antes / Depois;
- Operador.

Histórico de edição de configuração deve preservar:
- O que mudou;
- Valor anterior / Novo valor;
- Quem mudou;
- Quando.

Sem sobrescrita silenciosa de factos históricos.

## 11. Opções e Gestão de Campos

Áreas preferenciais do módulo:

- **Registo / Tabela Principal:** Tabela principal de configuração + quantidade.
- **Histórico:** Movimentos auditáveis / alterações de configuração.
- **Opções / Configuração:** Gerir campos, valores, configurações, valores de máquina, diâmetro, calote, campos futuros.

Uma área de Consulta separada é desnecessária se duplicar a tabela principal. Não preservar abas antigas apenas porque existiram historicamente.

## 12. Fronteiras e Ownership

Tampões é proprietário (owns):
- Configurações de Tampões;
- Associação Máquina/Máquinas nessas configurações;
- Diâmetro, Calote e outros campos configurados;
- Quantidades e categorias opcionais;
- Movimentos/histórico;
- Configurações/definições.

Tampões NÃO é proprietário de:
- Job On;
- Produção;
- Referência;
- Registos de negócio de outros módulos.

## 13. Regras Negativas

Tampões NÃO:
- Associa TP a uma Referência;
- Associa TP a uma Produção;
- Interage funcionalmente com Job On;
- Envia dados para Job On;
- Consome contexto de Job On;
- Planeia produção;
- Reserva stock para produção;
- Rastreia números individuais de TP;
- Requer um ciclo de vida rígido;
- Requer Enchidos/Por encher;
- Requer Maquinados/Por maquinar;
- Infere Máquina a partir de Referência;
- Infere Referência a partir de Máquina;
- Altera outros módulos;
- Permite quantidade negativa;
- Reescreve história silenciosamente.

## 14. Material Superseded

As seguintes regras de design antigas estão superseded (ultrapassadas) pelo modelo atual do Owner:
- Relação opcional com Job On;
- Planeamento (Planeamento tab, necessidade planeada, data esperada, estados de plano, histórico de plano, reserva de produção, planeamento Job On);
- Relação produção/referência;
- Regras de reserva de planeamento;
- Antiga interpretação obrigatória de Enchidos/Por encher;
- Antiga questão sobre Maquinado como estado obrigatório;
- Antiga incerteza sobre onde as configurações são criadas.

## 15. Detalhes de Implementação/UI Não Bloqueantes

Detalhes menores como:
- Mínimos/máximos numéricos exatos;
- Controlo UI exato para selecionar múltiplas máquinas;
- Redação/nome final das categorias opcionais de quantidade;

São apenas detalhes de implementação/UI. Não são bloqueadores funcionais.

## 16. Clarificações do Owner Fechadas

- Tampões é autónomo.
- Sem relação com Job On.
- Sem relação com Produção.
- Sem relação com Referência.
- Sem Planeamento.
- Máquina(s) faz parte da configuração.
- Configuração essencial atual = Máquina(s) + Diâmetro + Calote.
- Campos/valores/configurações editáveis.
- Um clique = ações de quantidade.
- Duplo clique = editar configuração.
- Sem numeração individual de TP.
- Categorias de quantidade opcionais.
- Sem ciclo de vida obrigatório.
- Operador / Controlador é o utilizador operacional.
- Zero questões funcionais abertas.

> **OWNER DECISION REQUIRED — conflito preservado (quarentena transversal):** as clarificações acima (Tampões autónomo; sem relação com Job On / Produção / Referência; sem Planeamento) conflitam com `10_JOB_ON_FUNCTIONAL.md` §6.1, onde **TP/Tampão é configuração específica de produção do Job On** (PU / CS / TP, configurados manualmente pelo Responsável no Job On). Ambas as afirmações são preservadas integralmente; a sua reconciliação é decisão do Owner — **não** é resolvida neste conjunto documental e não reabre as clarificações fechadas acima.

## 17. Resumo Funcional Final

O módulo Tampões é uma tabela simples e autónoma de configurações (Máquina/Máquinas, Diâmetro, Calote) e quantidades agregadas. Permite ao Operador / Controlador ver rapidamente a **quantidade disponível** por configuração/máquina; um clique seleciona a linha e expõe as ações rápidas de adicionar/remover quantidade, enquanto o duplo clique abre a edição da configuração. Não há integração com Job On, Produção, Referências ou Planeamento. O histórico é auditável e as categorias de quantidade são opcionais.

## Implementation Pointers

### Relevant implementation areas

- Application: autonomous configuration table (Machine(s) / Diâmetro / Calote) with aggregated quantities; single click selects the row and exposes quantity actions; double click edits the configuration; optional quantity categories; quantity movements with append-only history.
- Web / UI: exact numeric min/max, the multi-machine selection control, and the final naming of the optional quantity categories are non-blocking implementation details (see §15).
- Technical map: `maps\13_TAMPOES.md` (verify freshness before use).

### Known implementation gaps

- None verified in this document set.

### Design reference

- `AI-CONTEXT\design-coder\33_TAMPOES_01_VISUAL_AUTHORITY_tampoes.html`

### Cross-module dependencies

- Autonomous module (no functional integration with other modules); operational user: Operador / Controlador.