# XenoPreview developer fixture

This is a separately loadable developer-only fixture for XenoPreview. It owns
the deterministic live probes used by the release lane and is never part of a
shipping XenoPreview package.

The fixture is guarded for the exact supported RimWorld configurations `1.4`,
`1.5`, and `1.6`. Build it against the freshly validated shipping
`XenoPreview.dll` for the same version, then load this folder alongside the
shipping mod only in an isolated developer harness lane.

The fixture opens the real `Dialog_CreateXenotype` through RimWorld's public
window stack, applies deterministic gene changes to the real dialog state, and
reports the real XenoPreview window, preview pawns, rendering settings, and
cleanup state. Reflection is confined to this developer assembly so the
shipping payload has no test hooks or fixture dependency.

Do not include `Developer`, fixture source, symbols, or fixture assemblies in a
release package.
