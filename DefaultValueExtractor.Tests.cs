namespace KismetAnalyzer;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
using Xunit;

public class DefaultValueExtractorTests
{
    private static string FixturePath => Path.Combine(
        AppContext.BaseDirectory, "fixtures", "B_BombPatrol.uasset");

    private static string ComponentFixturePath => Path.Combine(
        AppContext.BaseDirectory, "fixtures", "BaseSpawner.uasset");

    private static List<LlmDefaultValue> Extract()
    {
        var asset = new UAsset(FixturePath, EngineVersion.VER_UE4_25);
        return DefaultValueExtractor.Extract(asset);
    }

    private static List<LlmDefaultValue> ExtractComponents()
    {
        var asset = new UAsset(ComponentFixturePath, EngineVersion.VER_UE4_25);
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

    [Fact]
    public void ExtractsCdoDefaultsForComponentAsset()
    {
        var defaults = ExtractComponents();

        Assert.Equal("Main Force", defaults.Single(value => value.Name == "SpawnerName (Ingame)").Value);
        Assert.Equal("CollisionCapsule", defaults.Single(value => value.Name == "RootComponent").Value);
    }

    [Fact]
    public void ExtractsComponentTemplateDefaultsWithQualifiedNames()
    {
        var defaults = ExtractComponents();
        var componentValues = defaults.Where(value => value.Name.Contains('.')).ToList();

        Assert.NotEmpty(componentValues);

        // Component-qualified names look like "TemplateName.PropertyName".
        foreach (var value in componentValues)
        {
            var separator = value.Name.IndexOf('.');
            Assert.True(separator > 0, $"Expected qualified name, got '{value.Name}'");
            Assert.NotEqual("None", value.Value);
        }
    }

    [Fact]
    public void ExtractsClassBearingComponentTemplateRecords()
    {
        var asset = new UAsset(ComponentFixturePath, EngineVersion.VER_UE4_25);
        var objects = DefaultValueExtractor.ExtractExportedObjects(asset);

        Assert.NotEmpty(objects);
        Assert.All(objects, obj => Assert.False(string.IsNullOrEmpty(obj.Name)));
        Assert.Contains(objects, obj =>
            obj.Name == "NODE_AddTextRenderComponent-0" &&
            obj.Class == "TextRenderComponent" &&
            obj.Properties.Any(property => property.Name == "bHiddenInGame"));
    }

    [Fact]
    public void LlmExportRetainsClassBearingObjectsAlongsideLegacyDefaults()
    {
        var asset = new UAsset(ComponentFixturePath, EngineVersion.VER_UE4_25);
        var export = LlmExporter.Build(asset, "BaseSpawner.uasset", EngineVersion.VER_UE4_25, includeDefaults: true);
        var json = LlmExporter.Serialize(export);

        Assert.Equal(2, export.SchemaVersion);
        Assert.NotNull(export.ExportedObjects);
        Assert.NotEmpty(export.ExportedObjects);
        Assert.Contains("\"exportedObjects\": [", json);
        Assert.Contains("\"class\": \"TextRenderComponent\"", json);
        Assert.Contains("\"defaultValues\": [", json);
    }

    [Fact]
    public void ExtractsClassForMapStylePlacedActorExport()
    {
        var asset = new UAsset
        {
            Imports = new List<Import>(),
            Exports = new List<Export>(),
        };
        asset.Imports.Add(new Import(
            "/Script/CoreUObject",
            "Class",
            new FPackageIndex(),
            "BP_FreeableVIPSpawner_C",
            false,
            asset));
        asset.Imports.Add(new Import(
            "/Script/Engine",
            "Level",
            new FPackageIndex(),
            "PersistentLevel",
            false,
            asset));
        asset.Exports.Add(new NormalExport
        {
            ObjectName = FName.DefineDummy(asset, "SpwnS4cAtkVIP1"),
            ClassIndex = FPackageIndex.FromImport(0),
            OuterIndex = FPackageIndex.FromImport(1),
            Data = new List<PropertyData>(),
        });

        var objects = DefaultValueExtractor.ExtractExportedObjects(asset);

        var spawner = Assert.Single(objects);
        Assert.Equal("SpwnS4cAtkVIP1", spawner.Name);
        Assert.Equal("BP_FreeableVIPSpawner_C", spawner.Class);
        Assert.Equal("PersistentLevel", spawner.Outer);
        Assert.Equal("PersistentLevel.SpwnS4cAtkVIP1", spawner.Path);
        Assert.Empty(spawner.Properties);
    }

    [Fact]
    public void SkipsBlueprintPlumbingExports()
    {
        var defaults = ExtractComponents();
        var names = defaults.Select(value => value.Name).ToList();

        Assert.DoesNotContain(names, name => name.StartsWith("SCS_Node.", StringComparison.Ordinal));
        Assert.DoesNotContain(names, name => name.StartsWith("SimpleConstructionScript", StringComparison.Ordinal));
        Assert.DoesNotContain(names, name => name.StartsWith("BaseSpawner_C.", StringComparison.Ordinal));
        // Function exports are not component data.
        Assert.DoesNotContain(names, name => name.StartsWith("ExecuteUbergraph_BaseSpawner.", StringComparison.Ordinal));
    }

    [Fact]
    public void SkipsEmptyAndNullOnlyTemplates()
    {
        var defaults = ExtractComponents();
        var names = defaults.Select(value => value.Name).ToList();

        // CollisionCapsule has no serialized properties at all.
        Assert.DoesNotContain(names, name => name.StartsWith("CollisionCapsule.", StringComparison.Ordinal));
        // DefaultSceneRoot_GEN_VARIABLE serializes only Mobility=0; zero/null values are skipped.
        Assert.DoesNotContain(names, name => name.StartsWith("DefaultSceneRoot_GEN_VARIABLE.", StringComparison.Ordinal));
        // NODE_AddTextRenderComponent-0 keeps meaningful values but drops its null/zero ones.
        Assert.DoesNotContain(names, name => name == "NODE_AddTextRenderComponent-0.Text");
        Assert.DoesNotContain(names, name => name == "NODE_AddTextRenderComponent-0.HorizontalAlignment");
        Assert.Contains(names, name => name == "NODE_AddTextRenderComponent-0.bHiddenInGame");
    }

    [Fact]
    public void ExtractsReferencedSubobjectDefaultsForGraphlessAsset()
    {
        // B_BombPatrol's referenced objects (action, target generator, considerations) are
        // instanced subobject exports carrying the behavior's real configuration.
        var defaults = Extract();

        Assert.Equal("150", defaults.Single(value => value.Name == "A_MoveTo_C_0.Acceptance Radius").Value);
        Assert.Equal("True", defaults.Single(value => value.Name == "A_MoveTo_C_0.Use Pathfinding").Value);
        Assert.Contains("AI.ObjectiveActor.Bomb",
            defaults.Single(value => value.Name == "TG_PointsAroundActorsWithAITag_0.Tag").Value);
    }

    [Fact]
    public void NullStringValuesDoNotAbortExtraction()
    {
        // Regression: StrPropertyData.ToString() used to throw on a null FString, aborting the
        // whole export for assets like GenericDialog whose CDO holds null-valued strings.
        var asset = new UAsset();
        var classExport = new ClassExport();
        var cdo = new NormalExport { Data = new List<PropertyData>() };
        asset.Exports = new List<Export> { classExport, cdo };
        classExport.ClassDefaultObject = FPackageIndex.FromExport(1);

        var strukt = new StructPropertyData(
            FName.DefineDummy(asset, "Meta"), FName.DefineDummy(asset, "DialogMeta"));
        strukt.Value.Add(new StrPropertyData(FName.DefineDummy(asset, "Title"))); // Value stays null
        cdo.Data.Add(strukt);

        var defaults = DefaultValueExtractor.Extract(asset);

        // The null string renders as an empty member instead of throwing.
        var meta = defaults.Single(value => value.Name == "Meta");
        Assert.Equal("DialogMeta(Title=)", meta.Value);
    }

    [Fact]
    public void EmptyGameplayTagContainerDoesNotAbortExtraction()
    {
        // Regression: GameplayTagContainerPropertyData.ToString() used to throw on an empty tag
        // container (Remove(-1)), aborting the export for maps like FFA_Falmire.umap whose level
        // actors hold empty tag containers.
        var asset = new UAsset();
        var classExport = new ClassExport();
        var cdo = new NormalExport
        {
            Data = new List<PropertyData>
            {
                new GameplayTagContainerPropertyData(FName.DefineDummy(asset, "Tags"))
                {
                    Value = Array.Empty<FName>(),
                },
            },
        };
        asset.Exports = new List<Export> { classExport, cdo };
        classExport.ClassDefaultObject = FPackageIndex.FromExport(1);

        var defaults = DefaultValueExtractor.Extract(asset);

        var tags = defaults.Single(value => value.Name == "Tags");
        Assert.Equal("", tags.Value);
    }
}
