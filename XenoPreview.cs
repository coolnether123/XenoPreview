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

                // Patch GeneCreationDialogBase for both xenotype creator and gene assembler
                MethodInfo originalMethod = AccessTools.Method(typeof(GeneCreationDialogBase), "DoWindowContents");
                MethodInfo postfixMethod = AccessTools.Method(typeof(Dialog_CreateXenotype_Patches), "DoWindowContents_Postfix");

                if (originalMethod != null && postfixMethod != null)
                {
                    harmony.Patch(originalMethod,
                        postfix: new HarmonyMethod(postfixMethod));
                }
                else
                {
                    Log.Error("[XenoPreview] Failed to find required methods for patching DoWindowContents");
                }

                // Patch the Window.Close method
                MethodInfo closeMethod = AccessTools.Method(typeof(Window), "Close", new[] { typeof(bool) });
                MethodInfo closePostfix = AccessTools.Method(typeof(Dialog_CreateXenotype_Patches), "Close_Postfix");

                if (closeMethod != null && closePostfix != null)
                {
                    harmony.Patch(closeMethod,
                        postfix: new HarmonyMethod(closePostfix));
                }
                else
                {
                    Log.Error("[XenoPreview] Failed to find required methods for patching Close");
                }

                // Keep an eye on WindowStack to detect when xenotype windows are opened
                MethodInfo windowAddMethod = AccessTools.Method(typeof(WindowStack), "Add");
                MethodInfo windowAddPostfix = AccessTools.Method(typeof(Dialog_CreateXenotype_Patches), "WindowStack_Add_Postfix");

                if (windowAddMethod != null && windowAddPostfix != null)
                {
                    harmony.Patch(windowAddMethod,
                        postfix: new HarmonyMethod(windowAddPostfix));
                }

                // Dynamically patch BetterPrerequisites.NotifyGenesChanges.Notify_GenesChanged_Postfix if the mod is present
                Type betterPrerequisitesType = AccessTools.TypeByName("BetterPrerequisites.NotifyGenesChanges");
                if (betterPrerequisitesType != null)
                {
                    MethodInfo notifyGenesChangedPostfixMethod = AccessTools.Method(betterPrerequisitesType, "Notify_GenesChanged_Postfix");
                    MethodInfo notifyGenesChangedPrefix = AccessTools.Method(typeof(Dialog_CreateXenotype_Patches), "NotifyGenesChanged_Postfix_Prefix");

                    if (notifyGenesChangedPostfixMethod != null && notifyGenesChangedPrefix != null)
                    {
                        harmony.Patch(notifyGenesChangedPostfixMethod,
                            prefix: new HarmonyMethod(notifyGenesChangedPrefix));
                    }
                    else
                    {
                        Log.Warning("[XenoPreview] Could not find BetterPrerequisites.NotifyGenesChanges.Notify_GenesChanged_Postfix. Patch skipped.");
                    }
                }

                // Dynamically patch BigAndSmall.HumanoidPawnScaler.LazyGetCache if the mod is present
                Type bigAndSmallScalerType = AccessTools.TypeByName("BigAndSmall.HumanoidPawnScaler");
                Log.Message($"[XenoPreview] BigAndSmall.HumanoidPawnScaler type found: {bigAndSmallScalerType != null}");
                if (bigAndSmallScalerType != null)
                {
                    MethodInfo lazyGetCacheMethod = AccessTools.Method(bigAndSmallScalerType, "LazyGetCache");
                    Log.Message($"[XenoPreview] BigAndSmall.HumanoidPawnScaler.LazyGetCache method found: {lazyGetCacheMethod != null}");
                    MethodInfo lazyGetCachePrefix = AccessTools.Method(typeof(Dialog_CreateXenotype_Patches), "LazyGetCache_Prefix");

                    if (lazyGetCacheMethod != null && lazyGetCachePrefix != null)
                    {
                        harmony.Patch(lazyGetCacheMethod,
                            prefix: new HarmonyMethod(lazyGetCachePrefix));
                        Log.Message("[XenoPreview] Patched BigAndSmall.HumanoidPawnScaler.LazyGetCache.");
                    }
                    else
                    {
                        Log.Warning("[XenoPreview] Could not find BigAndSmall.HumanoidPawnScaler.LazyGetCache. Patch skipped.");
                    }
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
            }
            catch (Exception ex)
            {
                Log.Error("[XenoPreview] MOD LOAD: CRITICAL - Failed to apply Harmony patches: " + ex.ToString());
            }
        }
    }

    public static class Dialog_CreateXenotype_Patches
    {
        // Helper to ensure the preview window is open and correctly configured
        private static void EnsurePreviewWindowOpen(Window dialogInstance)
        {
            Log.Message($"[XenoPreview] EnsurePreviewWindowOpen called for dialog: {dialogInstance.GetType().Name}");
            if (XenoPreview.PreviewWindowInstance == null || !XenoPreview.PreviewWindowInstance.IsOpen)
            {
                XenoPreview.PreviewWindowInstance = new XenoPreviewWindow();
                Find.WindowStack.Add(XenoPreview.PreviewWindowInstance);
                Log.Message("[XenoPreview] New XenoPreviewWindow instance created and added to WindowStack.");
            }

            if (dialogInstance is Dialog_CreateXenotype xenotypeDialog)
            {
                XenoPreview.PreviewWindowInstance.SetDialog(xenotypeDialog);
                Log.Message("[XenoPreview] XenoPreviewWindow set for Dialog_CreateXenotype.");
            }
            else if (dialogInstance is Dialog_CreateXenogerm xenogermDialog)
            {
                XenoPreview.PreviewWindowInstance.SetXenogermDialog(xenogermDialog);
                Log.Message("[XenoPreview] XenoPreviewWindow set for Dialog_CreateXenogerm.");
            }
            XenoPreview.PreviewWindowInstance.UpdatePosition();
            Log.Message("[XenoPreview] XenoPreviewWindow position updated.");
        }

        public static bool GenerateXenotypeNameFromGenes_Prefix(ref string __result)
        {
            Log.Message("[XenoPreview] GenerateXenotypeNameFromGenes_Prefix called.");
            if (XenoPreview.PreviewWindowInstance != null && XenoPreview.PreviewWindowInstance.IsOpen)
            {
                __result = "PreviewXenotype"; // Provide a dummy name
                Log.Message("[XenoPreview] GenerateXenotypeNameFromGenes_Prefix: Skipping original method, returning 'PreviewXenotype'.");
                return false; // Skip original method
            }
            Log.Message("[XenoPreview] GenerateXenotypeNameFromGenes_Prefix: Executing original method.");
            return true; // Execute original method
        }

        // Postfix for GeneCreationDialogBase.DoWindowContents - works for both xenotype and xenogerm
        public static void DoWindowContents_Postfix(GeneCreationDialogBase __instance, Rect rect)
        {
            try
            {
                // Handle both Dialog_CreateXenotype and Dialog_CreateXenogerm
                if (__instance is Dialog_CreateXenotype || __instance is Dialog_CreateXenogerm)
                {
                    EnsurePreviewWindowOpen(__instance);
                }
            }
            catch (Exception ex)
            {
                Log.Error("[XenoPreview] Error in DoWindowContents_Postfix: " + ex.ToString());
            }
        }

        // Prefix for BetterPrerequisites.NotifyGenesChanges.Notify_GenesChanged_Postfix
        // This prevents NullReferenceExceptions when BigAndSmall tries to access UI elements
        // during XenoPreview's temporary pawn generation.
        public static bool NotifyGenesChanged_Postfix_Prefix(Pawn_GeneTracker __instance)
        {
            Log.Message($"[XenoPreview] NotifyGenesChanged_Postfix_Prefix called for pawn: {__instance?.pawn?.Name?.ToStringFull ?? "N/A"}, Xenotype: {__instance?.pawn?.genes?.Xenotype?.defName ?? "N/A"}");
            // Check if the pawn is a temporary preview pawn generated by XenoPreview
            if (__instance?.pawn?.genes?.Xenotype?.defName == "TempPreviewXenotype")
            {
                Log.Message("[XenoPreview] NotifyGenesChanged_Postfix_Prefix: Skipping original method for TempPreviewXenotype.");
                return false; // Skip the original postfix
            }
            Log.Message("[XenoPreview] NotifyGenesChanged_Postfix_Prefix: Executing original method.");
            return true; // Execute the original postfix
        }

        // Prefix for BigAndSmall.HumanoidPawnScaler.LazyGetCache
        // This prevents InvalidCastExceptions when accessing UI elements before they are initialized.
        public static bool LazyGetCache_Prefix(Pawn pawn, int scheduleForce)
        {
            Log.Message($"[XenoPreview] LazyGetCache_Prefix: Current.Game = {Current.Game != null}, Current.Game.World = {Current.Game?.World != null}, Find.UIRoot = {Find.UIRoot != null}, Find.TickManager.NotPlaying = {Find.TickManager?.NotPlaying}");

            if (Current.Game == null || Current.Game.World == null || Find.UIRoot == null || (Find.TickManager != null && Find.TickManager.NotPlaying))
            {
                Log.Message("[XenoPreview] LazyGetCache_Prefix: Skipping original method.");
                return false; // Skip original method if game, world, UIRoot, or main UI elements are not loaded, or game is not playing
            }
            Log.Message("[XenoPreview] LazyGetCache_Prefix: Executing original method.");
            return true; // Execute original method
        }

        // Postfix for Close
        public static void Close_Postfix(Window __instance)
        {
            try
            {
                // Check if it's Dialog_CreateXenotype or Dialog_CreateXenogerm
                if (!(__instance is Dialog_CreateXenotype) && !(__instance is Dialog_CreateXenogerm))
                {
                    return;
                }

                if (XenoPreview.PreviewWindowInstance != null && XenoPreview.PreviewWindowInstance.IsOpen)
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

        // Watch for windows being added to detect gene-related dialogs
        public static void WindowStack_Add_Postfix(Window window)
        {
            try
            {
                // Check for Dialog_CreateXenotype or Dialog_CreateXenogerm
                if (window is Dialog_CreateXenotype || window is Dialog_CreateXenogerm)
                {
                    EnsurePreviewWindowOpen(window);
                }
            }
            catch (Exception ex)
            {
                // Just log and continue
                Log.Error("[XenoPreview] Error in WindowStack_Add_Postfix: " + ex.ToString());
            }
        }
    }
}