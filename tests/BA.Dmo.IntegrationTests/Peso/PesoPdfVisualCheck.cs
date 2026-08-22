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
            MoldNumber = "1075C142",
            NeckringNumber = "ST100",
            ProductionCode = "202603",
            Line = "B1",
            Lote = "L8",
            Revision = 1,
            PesoMedio = 143.31m,
            CapacidadeMedia = 71.66m,
            EstadoMolde = "Reparado",
            Processo = "NNPB",
            PesoNominal = 138m,
            ApprovedBy = "Gonçalo Duarte",
            ApprovedAtUtc = new DateTimeOffset(2026, 7, 28, 14, 54, 28, TimeSpan.Zero),
            PreviousPesoMedio = 143.63m,
            PreviousCapacidadeMedia = 71.88m,
            DeltaPeso = -0.53m,
            DeltaPesoPct = -0.37m,
            DeltaCapacidade = -0.22m,
            DeltaCapacidadePct = -0.3m,
            PreviousProductionCode = "202601 - 2026-03-05 - Linha B1",
            DeltaNominal = 5.1m,
            DeltaNominalPct = 3.7m,
            SapPesoMedio = 145m,
            SapPeriodo = "2026-03-05 a 2026-06-18 (106 dias)",
            TemperaturaC = 25m,
            Densidade = 0.99603m,
            CmRows = new[] {
                new PesoCmComparisonRow { CmNumber = "Leitura 1 · CM 61", PesoAtual = 71.1m, PesoAnterior = 71.8m, DeltaPeso = -0.7m, CapacidadeAtual = 71.38m, CapacidadeAnterior = 72m, DeltaCapacidade = -0.62m },
                new PesoCmComparisonRow { CmNumber = "Leitura 2 · CM 95", PesoAtual = 71.4m, PesoAnterior = 71.6m, DeltaPeso = -0.2m, CapacidadeAtual = 71.68m, CapacidadeAnterior = 71.8m, DeltaCapacidade = -0.12m },
                new PesoCmComparisonRow { CmNumber = "Leitura 3 · CM 63", PesoAtual = 71.2m, PesoAnterior = 71.6m, DeltaPeso = -0.4m, CapacidadeAtual = 71.48m, CapacidadeAnterior = 71.8m, DeltaCapacidade = -0.32m },
                new PesoCmComparisonRow { CmNumber = "Leitura 4 · CM 36", PesoAtual = 71.8m, PesoAnterior = 71.7m, DeltaPeso = 0.1m, CapacidadeAtual = 72.09m, CapacidadeAnterior = 71.9m, DeltaCapacidade = 0.18m },
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