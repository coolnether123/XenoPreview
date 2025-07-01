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
            if (XenoPreview.PreviewWindowInstance == null || !XenoPreview.PreviewWindowInstance.IsOpen)
            {
                XenoPreview.PreviewWindowInstance = new XenoPreviewWindow();
                Find.WindowStack.Add(XenoPreview.PreviewWindowInstance);
            }

            if (dialogInstance is Dialog_CreateXenotype xenotypeDialog)
            {
                XenoPreview.PreviewWindowInstance.SetDialog(xenotypeDialog);
            }
            else if (dialogInstance is Dialog_CreateXenogerm xenogermDialog)
            {
                XenoPreview.PreviewWindowInstance.SetXenogermDialog(xenogermDialog);
            }
            XenoPreview.PreviewWindowInstance.UpdatePosition();
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