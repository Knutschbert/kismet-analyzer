# Semantic asset graph schema

The schema is intentionally small and model-oriented. It should contain names
and relationships that help answer questions, not every field needed to write a
`.uasset` back to disk.

The exporter currently emits these top-level fields:

```text
schemaVersion, source, parseStatus, asset, class, parent, engineVersion,
properties, functions, callEdges, diagnostics
```

## Asset summary

```json
{
  "schemaVersion": 1,
  "source": "kismet-bytecode",
  "parseStatus": "complete",
  "asset": "/Game/Characters/BP_Player",
  "class": "BP_Player_C",
  "parent": "Character",
  "properties": [
    {"name": "Health", "type": "Float"},
    {"name": "bDead", "type": "Bool"}
  ],
  "functions": [
    {
      "name": "TakeDamage",
      "calls": ["Die"],
      "reads": ["Health"],
      "writes": ["Health", "bDead"],
      "branches": 1
    }
  ],
  "callEdges": [
    {"from": "TakeDamage", "to": "Die"}
  ]
}
```

## Function control flow

Return this only for a selected function or selected block range:

```json
{
  "schemaVersion": 1,
  "source": "kismet-bytecode",
  "parseStatus": "complete",
  "asset": "/Game/Characters/BP_Player",
  "function": "TakeDamage",
  "blocks": [
    {
      "id": 0,
      "code": ["Health = Health - Damage"],
      "successors": [
        {"to": 1, "when": "Health > 0"},
        {"to": 2, "when": "Health <= 0"}
      ]
    },
    {
      "id": 2,
      "code": ["call Die()"],
      "successors": []
    }
  ]
}
```

## Representation rules

- Resolve imports and exports to readable names whenever possible.
- Keep bytecode addresses as local evidence, not as the primary identity.
- Use explicit fields such as `calls`, `reads`, `writes`, `callEdges`, and
  `successors`.
- Omit empty optional fields from summary responses.
- Keep raw package indices, serialization offsets, and parser-specific fields in
  a separate detail response.
- Represent unknown or failed parsing explicitly, for example with
  `"parse_status": "raw"`, rather than silently dropping data.
- Treat `parseStatus: "partial"` and the `diagnostics` array as authoritative
  indications that some behavior could not be fully reconstructed.
- Preserve the original asset path and function names for citations and lookup.

## Response tiers

| Tier | Contents | Typical use |
| --- | --- | --- |
| 0 | Asset identity, properties, function names, direct call edges | Orientation |
| 1 | One function summary and nearby callers/callees | Behavioral questions |
| 2 | Selected CFG blocks and pseudo-code | Detailed reasoning |
| 3 | Filtered UAssetAPI JSON or `.txt` CFG | Verification and forensics |
| 4 | Raw/unparsed data and full asset JSON | Parser/debugging workflows |
