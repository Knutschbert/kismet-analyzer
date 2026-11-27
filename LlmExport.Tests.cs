namespace KismetAnalyzer;

using Xunit;

public class LlmExportTests
{
    [Fact]
    public void SerializesSemanticExportWithCamelCaseSchema()
    {
        var export = new LlmAssetExport
        {
            Asset = "/Game/Test/BP_Test",
            Class = "BP_Test_C",
            Functions =
            {
                new LlmFunctionExport
                {
                    Name = "BeginPlay",
                    Blocks =
                    {
                        new LlmBlockExport
                        {
                            Id = 0,
                            Code = { "call BeginPlay" },
                        },
                    },
                },
            },
        };

        var json = LlmExporter.Serialize(export);

        Assert.Contains("\"schemaVersion\": 2", json);
        Assert.Contains("\"parseStatus\": \"complete\"", json);
        Assert.Contains("\"functions\": [", json);
        Assert.Contains("\"successors\": []", json);
        Assert.DoesNotContain("\"SchemaVersion\"", json);
    }

    [Theory]
    [InlineData("StaticMeshComponent", true)]
    [InlineData("StaticMeshActor", true)]
    [InlineData("SceneComponent", true)]
    [InlineData("SplineMeshComponent", true)]      // mesh spline: dropped
    [InlineData("DecalActor", true)]
    [InlineData("TBLParticleSystemComponent", true)]
    [InlineData("AkComponent", true)]
    [InlineData("PointLightComponent", true)]      // explicit light
    [InlineData("MyFancyLight", true)]             // endswith-Light rule
    [InlineData("MovieSceneFloatTrack", true)]     // MovieScene prefix
    [InlineData("SplineComponent", false)]         // spawners/pushables: kept
    [InlineData("InteractableComponent", false)]
    [InlineData("TBLGameScoringComponent", false)]
    [InlineData("TBLNavModifierComponent", false)] // gameplay nav: kept (generic Nav* dropped)
    [InlineData("TBLBoxCollision", false)]         // gameplay collision: kept (bare BoxComponent dropped)
    [InlineData("RunInSpawner_C", false)]          // _C keep-override
    [InlineData("BP_LightUpBoxComponent_C", false)]// _C wins over the Light rule
    [InlineData("World", false)]
    [InlineData("TBLWorldSettings", false)]
    [InlineData("SomeUnknownGameplayThing", false)]// default is keep
    public void ShouldDropMatchesPolicy(string className, bool expectedDrop)
    {
        Assert.Equal(expectedDrop, MapPrunePolicy.ShouldDrop(className));
    }

    [Fact]
    public void PruneMapExportsOnlyAffectsMaps()
    {
        var nonMap = BuildExportWith("/Game/BP_Thing.uasset",
            ("StaticMeshComponent", "SM"), ("InteractableComponent", "Int"));
        LlmExporter.PruneMapExports(nonMap, nonMap.Asset);
        Assert.Equal(2, nonMap.ExportedObjects!.Count);
        Assert.Null(nonMap.PrunedExportCounts);
    }

    [Fact]
    public void PruneMapExportsDropsArtAndTalliesCounts()
    {
        var map = BuildExportWith("Maps/FightArea/FFA_Example.umap",
            ("StaticMeshComponent", "SM1"), ("StaticMeshComponent", "SM2"),
            ("SceneComponent", "Root"), ("InteractableComponent", "Door"),
            ("RunInSpawner_C", "Spawn"), ("World", "PersistentLevel"));
        LlmExporter.PruneMapExports(map, map.Asset);

        var keptClasses = map.ExportedObjects!.Select(o => o.Class).ToList();
        Assert.Contains("InteractableComponent", keptClasses);
        Assert.Contains("RunInSpawner_C", keptClasses);
        Assert.Contains("World", keptClasses);
        Assert.DoesNotContain("StaticMeshComponent", keptClasses);
        Assert.DoesNotContain("SceneComponent", keptClasses);

        Assert.NotNull(map.PrunedExportCounts);
        Assert.Equal(2, map.PrunedExportCounts!["StaticMeshComponent"]);
        Assert.Equal(1, map.PrunedExportCounts!["SceneComponent"]);
        // highest count first (ordered for readability)
        Assert.Equal("StaticMeshComponent", map.PrunedExportCounts!.Keys.First());

        var json = LlmExporter.Serialize(map);
        Assert.Contains("\"prunedExportCounts\":", json);
    }

    private static LlmAssetExport BuildExportWith(string asset, params (string cls, string name)[] objects)
    {
        return new LlmAssetExport
        {
            Asset = asset,
            ExportedObjects = objects
                .Select(o => new LlmExportedObject { Name = o.name, Class = o.cls, Path = o.name })
                .ToList(),
        };
    }

    [Fact]
    public void SerializesManifestWithRelativeOutput()
    {
        var manifest = new LlmManifest
        {
            Assets =
            {
                new LlmManifestEntry
                {
                    Asset = "Game/BP_Test.uasset",
                    Output = "Game/BP_Test.llm.json",
                    Functions = { "BeginPlay" },
                },
            },
        };

        var json = LlmExporter.SerializeManifest(manifest);

        Assert.Contains("\"assets\": [", json);
        Assert.Contains("Game/BP_Test.llm.json", json);
    }
}
