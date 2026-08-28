namespace BA.Dmo.Domain.Modules.Tampoes;

/// <summary>
/// Raised by the persistence layer when a tampao configuration create hits
/// <c>uq_tampao_configurations_values</c> (N10): a concurrent transformation
/// already created a configuration with the exact same serialized values. The
/// service maps this to a structured domain conflict
/// (TAMPAO_CONFIGURATION_DUPLICATE) instead of the generic save failure
/// (audit TP-06).
/// </summary>
public sealed class TampaoConfigurationDuplicateException : Exception
{
    public TampaoConfigurationDuplicateException(string message)
        : base(message)
    {
    }
}