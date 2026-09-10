namespace BA.Dmo.Domain.Modules.Tampoes;

/// <summary>
/// The Opções create paths hit a UNIQUE constraint: a field name that already
/// exists (uq_tampao_field_defs.field_name) or a field value that duplicates an
/// existing (tampao_field_def_id, value_numeric) pair. Raised by the repository
/// so the service can answer a domain error instead of leaking 23505 as a 500.
/// </summary>
public sealed class TampaoFieldDuplicateException(string message) : Exception(message);