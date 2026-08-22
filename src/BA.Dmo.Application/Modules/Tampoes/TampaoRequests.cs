using BA.Dmo.Domain.Modules.Tampoes;

namespace BA.Dmo.Application.Modules.Tampoes;

// ---- Commands --------------------------------------------------------------

/// <summary>Add quantity to a single balance of a configuration.</summary>
public sealed record AdicionarQuantidadeRequest(Guid ConfigurationId, TampaoBalanceKind Balance, int Qty);

/// <summary>Remove quantity from a single balance of a configuration.</summary>
public sealed record RemoverQuantidadeRequest(Guid ConfigurationId, TampaoBalanceKind Balance, int Qty);

/// <summary>Alterar estado: transfer quantity towards the chosen balance (Enchidos↔Por encher, single atomic movement).</summary>
public sealed record AlterarEstadoRequest(Guid ConfigurationId, TampaoBalanceKind Destination, int Qty);

/// <summary>
/// Alterar configuração: transform quantity from an origin configuration to a
/// destination expressed by its characteristic values. The destination may carry
/// a reused id or none (created after validation+confirmation).
/// </summary>
public sealed record AlterarConfiguracaoRequest(
    Guid OriginConfigurationId,
    IReadOnlyDictionary<string, decimal> DestinationValues,
    int Qty);

/// <summary>Planear: planned need (planning never reserves stock).</summary>
public sealed record PlanearRequest(Guid ConfigurationId, int PlannedQty, DateOnly? PlannedForDate, string? Notes);

/// <summary>Cancel a plan (never touches balances).</summary>
public sealed record CancelarPlanoRequest(Guid PlanoId);

// ---- Machines & notes (R008) ---------------------------------------------------

/// <summary>Replace the machine set of a configuration (never duplicates the configuration).</summary>
public sealed record SetConfigurationMachinesRequest(Guid ConfigurationId, IReadOnlyList<string> Machines);

/// <summary>Append a comment/note to a configuration (kept for history).</summary>
public sealed record AddConfigurationNoteRequest(Guid ConfigurationId, string Note);

/// <summary>Consultation filter (R008): optional machine — returns configurations whose machine set contains it (ANY match, once).</summary>
public sealed record TampaoMachineDto(string Machine);

public sealed record TampaoConfigurationNoteDto(Guid NoteId, string Note, string? ActorId, DateTimeOffset OccurredAtUtc);

public sealed record TampaoMachineEventDto(string Machine, string Action, string? ActorId, DateTimeOffset OccurredAtUtc);

// ---- Opções -----------------------------------------------------------------

public sealed record CreateFieldDefRequest(string? FieldName, string? Unit, int? PrecisionDigits, int? DisplayOrder);
public sealed record UpdateFieldDefRequest(Guid FieldDefId, string? FieldName, string? Unit, int? PrecisionDigits, int? DisplayOrder, bool? Active);
public sealed record CreateFieldValueRequest(Guid FieldDefId, decimal ValueNumeric, string? ValueLabel, int? DisplayOrder);
public sealed record UpdateFieldValueRequest(Guid FieldValueId, string? ValueLabel, int? DisplayOrder, bool? Active);

// ---- Queries ---------------------------------------------------------------

public sealed record ConsultaFilter(Guid? ConfigurationId, string? Machine = null);

public sealed record PlanoFilter(Guid? ConfigurationId, DateOnly? From, DateOnly? To, bool IncludeCanceled);

// ---- DTOs returned to the UI -------------------------------------------------

public sealed record TampaoFieldDefDto(Guid FieldDefId, string FieldName, string? Unit, int? PrecisionDigits, int DisplayOrder, bool Active);
public sealed record TampaoFieldValueDto(Guid FieldValueId, Guid FieldDefId, decimal ValueNumeric, string ValueLabel, int DisplayOrder, bool Active);

public sealed record TampaoConfigurationDto(
    Guid ConfigurationId,
    IReadOnlyDictionary<string, decimal> Values,
    bool Active,
    int Enchidos,
    int PorEncher,
    IReadOnlySet<string> Machines);

/// <summary>R008 — Record/detail sheet payload for one Tampões configuration.</summary>
public sealed record TampaoConfigurationDetailDto(
    TampaoConfigurationDto Configuration,
    string? LatestComment,
    IReadOnlyList<TampaoConfigurationNoteDto> Notes,
    IReadOnlyList<TampaoMachineEventDto> MachineEvents);

public sealed record TampaoMovimentoDto(
    Guid MovementId,
    string MovementType,
    Guid? OriginConfigurationId,
    Guid? DestinationConfigurationId,
    int Qty,
    string? BalancesBefore,
    string? BalancesAfter,
    string? ActorId,
    DateTimeOffset OccurredAtUtc);

public sealed record TampaoPlanoDto(
    Guid PlanoId,
    Guid ConfigurationId,
    int PlannedQty,
    DateOnly? PlannedForDate,
    Guid? JobOnId,
    string? ProductionCode,
    string? Notes,
    bool Canceled,
    DateTimeOffset CreatedAtUtc,
    string? CreatedBy,
    string ConfigurationLabel,
    int? Enchidos,
    int? Difference);