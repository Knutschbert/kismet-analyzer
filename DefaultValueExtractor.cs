using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;

namespace KismetAnalyzer;

/// <summary>A single extracted default value (name, type, and a compact readable value).</summary>
public sealed class LlmDefaultValue
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Value { get; set; } = "";
}

/// <summary>
/// Reads CDO (Class Default Object) property values from a cooked <see cref="UAsset"/> and
/// renders them as compact, readable default-value records. This is intentionally separate from
/// the kismet bytecode analysis so graph-less, property-only assets (e.g. utility AI behavior
/// blueprints) can surface their data without re-running tree generation.
/// </summary>
public static class DefaultValueExtractor
{
    /// <summary>
    /// Extracts default values from the asset's CDO. Returns an empty list when the asset has no
    /// class export or no CDO.
    /// </summary>
    public static List<LlmDefaultValue> Extract(UAsset asset)
    {
        var results = new List<LlmDefaultValue>();
        if (asset.Exports == null) return results;

        var cdo = FindCdo(asset);
        if (cdo == null) return results;

        foreach (var property in cdo.Data)
        {
            results.Add(new LlmDefaultValue
            {
                Name = property.Name?.ToString() ?? "",
                Type = TypeLabel(property),
                Value = RenderValue(property, asset),
            });
        }

        return results;
    }

    private static NormalExport? FindCdo(UAsset asset)
    {
        var classExport = asset.GetClassExport();
        if (classExport == null) return null;

        var cdoIndex = classExport.ClassDefaultObject;
        if (cdoIndex == null || cdoIndex.IsNull()) return null;
        if (!cdoIndex.IsExport()) return null;

        return cdoIndex.ToExport(asset) as NormalExport;
    }
    private static string TypeLabel(PropertyData property)
    {
        var name = property.GetType().Name;
        return name.EndsWith("PropertyData")
            ? name[..^"PropertyData".Length]
            : name;
    }

    private static string RenderValue(PropertyData property, UAsset asset)
    {
        if (property is ArrayPropertyData array && array.Value is { Length: > 0 })
        {
            var items = array.Value.Select(item => RenderValue(item, asset)).ToList();
            return "[" + string.Join(", ", items) + "]";
        }

        if (property is StructPropertyData strukt && strukt.Value is { Count: > 0 })
        {
            var members = strukt.Value.Select(member => $"{member.Name}={RenderValue(member, asset)}");
            var structName = strukt.StructType?.ToString();
            return string.IsNullOrEmpty(structName)
                ? "{" + string.Join(", ", members) + "}"
                : $"{structName}({string.Join(", ", members)})";
        }

        if (property is ObjectPropertyData objectData)
        {
            return ResolveObject(objectData, asset) ?? objectData.ToString();
        }

        return RenderScalar(property);
    }

    private static string? ResolveObject(ObjectPropertyData objectData, UAsset asset)
    {
        if (objectData.Value == null || objectData.Value.IsNull()) return "None";
        if (objectData.IsExport())
        {
            return objectData.ToExport(asset)?.ObjectName.ToString() ?? "None";
        }
        if (objectData.IsImport())
        {
            return objectData.ToImport(asset)?.ObjectName.ToString() ?? "None";
        }
        return null;
    }

    private static string RenderScalar(PropertyData property)
    {
        if (property is FloatPropertyData floatData)
        {
            return floatData.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        if (property is DoublePropertyData doubleData)
        {
            return doubleData.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var text = property.ToString() ?? "";
        if (!string.IsNullOrWhiteSpace(text) && text != property.GetType().Name)
        {
            return text;
        }
        if (property.RawValue != null)
        {
            var raw = property.RawValue.ToString();
            if (!string.IsNullOrWhiteSpace(raw)) return raw;
        }
        return text;
    }
}
