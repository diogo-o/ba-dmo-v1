# BA DMO — Functional Rules & Owner Decisions (source of truth)

> This concise document preserves the unique **functional/business rules and owner
> decisions** that previously lived inside `IMPLEMENTATION_STATE.md`. It is a concise
> source of truth; the full functional/technical history remains available in
> `docs/IMPLEMENTATION_STATE.md` (which carries the `DESIGN IMPLEMENTATION RESET —
> 2026-08-22` banner, so its visual/design claims are historical only).
>
> It is a **source-of-truth reference for requirements**, NOT a completion/parity
> claim and NOT a visual audit. Treat it as the authoritative statement of the
> module boundaries and business rules below. Where any of these items conflict with
> the authoritative design mockups / handoffs in `design/portal-dmo-design-final/`,
> the design authority governs presentation; these rules govern **behavioral
> invariants, ownership boundaries, and owner decisions**.
>
> These facts are **current requirements**, not historical reports. Visual parity of
> the shipped UI against the design must still be independently audited.

---

## 1. Overall architecture (authoritative)

- Clean architecture, Razor Pages (ASP.NET Core `net10.0`), Npgsql + Dapper,
  PostgreSQL/Supabase. Projects: `BA.Dmo.Domain`, `BA.Dmo.Application`,
  `BA.Dmo.Infrastructure`, `BA.Dmo.Web`; tests: `BA.Dmo.UnitTests`,
  `BA.Dmo.IntegrationTests`.
- Canonical dependency direction: Application→Domain; Infrastructure→Application+Domain;
  Web→Application+Infrastructure; UnitTests→Domain+Application; IntegrationTests→Web+Infrastructure.
- Modules: Job On, Peso, Pegamentos, Ferramentas, Armazém, Tampões, Reparação Interna,
  Reparação Externa, História, Boquilhas, Controlo, Admin, Login.
- Sole canonical cross-module append-only fact source: `audit_events` (N01).

## 2. History / immutability (global)

- Corrections are always NEW rows; the original fact never disappears
  (GLM-DATA-07). A Peso/Pegamentos/Reparação-Interna record stays pinned to the exact
  `job_on_revision_id` under which it was performed; later Job On revisions must not
  reinterpret that historical context.
- Canonical direction: `Job On revision → production context → dependent record
  (Peso control / Pegamentos / Reparação Interna)`.
- No scoring / rankings / performance-judgement logic.

## 3. Job On (U-13/related)

- Job On é o **contexto central de produção/planeamento**, a **folha operacional** da produção e o **hub central de consulta operacional** dessa produção. Para cada produção, o Job On reúne para compreensão conjunta: planeamento/calendário; o Job On exato; a revisão exata; Referência/Produção/Máquina; ferramentas/lotes exatos; verificações; os registos associados de Controlo e de Reparação Interna; histórico; e impressão/documentos.
- **Fronteira de posse (não duplicar):** o Job On **integra/liga** os registos de Controlo e de Reparação Interna associados à produção para consulta, mas **não é dono desses registos**. O Controlo é dono dos registos/resultados de controlo; a Reparação Interna é dona dos registos de reparação. Não criar nem descrever um segundo modelo geral de rastreio de produção/ferramentas dentro do Controlo ou de outro módulo.
- **Contexto central único:** existe um único contexto central de produção/ferramentas — o **Job On**. Uma revisão de Job On define o contexto exato da produção: Referência, Produção, Máquina/Linha, CM+lote exato, MF+lote exato, BQ+lote exato e restantes ferramentas/componentes. Os módulos operacionais consomem esse contexto; não o reconstroem de forma independente nem redefinem a configuração de ferramentas planeada para a produção. As etapas de célula/lote (CM/MF/BQ) exatas são herdadas pelos módulos que abrem uma produção de Job On. (Exceção de domínio, sem efeito no plano de produção: um módulo pode identificar outro lote válido para a sua própria regra de domínio — p. ex., o Controlo regista/controla um lote recém-chegado — ver §10.)
- **Produção planeada por Máquina/Linha:** cada Job On representa uma produção concreta numa **Máquina/Linha** concreta (ex.: B1, B2, B3, C1, C2, C3). Para essa produção, o RESPONSÁVEL escolhe a configuração exata de ferramentas principais — CM, MF e BQ — cada uma com Referência, Lote **e** Máquina/Linha (todos os campos que façam parte do registo da opção).
- **Identidade da opção = Tipo + Referência + Lote + Máquina/Linha:** Reference+Lote **não basta** para distinguir todas as opções registadas. A mesma Referência pode existir para várias Máquinas; o **mesmo número de Lote pode existir para a mesma Referência em Máquinas diferentes** (ex.: `CM | Referência 5447 | Lote 3 | B1` vs `CM | Referência 5447 | Lote 3 | C3`). Isto **não** significa «máquina diferente ⇒ lote diferente» — o lote pode ser igual ou diferente. Para registo/consulta operacional, o contexto da opção preserva **Tipo + Referência + Lote + Máquina/Linha**.
- **Decisão de ferramentas pertence ao RESPONSÁVEL (não automática):** o Job On **não infere** qual a ferramenta correta para uma produção. Não existe regra de decisão automática do tipo «produção em B1 ⇒ selecionar automaticamente CM X». A Máquina/Linha pode ser usada como contexto visível, informação distintiva e ajuda de filtragem onde já estiver definida, mas a **escolha final das ferramentas pertence ao RESPONSÁVEL** que prepara o Job On. O sistema apoia a decisão do RESPONSÁVEL; não a substitui.
- **A configuração da produção não é redefinida a jusante (regra central de herança):** os **módulos a jusante não redefinem de forma independente a configuração de ferramentas de uma produção de Job On** — depois de o RESPONSÁVEL selecionar CM/MF/BQ no Job On, os módulos registam os seus próprios dados usando a configuração de produção já definida pelo Job On (subconjunto herdado). O Job On **é** a configuração operacional de produção autoritativa para os módulos a jusante. Quando um módulo apenas identifica **outro lote válido como sujeito da sua própria regra de domínio** (p. ex., o Controlo regista/controla um lote recém-chegado), isso **não** reconfigura nem altera o plano de produção do Job On — voltar a escolher o ferramental de produção continua a ser uma ação do RESPONSÁVEL no Job On. (Ver §10.)
- **Regra de acessos (papéis):** o **RESPONSÁVEL** é o ÚNICO perfil autorizado a modificar o Job On (criar, editar, duplicar, alterar campos de produção/ferramentas, alterar associações CM/MF/BQ, guardar revisões, ações de gestão). O **OPERADOR** consulta o Job On e pode apenas **confirmar manualmente** os checks de verificação onde autorizado; não edita campos, ferramentas/lotes, revisões nem entra em Modo edição. O controlo técnico de acesso usa templates/capabilities, mas a edição do Job On pertence exclusivamente ao RESPONSÁVEL.
- Revisions are immutable; the graph (revision + components + fields + rows +
  verifications) must be persisted and rehydrated atomically. **HISTÓRICO / SUPERSEDED (not a business rule):** the aggregate-hydration gap (`GetByIdAsync` not hydrating `Components`/`Verifications`) is a historical implementation/hardening issue (see AUDIT-REVIEWED / refine-v1). It is NOT a current business rule or design ambiguity; if it persists it is a backlog item to verify as fixed. Rehydration of the aggregate (revision + components + fields + rows + verifications) remains the intent so PDF, duplication, Peso context and the "Confirmar" tab read a fully-hydrated aggregate.
- Universally the landing: calendar + production list; deterministic machine/line→
  colour mapping (`B1..C3 → b1..c3 → --dmo-line-*`); the colour identifies the
  MACHINE/LINE, never a semantic status.
- Per-user "current open Job On" via `jobon_user_current` (N24).

## 4. Boquilhas (U-19) — confirmed rules

- Owns its own `bq_*` schema (N03) — NOT Ferramentas CM/MF `tool_lotes` identity.
  Boquilhas is NOT modelled as the CM/MF batch-repair flow (02_DEC AB-03).
- **20→25 excess-return rule:** `matched = min(return, repair)`; unmatched →
  `exceptional_received_qty` + open `bq_discrepancy`; never a hard block and never
  auto-added to production. NO `AllowUnmatched` hard block (UD-08/UD-09).
- Repairer vocabulary is the canonical `repairers` / `line_repairer_defaults`
  (`tool_type='BQ'`) — reused, not duplicated.
- Reference regex `^[A-Z][0-9]{3}$`; dynamic lines B1–C3.
- Owner decisions: **D1** — Reparação-Externa BQ workflow NOT activated/redesigned in
  Boquilhas U-19 (existing `repair_exit_items.bq_lote_id` hook stays for a later pass);
  **D2** — no live Job On → Boquilhas lookup; immutable Job On/BQ snapshots remain the
  default historical integration.
- **BQ no planeamento do Job On:** a **BQ é uma ferramenta cujo master pertence ao Ferramentas** (CM, MF, BQ, PU e CS são ferramentas); o módulo **Boquilhas** regista **apenas os movimentos relacionados com a reparação externa da BQ**. A BQ **participa normalmente no plano de produção do Job On**, no contexto de localização/estado do Armazém, e é selecionada como o **lote de BQ exato** herdado pelo Controlo a partir do Job On (como CM/MF). **Posse funcional da reparação:** os movimentos de reparação externa da BQ pertencem ao módulo Boquilhas / fluxo de reparação externa; a BQ **nunca** pertence à Reparação Interna. **Distinguir posse de negócio de estado de implementação:** a posse funcional não implica que o fluxo completo de reparação externa da BQ esteja já ativo — o fluxo redesenhado pode permanecer adiado (ver D1).
- **Owner-confirmed clarifications Q1–Q4 (merged into design-coder):**
  - **Q1 — Perfil:** dentro de Boquilhas, **Operador/Controlador e Responsável têm as mesmas ações** quando o módulo está atribuído; sem variantes por perfil; sem aprovação/revisão por ser Responsável; o único gate é o módulo atribuído (Admin não é operacional).
  - **Q2 — `% utilização`:** valor **sempre manual** — o sistema **nunca** calcula, incrementa, deriva, sincroniza nem atualiza automaticamente; nenhum movimento altera o valor por si; SAP existente não implica atualização automática. Transição **Produção → Armazém**: apenas **reminder/alarme para atualizar `% utilização`** (não calcula, não infere, não modifica o valor, não bloqueia; `REMINDER ≠ AUTOMATIC UPDATE`). A `% uso` master pertence ao Ferramentas.
  - **Q3 — `Data de abertura`:** **campo DATE editável** no Registo (preenchível manualmente ou por date picker); **default = hoje** (`DEFAULT = TODAY` ≠ `FIXED = TODAY`); alterável antes de guardar; é a data de abertura do registo do fluxo de reparação, **não** timestamp técnico (timestamps de auditoria são separados).
  - **Q4 — Registo BQ/Lote existente (superfície de manutenção):** `EXISTE → SELECIONA`; `NÃO EXISTE → CRIA EM BOQUILHAS → CONTINUA` (a criação em falta não transfere a posse do master). O registo **já existente** é **consultado/mantido a partir do ARMAZÉM**, pelo perfil **RESPONSÁVEL**, nas **características funcionalmente confirmadas como editáveis** — a Q4 **não torna automaticamente todos os campos editáveis**. A BQ/Lote criada em Boquilhas é o **mesmo registo lógico** depois visto/mantido no Armazém — **sem master duplicado, sem cópia manual, sem segunda identidade**. Boquilhas não é a superfície normal de manutenção; o Armazém não passa a ser dono do fluxo de reparação; Ferramentas permanece o domínio master da ferramenta. A atualização manual de `% utilização` pode ser realizada na superfície de manutenção do Armazém pelo Responsável (Q2+Q4).
  - **Reconciliação de wording:** "Armazém does not own or edit the utilisation record" fica **`SUPERSEDED / REFINED BY LATER OWNER CLARIFICATION Q2 + Q4`** — válido na parte em que o Armazém **não calcula nem atualiza automaticamente** o valor e o alerta SAP não o muta; refinado na parte em que a **atualização manual** pode ser feita pelo Responsável na superfície de manutenção do Armazém (ver `32_ARMAZEM_03_OWNER_DECISION_SAP_ALERT.md`).

## 5. Ferramentas (U-12)

- CM/MF references, lotes, per-lote verificações, SAP utilisation readings
  (append-only `tool_usage_records`, N19), rule lookup feeding Job On.
- R003 SAP utilisation (`% use`, manual) — backend + endpoints exist; UI is an
  **owner-decision** item (not invented).

## 6. Reparação Externa (U-15) — owner decisions A–G

- **A:** BQ functional repair deferred (not in U-15); Boquilhas tab exists but holds no
  fake BQ behavior.
- **B:** Armazém remains the SOLE owner of `warehouse_stock`/`warehouse_movements` and
  physical release/re-occupation; U-15 consumes the Armazém-owned port, never writes
  Armazém tables.
- **C:** any confirmation that changes BOTH repair-cycle state AND warehouse physical
  state (pickup, return) runs in ONE Dapper unit of work.
- **D:** no physical effect is inferred; only explicit persisted confirmations move tools.
- **E:** `Cancelado` is schema/status-compat only; functional CancelarLista deferred.
- **F:** duplicate-item-in-open-exit is a hard Application/domain rule.
- **G:** non-returning-close / destination / other GLM-RE-12 rules safe-deferred.

## 7. Armazém (U-14) — owner decisions

- Ferramentas owns read-only `IFerramentasIdentityLookup`; Armazém owns
  `IToolIdentityResolver`. Two-different-references warning (no silent normalization);
  `fora` derived, never persisted; 4-digit positions.
- **Armazém → Job On (planeamento):** o Armazém diz ao Job On **onde está fisicamente a ferramenta** (CM/MF/BQ onde suportado) e o Job On usa essa informação para **planear a produção**: posição/localização, presença, em produção, fora para reparação, regressada, disponibilidade e estado que exija atenção antes do início. Selecionar/associar uma ferramenta no Job On **não cria movimento de Armazém nem a reserva**; os movimentos físicos continuam a ser operações do Armazém.
- Occupation 1:1 — a location may not hold two active tools. **Known hardening item**
  (AUDIT-REVIEWED / refine-v1): the check+INSERT is not atomic (TOCTOU); requires
  `SELECT ... FOR UPDATE` or `ON CONFLICT`.
- Programmed external-repair exits and BQ are out of Armazém U-14 scope.

## 8. Tampões (U-17)

- **Módulo top-level SIMPLES e AUTÓNOMO.** Finalidade: saber quantas TP/tampões existem
  disponíveis por configuração técnica / máquina (controlo agregado de quantidades; sem
  IDs individuais de TP/tampão). **SEM relação funcional com Job On, SEM relação com
  Production, SEM relação com Reference** — Tampões não envia dados para o Job On, não
  consome contexto do Job On, não planeia produção e não reserva quantidades para produção.
  Razão: os TP/tampões rodam com alta frequência e são reutilizados/adaptados em muitas
  referências — são controlados por configuração técnica/máquina, não por produção/referência.
- Configuração essencial atual: **Máquina/Máquinas + Diâmetro + Calote**; uma configuração
  pode aplicar-se a **uma ou várias máquinas**. Campos, valores e configurações são
  **editáveis/configuráveis pelo operador** (Operador/Controlador) — a lista de campos não
  é fixa no tempo. Interação: 1 clique na linha = selecionar + ações rápidas de quantidade
  (adicionar/remover); 2 cliques = editar essa configuração; criar nova configuração;
  histórico auditável de movimentos de quantidade e de edições de configuração.
- Owns saldos, movements, configurations, settings. Actor server-derived; every change is
  a NEW append-only movement; balances derived from facts.
- Classificações opcionais de quantidade (ex.: Enchidos/Por encher; Maquinados/Por maquinar)
  podem separar quantidades quando útil — **opcionais, sem ciclo de vida obrigatório e sem
  máquina de estados rígida**.
- Acesso: módulo top-level **atribuível por utilizador na Admin**; se não atribuído, não
  aparece na navegação nem há acesso funcional. Utilizador operacional confirmado:
  Operador/Controlador. Admin não é operacional por omissão; sem comportamento operacional
  de Responsável inventado.
- Balance/quantity updates must be **atomic deltas or row-locked** — known historical
  lost-update risk (`SetSaldoAsync` absolute rewrite) requires delta/`FOR UPDATE`
  (AUDIT-REVIEWED / refine-v1).

## 9. Reparação Interna (U-16) + R009 / R015

- Owns `internal_repair_records` (write) + repair_events scope interna.
- **Usa o contexto do Job On, não o reconstrói:** a Reparação Interna associa os seus
  registos ao **Job On / contexto de produção exato** e **não reconstitui nem decide
  independentemente** qual foi a configuração de ferramentas da produção. O Job On fornece
  o contexto/associação de produção; a RI é dona dos seus registos de reparação.
- **Definitive scope correction (2026-08-22): Reparação Interna repairs only CM and
  MF. BQ is not repairable, selectable, or processed in Reparação Interna.** Boquilhas
  use their own separate external-repair flow (external repairers plus dedicated entry
  and exit registration). **BQ nunca é input de reparação da Reparação Interna:** pode
  permanecer visível no contexto de identificação geral da Referência/produção, mas não é
  uma ferramenta reparável na RI.
- The production/reference context must always show and preserve the **complete
  reference**, including the Boquilhas identifier — for example `5447T173`, never
  truncated to `5447`. Here `CM 5447`, `MF 5447`, and `BQ T173` identify the complete
  production reference; showing `T173` is context/identification only and does not mean
  that BQ is repaired internally. Any historical statement that BQ is recordable as a
  Reparação Interna type is superseded by this rule.
- Production activation = most recent start date at 09:00 local factory, no end-date
  test, line-scoped, deterministic. Repeated CM/MF numbers are valid occurrences (never
  deduplicated). **NO operational hard blocks.**
- Correction to a new line recalibrates the automatic production context to the NEW
  line (explicit override wins; no-production new line persists a clean null context —
  never the old line's context, never a block). Job On untouched. Original row preserved.

## 10. Controlo (R010 / R012)

- **R010 Folha de Controlo:** production-level control summary sheet INSIDE the Controlo
  area (not a new module). Anchored to `job_on_id` + exact `job_on_revision_id`;
  snapshots the production's MP_CM/MF/BQ components at creation; per-component OK/NOK +
  observation + manual MCaliper link; workflow draft → submitted → approved/rejected with
  reopen (append-only history). Capabilities on Controlo area (N23).
- **Controlo herda e pode, caso a caso, identificar outro lote válido (distinção sujeito-de-controlo vs ferramental-de-produção):** o Controlo está associado ao **Job On exato, à revisão exata e à Produção** e não redefine a configuração de ferramentas da produção, que pertence ao Job On. Distinguem-se dois casos legítimos:
  - **Caso A — controlo do ferramental do Job On/produção:** quando o Controlo é aberto no contexto de um Job On/produção existente, recebe/conserva/apresenta automaticamente o **resumo de ferramentas já selecionado no Job On** (`CM + Lote`, `MF + Lote`, `BQ + Lote`), com identidade/contexto suficiente para rastrear essas escolhas. Onde a **Máquina/Linha** fizer parte da opção selecionada, é preservada. Neste caso o Controlo **não pede ao utilizador para reconstruir manualmente o contexto de produção**; usa automaticamente as associações planeadas.
  - **Caso B — controlo de outro lote válido:** o Controlo pode também necessitar de **selecionar/identificar outro lote de ferramenta válido que não é o lote planeado naquele Job On**, para o registo/controlo de um lote recém-chegado (ex.: um lote novo que acabou de chegar e tem de ser controlado antes de ser selecionado para produção). Esse lote pode ser identificado como **sujeito de um registo de Controlo**, mesmo não sendo ainda o lote de produção no Job On.
  - **Regra essencial:** selecionar o **sujeito de um controlo** NÃO é selecionar o **ferramental de produção**. O Controlo identifica o lote que está a controlar; o Job On decide qual o lote associado à produção. Registar/controlar outro lote válido no Controlo não altera o Job On, não substitui uma ferramenta do Job On, não o torna o lote de produção e não cria um segundo modelo geral de planeamento de produção — o Job On continua a ser o contexto central de produção/ferramentas. Alterar o ferramental planeado para a produção é uma ação do Job On, feita pelo RESPONSÁVEL.
- **R012 Unified Production Workspace:** active-production card binds all tabs
  (Resumo/Peso/Comparação/Pegamentos/Histórico) to the same production; consumes the
  R011 per-user current-open Job On. No second calendar; no re-selection per tab;
  free-mode consultation without a fake production.

## 11. Peso (functional rules — see also LEGACY_PESO_VERIFIED_BEHAVIOR.md)

- Pinned to the exact Job On revision; `job_on_revision_id` authoritative.
- Peso is automatically associated with the same exact Job On, revision and Production.
  The production as a whole still has **CM + MF + BQ** selected in the Job On; however the
  **Peso domain functionally uses only CM + Lote** for the weight record (the Peso record is
  the weight associated with the CM used for that Reference/production). Distinguish
  **global production tooling (CM + MF + BQ)** from **Peso functional tooling (CM + Lote)**.
  Peso does **not** select CM again, does **not** select MF, does **not** select BQ, and does
  **not** reconstruct the production tooling; it simply registers weight data against the
  inherited production/CM context already chosen in Job On.
- Measurement calculations are C# server-side only (GLM-PESO-05):
  `glass_weight = (capacity + volume_neck - volume_pu) * process_value`;
  water-density lookup 5–35 °C; pairing by reading/table position — CM number is an
  identifier, NOT the pairing key.
- NNPB/PS configurable values; every saved control preserves the exact value used.

## 12. Pegamentos (U-11)

- Pinned to exact `job_on_revision_id` (immutable by construction); CM/BQ/MF inherited,
  never reselectable; append-only measurements computed server-side.
- Tolerance check ±0.20 (boundary = `Exceeded`), C# only.
- PDF generated server-side from the frozen snapshot; final document persisted exactly
  once (`ON CONFLICT`), filename `Pegamentos_{producao}_{referencia}_{maquina}_relatorio.pdf`;
  closed control cannot silently replace its final document.

## 13. História (U-18)

- Transversal READ module (`historia`, `/historia`), reads `audit_events` (N01) read-only,
  no new table, no writes. TD-24: a user sees only events of modules their active template
  grants (`user.Modules ∩ origin modules`); admin events only with `audit.view`.

## 14. Admin / Login / Identity (R014)

- Capability-driven access; grants in `access_templates.modules` (jsonb); users link via
  `internal_users.template_id`; per-user optional `modules_override` (N26). Gates fail
  closed. `admin.gerir` qualifies pure admin. No anonymous/default admin; bootstrap admin
  only via the explicit CLI. User creation reconciles partial-failure idempotently
  (no orphan/duplicate mapping).

## 15. Known hardening items still required (functional/security, not visual)

These genuine defects/requirements are documented in detail in `AUDIT-REVIEWED.md` and
the `refine-v1.md` backlog (removed from AI-CONTEXT active status but not deleted from
the repo). Highlights that must remain requirements:
- JobOn aggregate hydration (C2) + transactional save/duplication (C3).
- `reparacao-externa.js` const-redeclaration SyntaxError (C1).
- PDF interpolation escaping in `JobOnPdfRenderer` (A2) + valid PDF escapes (L5).
- Armazém 1:1 TOCTOU (A3); Tampões lost-update (A4).
- RLS coverage for post-N12 tables (A5 — addressed in N25).
- Enum string-vs-number JS comparisons in Peso / Reparação Interna (M5/X5).
- `esc()` missing in `jobon.js` (M6/X6); `peso.js` innerHTML XSS (X4).
- Auth hardening: rate-limit login, cookie `Secure` always, HSTS, persist DataProtection,
  concrete `AllowedHosts` (X7).
- Admin Users list shows auth UUID under an "Email" column (X12).

---
*End of extracted functional-rules source of truth. Origin of facts:
`docs/IMPLEMENTATION_STATE.md` (full functional/technical history, carrying the
`DESIGN IMPLEMENTATION RESET — 2026-08-22` banner; its visual claims are historical).*
