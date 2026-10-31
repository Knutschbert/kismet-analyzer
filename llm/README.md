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
