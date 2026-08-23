# XenoPreview Agent Guide

Also follow `A:\Dev\RimWorld\AGENTS.md`.

`About\About.xml`, the project file, and release notes define
supported versions and DLC requirements. Preserve version-specific payloads;
release ZIPs are artifacts, not source. Do not bundle new dependency copies.

Read `README.md`, `RELEASE_NOTES_1.4.0.md`, `XenoPreview.csproj`, and package
metadata. Resolve each changed version through the shared manifest and test it
in its own harness lane, selecting DLC only when required.

Verify xenotype-name stability, gender locks, reroll, rotation, apparel,
Ideology tattoos, resizing/minimizing, repeated opening, settings persistence,
and preview-pawn cleanup. Compilation does not prove UI or pawn-lifecycle
compatibility.
