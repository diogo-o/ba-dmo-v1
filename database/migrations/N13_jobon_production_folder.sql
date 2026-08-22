-- ============================================================================
-- BA DMO fresh-build migration family (Plan-V3 BT-08, 06_DATA §2).
-- N13_jobon_production_folder.sql — Add production_folder to job_on.
-- Authority: Owner decision (2026-08-17) — shared production directory via Job On.
--            The production folder is owned by the Job On production context,
--            not by Peso/Pegamentos/other modules.
-- N05 job_on lacks this field; the shared production-directory model requires it.
-- production_folder is the relative production-folder name/identifier of that
-- production inside the configured main output directory. It is a stable logical
-- identifier — NOT a machine-specific absolute path, NOT a browser
-- FileSystemDirectoryHandle, and NOT browser permission state.
-- Existing rows remain NULL (legacy).
-- Idempotent, forward-only. Executed WHOLE by the Npgsql migration runner.
-- ============================================================================

ALTER TABLE job_on
    ADD COLUMN IF NOT EXISTS production_folder text NULL;