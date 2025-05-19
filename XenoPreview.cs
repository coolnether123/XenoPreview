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
            //Log.Message("[XenoPreview] MOD LOAD: Static constructor called. Version 1.0.0");
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
                    //Log.Message("[XenoPreview] Successfully patched GeneCreationDialogBase.DoWindowContents");
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
                    //Log.Message("[XenoPreview] Successfully patched Window.Close");
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
                    //Log.Message("[XenoPreview] Successfully patched WindowStack.Add");
                }

                //Log.Message("[XenoPreview] MOD LOAD: Harmony patches applied successfully.");
            }
            catch (Exception ex)
            {
                Log.Error("[XenoPreview] MOD LOAD: CRITICAL - Failed to apply Harmony patches: " + ex.ToString());
            }
        }
    }

    public static class Dialog_CreateXenotype_Patches
    {
        // Postfix for GeneCreationDialogBase.DoWindowContents - works for both xenotype and xenogerm
        public static void DoWindowContents_Postfix(GeneCreationDialogBase __instance, Rect rect)
        {
            try
            {
                // Handle both Dialog_CreateXenotype and Dialog_CreateXenogerm
                if (__instance is Dialog_CreateXenotype xenotypeDialog)
                {
                    if (XenoPreview.PreviewWindowInstance == null || !XenoPreview.PreviewWindowInstance.IsOpen)
                    {
                        XenoPreview.PreviewWindowInstance = new XenoPreviewWindow();
                        Find.WindowStack.Add(XenoPreview.PreviewWindowInstance);
                    }

                    XenoPreview.PreviewWindowInstance.SetDialog(xenotypeDialog);
                    XenoPreview.PreviewWindowInstance.UpdatePosition();
                }
                else if (__instance is Dialog_CreateXenogerm xenogermDialog)
                {
                    if (XenoPreview.PreviewWindowInstance == null || !XenoPreview.PreviewWindowInstance.IsOpen)
                    {
                        XenoPreview.PreviewWindowInstance = new XenoPreviewWindow();
                        Find.WindowStack.Add(XenoPreview.PreviewWindowInstance);
                    }

                    XenoPreview.PreviewWindowInstance.SetXenogermDialog(xenogermDialog);
                    XenoPreview.PreviewWindowInstance.UpdatePosition();
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
                    //Log.Message("[XenoPreview] Preview window closed with main dialog.");
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
                // Check for Dialog_CreateXenotype
                if (window is Dialog_CreateXenotype dialogInstance)
                {
                    if (XenoPreview.PreviewWindowInstance == null || !XenoPreview.PreviewWindowInstance.IsOpen)
                    {
                        XenoPreview.PreviewWindowInstance = new XenoPreviewWindow();
                        XenoPreview.PreviewWindowInstance.SetDialog(dialogInstance);
                        Find.WindowStack.Add(XenoPreview.PreviewWindowInstance);
                    }
                    return;
                }

                // Check for Dialog_CreateXenogerm (gene assembler)
                if (window is Dialog_CreateXenogerm xenogermDialog)
                {
                    if (XenoPreview.PreviewWindowInstance == null || !XenoPreview.PreviewWindowInstance.IsOpen)
                    {
                        XenoPreview.PreviewWindowInstance = new XenoPreviewWindow();
                        XenoPreview.PreviewWindowInstance.SetXenogermDialog(xenogermDialog);
                        Find.WindowStack.Add(XenoPreview.PreviewWindowInstance);
                    }
                    return;
                }

                // Log gene-related windows for debugging
                string windowTitle = window.GetType().Name;
                if (windowTitle.Contains("Gene") || windowTitle.Contains("Xeno"))
                {
                    //Log.Message($"[XenoPreview] Found potential gene-related window: {windowTitle}");
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