-- ============================================================================
-- BA DMO N39 — make pegamento_medicoes.contra_costura optional (owner
-- decision D-12 branch A / OD-2: ALLOW_ONE_SIDED_MEASUREMENT; N39 design in
-- reports/post_codex_database_rationalization_plan.md §13.6).
--
-- Canonical functional rule: a Pegamentos measurement must NEVER be blocked
-- merely because one measurement side is absent. Contra costura may be
-- measured and stored normally, or be absent (NULL). When it is absent the
-- existing functional calculation uses the defined fallback (Ovalização =
-- NULL/absent; Média = the single value). Absence must never produce a
-- database blocker, service blocker, validation blocker or raw 23502.
--
-- Change: DROP NOT NULL only (widening — the column and its data are kept).
-- Every existing row carries a non-null value, so no backfill is required and
-- no data is reinterpreted. The append-only trigger
-- (trg_pegamento_medicoes_append_only) and all other constraints stay
-- unchanged. The same-release domain/service rule keeps costura mandatory and
-- contra_costura optional with explicit semantics.
-- ============================================================================

ALTER TABLE pegamento_medicoes
    ALTER COLUMN contra_costura DROP NOT NULL;