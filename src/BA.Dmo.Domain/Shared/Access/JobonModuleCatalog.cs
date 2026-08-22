using System;
using System.Collections.Generic;

namespace BA.Dmo.Domain.Shared.Access;

/// <summary>
/// U-13 — Module catalog entry for Job On (Plan-V3 modules/05).
/// Canonical identifiers per TD-18/TD-20/GLM-JOB-01.
/// </summary>
public static class JobonModuleCatalog
{
    public const string JobonModuleId = "jobon";
    public const string JobonViewCapabilityId = "jobon.view";
    public const string JobonEditCapabilityId = "jobon.edit";
    public const string JobonConfigureCapabilityId = "jobon.configure";
    public const string JobonConfirmarCapabilityId = "jobon.confirmar";

    // Field option families (TD-18)
    public const string FamilyMp = "MP";
    public const string FamilyMf = "MF";
    public const string FamilyBq = "BQ";
    public const string FamilyPu = "PU";
    public const string FamilyCal = "CAL";
    public const string FamilyAn = "AN";
    public const string FamilyArr = "ARR";
    public const string FamilyPi = "PI";
    public const string FamilyCs = "CS";
    public const string FamilyTp = "TP";
    public const string FamilyFo = "FO";
}
