# Final cross-module acceptance

## IMPLEMENTATION TASK

DES-018. See `99_FINAL_ACCEPTANCE_90_DES_TASK.md`.

## READ IN THIS ORDER

1. `99_FINAL_ACCEPTANCE_90_DES_TASK.md`
2. `99_FINAL_ACCEPTANCE_91_ACCEPTANCE.md`

Before these local files, read `0_GLOBAL_READ_FIRST.md`, `0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`, `0_GLOBAL_DESIGN_SYSTEM.md`, `0_GLOBAL_IMPLEMENTATION_CONTRACT.md`, and `0_GLOBAL_CODER_EXECUTION_RULES.md`.

## VISUAL AUTHORITY

**All module authorities listed in their local README files**

This defines the required composition. Supporting/demo behavior never overrides the functional source of truth.

## FUNCTIONAL AUTHORITY

`0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`

## SUPPORTING AUTHORITY

- None beyond the global authorities.

## CURRENT APP LOCATION

All presentation files changed by DES-003–DES-017 and existing functional tests

## TARGET PAGE ANATOMY

Cross-module screenshot/diff review, shell consistency, context transitions, responsive integrity, keyboard/accessibility and functional regression.

## CRITICAL LOCAL FUNCTIONAL RULES

Explicitly prove 5447T173, CM/MF-only RI, one current-open Job On in Controlo, separate Boquilhas/Ferramentas and no schema-driven UI.

## MUST PRESERVE

Authorization, routing, ownership, server calculations, exact revision context, append-only facts, antiforgery, server actor, complete identifiers.

## MUST NOT

Build/tests as visual proof; schema redesign; waiving mismatches based on prior claims.

## DO NOT USE

- previous completion/parity claims — never acceptance evidence

## STOP CONDITIONS

Stop and report if required data is unavailable, a schema change appears necessary, HTML and functional authority conflict, or a business rule would have to be invented. Functional behavior wins conflicts, but the conflict must be reported before implementation proceeds.

## ACCEPTANCE

Follow `99_FINAL_ACCEPTANCE_91_ACCEPTANCE.md`.

