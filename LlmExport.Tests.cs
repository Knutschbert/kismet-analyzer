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

        Assert.Contains("\"schemaVersion\": 1", json);
        Assert.Contains("\"parseStatus\": \"complete\"", json);
        Assert.Contains("\"functions\": [", json);
        Assert.Contains("\"successors\": []", json);
        Assert.DoesNotContain("\"SchemaVersion\"", json);
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
