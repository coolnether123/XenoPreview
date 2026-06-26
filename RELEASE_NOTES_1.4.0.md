# XenoPreview 1.4.0

## Highlights

- Fixed xenotype name randomization returning `PreviewXenotype`.
- Added a standalone xenotype name generator instead of relying on RimWorld's broken vanilla name-generation path.
- Improved generated names for larger gene sets with richer gene-aware patterns such as clades, lineages, strains, genomes, and variants.
- Gene selection changes no longer randomize the xenotype name. Names now change only when using the Randomize button or name options menu.
- Reduced repeated preview pawn-generation error spam when RimWorld cannot generate a preview pawn for the current gene set.
- Prepared a clean release package without source files, project files, debug symbols, or bundled Harmony.

## Compatibility

- Supports RimWorld 1.4, 1.5, and 1.6.
- Requires Biotech.
- Harmony remains a required dependency and is not bundled in the release ZIP.
- Safe to add or remove from saves.

## Notes

- The internal temporary preview xenotype name is still used only for preview pawn generation and is not used for randomized xenotype names.
- If Steam redownloads an older Workshop copy while testing locally, make sure only one `coolnether123.XenoPreview` package is present in RimWorld's active mod scan paths.
