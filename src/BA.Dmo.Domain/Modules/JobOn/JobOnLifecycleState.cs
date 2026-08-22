namespace BA.Dmo.Domain.Modules.JobOn;

/// <summary>
/// Job On lifecycle state per N05 (TD-27, modules/05 §4).
/// </summary>
public enum JobOnLifecycleState
{
    /// <summary>Rascunho – new creation, not yet planned.</summary>
    Rascunho,
    
    /// <summary>Planeado – saved with planned dates, active in calendar.</summary>
    Planeado,
    
    /// <summary>Em fabrico – production started.</summary>
    EmFabrico,
    
    /// <summary>Fechado – production completed.</summary>
    Fechado,
    
    /// <summary>Cancelado – production cancelled.</summary>
    Cancelado
}

/// <summary>
/// Lifecycle state persistence helpers (N05 status column, TD-27).
/// </summary>
public static class JobOnLifecycleStateCodec
{
    /// <summary>Maps the N05 status text to the domain enum.</summary>
    public static JobOnLifecycleState Parse(string status) => status?.Trim().ToLowerInvariant() switch
    {
        "rascunho" => JobOnLifecycleState.Rascunho,
        "planeado" => JobOnLifecycleState.Planeado,
        "em_fabrico" => JobOnLifecycleState.EmFabrico,
        "fechado" => JobOnLifecycleState.Fechado,
        "cancelado" => JobOnLifecycleState.Cancelado,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown Job On lifecycle status.")
    };

    /// <summary>Maps the domain enum to the N05 status text.</summary>
    public static string ToStorage(JobOnLifecycleState state) => state switch
    {
        JobOnLifecycleState.Rascunho => "rascunho",
        JobOnLifecycleState.Planeado => "planeado",
        JobOnLifecycleState.EmFabrico => "em_fabrico",
        JobOnLifecycleState.Fechado => "fechado",
        JobOnLifecycleState.Cancelado => "cancelado",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown Job On lifecycle state.")
    };
}
