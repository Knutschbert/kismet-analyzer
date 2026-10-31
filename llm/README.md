# LLM and MCP integration

This directory documents how to present cooked Unreal assets produced by
`kismet-analyzer` to language models.

## Recommendation

Use a **compact semantic JSON graph** as the canonical model-facing format.
Expose it through an MCP server with progressive disclosure:

1. Return a small asset summary first.
2. Retrieve the call graph around a relevant function.
3. Retrieve a function's control-flow blocks only when needed.
4. Fall back to filtered asset JSON or the generated text CFG for forensic detail.

The existing formats have different roles:

| Format | Recommended role |
| --- | --- |
| Semantic JSON graph | Primary MCP response and index format |
| `gen-cfg` `.txt` | Human-readable behavior/evidence view |
| UAssetAPI JSON | Exact, lossless-ish parser/object dump and round-trip input |
| DOT/SVG/HTML | Human visualization |

See:

- [`schema.md`](schema.md) for the proposed response structure
- [`mcp-design.md`](mcp-design.md) for tools, retrieval levels, and token rules
- [`examples/asset-response.json`](examples/asset-response.json) for an example

## Why not send raw asset JSON?

`to-json` serializes the UAssetAPI object graph. It contains useful exports and
properties, but also .NET `$type` metadata, package indices, serialization
fields, null-heavy structures, and Base64 byte arrays. Those details are useful
for exact inspection but pollute the context for behavioral questions.

## Why not use `.txt` as the canonical format?

`gen-cfg` is a good readable projection of Kismet bytecode. It includes
functions, instructions, basic blocks, branch successors, and resolved names.
However, calls are embedded in instruction text and CFG edges rather than being
represented as a dedicated call graph. A normalized graph makes callers,
callees, reads, writes, and dependencies directly queryable.

Cooked assets should also be treated as compiled behavior. The recovered CFG is
not guaranteed to reproduce the original Blueprint editor node graph or layout.

## Semantic exporter usage

The analyzer provides a separate semantic exporter without changing the
existing `to-json`, `gen-cfg`, or DOT commands:

```console
kismet-analyzer --ue-version VER_UE4_25 to-llm-json Asset.uasset > Asset.llm.json
kismet-analyzer --ue-version VER_UE4_25 gen-llm-tree unpacked-assets llm-output/
```

`to-llm-json` emits one compact semantic asset document. `gen-llm-tree` emits
one `.llm.json` document per asset plus `manifest.json` for discovery.

The `kismet-analyzer-llm` workspace includes `scripts\produce-llm.bat`. Drop a
single `.uasset`/`.umap` file or an unpacked asset directory onto it. The script
automatically selects the single-asset or tree command, uses the bundled
`bin\kismet-analyzer.exe`, and defaults to UE4.25. Set `KISMET_UE_VERSION` or
`KISMET_ANALYZER_EXE` to override those defaults.
