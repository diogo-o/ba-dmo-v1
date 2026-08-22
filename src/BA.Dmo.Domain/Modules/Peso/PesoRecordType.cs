namespace BA.Dmo.Domain.Modules.Peso;

/// <summary>
/// Peso control record type (GLM-PESO-06, N06 <c>peso_controlos.record_type</c>).
/// Novo controlo (preparation/entry) and Comparação (CM already in production
/// compared to the approved Novo controlo of the same Job On). "Comparação" is
/// a record TYPE, never a status.
/// </summary>
public enum PesoRecordType
{
    NovoControlo,
    Comparacao
}

/// <summary>Record type persistence/text helpers (N06 record_type).</summary>
public static class PesoRecordTypeCodec
{
    public static PesoRecordType Parse(string value) => value?.Trim().ToLowerInvariant() switch
    {
        "novo_controlo" => PesoRecordType.NovoControlo,
        "comparacao" => PesoRecordType.Comparacao,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown Peso record type.")
    };

    public static string ToStorage(PesoRecordType value) => value switch
    {
        PesoRecordType.NovoControlo => "novo_controlo",
        PesoRecordType.Comparacao => "comparacao",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown Peso record type.")
    };

    /// <summary>User-facing label for the Histórico/Responsável type filter.</summary>
    public static string ToDisplay(PesoRecordType value) => value switch
    {
        PesoRecordType.NovoControlo => "Registo de peso",
        PesoRecordType.Comparacao => "Comparação",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown Peso record type.")
    };
}