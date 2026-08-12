#if XENOPREVIEW_USE_SPINE
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

        internal static readonly SettingsSchema<XenoPreviewSettings> Schema =
            new SettingsSchema<XenoPreviewSettings>(
                SettingsSchemaConventions.LowerCamelCase);

        static XenoPreviewSettingsRegistry()
        {
            var preview = Schema.Section(HeaderId, "Preview");
            preview.Toggle(
                EnablePreviewId,
                settings => settings.EnablePreview,
                "Enable preview",
                tooltip: "Automatically open the XenoPreview window when a xenotype dialog opens.");
            preview.Toggle(
                StartMinimizedId,
                settings => settings.StartMinimized,
                "Start minimized",
                tooltip: "Open new preview windows as the compact Show Preview button.");
            preview.Toggle(
                RememberWindowPositionId,
                settings => settings.RememberWindowPosition,
                "Remember window position",
                tooltip: "Restore the preview window's last position when it opens again.");
            preview.Enum(
                DefaultRotationId,
                settings => settings.DefaultRotation,
                "Default pawn rotation",
                tooltip: "Choose the direction used by new female and male previews.",
                labelProvider: value => value.ToString());
            preview.Enum(
                PreviewSizeId,
                settings => settings.PreviewSize,
                "Preview window size",
                tooltip: "Choose the scale used by new preview windows and portraits.",
                labelProvider: value => value.ToString());
            preview.Toggle(
                FemaleClothesId,
                settings => settings.FemaleShowClothes,
                "Show female clothes",
                tooltip: "Start the female preview with clothes visible.");
            preview.Toggle(
                MaleClothesId,
                settings => settings.MaleShowClothes,
                "Show male clothes",
                tooltip: "Start the male preview with clothes visible.");
            preview.Toggle(
                FemaleTattoosId,
                settings => settings.FemaleShowTattoos,
                "Show female tattoos",
                tooltip: "Start the female preview with tattoos visible.")
                .ShownWhen(_ => ModsConfig.IdeologyActive);
            preview.Toggle(
                MaleTattoosId,
                settings => settings.MaleShowTattoos,
                "Show male tattoos",
                tooltip: "Start the male preview with tattoos visible.")
                .ShownWhen(_ => ModsConfig.IdeologyActive);
        }
    }
}
#endif
