namespace BA.Dmo.Domain.Shared.Access;

/// <summary>The three and only three functional profiles in BA DMO.</summary>
public enum FunctionalProfile
{
    Admin,
    OperatorController,
    Responsible
}

/// <summary>Canonical persistence/display names for functional profiles.</summary>
public static class FunctionalProfileNames
{
    public const string Admin = "Admin";
    public const string OperatorController = "Operador / Controlador";
    public const string Responsible = "Responsável";

    public static bool TryParse(string? value, out FunctionalProfile profile)
    {
        switch (value?.Trim())
        {
            case Admin:
                profile = FunctionalProfile.Admin;
                return true;
            case OperatorController:
                profile = FunctionalProfile.OperatorController;
                return true;
            case Responsible:
                profile = FunctionalProfile.Responsible;
                return true;
            default:
                profile = default;
                return false;
        }
    }

    public static string DisplayName(this FunctionalProfile profile) => profile switch
    {
        FunctionalProfile.Admin => Admin,
        FunctionalProfile.OperatorController => OperatorController,
        FunctionalProfile.Responsible => Responsible,
        _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null)
    };
}
