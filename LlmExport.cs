namespace KismetAnalyzer;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.UnrealTypes;

public sealed class LlmAssetExport
{
    public int SchemaVersion { get; set; } = 2;
    public string Source { get; set; } = "kismet-bytecode";
    public string ParseStatus { get; set; } = "complete";
    public string? Asset { get; set; }
    public string? Class { get; set; }
    public string? Parent { get; set; }
    public string EngineVersion { get; set; } = "UNKNOWN";
    public List<LlmPropertyExport> Properties { get; set; } = new();
    public List<LlmDefaultValue>? DefaultValues { get; set; }
    public List<LlmExportedObject>? ExportedObjects { get; set; }
    /// <summary>
    /// For map (.umap) docs only: aggregate count of exported objects pruned per class (v3 map
    /// pruning, <see cref="MapPrunePolicy"/>), so map density stays answerable after the art/geometry
    /// exports are removed. Null for non-map docs and for maps where nothing was pruned.
    /// </summary>
    public Dictionary<string, int>? PrunedExportCounts { get; set; }
    public List<LlmFunctionExport> Functions { get; set; } = new();
    public List<LlmCallEdge> CallEdges { get; set; } = new();
    public List<LlmDiagnostic> Diagnostics { get; set; } = new();
    public bool HasSemanticContent => Properties.Count > 0 || (DefaultValues?.Count > 0) || (ExportedObjects?.Count > 0) || Functions.Count > 0 || CallEdges.Count > 0 || Diagnostics.Count > 0;
}

public sealed class LlmPropertyExport
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string? Flags { get; set; }
}

/// <summary>
/// A non-CDO export with its resolved Unreal class and meaningful serialized properties.
/// This retains the class identity of placed map actors and Blueprint component templates that
/// the legacy flat <see cref="LlmAssetExport.DefaultValues"/> projection cannot represent.
/// </summary>
public sealed class LlmExportedObject
{
    public string Name { get; set; } = "";
    public string? Class { get; set; }
    public string? Outer { get; set; }
    public string Path { get; set; } = "";
    public List<LlmDefaultValue> Properties { get; set; } = new();
}

public sealed class LlmFunctionExport
{
    public string Name { get; set; } = "";
    public List<string> Calls { get; set; } = new();
    public List<string> Variables { get; set; } = new();
    public List<string> Reads { get; set; } = new();
    public List<string> Writes { get; set; } = new();
    public int Branches { get; set; }
    public List<LlmBlockExport> Blocks { get; set; } = new();
    public List<LlmDiagnostic> Diagnostics { get; set; } = new();
}

public sealed class LlmBlockExport
{
    public uint Id { get; set; }
    public List<string> Code { get; set; } = new();
    public List<LlmSuccessorExport> Successors { get; set; } = new();
}

public sealed class LlmSuccessorExport
{
    public uint Target { get; set; }
    public string Kind { get; set; } = "Normal";
    public string? Function { get; set; }
}

public sealed class LlmCallEdge
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
}

public sealed class LlmDiagnostic
{
    public string Kind { get; set; } = "warning";
    public string Message { get; set; } = "";
    public string? Function { get; set; }
    public uint? Address { get; set; }
}

public sealed class LlmManifest
{
    public int SchemaVersion { get; set; } = 1;
    public string Source { get; set; } = "kismet-bytecode";
    public List<LlmManifestEntry> Assets { get; set; } = new();
}

public sealed class LlmManifestEntry
{
    public string Asset { get; set; } = "";
    public string? Output { get; set; }
    public string? Class { get; set; }
    public List<string> Functions { get; set; } = new();
    public string ParseStatus { get; set; } = "complete";
    public string? Reason { get; set; }
}

public static class LlmExporter
{
    private static JsonSerializerSettings JsonSettings => new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
    };

    public static LlmAssetExport Build(UAsset asset, string? assetPath, EngineVersion engineVersion, bool includeDefaults = false)
    {
        var classExport = asset.GetClassExport();
        var result = new LlmAssetExport
        {
            Asset = assetPath,
            Class = classExport?.ObjectName.ToString(),
            Parent = classExport?.SuperStruct.ToImport(asset).ObjectName.ToString(),
            EngineVersion = engineVersion.ToString(),
        };

        if (includeDefaults)
        {
            result.DefaultValues = DefaultValueExtractor.Extract(asset);
            result.ExportedObjects = DefaultValueExtractor.ExtractExportedObjects(asset);
            // v3 map pruning (kismet-llm-rb6.1): maps only. Drop engine art/geometry/cinematic/editor
            // exported objects, replacing them with a per-class aggregate count so density stays
            // answerable. Gameplay actors/components and Blueprint instances are retained.
            PruneMapExports(result, assetPath);
        }

        if (classExport != null)
        {
            foreach (var property in GetProperties(classExport, asset))
            {
                result.Properties.Add(property);
            }
        }

        var analyses = new SummaryGenerator(asset, TextWriter.Null, TextWriter.Null).AnalyzeFunctions();
        foreach (var analysis in analyses)
        {
            var function = new LlmFunctionExport { Name = analysis.Name };
            var calls = new HashSet<string>(StringComparer.Ordinal);
            var variables = new HashSet<string>(StringComparer.Ordinal);
            var reads = new HashSet<string>(StringComparer.Ordinal);
            var writes = new HashSet<string>(StringComparer.Ordinal);

            foreach (var instruction in analysis.Instructions)
            {
                var elements = instruction.Content.GetElements().SelectMany(item => item.Elements).ToList();
                foreach (var element in elements)
                {
                    if (element.Kind == ElementKind.Function) calls.Add(element.Value);
                    if (element.Kind == ElementKind.Variable) variables.Add(element.Value);
                }

                var instructionVariables = elements
                    .Where(element => element.Kind == ElementKind.Variable)
                    .Select(element => element.Value)
                    .ToList();
                var expression = elements.FirstOrDefault(element => element.Kind == ElementKind.ExpressionType).Value;
                var writesDestination = expression is "EX_Let" or "EX_LetObj" or "EX_LetBool"
                    or "EX_LetDelegate" or "EX_LetWeakObjPtr" or "EX_LetValueOnPersistentFrame"
                    or "EX_SetArray" or "EX_SetSet" or "EX_SetMap";
                for (var variableIndex = 0; variableIndex < instructionVariables.Count; variableIndex++)
                {
                    if (writesDestination && variableIndex == 0) writes.Add(instructionVariables[variableIndex]);
                    else reads.Add(instructionVariables[variableIndex]);
                }

                foreach (var reference in instruction.ReferencedAddresses)
                {
                    if (reference.Type == SummaryGenerator.ReferenceType.Function && reference.FunctionName != null)
                    {
                        calls.Add(reference.FunctionName);
                    }
                }
            }

            function.Calls = calls.OrderBy(value => value).ToList();
            function.Variables = variables.OrderBy(value => value).ToList();
            function.Reads = reads.OrderBy(value => value).ToList();
            function.Writes = writes.OrderBy(value => value).ToList();
            function.Branches = analysis.Blocks.Sum(block => block.Successors.Count(successor =>
                successor.Type is SummaryGenerator.ReferenceType.JumpTrue or SummaryGenerator.ReferenceType.JumpFalse));

            foreach (var block in analysis.Blocks)
            {
                var outputBlock = new LlmBlockExport { Id = block.StartAddress };
                foreach (var instruction in block.Instructions)
                {
                    var text = new StringWriter();
                    PlainTextRenderer.RenderTo(text, instruction.Content);
                    outputBlock.Code.Add(text.ToString().TrimEnd('\r', '\n'));
                }

                foreach (var successor in block.Successors)
                {
                    outputBlock.Successors.Add(new LlmSuccessorExport
                    {
                        Target = successor.TargetAddress,
                        Kind = successor.Type.ToString(),
                        Function = successor.FunctionName,
                    });
                }
                function.Blocks.Add(outputBlock);
            }

            foreach (var diagnostic in analysis.Diagnostics)
            {
                function.Diagnostics.Add(new LlmDiagnostic
                {
                    Kind = "unsupported_expression",
                    Function = analysis.Name,
                    Message = diagnostic,
                });
                result.Diagnostics.Add(function.Diagnostics[^1]);
            }

            foreach (var call in function.Calls)
            {
                result.CallEdges.Add(new LlmCallEdge { From = analysis.Name, To = call });
            }
            result.Functions.Add(function);
        }

        if (result.Diagnostics.Count > 0) result.ParseStatus = "partial";
        else if (!result.HasSemanticContent) result.ParseStatus = "empty";
        return result;
    }

    /// <summary>
    /// Applies the v3 map-doc prune in place (<see cref="MapPrunePolicy"/>). No-op unless
    /// <paramref name="assetPath"/> is a map and there are exported objects. Dropped objects are
    /// tallied per class into <see cref="LlmAssetExport.PrunedExportCounts"/>.
    /// </summary>
    internal static void PruneMapExports(LlmAssetExport result, string? assetPath)
    {
        if (result.ExportedObjects is not { Count: > 0 }) return;
        if (!MapPrunePolicy.IsMapAsset(assetPath)) return;

        var kept = new List<LlmExportedObject>(result.ExportedObjects.Count);
        Dictionary<string, int>? pruned = null;
        foreach (var obj in result.ExportedObjects)
        {
            if (MapPrunePolicy.ShouldDrop(obj.Class))
            {
                pruned ??= new Dictionary<string, int>(StringComparer.Ordinal);
                var key = obj.Class ?? "<unknown>";
                pruned[key] = pruned.TryGetValue(key, out var n) ? n + 1 : 1;
            }
            else
            {
                kept.Add(obj);
            }
        }

        if (pruned == null) return; // nothing matched the denylist
        result.ExportedObjects = kept;
        result.PrunedExportCounts = pruned
            .OrderByDescending(entry => entry.Value)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .ToDictionary(entry => entry.Key, entry => entry.Value);
    }

    public static string Serialize(LlmAssetExport export, bool indented = true)
    {
        return JsonConvert.SerializeObject(export, indented ? Formatting.Indented : Formatting.None, JsonSettings);
    }

    public static string SerializeManifest(LlmManifest manifest)
    {
        return JsonConvert.SerializeObject(manifest, Formatting.Indented, new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Include,
        });
    }

    private static IEnumerable<LlmPropertyExport> GetProperties(StructExport structExport, UAsset asset)
    {
        if (structExport.LoadedProperties.Length > 0)
        {
            foreach (var property in structExport.LoadedProperties)
            {
                yield return new LlmPropertyExport
                {
                    Name = property.Name.ToString(),
                    Type = property.SerializedType.ToString(),
                    Flags = property.PropertyFlags.ToString(),
                };
            }
        }
        else if (structExport.Children != null)
        {
            foreach (var childIndex in structExport.Children)
            {
                if (!childIndex.IsExport()) continue;
                if (childIndex.ToExport(asset) is not PropertyExport property) continue;
                yield return new LlmPropertyExport
                {
                    Name = property.ObjectName.ToString(),
                    Type = property.GetExportClassType().ToString().Replace("Property", ""),
                    Flags = property.Property.PropertyFlags.ToString(),
                };
            }
        }
    }
}
