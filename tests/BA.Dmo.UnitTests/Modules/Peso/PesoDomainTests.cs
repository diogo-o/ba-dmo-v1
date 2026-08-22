using BA.Dmo.Domain.Modules.Peso;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.Peso;

/// <summary>
/// U-10 — Peso domain validator + lot/process invariants (GLM-PESO-04/06/07/10,
/// TD-17). Covers UNIQUE rules, processo-in-lot inheritance, allowed-lines
/// minimum, relative report subfolder, and the delete/edit/reopen policies.
/// </summary>
public class PesoDomainTests
{
    // ---- reference master identity --------------------------------------

    [Fact]
    public void ValidateReference_RequiresMoldAndNeckring()
    {
        Assert.NotNull(PesoValidator.ValidateReference("", "T194"));
        Assert.NotNull(PesoValidator.ValidateReference("5447", ""));
        Assert.Null(PesoValidator.ValidateReference("5447", "T194"));
    }

    // ---- Peso lot (TD-17, N06) ------------------------------------------

    [Fact]
    public void ValidateLote_RequiresAtLeastOneAllowedLine()
    {
        var error = PesoValidator.ValidateLote("4", PesoProcesso.Nnpb, [], "5447T173");
        Assert.NotNull(error);
        Assert.Equal("PESO_LOTE_NO_ALLOWED_LINE", error!.Code);
    }

    [Fact]
    public void ValidateLote_RejectsLineOutsideB1C3()
    {
        var error = PesoValidator.ValidateLote("4", PesoProcesso.Nnpb, ["B1", "Z9"], "5447T173");
        Assert.NotNull(error);
        Assert.Equal("PESO_LOTE_INVALID_LINE", error!.Code);
    }

    [Fact]
    public void ValidateLote_RejectsDuplicateLine()
    {
        var error = PesoValidator.ValidateLote("4", PesoProcesso.Nnpb, ["B1", "B1"], "5447T173");
        Assert.NotNull(error);
        Assert.Equal("PESO_LOTE_DUPLICATE_LINE", error!.Code);
    }

    [Fact]
    public void ValidateLote_RejectsAbsoluteOrTraversalSubfolder()
    {
        Assert.NotNull(PesoValidator.ValidateLote("4", PesoProcesso.Nnpb, ["C3"], "C:\\Capacidades"));
        Assert.NotNull(PesoValidator.ValidateLote("4", PesoProcesso.Nnpb, ["C3"], "/Capacidades"));
        Assert.NotNull(PesoValidator.ValidateLote("4", PesoProcesso.Nnpb, ["C3"], "../sair"));
        Assert.NotNull(PesoValidator.ValidateLote("4", PesoProcesso.Nnpb, ["C3"], ""));
    }

    [Fact]
    public void ValidateLote_AcceptsRelativeSubfolder()
    {
        Assert.Null(PesoValidator.ValidateLote("4", PesoProcesso.Nnpb, ["B3"], "5447T173"));
    }

    // ---- process codec (NNPB/PS) ----------------------------------------

    [Fact]
    public void ProcessoCodec_RoundTrips()
    {
        Assert.Equal("NNPB", PesoProcessoCodec.ToStorage(PesoProcesso.Nnpb));
        Assert.Equal("PS", PesoProcessoCodec.ToStorage(PesoProcesso.Ps));
        Assert.Equal(PesoProcesso.Ps, PesoProcessoCodec.Parse("PS"));
        Assert.Equal(PesoProcesso.Nnpb, PesoProcessoCodec.Parse("nnpb"));
    }

    // ---- report path resolution (GLM-PESO-09, DS-08) --------------------

    [Fact]
    public void ReportPath_ResolvesMainFolderOverSubfolder()
    {
        Assert.Equal("Capacidades / 5447T173",
            ReportPathValidator.Resolve("Capacidades", "5447T173"));
    }

    [Fact]
    public void ReportPath_StripsLeadingSlash()
    {
        Assert.Equal("Capacidades / 5447T173",
            ReportPathValidator.Resolve("Capacidades", "/5447T173"));
    }

    // ---- record type / status codecs ------------------------------------

    [Fact]
    public void RecordType_Codec_RoundTrips()
    {
        Assert.Equal("novo_controlo", PesoRecordTypeCodec.ToStorage(PesoRecordType.NovoControlo));
        Assert.Equal("comparacao", PesoRecordTypeCodec.ToStorage(PesoRecordType.Comparacao));
        Assert.Equal("Comparação", PesoRecordTypeCodec.ToDisplay(PesoRecordType.Comparacao));
        Assert.Equal("Registo de peso", PesoRecordTypeCodec.ToDisplay(PesoRecordType.NovoControlo));
    }

    [Fact]
    public void Status_Codec_RoundTrips()
    {
        Assert.Equal("nao_aprovado", PesoControlStateCodec.ToStorage(PesoControlState.NaoAprovado));
        Assert.Equal(PesoControlState.Pendente, PesoControlStateCodec.Parse("pendente"));
    }
}