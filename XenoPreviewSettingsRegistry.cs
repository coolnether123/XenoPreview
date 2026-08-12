#if XENOPREVIEW_USE_SPINE
using System.Collections.Generic;
using Spine.UI.SettingsFramework;
using Verse;

namespace XenoPreview
{
    internal static class XenoPreviewSettingsRegistry
    {
        internal const string HeaderId = "preview.header";
        internal const string EnablePreviewId = "preview.enable";
        internal const string StartMinimizedId = "preview.startMinimized";
        internal const string RememberWindowPositionId = "preview.rememberWindowPosition";
        internal const string DefaultRotationId = "preview.defaultRotation";
        internal const string PreviewSizeId = "preview.size";
        internal const string FemaleClothesId = "preview.female.clothes";
        internal const string MaleClothesId = "preview.male.clothes";
        internal const string FemaleTattoosId = "preview.female.tattoos";
        internal const string MaleTattoosId = "preview.male.tattoos";

        internal static readonly IReadOnlyList<SettingDefinition> Definitions =
            new[]
            {
                SettingDefinitions.Header(HeaderId, "Preview"),
                SettingDefinitions.Toggle(
                    EnablePreviewId,
                    nameof(XenoPreviewSettings.EnablePreview),
                    "Enable preview",
                    tooltip: "Automatically open the XenoPreview window when a xenotype dialog opens.",
                    scribeKey: "enablePreview"),
                SettingDefinitions.Toggle(
                    StartMinimizedId,
                    nameof(XenoPreviewSettings.StartMinimized),
                    "Start minimized",
                    tooltip: "Open new preview windows as the compact Show Preview button.",
                    scribeKey: "startMinimized"),
                SettingDefinitions.Toggle(
                    RememberWindowPositionId,
                    nameof(XenoPreviewSettings.RememberWindowPosition),
                    "Remember window position",
                    tooltip: "Restore the preview window's last position when it opens again.",
                    scribeKey: "rememberWindowPosition"),
                SettingDefinitions.Enum(
                    DefaultRotationId,
                    nameof(XenoPreviewSettings.DefaultRotation),
                    typeof(XenoPreviewRotation),
                    "Default pawn rotation",
                    tooltip: "Choose the direction used by new female and male previews.",
                    scribeKey: "defaultRotation",
                    labelProvider: value => ((XenoPreviewRotation)value).ToString()),
                SettingDefinitions.Enum(
                    PreviewSizeId,
                    nameof(XenoPreviewSettings.PreviewSize),
                    typeof(XenoPreviewSize),
                    "Preview window size",
                    tooltip: "Choose the scale used by new preview windows and portraits.",
                    scribeKey: "previewSize",
                    labelProvider: value => ((XenoPreviewSize)value).ToString()),
                SettingDefinitions.Toggle(
                    FemaleClothesId,
                    nameof(XenoPreviewSettings.FemaleShowClothes),
                    "Show female clothes",
                    tooltip: "Start the female preview with clothes visible.",
                    scribeKey: "femaleShowClothes"),
                SettingDefinitions.Toggle(
                    MaleClothesId,
                    nameof(XenoPreviewSettings.MaleShowClothes),
                    "Show male clothes",
                    tooltip: "Start the male preview with clothes visible.",
                    scribeKey: "maleShowClothes"),
                SettingDefinitions.Toggle(
                    FemaleTattoosId,
                    nameof(XenoPreviewSettings.FemaleShowTattoos),
                    "Show female tattoos",
                    tooltip: "Start the female preview with tattoos visible.",
                    scribeKey: "femaleShowTattoos")
                    .ShownWhen(_ => ModsConfig.IdeologyActive),
                SettingDefinitions.Toggle(
                    MaleTattoosId,
                    nameof(XenoPreviewSettings.MaleShowTattoos),
                    "Show male tattoos",
                    tooltip: "Start the male preview with tattoos visible.",
                    scribeKey: "maleShowTattoos")
                    .ShownWhen(_ => ModsConfig.IdeologyActive)
            };
    }
}
#endif
