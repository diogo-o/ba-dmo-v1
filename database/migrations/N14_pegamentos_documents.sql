-- ============================================================================
-- BA DMO fresh-build migration family (Plan-V3 BT-08, 06_DATA §2).
-- N14_pegamentos_documents.sql — Pegamentos document metadata (module owner: Pegamentos).
-- Authority: 04_PEGAMENTOS_SPEC §14 (GLM-PEG-14), DS-08,
--            PEGAMENTOS_INTERFACE_HANDOFF (PDF persistence).
-- One PegamentoControlo → one final PDF metadata record (no document_version).
-- Idempotent, forward-only. Executed WHOLE by the Npgsql migration runner.
-- ============================================================================

CREATE TABLE IF NOT EXISTS pegamento_documentos (
    pegamento_documento_id      uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    pegamento_controlo_id       uuid        NOT NULL UNIQUE REFERENCES pegamento_controlos (pegamento_controlo_id),
    filename                    text        NOT NULL,
    output_root_snapshot        text        NOT NULL,
    production_folder_snapshot  text        NOT NULL,
    generated_at_utc            timestamptz NOT NULL DEFAULT now(),
    generated_by                text        NULL REFERENCES internal_users (actor_id)
);

CREATE INDEX IF NOT EXISTS ix_pegamento_documentos_controlo
    ON pegamento_documentos (pegamento_controlo_id);