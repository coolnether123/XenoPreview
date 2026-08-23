using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
#if XENO_FIXTURE_LUDEON_TK
using LudeonTK;
#endif
using RimWorld;
using UnityEngine;
using Verse;
using XenoPreviewRuntime = XenoPreview.XenoPreview;

namespace XenoPreview.TestFixture
{
    public static class XenoPreviewDebugActions
    {
#if XENO_FIXTURE_V1_4
        private const string FixtureVersion = "1.4";
#elif XENO_FIXTURE_V1_5
        private const string FixtureVersion = "1.5";
#elif XENO_FIXTURE_V1_6
        private const string FixtureVersion = "1.6";
#else
#error XenoPreview fixture must define exactly one XENO_FIXTURE_V1_* symbol.
#endif

        private const string Prefix = "[XenoPreview Fixture] ";
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

#if XENO_FIXTURE_V1_4
        [DebugAction("Open XenoPreview creator", "XenoPreview", actionType = DebugActionType.Action)]
#else
        [DebugAction("XenoPreview", "Open XenoPreview creator", actionType = DebugActionType.Action)]
#endif
        public static void OpenCreator()
        {
            if (Find.WindowStack.WindowOfType<Dialog_CreateXenotype>() != null)
            {
                Log.Message(Prefix + "open skipped: creator already open version=" + FixtureVersion);
                return;
            }

            Find.WindowStack.Add(new Dialog_CreateXenotype(-1, () =>
                Log.Message(Prefix + "creator callback invoked version=" + FixtureVersion)));
            Log.Message(Prefix + "creator opened version=" + FixtureVersion);
        }

#if XENO_FIXTURE_V1_4
        [DebugAction("Apply deterministic gene set", "XenoPreview", actionType = DebugActionType.Action)]
#else
        [DebugAction("XenoPreview", "Apply deterministic gene set", actionType = DebugActionType.Action)]
#endif
        public static void ApplyDeterministicGenes()
        {
            Dialog_CreateXenotype dialog = Find.WindowStack.WindowOfType<Dialog_CreateXenotype>();
            if (dialog == null || !dialog.IsOpen)
            {
                Log.Error(Prefix + "gene change failed: creator is not open version=" + FixtureVersion);
                return;
            }

            List<GeneDef> selectedGenes = ReadField<List<GeneDef>>(dialog, "selectedGenes");
            List<GeneDef> candidates = DefDatabase<GeneDef>.AllDefsListForReading
                .Where(HasNameSymbols)
                .OrderBy(gene => gene.defName, StringComparer.Ordinal)
                .Take(3)
                .ToList();
            if (candidates.Count == 0)
            {
                Log.Error(Prefix + "gene change failed: no GeneDef candidates version=" + FixtureVersion);
                return;
            }

            selectedGenes.Clear();
            selectedGenes.AddRange(candidates);
            Invoke(dialog, typeof(GeneCreationDialogBase), "OnGenesChanged");

            string generatedName = GeneUtility.GenerateXenotypeNameFromGenes(selectedGenes);
            if (string.IsNullOrWhiteSpace(generatedName)) generatedName = "XenoPreviewFixture";
            WriteField(dialog, "xenotypeName", generatedName);
            TickPreviewWindow();
            Log.Message(Prefix + "genes applied version=" + FixtureVersion +
                        " count=" + selectedGenes.Count +
                        " defs=" + string.Join(",", selectedGenes.Select(gene => gene.defName).ToArray()) +
                        " generatedName=" + generatedName);
            ReportPreviewState();
        }

#if XENO_FIXTURE_V1_4
        [DebugAction("Reroll gene set", "XenoPreview", actionType = DebugActionType.Action)]
#else
        [DebugAction("XenoPreview", "Reroll gene set", actionType = DebugActionType.Action)]
#endif
        public static void RerollGeneSet()
        {
            Dialog_CreateXenotype dialog = Find.WindowStack.WindowOfType<Dialog_CreateXenotype>();
            if (dialog == null || !dialog.IsOpen)
            {
                Log.Error(Prefix + "reroll failed: creator is not open version=" + FixtureVersion);
                return;
            }

            List<GeneDef> selectedGenes = ReadField<List<GeneDef>>(dialog, "selectedGenes");
            List<GeneDef> replacement = DefDatabase<GeneDef>.AllDefsListForReading
                .Where(HasNameSymbols)
                .OrderByDescending(gene => gene.defName, StringComparer.Ordinal)
                .Take(2)
                .ToList();
            selectedGenes.Clear();
            selectedGenes.AddRange(replacement);
            Invoke(dialog, typeof(GeneCreationDialogBase), "OnGenesChanged");
            string rerolledName = GeneUtility.GenerateXenotypeNameFromGenes(selectedGenes);
            if (string.IsNullOrWhiteSpace(rerolledName)) rerolledName = "XenoPreviewFixture";
            WriteField(dialog, "xenotypeName", rerolledName);
            TickPreviewWindow();
            Log.Message(Prefix + "gene reroll applied version=" + FixtureVersion +
                        " count=" + selectedGenes.Count +
                        " defs=" + string.Join(",", selectedGenes.Select(gene => gene.defName).ToArray()));
            ReportPreviewState();
        }

#if XENO_FIXTURE_V1_4
        [DebugAction("Set preview rotation east", "XenoPreview", actionType = DebugActionType.Action)]
#else
        [DebugAction("XenoPreview", "Set preview rotation east", actionType = DebugActionType.Action)]
#endif
        public static void SetRotationEast()
        {
            XenoPreviewWindow window = RequirePreview();
            if (window == null) return;
            WriteField(window, "femaleRotation", Rot4.East);
            WriteField(window, "maleRotation", Rot4.East);
            Log.Message(Prefix + "rotation=east version=" + FixtureVersion);
            ReportPreviewState();
        }

#if XENO_FIXTURE_V1_4
        [DebugAction("Hide clothes and tattoos", "XenoPreview", actionType = DebugActionType.Action)]
#else
        [DebugAction("XenoPreview", "Hide clothes and tattoos", actionType = DebugActionType.Action)]
#endif
        public static void HideClothesAndTattoos()
        {
            XenoPreviewWindow window = RequirePreview();
            if (window == null) return;
            WriteField(window, "femaleShowClothes", false);
            WriteField(window, "maleShowClothes", false);
            WriteField(window, "femaleShowTattoos", false);
            WriteField(window, "maleShowTattoos", false);
            Invoke(window, "UpdateClothingVisibility");
            Invoke(window, "UpdateTattooVisibility");
            Log.Message(Prefix + "appearance hidden ideology=" + ModsConfig.IdeologyActive +
                        " version=" + FixtureVersion);
            ReportPreviewState();
        }

#if XENO_FIXTURE_V1_4
        [DebugAction("Reroll appearance", "XenoPreview", actionType = DebugActionType.Action)]
#else
        [DebugAction("XenoPreview", "Reroll appearance", actionType = DebugActionType.Action)]
#endif
        public static void RerollAppearance()
        {
            XenoPreviewWindow window = RequirePreview();
            if (window == null) return;
            Invoke(window, "RerollClothing");
            if (ModsConfig.IdeologyActive)
            {
                Invoke(window, "RerollTattoos");
            }
            Log.Message(Prefix + "appearance rerolled ideology=" + ModsConfig.IdeologyActive +
                        " version=" + FixtureVersion);
            ReportPreviewState();
        }

#if XENO_FIXTURE_V1_4
        [DebugAction("Minimize preview", "XenoPreview", actionType = DebugActionType.Action)]
#else
        [DebugAction("XenoPreview", "Minimize preview", actionType = DebugActionType.Action)]
#endif
        public static void MinimizePreview()
        {
            XenoPreviewWindow window = RequirePreview();
            if (window == null) return;
            WriteStaticField(typeof(XenoPreviewWindow), "isMinimized", true);
            window.UpdatePosition();
            Log.Message(Prefix + "preview minimized version=" + FixtureVersion +
                        " initialSize=" + window.InitialSize);
        }

#if XENO_FIXTURE_V1_4
        [DebugAction("Restore preview", "XenoPreview", actionType = DebugActionType.Action)]
#else
        [DebugAction("XenoPreview", "Restore preview", actionType = DebugActionType.Action)]
#endif
        public static void RestorePreview()
        {
            XenoPreviewWindow window = RequirePreview();
            if (window == null) return;
            WriteStaticField(typeof(XenoPreviewWindow), "isMinimized", false);
            window.UpdatePosition();
            Log.Message(Prefix + "preview restored version=" + FixtureVersion +
                        " initialSize=" + window.InitialSize);
        }

#if XENO_FIXTURE_V1_4
        [DebugAction("Report preview state", "XenoPreview", actionType = DebugActionType.Action)]
#else
        [DebugAction("XenoPreview", "Report preview state", actionType = DebugActionType.Action)]
#endif
        public static void ReportPreviewState()
        {
            XenoPreviewWindow window = XenoPreviewRuntime.PreviewWindowInstance;
            if (window == null)
            {
                Log.Message(Prefix + "previewState=open:false version=" + FixtureVersion);
                return;
            }

            Pawn female = ReadField<Pawn>(window, "femalePawn");
            Pawn male = ReadField<Pawn>(window, "malePawn");
            Dialog_CreateXenotype dialog = Find.WindowStack.WindowOfType<Dialog_CreateXenotype>();
            List<GeneDef> selectedGenes = dialog == null
                ? null
                : ReadField<List<GeneDef>>(dialog, "selectedGenes");
            string xenotypeName = dialog == null ? null : ReadField<string>(dialog, "xenotypeName");
            bool minimized = ReadStaticField<bool>(typeof(XenoPreviewWindow), "isMinimized");
            Log.Message(Prefix + "previewState=open:" + window.IsOpen.ToString().ToLowerInvariant() +
                        " minimized:" + minimized.ToString().ToLowerInvariant() +
                        " femalePawn:" + (female?.thingIDNumber.ToString() ?? "none") +
                        " malePawn:" + (male?.thingIDNumber.ToString() ?? "none") +
                        " dialogGenes:" + (selectedGenes?.Count.ToString() ?? "none") +
                        " previewLastGeneCount:" + ReadField<int>(window, "lastGeneCount") +
                        " femalePawnGenes:" + (female?.genes?.GenesListForReading.Count.ToString() ?? "none") +
                        " malePawnGenes:" + (male?.genes?.GenesListForReading.Count.ToString() ?? "none") +
                        " xenotypeName:" + (xenotypeName ?? "none") +
                        " femaleRotation:" + ReadField<Rot4>(window, "femaleRotation") +
                        " maleRotation:" + ReadField<Rot4>(window, "maleRotation") +
                        " windowRect:" + window.windowRect +
                        " ideology:" + ModsConfig.IdeologyActive.ToString().ToLowerInvariant() +
                        " version=" + FixtureVersion);
        }

#if XENO_FIXTURE_V1_4
        [DebugAction("Close and verify cleanup", "XenoPreview", actionType = DebugActionType.Action)]
#else
        [DebugAction("XenoPreview", "Close and verify cleanup", actionType = DebugActionType.Action)]
#endif
        public static void CloseAndVerifyCleanup()
        {
            Dialog_CreateXenotype dialog = Find.WindowStack.WindowOfType<Dialog_CreateXenotype>();
            if (dialog != null && dialog.IsOpen)
            {
                dialog.Close(false);
            }

            XenoPreviewWindow window = XenoPreviewRuntime.PreviewWindowInstance;
            Pawn female = window == null ? null : ReadField<Pawn>(window, "femalePawn");
            Pawn male = window == null ? null : ReadField<Pawn>(window, "malePawn");
            Log.Message(Prefix + "cleanup dialogOpen=" + (dialog != null && dialog.IsOpen) +
                        " previewOpen=" + (window != null && window.IsOpen) +
                        " femalePawn=" + (female?.thingIDNumber.ToString() ?? "none") +
                        " malePawn=" + (male?.thingIDNumber.ToString() ?? "none") +
                        " version=" + FixtureVersion);
        }

        private static XenoPreviewWindow RequirePreview()
        {
            XenoPreviewWindow window = XenoPreviewRuntime.PreviewWindowInstance;
            if (window == null || !window.IsOpen)
            {
                Log.Error(Prefix + "preview is not open version=" + FixtureVersion);
                return null;
            }

            return window;
        }

        private static void TickPreviewWindow()
        {
            XenoPreviewWindow window = XenoPreviewRuntime.PreviewWindowInstance;
            if (window == null || !window.IsOpen) return;
            for (int i = 0; i < 20; i++)
            {
                window.WindowUpdate();
            }
        }

        private static bool HasNameSymbols(GeneDef gene)
        {
            if (gene == null || gene.symbolPack == null) return false;
            foreach (string fieldName in new[] { "wholeNameSymbols", "prefixSymbols", "suffixSymbols" })
            {
                FieldInfo field = gene.symbolPack.GetType().GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                ICollection symbols = field?.GetValue(gene.symbolPack) as ICollection;
                if (symbols != null && symbols.Count > 0) return true;
            }

            return false;
        }

        private static void Invoke(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, InstancePrivate);
            if (method == null)
            {
                method = typeof(XenoPreviewWindow).GetMethod(methodName, InstancePrivate);
            }

            if (method == null)
            {
                throw new MissingMethodException(target.GetType().FullName, methodName);
            }

            method.Invoke(target, null);
        }

        private static void Invoke(object target, Type declaringType, string methodName)
        {
            MethodInfo method = declaringType.GetMethod(methodName, InstancePrivate);
            if (method == null)
            {
                throw new MissingMethodException(declaringType.FullName, methodName);
            }

            method.Invoke(target, null);
        }

        private static T ReadField<T>(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, InstancePrivate);
            if (field == null)
            {
                field = typeof(XenoPreviewWindow).GetField(name, InstancePrivate);
            }

            if (field == null) throw new MissingFieldException(target.GetType().FullName, name);
            return (T)field.GetValue(target);
        }

        private static void WriteField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, InstancePrivate);
            if (field == null) field = typeof(XenoPreviewWindow).GetField(name, InstancePrivate);
            if (field == null) throw new MissingFieldException(target.GetType().FullName, name);
            field.SetValue(target, value);
        }

        private static T ReadStaticField<T>(Type type, string name)
        {
            FieldInfo field = type.GetField(name, StaticPrivate);
            if (field == null) throw new MissingFieldException(type.FullName, name);
            return (T)field.GetValue(null);
        }

        private static void WriteStaticField(Type type, string name, object value)
        {
            FieldInfo field = type.GetField(name, StaticPrivate);
            if (field == null) throw new MissingFieldException(type.FullName, name);
            field.SetValue(null, value);
        }
    }

#if XENO_FIXTURE_V1_4
    // RimWorld 1.4 does not expose this assembly's DebugAction metadata to the
    // runtime registry.  Keep the same public test seam, but drive it from an
    // isolated game component so the real in-game windows and APIs are tested.
    public sealed class XenoPreviewFixtureGameComponent : GameComponent
    {
        private int phase;
        private int guiFrames;

        public XenoPreviewFixtureGameComponent(Game game)
        {
        }

        public override void GameComponentTick()
        {
        }

        public override void GameComponentOnGUI()
        {
            if (Find.CurrentMap == null || ++guiFrames < 30)
            {
                return;
            }

            guiFrames = 0;
            switch (phase)
            {
                case 0:
                    XenoPreviewDebugActions.OpenCreator();
                    phase++;
                    break;
                case 1:
                    XenoPreviewDebugActions.ApplyDeterministicGenes();
                    phase++;
                    break;
                case 2:
                    XenoPreviewDebugActions.RerollGeneSet();
                    XenoPreviewDebugActions.SetRotationEast();
                    XenoPreviewDebugActions.HideClothesAndTattoos();
                    XenoPreviewDebugActions.RerollAppearance();
                    phase++;
                    break;
                case 3:
                    XenoPreviewDebugActions.MinimizePreview();
                    phase++;
                    break;
                case 4:
                    XenoPreviewDebugActions.RestorePreview();
                    XenoPreviewDebugActions.ReportPreviewState();
                    phase++;
                    break;
                case 5:
                    XenoPreviewDebugActions.CloseAndVerifyCleanup();
                    phase++;
                    break;
                case 6:
                    XenoPreviewDebugActions.OpenCreator();
                    phase++;
                    break;
                case 7:
                    XenoPreviewDebugActions.ApplyDeterministicGenes();
                    XenoPreviewDebugActions.CloseAndVerifyCleanup();
                    phase = 8;
                    Log.Message("[XenoPreview Fixture] repeated opening complete version=1.4");
                    break;
            }
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref phase, "xenoPreviewFixturePhase", 0);
        }
    }
#endif
}
