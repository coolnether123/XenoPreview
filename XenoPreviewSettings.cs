#if XENOPREVIEW_USE_SPINE
using Spine.Api;
using Verse;

namespace XenoPreview
{
    public enum XenoPreviewRotation
    {
        South,
        East,
        North,
        West
    }

    public enum XenoPreviewSize
    {
        Compact,
        Standard,
        Large
    }

    public sealed class XenoPreviewSettings : ModSettings
    {
        public bool EnablePreview = true;
        public bool StartMinimized;
        public bool RememberWindowPosition = true;
        public XenoPreviewRotation DefaultRotation = XenoPreviewRotation.South;
        public XenoPreviewSize PreviewSize = XenoPreviewSize.Standard;
        public bool FemaleShowClothes = true;
        public bool MaleShowClothes = true;
        public bool FemaleShowTattoos = true;
        public bool MaleShowTattoos = true;

        // These fields are persisted state for the RememberWindowPosition preference,
        // not player-facing settings rows.
        public float WindowPositionX;
        public float WindowPositionY;
        public bool HasSavedWindowPosition;

        public override void ExposeData()
        {
            SpineApi.Settings.Scribe(
                this,
                XenoPreviewSettingsRegistry.Definitions);
            Scribe_Values.Look(ref WindowPositionX, "windowPositionX", 0f);
            Scribe_Values.Look(ref WindowPositionY, "windowPositionY", 0f);
            Scribe_Values.Look(
                ref HasSavedWindowPosition,
                "hasSavedWindowPosition",
                false);
            base.ExposeData();
        }
    }
}
#endif
