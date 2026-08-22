namespace BA.Dmo.Application.Modules.Pegamentos;

/// <summary>
/// Application-layer filename helper. Computes the canonical Pegamentos PDF filename
/// from the control's historical production context.
/// Infrastructure must NOT own or duplicate this helper.
/// Canonical: Pegamentos_{producao}_{referencia}_{maquina}_relatorio.pdf
/// </summary>
public static class PegamentoPdfFilename
{
    public static string Compute(Domain.Modules.Pegamentos.PegamentoControlo control)
    {
        return $"Pegamentos_{control.ProductionCode}_{control.ReferenceSnapshot}_{control.MachineCode}_relatorio.pdf";
    }
}