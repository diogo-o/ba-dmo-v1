namespace BA.Dmo.Application.Shared.Persistence;

/// <summary>
/// Internal signal that a required additive schema migration has not yet been
/// applied (N26: <c>internal_users.modules_override</c>). Raised by the
/// Infrastructure repository when a query hits a missing column; the use case
/// catches it and returns an <see cref="BA.Dmo.Domain.Shared.Kernel.ErrorCategory.BackendUnavailable"/>
/// failure with a user-safe, non-leaking Portuguese message.
///
/// The exception message intentionally carries a stable opaque code only — no
/// SQL, no table/column names, no SQLSTATE, no connection details — because it
/// may reach error-reporting boundaries. It is never surfaced to the UI
/// directly. All other database errors are NOT mapped to this type and keep
/// propagating through their established handling.
/// </summary>
public sealed class SchemaMigrationRequiredException : Exception
{
    public SchemaMigrationRequiredException()
        : base("SCHEMA_MIGRATION_REQUIRED")
    {
    }
}