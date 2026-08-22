using System.Data;

namespace BA.Dmo.Application.Shared.Persistence;

/// <summary>
/// Support port of the persistence foundation (Plan-V3 U-03 scope, GLM-DATA-01).
/// Opens server-side PostgreSQL connections (Npgsql underneath, implemented in
/// Infrastructure). Application/Web code accesses the database exclusively
/// through ports like this one — the browser never connects to the database
/// (GLM-DATA-01, GLM-DATA-06).
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>
    /// Opens a new connection. Every call returns an independent connection:
    /// there are no hidden global/static connections and no connection reuse
    /// between requests (GLM-DATA-05 transaction discipline).
    /// </summary>
    Task<IDbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
