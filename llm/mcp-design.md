# MCP design

MCP should provide query-oriented access to the asset graph rather than return
whole assets by default. The protocol is the retrieval interface; the compact
semantic JSON described in [`schema.md`](schema.md) is the response format.

## Suggested tools

```text
find_assets(query, limit)
get_asset_summary(asset)
get_function_summary(asset, function)
get_call_graph(asset, function?, depth?, direction?, limit?)
get_control_flow(asset, function, block_ids?, max_blocks?)
find_references(symbol, asset?, limit?)
search_bytecode(query, asset?, function?, limit?)
get_asset_detail(asset, section, limit?)
```

### `get_asset_summary`

Return the Tier 0 structure: identity, parent, properties, function summaries,
and direct call edges. This should be the default starting point.

### `get_call_graph`

Return a bounded neighborhood around a function. Support both directions:

- `direction: "callees"` for what the function invokes
- `direction: "callers"` for what invokes it
- `direction: "both"` for a small local graph

Avoid returning an unbounded transitive closure. Use `depth` and `limit`.

### `get_control_flow`

Return only the requested function's selected blocks. Include successor
conditions and compact pseudo-code, not the full serialized expression object.

### `get_asset_detail`

Use for filtered properties, imports, exports, strings, raw parse failures, or
the generated `.txt` CFG. This is an escape hatch, not the normal path.

## Context minimization

1. Make every result bounded by default.
2. Return names instead of repeated nested object definitions.
3. Do not include raw `$type` fields in normal responses.
4. Do not include Base64 data unless explicitly requested.
5. Do not include DOT, SVG, or HTML in model responses.
6. Do not return both semantic JSON and an equivalent full text dump.
7. Include a short `truncated`/`next_cursor` field when results are limited.
8. Keep evidence close to the claim: include only the blocks supporting the
   requested behavior.
9. Cache and index the graph server-side so repeated questions do not require
   repeated full-asset serialization.

## Small versus large models

Use the same schema for both. Change the retrieval budget, not the data model:

- Smaller models: Tier 0 or Tier 1, one function at a time, short code lines,
  low call-graph depth.
- Larger models: Tier 1 plus selected Tier 2 blocks, bounded call graphs, and
  filtered detail JSON when necessary.

The MCP server may provide a `compact` option, but it should remove optional
metadata rather than rename core fields. Stable field names improve tool use
across models.

## Evidence and uncertainty

Responses should distinguish reconstructed behavior from exact raw data. Useful
metadata includes:

```json
{
  "source": "kismet-bytecode",
  "parse_status": "complete",
  "confidence": "inferred-control-flow",
  "warnings": []
}
```

If an expression is unsupported by `SummaryGenerator`, expose the warning and
offer the raw/detail endpoint instead of presenting an incomplete function as
complete.

## What the existing commands contribute

- `gen-cfg` supplies readable pseudo-code and basic-block successors.
- `gen-cfg-tree` supplies the class hierarchy and rendered CFG pages.
- `to-json`/`gen-json-tree` supplies the complete UAssetAPI serialization.
- `to-llm-json` supplies one compact semantic asset response.
- `gen-llm-tree` supplies per-asset semantic responses and `manifest.json`.

An MCP indexer can consume these projections, but a direct integration with the
parsed `UAsset` object is preferable because it can create explicit call,
property, and asset-reference edges without reparsing rendered output.
