#if XENOPREVIEW_USE_SPINE
using Spine.Api;
using Spine.UI.SettingsFramework;
using Verse;

namespace XenoPreview
{
    public sealed class XenoPreviewMod : SpineMod<XenoPreviewSettings>
    {
        public XenoPreviewMod(ModContentPack content)
            : base(
                content,
                "coolnether123.XenoPreview",
                new SemanticVersion(1, 1, 0),
                XenoPreviewSettingsRegistry.Schema.Definitions,
                SpineCapability.SettingsSchema)
        {
        }

        protected override string SettingsCategoryLabel => "XenoPreview";
    }
}
#endif
