namespace KismetAnalyzer;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using Xunit;

public class DefaultValueExtractorTests
{
    private static string FixturePath => Path.Combine(
        AppContext.BaseDirectory, "fixtures", "B_BombPatrol.uasset");

    private static List<LlmDefaultValue> Extract()
    {
        var asset = new UAsset(FixturePath, EngineVersion.VER_UE4_25);
        return DefaultValueExtractor.Extract(asset);
    }

    [Fact]
    public void ExtractsTopLevelScalar()
    {
        var defaults = Extract();
        var weight = defaults.Single(value => value.Name == "Weight");

        Assert.Equal("Int", weight.Type);
        Assert.Equal("5", weight.Value);
    }

    [Fact]
    public void ResolvesObjectReferencesToNames()
    {
        var defaults = Extract();

        Assert.Equal("A_MoveTo_C_0", defaults.Single(value => value.Name == "Action").Value);
        Assert.Equal("TG_PointsAroundActorsWithAITag_0", defaults.Single(value => value.Name == "TargetGenerator").Value);
    }

    [Fact]
    public void FlattensConsiderationArrayStructs()
    {
        var defaults = Extract();
        var considerations = defaults.Single(value => value.Name == "Considerations").Value;

        Assert.StartsWith("[UtilityAI_Consideration(", considerations);
        Assert.Contains("Name=Prefer further if close", considerations);
        Assert.Contains("Input=I_Distance_0", considerations);
        Assert.Contains("Curve=UtilityAI_Curve(m=0.4, k=1, c=0, b=0.6", considerations);
        Assert.Contains("Type=EUtilityAI_Curve::Polynomial", considerations);
    }

    [Fact]
    public void UsesInvariantCultureForFloats()
    {
        var defaults = Extract();
        var considerations = defaults.Single(value => value.Name == "Considerations").Value;

        // Decimal points, never locale-dependent commas.
        Assert.Contains("m=0.4", considerations);
        Assert.DoesNotContain("0,4", considerations);
        Assert.Contains("b=0.6", considerations);
    }

    [Fact]
    public void EmptyAssetHasNoDefaults()
    {
        // An in-memory empty UAsset has no class export/CDO, so extraction is empty.
        var asset = new UAsset();
        var defaults = DefaultValueExtractor.Extract(asset);

        Assert.Empty(defaults);
    }

    [Fact]
    public void BuildWithoutDefaultsOmitsDefaultValuesKey()
    {
        var asset = new UAsset(FixturePath, EngineVersion.VER_UE4_25);
        var export = LlmExporter.Build(asset, "B_BombPatrol.uasset", EngineVersion.VER_UE4_25, includeDefaults: false);
        var json = LlmExporter.Serialize(export);

        Assert.DoesNotContain("defaultValues", json);
        Assert.False(export.HasSemanticContent);
        Assert.Equal("empty", export.ParseStatus);
    }

    [Fact]
    public void BuildWithDefaultsIncludesDefaultValuesAndIsSemantic()
    {
        var asset = new UAsset(FixturePath, EngineVersion.VER_UE4_25);
        var export = LlmExporter.Build(asset, "B_BombPatrol.uasset", EngineVersion.VER_UE4_25, includeDefaults: true);
        var json = LlmExporter.Serialize(export);

        Assert.True(export.HasSemanticContent);
        Assert.Equal("complete", export.ParseStatus);
        Assert.Contains("\"defaultValues\": [", json);
        Assert.Contains("A_MoveTo_C_0", json);
    }
}
