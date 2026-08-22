using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Domain.Modules.Boquilhas;

/// <summary>
/// U-19 — Confirmed BQ hard blocks and the 20→25 excess-return behaviour
/// (modules/01 §6/§7/§11, 02_DEC §5, UD-08/UD-09).
///
/// HARD BLOCKS (CONFIRMED BUSINESS RULES):
///   BQ-RULE-001  reference pattern  ^[A-Z][0-9]{3}$  (validated in application too).
///   BQ-RULE-003  a Saída (dispatch) cannot exceed current production.
///   BQ-RULE-005  a Não reparada (non-repairable) cannot exceed current repair.
///   BQ-RULE-006  a correction cannot create a negative balance.
///   BQ-RULE-007  reopen only of the LAST closed trace with no other active trace.
///   BQ-RULE-008  lifecycle change only with no active trace.
///
/// NEVER a block (glm-core-01, UD-08/UD-09): a return exceeding the expected
/// repair balance is recorded in full (actual qty) with a warning + note +
/// discrepancy — the legacy <c>RETURN_UNMATCHED_NOT_ALLOWED</c> / <c>AllowUnmatched</c>
/// hard block is NOT carried forward.
/// </summary>
public static class BqRules
{
    public const string ReferenceInvalidCode = BoquilhasModuleCatalog.ReferenceInvalidCode;

    public const string DispatchExceedsProductionCode = "BQ_DISPATCH_EXCEEDS_PRODUCTION";

    public const string NonRepairableExceedsRepairCode = "BQ_NONREPAIRABLE_EXCEEDS_REPAIR";

    public const string CorrectionNegativeCode = "BQ_CORRECTION_NEGATIVE";

    public const string ReopenNotLastCode = "BQ_REOPEN_NOT_LAST";

    public const string ReopenHasActiveTraceCode = "BQ_REOPEN_HAS_ACTIVE_TRACE";

    public const string LifecycleActiveTraceCode = "BQ_LIFECYCLE_ACTIVE_TRACE";

    public const string LifecycleStateCode = "BQ_LIFECYCLE_STATE";

    public const string InvalidQuantityCode = "BQ_INVALID_QUANTITY";

    public const string MovementOnClosedTraceCode = "BQ_MOVEMENT_CLOSED_TRACE";

    public const string MovementOnMissingTraceCode = "BQ_MOVEMENT_NO_TRACE";

    /// <summary>Validates a positive quantity for a movement/correction.</summary>
    public static Result<decimal, DomainError> ValidateQuantity(decimal? qty) =>
        qty is > 0
            ? Result<decimal, DomainError>.Success(qty.Value)
            : Result<decimal, DomainError>.Failure(DomainError.Validation(
                InvalidQuantityCode, "A quantidade deve ser maior que zero."));

    /// <summary>Validates a decimal utilisation value 0–100.</summary>
    public static Result<decimal, DomainError> ValidateUtilisation(decimal value) =>
        value >= 0 && value <= 100
            ? Result<decimal, DomainError>.Success(decimal.Round(value, 2))
            : Result<decimal, DomainError>.Failure(DomainError.Validation(
                "BQ_UTILISATION_RANGE", "A utilização deve estar entre 0 e 100."));

    /// <summary>Validates the canonical reference pattern.</summary>
    public static bool IsValidReference(string reference) =>
        !string.IsNullOrWhiteSpace(reference) && BoquilhasModuleCatalog.ReferencePattern.IsMatch(reference);
}

/// <summary>
/// U-19 — The CONFIRMED <c>matched / unmatched / exceptionalReceived</c>
/// calculation of a BQ return (02_DEC §3.34, GLM-BQ-06, InventoryCalculator
/// classification A). It is the exact legacy inventory formula, preserved — a
/// return is matched to the expected repair balance and any excess becomes an
/// exceptional-received quantity, never silently added to production.
/// </summary>
public static class BqInventoryCalculator
{
    /// <summary>
    /// Result of reconciling a return (entrada) against the expected repair balance.
    /// </summary>
    public readonly record struct ReturnReconciliation(
        decimal MatchedQty,
        decimal UnmatchedQty);

    /// <summary>
    /// Computes <c>matched/min(qty,repair)</c> and <c>unmatched = qty − matched</c>
    /// (GLM-BQ-06: return 25 vs repair 20 → matched 20, unmatched 5).
    /// </summary>
    public static ReturnReconciliation ReconcileReturn(decimal returnQty, decimal expectedRepairBalance) =>
        new(
            MatchedQty: Math.Min(returnQty, Math.Max(0, expectedRepairBalance)),
            UnmatchedQty: Math.Max(0, returnQty - Math.Max(0, expectedRepairBalance)));

    /// <summary>
    /// Applies one ACCOUNTING movement to a running <see cref="BqSaldos"/> and
    /// returns the resulting state. Applies the confirmed rules:
    ///   Inicio:      prod += qty
    ///   Saida:       prod −= qty; repair += qty         (error if qty &gt; prod)
    ///   Entrada:     matched → repair −= matched; prod += matched;
    ///                unmatched → ExceptionalReceived (never added to prod)
    ///   Irreparavel: repair −= qty; irreparable += qty  (error if qty &gt; repair)
    ///   Linha:       no balance change (qty null)
    ///   Contagem:    prod += delta (correction); never negative
    ///   Fim:         no balance change (close marker)
    /// </summary>
    public static Result<BqSaldos, DomainError> Apply(BqSaldos current, BqMovement movement)
    {
        var next = current.Clone();
        switch (movement.MovementType)
        {
            case BqMovementType.Inicio:
                var startQty = movement.Qty ?? 0;
                if (startQty < 0)
                    return Failure(BqRules.InvalidQuantityCode, "A quantidade inicial não pode ser negativa.");
                next.Prod += startQty;
                next.TransactionalBalance += startQty;
                break;

            case BqMovementType.Saida:
                var outQty = movement.Qty ?? 0;
                if (outQty > next.Prod)
                    return Failure(BqRules.DispatchExceedsProductionCode,
                        $"Saída excede a produção atual: disponíveis {next.Prod}, pretendidas {outQty}.");
                next.Prod -= outQty;
                next.Repair += outQty;
                next.TransactionalBalance -= outQty;
                break;

            case BqMovementType.Entrada:
                var inQty = movement.Qty ?? 0;
                var rec = ReconcileReturn(inQty, next.Repair);
                next.Repair -= rec.MatchedQty;
                next.Prod += rec.MatchedQty;
                next.ExceptionalReceived += rec.UnmatchedQty;
                next.TransactionalBalance += rec.MatchedQty;
                break;

            case BqMovementType.Irreparavel:
                var badQty = movement.Qty ?? 0;
                if (badQty > next.Repair)
                    return Failure(BqRules.NonRepairableExceedsRepairCode,
                        $"Não reparadas excedem a reparação atual: em reparação {next.Repair}, pretendidas {badQty}.");
                next.Repair -= badQty;
                next.Irreparable += badQty;
                break;

            case BqMovementType.Linha:
            case BqMovementType.Fim:
                // Line-change and close: no balance change.
                break;

            case BqMovementType.Contagem:
                var delta = movement.Qty ?? 0;
                if (next.Prod + delta < 0)
                    return Failure(BqRules.CorrectionNegativeCode,
                        "Correção de contagem criaria saldo de produção negativo.");
                next.Prod += delta;
                break;

            default:
                return Failure(BqRules.InvalidQuantityCode, "Tipo de movimento não suportado no cálculo.");
        }

        return Result<BqSaldos, DomainError>.Success(next);
    }

    private static Result<BqSaldos, DomainError> Failure(string code, string message) =>
        Result<BqSaldos, DomainError>.Failure(DomainError.DomainConflict(code, message));
}