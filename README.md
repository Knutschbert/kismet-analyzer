# KismetAnalyzer
Tools for analyzing and manipulating kismet bytecode in cooked Unreal Engine
assets.

## Example CFG generation
Assuming [graphviz](https://graphviz.org/) is in the `PATH`:
```console
kismet-analyzer gen-cfg-tree unpacked-fsd/ output/ FSD --render --progress
```
`output/` should now contain generated CFGs as well as a class hierarchy.

There is also a command to generate a CFG for a single asset and open it
directly in a web browser:
```console
kismet-analyzer cfg unpacked/path/to/YourAsset.uasset
```

## LLM and MCP integration

Guidance for exposing cooked asset behavior to language models is documented
under [`llm/schema.md`](llm/schema.md) and [`llm/mcp-design.md`](llm/mcp-design.md).
The complete usage guide lives in the companion `kismet-analyzer-llm` workspace.

For direct semantic export, use:

```console
kismet-analyzer to-llm-json unpacked/path/to/YourAsset.uasset > YourAsset.llm.json
kismet-analyzer gen-llm-tree unpacked-assets/ llm-output/
```

The tree command writes one `.llm.json` file per asset and a `manifest.json`
for discovery. Existing `to-json`, `gen-cfg`, and DOT output are unchanged.

## Example Output

![CFG](https://github.com/trumank/kismet-analyzer/assets/1144160/a59014c1-a65c-4c1e-9f70-2589f59ca33d)
![class hierachy](https://github.com/trumank/kismet-analyzer/assets/1144160/70ad2157-6648-4140-8f12-019fb3a9cd73)
