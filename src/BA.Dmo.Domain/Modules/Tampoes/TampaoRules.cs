using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Domain.Modules.Tampoes;

/// <summary>
/// U-17 — Tampões domain rules (GLM-TP-08, TAMPOES_DESIGN_BRIEF §5/§6/§11).
/// Hard blocks (TECHNICAL INTEGRITY): non-negative balances; transfer atomicity;
/// destination equal to origin blocked. Warnings (never hard blocks): concurrent
/// transform (UI reloads balances and asks for a new confirmation). No automatic
/// deduction: planning never converts Por encher into Enchidos.
/// </summary>
public static class TampaoRules
{
    /// <summary>Hard block: a balance cannot become negative.</summary>
    public const string NegativeBalanceCode = "TAMPAO_NEGATIVE_BALANCE";

    /// <summary>Hard block: configuration destination equals origin (no characteristic changed).</summary>
    public const string DestinationEqualsOriginCode = "TAMPAO_DESTINATION_EQUALS_ORIGIN";

    /// <summary>Hard block: quantity must be a positive integer.</summary>
    public const string InvalidQuantityCode = "TAMPAO_INVALID_QUANTITY";

    /// <summary>Hard block: origin balance has insufficient quantity.</summary>
    public const string InsufficientOriginCode = "TAMPAO_INSUFFICIENT_ORIGIN";

    /// <summary>Hard block: a transform must differ in at least one characteristic.</summary>
    public const string NoCharacteristicChangedCode = "TAMPAO_NO_CHARACTERISTIC_CHANGED";

    /// <summary>
    /// Validates a positive integer quantity for a single-balance or transfer movement.
    /// </summary>
    public static Result<int, DomainError> ValidateQuantity(int qty) =>
        qty >= 1
            ? Result<int, DomainError>.Success(qty)
            : Result<int, DomainError>.Failure(DomainError.Validation(
                InvalidQuantityCode, "A quantidade deve ser um inteiro positivo."));

    /// <summary>
    /// Applies a single-balance change (adicionar/remover) to the origin balance.
    /// Returns the new balance value; blocks a negative result (GLM-TP-08).
    /// </summary>
    public static Result<int, DomainError> ApplySingleBalanceChange(int current, int delta)
    {
        var next = current + delta;
        if (next < 0)
            return Result<int, DomainError>.Failure(DomainError.DomainConflict(
                NegativeBalanceCode,
                $"Saldo insuficiente: disponível {current}, pretendido {-delta}."));
        return Result<int, DomainError>.Success(next);
    }

    /// <summary>
    /// Validates an alterar-estado transfer: the OPPOSITE balance is the origin;
    /// the origin must have enough quantity for the transfer (GLM-TP-05.2).
    /// </summary>
    public static Result<TampaoBalanceKind, DomainError> ResolveStateOrigin(
        TampaoSaldo saldo, TampaoBalanceKind destination, int qty)
    {
        var origin = destination == TampaoBalanceKind.Enchidos
            ? TampaoBalanceKind.PorEncher
            : TampaoBalanceKind.Enchidos;
        var originBalance = saldo.Get(origin);
        if (originBalance < qty)
            return Result<TampaoBalanceKind, DomainError>.Failure(DomainError.DomainConflict(
                InsufficientOriginCode,
                $"Saldo de origem insuficiente: {origin} tem {originBalance}, necessário {qty}."));
        return Result<TampaoBalanceKind, DomainError>.Success(origin);
    }

    /// <summary>
    /// Applies a two-balance transfer to a copy of the saldo (origin −qty,
    /// destination +qty), both forced non-negative. Returns the updated saldo.
    /// </summary>
    public static Result<TampaoSaldo, DomainError> ApplyBalanceTransfer(
        TampaoSaldo saldo, TampaoBalanceKind origin, TampaoBalanceKind destination, int qty)
    {
        if (origin == destination)
            return Result<TampaoSaldo, DomainError>.Failure(DomainError.DomainConflict(
                DestinationEqualsOriginCode, "O destino é igual à origem."));

        var clone = new TampaoSaldo
        {
            TampaoConfigurationId = saldo.TampaoConfigurationId,
            Enchidos = saldo.Enchidos,
            PorEncher = saldo.PorEncher,
            UpdatedAtUtc = saldo.UpdatedAtUtc
        };

        var originDelta = origin == TampaoBalanceKind.Enchidos ? -qty : -qty;
        var originNext = origin == TampaoBalanceKind.Enchidos ? clone.Enchidos + originDelta : clone.PorEncher + originDelta;
        var destNext = destination == TampaoBalanceKind.Enchidos ? clone.Enchidos + qty : clone.PorEncher + qty;

        if (originNext < 0 || destNext < 0)
            return Result<TampaoSaldo, DomainError>.Failure(DomainError.DomainConflict(
                NegativeBalanceCode, "Saldo insuficiente para a transferência."));

        if (origin == TampaoBalanceKind.Enchidos) clone.Enchidos = originNext; else clone.PorEncher = originNext;
        if (destination == TampaoBalanceKind.Enchidos) clone.Enchidos = destNext; else clone.PorEncher = destNext;
        return Result<TampaoSaldo, DomainError>.Success(clone);
    }

    /// <summary>
    /// Hard block for alterar-configuração: origin and destination cannot be the
    /// same id, and at least one characteristic must differ (GLM-TP-05.3).
    /// </summary>
    public static Result<TampaoConfiguration, DomainError> ValidateConfigurationTransform(
        TampaoConfiguration origin, TampaoConfiguration destination)
    {
        if (origin.TampaoConfigurationId == destination.TampaoConfigurationId)
            return Result<TampaoConfiguration, DomainError>.Failure(DomainError.DomainConflict(
                DestinationEqualsOriginCode, "O destino é igual à origem."));

        if (!origin.DiffersFrom(destination))
            return Result<TampaoConfiguration, DomainError>.Failure(DomainError.DomainConflict(
                NoCharacteristicChangedCode, "Nenhuma característica mudou entre a origem e o destino."));

        return Result<TampaoConfiguration, DomainError>.Success(destination);
    }

    /// <summary>
    /// Normalizes a decimal input to a canonical 4-dp value so 4, 4.0, 4.00 are one
    /// canonical value (GLM-TP-04; brief §4). Used to build dropdown values and keys.
    /// </summary>
    public static decimal NormalizeValue(decimal value) =>
        decimal.Round(value, 4, System.MidpointRounding.AwayFromZero);
}