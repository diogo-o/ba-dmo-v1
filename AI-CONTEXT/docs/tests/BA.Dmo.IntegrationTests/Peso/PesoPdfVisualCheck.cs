using BA.Dmo.Application.Modules.Peso;

namespace BA.Dmo.IntegrationTests.Peso;

/// <summary>
/// Generates a sample PesoFolhaPdf to real PDF file for manual visual inspection.
/// Run: dotnet test --filter PesoPdfVisualCheck --no-build
/// Output: bin/Debug/net10.0/sample_peso.pdf
/// Lives in the integration project (references BA.Dmo.Infrastructure) to keep
/// the unit test dependency graph on Application + Domain (Plan-V3 03_ARCH §1/§4).
/// </summary>
public class PesoPdfVisualCheck
{
    [Fact]
    public void RenderSample_ToFile_ForManualInspection()
    {
        var data = new PesoFolhaPdf
        {
            IsComparison = true,
            MoldNumber = "1075C142",
            NeckringNumber = "ST100",
            ProductionCode = "202603",
            Line = "B1",
            Lote = "8",
            Revision = 1,
            PesoMedio = 143.31m,
            CapacidadeMedia = 71.66m,
            EstadoMolde = "Reparado",
            Processo = "NNPB",
            PesoNominal = 138m,
            ApprovedBy = "Gonçalo Duarte",
            ApprovedAtUtc = new DateTimeOffset(2026, 7, 28, 14, 54, 28, TimeSpan.Zero),
            PreviousProductionCode = "202601 · Linha B1 · Lote 7",
            DeltaNominal = 5.1m,
            DeltaNominalPct = 3.7m,
            SapPesoMedio = 145m,
            SapPeriodo = "2026-03-05 a 2026-06-18 (106 dias)",
            TemperaturaC = 25m,
            Densidade = 0.99603m,
            CmRows = new[] {
                new PesoCmComparisonRow { CurrentCmNumber = "61", PreviousCmNumber = "95", PesoAtual = 171.1m, PesoAnterior = 171.8m, DeltaPeso = -0.7m, DeltaPesoPct = -0.41m },
                new PesoCmComparisonRow { CurrentCmNumber = "95", PreviousCmNumber = "63", PesoAtual = 171.4m, PesoAnterior = 171.6m, DeltaPeso = -0.2m, DeltaPesoPct = -0.12m },
                new PesoCmComparisonRow { CurrentCmNumber = "63", PreviousCmNumber = "36", PesoAtual = 171.2m, PesoAnterior = 171.6m, DeltaPeso = -0.4m, DeltaPesoPct = -0.23m },
                new PesoCmComparisonRow { CurrentCmNumber = "36", PreviousCmNumber = "61", PesoAtual = 171.8m, PesoAnterior = 171.7m, DeltaPeso = 0.1m, DeltaPesoPct = 0.06m },
            }.ToList().AsReadOnly()
        };

        var bytes = new BA.Dmo.Infrastructure.Access.PesoSingleFilePdfRenderer().RenderPesoFolha(data);
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory!, "sample_peso.pdf");
        File.WriteAllBytes(path, bytes);

        Assert.True(bytes.Length > 100);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));

        Console.WriteLine($"\nPDF written to {path} ({bytes.Length} bytes)\n");
    }
}
