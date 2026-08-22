namespace BA.Dmo.Application.Shared.Persistence;

/// <summary>
/// Optimistic concurrency failure (Plan-V3 06_DATA §8, BT-06):
/// an UPDATE guarded by <c>WHERE id = @id AND updated_at = @expected</c>
/// affected no row — the record was changed by someone else and must be
/// reloaded. Append-only facts are exempt from optimistic concurrency.
/// </summary>
public sealed class ConcurrencyConflictException(string entityDescription)
    : Exception(
        $"Concurrency conflict on '{entityDescription}': the record was changed by another " +
        "operation. Reload the latest version and try again.")
{
    public string EntityDescription { get; } = entityDescription;
}

/// <summary>
/// Concurrency helper of the persistence foundation (Plan-V3 U-03 acceptance:
/// "concurrency helper testado"; 06_DATA §8). Edit operations (Admin,
/// editLote, controlos, templates, …) apply their UPDATE with the expected
/// updated_at and then verify exactly one affected row.
/// </summary>
public static class ConcurrencyGuard
{
    /// <summary>
    /// Ensures a guarded UPDATE affected exactly one row; zero rows means the
    /// record changed meanwhile (<see cref="ConcurrencyConflictException"/>
    /// with a reload message), more than one row indicates a guard error.
    /// </summary>
    public static void EnsureSingleRowUpdated(int rowsAffected, string entityDescription)
    {
        if (rowsAffected == 1)
            return;

        if (string.IsNullOrWhiteSpace(entityDescription))
            throw new ArgumentException(
                "Entity description must not be empty.", nameof(entityDescription));

        throw new ConcurrencyConflictException(entityDescription.Trim());
    }
}
