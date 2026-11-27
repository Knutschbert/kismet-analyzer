namespace KismetAnalyzer;

/// <summary>
/// v3 map-doc pruning policy (ticket kismet-llm-rb6.1). Applies ONLY to map assets
/// (<c>asset</c> ending in <c>.umap</c>): drops engine art/geometry/cinematic/editor exported
/// objects that carry no gameplay/modding semantics (~86% of a map's exports) while keeping
/// gameplay actors and logic.
///
/// <para>Default is KEEP. An export is dropped only when its class matches the denylist below and
/// is not caught by a keep-override. This fails toward keeping gameplay information: unknown or
/// new classes are retained, never silently discarded.</para>
///
/// <para>Keep-overrides (never dropped): any Blueprint instance (class ending in <c>_C</c>) and the
/// map descriptor (<c>World</c>/<c>WorldSettings</c>/<c>TBLWorldSettings</c>/<c>LevelScriptActor</c>).
/// The gameplay-component allowlist from the plan (SplineComponent, InteractableComponent,
/// HudMarkerDisplayComponent, TBLGameScoringComponent, StatsComponent, TBLProjectileMovementComponent,
/// TimelineComponent, WeaponRackComponent, TBLNavModifierComponent, …) is kept simply by not being on
/// the denylist. Note SplineComponent is kept; SplineMeshComponent is dropped.</para>
///
/// <para>This exact policy is mirrored by <c>scripts/prune_map_tree.py</c> in the kismet-analyzer-llm
/// repo (the one-time v2→v3 post-process) and documented in the workbench note
/// <c>docs/research/kismet/notes/2026-11-27-v3-map-pruning-plan.md</c>. The two MUST stay in sync.</para>
/// </summary>
public static class MapPrunePolicy
{
    /// <summary>Returns true when the asset path is a map (prune applies only to these).</summary>
    public static bool IsMapAsset(string? assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)) return false;
        return assetPath.EndsWith(".umap", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Exported-object classes dropped from map docs (art/geometry/cinematic/editor).</summary>
    private static readonly HashSet<string> DropExact = new(StringComparer.Ordinal)
    {
        // meshes / geometry / scene scaffolding
        "StaticMeshComponent", "StaticMeshActor", "InstancedStaticMeshComponent",
        "HierarchicalInstancedStaticMeshComponent", "FoliageInstancedStaticMeshComponent",
        "SkeletalMeshComponent", "SceneComponent",
        // bare collision primitives (TBLBoxCollision is gameplay and intentionally NOT here)
        "BoxComponent", "CapsuleComponent", "SphereComponent",
        // physics / brush / model
        "Model", "BodySetup", "BrushComponent", "Brush",
        // decals
        "DecalComponent", "DecalActor",
        // dynamic/baked materials + baked textures
        "MaterialInstanceDynamic", "LandscapeMaterialInstanceConstant", "MaterialInstanceConstant",
        "LightMapTexture2D", "ShadowMapTexture2D", "Texture2D", "TextureCube", "Texture2DArray",
        // particles / vfx
        "ParticleSystemComponent", "TBLParticleSystemComponent", "Emitter", "ParticleSystem",
        // audio (Wwise)
        "AkComponent", "AkAmbientSound", "AkReverbVolume", "AkAcousticPortal",
        "AkLateReverbComponent", "AkRoomComponent", "AkSurfaceReflectorSetComponent", "AkGeometryComponent",
        // splines: mesh only (SplineComponent is kept)
        "SplineMeshComponent",
        // generic navigation (TBLNavModifierComponent is gameplay and intentionally NOT here)
        "NavLinkCustomComponent", "NavLinkProxy", "NavModifierVolume", "NavMeshBoundsVolume",
        "RecastNavMesh", "NavLinkComponent",
        // editor-only gizmos
        "ArrowComponent", "BillboardComponent", "DrawSphereComponent", "DrawFrustumComponent",
        "TextRenderComponent", "BookMark",
        // rendering captures / terrain geometry / lightmass scaffolding
        "SphereReflectionCapture", "SphereReflectionCaptureComponent", "PlanarReflectionComponent",
        "LandscapeComponent", "LandscapeHeightfieldCollisionComponent", "LandscapeGizmoActiveActor",
        "LightmassImportanceVolume", "LightmassPortal", "PrecomputedVisibilityVolume",
        // explicit lights (plus the endswith-Light rule below)
        "PointLightComponent", "PointLight", "SpotLightComponent", "SpotLight",
        "DirectionalLightComponent", "DirectionalLight", "SkyLightComponent", "SkyLight",
        "RectLightComponent", "RectLight", "SkyAtmosphereComponent", "ExponentialHeightFogComponent",
    };

    /// <summary>Class-name prefixes dropped from map docs (sequencer/cinematic scaffolding).</summary>
    private static readonly string[] DropPrefix = { "MovieScene" };

    private static bool IsLight(string cls) =>
        cls.EndsWith("Light", StringComparison.Ordinal) ||
        cls.EndsWith("LightComponent", StringComparison.Ordinal);

    /// <summary>True when an exported object of this class should be dropped from a map doc.</summary>
    public static bool ShouldDrop(string? className)
    {
        if (string.IsNullOrEmpty(className)) return false;
        // Keep-overrides: Blueprint instances and the map descriptor are never dropped.
        if (className.EndsWith("_C", StringComparison.Ordinal)) return false;
        if (className is "World" or "WorldSettings" or "TBLWorldSettings" or "LevelScriptActor") return false;

        if (DropExact.Contains(className)) return true;
        foreach (var prefix in DropPrefix)
        {
            if (className.StartsWith(prefix, StringComparison.Ordinal)) return true;
        }
        return IsLight(className);
    }
}
