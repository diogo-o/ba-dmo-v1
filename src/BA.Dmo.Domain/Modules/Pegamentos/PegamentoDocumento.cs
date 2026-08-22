namespace BA.Dmo.Domain.Modules.Pegamentos;

/// <summary>
/// Pegamento document metadata record (N14 <c>pegamento_documentos</c>).
/// One per PegamentoControlo (enforced by UNIQUE constraint).
/// Domain entity — owned by Pegamentos module.
/// </summary>
public sealed class PegamentoDocumento
{
    public Guid PegamentoDocumentoId { get; set; }
    public Guid PegamentoControloId { get; set; }
    public string Filename { get; set; } = string.Empty;
    public string OutputRootSnapshot { get; set; } = string.Empty;
    public string ProductionFolderSnapshot { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public string? GeneratedBy { get; set; }
}