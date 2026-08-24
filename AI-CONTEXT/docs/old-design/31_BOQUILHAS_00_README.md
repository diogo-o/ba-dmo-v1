# Boquilhas

## IMPLEMENTATION TASK

DES-011. See `31_BOQUILHAS_90_DES_TASK.md`.

## READ IN THIS ORDER

1. `31_BOQUILHAS_01_VISUAL_AUTHORITY_boquilhas.html`
2. `31_BOQUILHAS_02_HANDOFF_BEHAVIOR.md`
3. `31_BOQUILHAS_90_DES_TASK.md`
4. `31_BOQUILHAS_91_ACCEPTANCE.md`

Before these local files, read `0_GLOBAL_READ_FIRST.md`, `0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`, `0_GLOBAL_DESIGN_SYSTEM.md`, `0_GLOBAL_IMPLEMENTATION_CONTRACT.md`, and `0_GLOBAL_CODER_EXECUTION_RULES.md`.

## VISUAL AUTHORITY

**31_BOQUILHAS_01_VISUAL_AUTHORITY_boquilhas.html**

This defines the required composition. Supporting/demo behavior never overrides the functional source of truth.

## FUNCTIONAL AUTHORITY

`0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`

## SUPPORTING AUTHORITY

- `31_BOQUILHAS_02_HANDOFF_BEHAVIOR.md`

## CURRENT APP LOCATION

AI-CONTEXT\app\BA.Dmo.Web\Pages\Boquilhas\*.cshtml; wwwroot\scripts\boquilhas.js; shared/module CSS

## TARGET PAGE ANATOMY

Canonical line sidebar; lot creation; active summary/state/movements; lot cards; calendar/filter history; external actions; repairer matrix/editors.

## CRITICAL LOCAL FUNCTIONAL RULES

Boquilhas registers BQ external-repair movements. A 20→25 excess return records discrepancy and never blocks or auto-adds production.

**BQ no planeamento e fecho do Boquilhas:** a **BQ** é uma ferramenta cujo **master pertence ao Ferramentas** (CM, MF, BQ, PU e CS são ferramentas do Ferramentas). O módulo **Boquilhas** regista **apenas os movimentos relacionados com a reparação externa da BQ**. A BQ **participa normalmente no plano de produção do Job On**, no contexto de localização/estado do Armazém, e é selecionada como o **lote de BQ exato** herdado pelo Controlo a partir do Job On (como CM/MF). **Posse funcional da reparação:** os movimentos de reparação externa da BQ pertencem ao módulo Boquilhas / fluxo de reparação externa; a BQ **nunca** pertence à Reparação Interna. **Distinguir posse de negócio de estado de implementação:** esta posse funcional não implica que o fluxo completo de reparação externa da BQ esteja já ativo — o fluxo redesenhado pode permanecer adiado (só um hook `repair_exit_items.bq_lote_id` fica para mais tarde).

**Registo — select/create (OWNER-CONFIRMED):** `EXISTE → SELECIONA`; `NÃO EXISTE → CRIA EM BOQUILHAS → CONTINUA`. A criação em falta é válida neste ecrã (não bloqueia por o master de Ferramentas não estar preenchido; sem onboarding separado) e **não transfere a posse do master** (Ferramentas permanece dono). A BQ/Lote criada em Boquilhas é o **mesmo registo lógico** depois visto/mantido no Armazém — **sem master duplicado, sem cópia manual, sem segunda identidade**.

**Perfil (OWNER-CONFIRMED):** dentro de Boquilhas, **Operador/Controlador e Responsável têm as mesmas ações** quando o módulo está atribuído — sem variantes por perfil, sem aprovação/revisão por ser Responsável; o único gate é o módulo atribuído (Admin não é operacional).

**`% utilização` (OWNER-CONFIRMED):** valor **sempre manual** — o sistema nunca calcula, incrementa, deriva nem atualiza automaticamente; nenhum movimento altera o valor por si. Quando a ferramenta **sai de Produção e entra no Armazém**, o sistema apresenta apenas um **reminder/alarme para atualizar `% utilização`** (não calcula, não infere, não modifica o valor, não bloqueia). O master `% uso` da ferramenta pertence ao Ferramentas.

**`Data de abertura` (OWNER-CONFIRMED):** campo **DATE editável** no Registo — preenchível manualmente ou por date picker; **default = hoje**; o utilizador pode alterar antes de guardar; não é timestamp técnico de auditoria (esses timestamps são separados).

**Registo BQ/Lote existente — superfície de manutenção (OWNER-CONFIRMED):** Boquilhas **não** é a superfície normal de manutenção de um registo existente. O registo BQ/Lote **já existente** é **consultado/mantido a partir do ARMAZÉM**, pelo perfil **RESPONSÁVEL**, apenas nas **características funcionalmente confirmadas como editáveis** — a Q4 **não** torna automaticamente todos os campos editáveis; o Armazém não passa a ser dono do fluxo de reparação (continua Boquilhas) nem do master (continua Ferramentas).

## MUST PRESERVE

BQ repair-movement records (Boquilhas scope; the BQ master belongs to Ferramentas); 20→25 discrepancy behavior; repairer vocabulary; complete reference context; immutable close snapshot; select-existing / create-when-missing with continue; same actions for Operador and Responsável inside Boquilhas; manual `% utilização` with Production→Armazém reminder only; editable `Data de abertura` (DATE, default today); existing BQ/Lot record viewed/maintained in Armazém by Responsável (confirmed-editable characteristics only; no duplicate master).

## MUST NOT

CM/MF repair model; excess-return block; unmatched auto-add; inferred Job On relationships; browser prompt inputs; BQ master editing or normal maintenance of existing BQ/Lot records inside Boquilhas; automatic calculation/update of `% utilização`; treating `Data de abertura` as a hidden system timestamp; per-profile Boquilhas variants.

## DO NOT USE

- Reparação Interna BQ variants — forbidden by functional authority

## STOP CONDITIONS

Stop and report if required data is unavailable, a schema change appears necessary, HTML and functional authority conflict, or a business rule would have to be invented. Functional behavior wins conflicts, but the conflict must be reported before implementation proceeds.

## ACCEPTANCE

Follow `31_BOQUILHAS_91_ACCEPTANCE.md`.

