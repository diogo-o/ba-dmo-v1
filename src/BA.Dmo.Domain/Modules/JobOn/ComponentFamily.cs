namespace BA.Dmo.Domain.Modules.JobOn;

/// <summary>
/// Job On component family per N05 (TD-18).
/// One component per family per revision: MP/CM, MF, BQ, PU, CAL, AN, ARR, PI, CS, TP, FO.
/// </summary>
public enum ComponentFamily
{
    MP_CM,   /// <summary>Molde principal / Contra-molde.</summary>
    MF,      /// <summary>Molde final.</summary>
    BQ,      /// <summary>Boquilha.</summary>
    PU,      /// <summary>Punção.</summary>
    CAL,     /// <summary>Calibres.</summary>
    AN,      /// <summary>Anéis.</summary>
    ARR,     /// <summary>Aros.</summary>
    PI,      /// <summary>Pinças.</summary>
    CS,      /// <summary>Cilindros de sopro.</summary>
    TP,      /// <summary>Tampões.</summary>
    FO       /// <summary>Ferramentas diversas.</summary>
}
