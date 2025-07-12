using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace XenoPreview
{
    [StaticConstructorOnStartup]
    public static class XenoPreview
    {
        public static XenoPreviewWindow PreviewWindowInstance;

        static XenoPreview()
        {
            try
            {
                var harmony = new Harmony("coolnether123.XenoPreview");

                // Patch the Window.Close method
                MethodInfo closeMethod = AccessTools.Method(
                    typeof(Window),
                    "Close",
                    new[] { typeof(bool) }
                );
                MethodInfo closePostfix = AccessTools.Method(
                    typeof(Dialog_CreateXenotype_Patches),
                    "Close_Postfix"
                );

                if (closeMethod != null && closePostfix != null)
                {
                    harmony.Patch(closeMethod,
                        postfix: new HarmonyMethod(closePostfix));
                }
                else
                {
                    Log.Error(
                        "[XenoPreview] Failed to find required methods for patching Close"
                    );
                }

                // Patch for RimWorld.GeneUtility.GenerateXenotypeNameFromGenes to prevent NullReferenceException
                MethodInfo generateXenotypeNameMethod = AccessTools.Method(typeof(RimWorld.GeneUtility), "GenerateXenotypeNameFromGenes");
                MethodInfo generateXenotypeNamePrefix = AccessTools.Method(typeof(Dialog_CreateXenotype_Patches), "GenerateXenotypeNameFromGenes_Prefix");

              if (generateXenotypeNameMethod != null && generateXenotypeNamePrefix != null)
                {
                    harmony.Patch(generateXenotypeNameMethod,
                        prefix: new HarmonyMethod(generateXenotypeNamePrefix));
                }
                else
                {
                    Log.Error("[XenoPreview] Failed to find required methods for patching GenerateXenotypeNameFromGenes");
                }

                //Open the XenoPreview window when the Xenotype Creator or Gene Assembler is opened
                MethodInfo OpenXenoWindowMethod = AccessTools.Method(typeof(GeneCreationDialogBase), "PreOpen");
                MethodInfo OpenXenoWindowPostfix = AccessTools.Method(typeof(Dialog_CreateXenotype_Patches), "PreOpen_Prefix");

                if (generateXenotypeNameMethod != null && generateXenotypeNamePrefix != null)
                {
                    harmony.Patch(OpenXenoWindowMethod,
                        postfix: new HarmonyMethod(OpenXenoWindowPostfix));
                }
                else
                {
                    Log.Error("[XenoPreview] Failed to find required methods for patching PreOpen");
                }

            }
            catch (Exception ex)
            {
                Log.Error(
                    "[XenoPreview] MOD LOAD: CRITICAL - Failed to apply Harmony patches: "
                        + ex.ToString()
                );
            }
        }
    }

    

    public static class Dialog_CreateXenotype_Patches
    {
        // Helper to ensure the preview window is open and correctly configured
        private static void EnsurePreviewWindowOpen(Window dialogInstance)
        {
            if (XenoPreview.PreviewWindowInstance == null || !XenoPreview.PreviewWindowInstance.IsOpen)
            {
                XenoPreview.PreviewWindowInstance = new XenoPreviewWindow();
                Find.WindowStack.Add(XenoPreview.PreviewWindowInstance);
            }

            XenoPreview.PreviewWindowInstance.SetDialog(dialogInstance);
            //I don't want to set it manually, but this window doesn't know its size till after this one is opened. I tried to use a PostOpen Postfix patch, but it didn't work.
            XenoPreview.PreviewWindowInstance.UpdatePosition(new Vector2(1474, 1009));

        }

        public static bool GenerateXenotypeNameFromGenes_Prefix(ref string __result)
        {
            if (XenoPreview.PreviewWindowInstance != null && XenoPreview.PreviewWindowInstance.IsOpen)
            {
                __result = "PreviewXenotype"; // Provide a dummy name
                return false; // Skip original method
            }
            return true; // Execute original method
        }

        public static void PreOpen_Prefix(GeneCreationDialogBase __instance)
        {
            try
            {
                    EnsurePreviewWindowOpen(__instance);
            }
            catch (Exception ex)
            {
                Log.Error("[XenoPreview] Error in PostOpen_Postfix: " + ex.ToString());
            }
        }

        // Postfix for Close
        public static void Close_Postfix(Window __instance)
        {
            try
            {
                // Check if it's Dialog_CreateXenotype or Dialog_CreateXenogerm
                if (
                    !(__instance is Dialog_CreateXenotype)
                    && !(__instance is Dialog_CreateXenogerm)
                )
                {
                    return;
                }

                if (
                    XenoPreview.PreviewWindowInstance != null
                    && XenoPreview.PreviewWindowInstance.IsOpen
                )
                {
                    XenoPreview.PreviewWindowInstance.Close(false);
                    XenoPreview.PreviewWindowInstance = null;
                }
            }
            catch (Exception ex)
            {
                Log.Error("[XenoPreview] Error in Close_Postfix: " + ex.ToString());
            }
        }


    }
}