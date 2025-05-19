using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace XenoPreview
{
    public class XenoPreviewWindow : Window
    {
        private Dialog_CreateXenotype xenotypeDialog;
        private Dialog_CreateXenogerm xenogermDialog;

        private Pawn femalePawn;
        private Pawn malePawn;

        // Independent locking per‑gender
        private bool femaleLocked = false;
        private bool maleLocked = false;

        private int lastGeneCount = -1;
        private bool needsPawnUpdate = true;

        // Periodic refresh
        private int updateTicks = 0;
        private const int UPDATE_INTERVAL = 15;

        private static readonly Vector2 WindowSize = new Vector2(280f, 330f);
        public override Vector2 InitialSize => WindowSize;

        public XenoPreviewWindow()
        {
            closeOnCancel = closeOnAccept = closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
            draggable = false;
            resizeable = false;
            drawShadow = true;
            forcePause = preventCameraMotion = false;
            doCloseX = false;
            layer = WindowLayer.Super;
            soundAppear = soundClose = null;
        }

        #region Dialog plumbing
        public void SetDialog(Dialog_CreateXenotype dlg)
        {
            xenotypeDialog = dlg;
            xenogermDialog = null;
            UpdatePosition();
        }
        public void SetXenogermDialog(Dialog_CreateXenogerm dlg)
        {
            xenogermDialog = dlg;
            xenotypeDialog = null;
            UpdatePosition();
        }
        public void UpdatePosition()
        {
            float x = 0f, y = 0f;
            bool ok = false;

            if (xenotypeDialog != null && xenotypeDialog.IsOpen)
            {
                x = xenotypeDialog.windowRect.xMax + 10f;
                y = xenotypeDialog.windowRect.yMin;
                ok = true;
            }
            else if (xenogermDialog != null && xenogermDialog.IsOpen)
            {
                x = xenogermDialog.windowRect.xMax + 10f;
                y = xenogermDialog.windowRect.yMin;
                ok = true;
            }

            if (ok)
            {
                x = Mathf.Min(x, UI.screenWidth - WindowSize.x);
                y = Mathf.Clamp(y, 0f, UI.screenHeight - WindowSize.y);
                windowRect = new Rect(x, y, WindowSize.x, WindowSize.y);
            }
        }
        #endregion

        #region RimWorld callbacks
        public override void DoWindowContents(Rect inRect)
        {
            // Auto‑close if parent dialogs vanished
            if ((xenotypeDialog == null || !xenotypeDialog.IsOpen) &&
                (xenogermDialog == null || !xenogermDialog.IsOpen))
            {
                Close(false); return;
            }

            List<GeneDef> selectedGenes = TryGetCurrentGenes(out int currentGeneCount);

            // Detect gene‑list changes
            if (currentGeneCount != lastGeneCount)
            {
                if (femaleLocked && femalePawn != null) UpdatePreviewPawnGenes(femalePawn, selectedGenes);
                else needsPawnUpdate = true;

                if (maleLocked && malePawn != null) UpdatePreviewPawnGenes(malePawn, selectedGenes);
                else needsPawnUpdate = true;

                lastGeneCount = currentGeneCount;
            }

            if (needsPawnUpdate)
            {
                GenerateOrRefreshPawns(selectedGenes);
                needsPawnUpdate = false;
            }

            DrawLayout(inRect, selectedGenes);
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();

            // Close if main dialogs closed from elsewhere
            if ((xenotypeDialog == null || !xenotypeDialog.IsOpen) &&
                (xenogermDialog == null || !xenogermDialog.IsOpen) &&
                IsOpen)
            {
                Close(false); return;
            }

            // Periodic gene‑change check
            if (++updateTicks >= UPDATE_INTERVAL)
            {
                updateTicks = 0;
                List<GeneDef> genes = TryGetCurrentGenes(out int cnt);
                if (cnt != lastGeneCount) needsPawnUpdate = true;
            }
        }

        public override void PreClose()
        {
            base.PreClose();
            femaleLocked = maleLocked = false;
            Cleanup();
            if (XenoPreview.PreviewWindowInstance == this)
                XenoPreview.PreviewWindowInstance = null;
        }
        #endregion

        #region Drawing helpers
        private void DrawLayout(Rect inRect, List<GeneDef> activeGenes)
        {
            const float labelH = 20f;
            const float buttonH = 24f;
            const float verticalGap = 5f;
            const float halfGap = 5f;

            float usablePortraitH = inRect.height - (labelH + buttonH + buttonH + 3 * verticalGap);
            float portraitW = (inRect.width - halfGap) / 2;

            // ----- Portraits -----
            Rect femPortrait = new Rect(0f, 0f, portraitW, usablePortraitH);
            Rect malePortrait = new Rect(portraitW + halfGap, 0f, portraitW, usablePortraitH);

            DrawPawnPortrait(femalePawn, femPortrait);
            DrawPawnPortrait(malePawn, malePortrait);

            // ----- Gender labels -----
            Rect femLabel = new Rect(femPortrait.x, femPortrait.yMax, femPortrait.width, labelH);
            Rect maleLabel = new Rect(malePortrait.x, malePortrait.yMax, malePortrait.width, labelH);

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(femLabel, "Female");
            Widgets.Label(maleLabel, "Male");
            Text.Anchor = TextAnchor.UpperLeft;

            // ----- Lock buttons (per‑gender) -----
            Rect femLockRect = new Rect(femLabel.x, femLabel.yMax + verticalGap, femLabel.width, buttonH);
            Rect maleLockRect = new Rect(maleLabel.x, maleLabel.yMax + verticalGap, maleLabel.width, buttonH);

            if (Widgets.ButtonText(femLockRect, femaleLocked ? "Locked" : "Unlocked"))
            {
                femaleLocked = !femaleLocked;
                if (!femaleLocked && activeGenes != null) needsPawnUpdate = true;
            }
            if (Widgets.ButtonText(maleLockRect, maleLocked ? "Locked" : "Unlocked"))
            {
                maleLocked = !maleLocked;
                if (!maleLocked && activeGenes != null) needsPawnUpdate = true;
            }

            // ----- Reroll button (centered) -----
            float rerollY = femLockRect.yMax + verticalGap;
            float rerollW = 90f;
            Rect rerollRect = new Rect(inRect.width / 2 - rerollW / 2, rerollY, rerollW, buttonH);

            if (Widgets.ButtonText(rerollRect, "Reroll"))
            {
                // Only regenerate pawns that are **not** locked.
                if (!femaleLocked || !maleLocked)
                {
                    GenerateOrRefreshPawns(activeGenes); // default parameter -> forceNewUnlocked = false
                }
                else
                {
                    //Log.Message("[XenoPreview] Both pawns are locked – nothing to reroll.");
                }
            }

        }

        private void DrawPawnPortrait(Pawn pawn, Rect rect)
        {
            if (pawn != null)
            {
                try
                {
                    Texture tex = PortraitsCache.Get(pawn, rect.size, Rot4.South, Vector3.zero, 1f);
                    Widgets.DrawTextureFitted(rect, tex, 1f);
                }
                catch (Exception ex)
                {
                    Log.Error($"[XenoPreview] Portrait draw error: {ex}");
                    DrawPlaceholder(rect, "Portrait error");
                }
            }
            else
                DrawPlaceholder(rect, "Add genes to see preview");
        }

        private void DrawPlaceholder(Rect rect, string msg)
        {
            GUI.color = Color.gray;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, msg);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }
        #endregion

        #region Pawn generation / updates
        private List<GeneDef> TryGetCurrentGenes(out int count)
        {
            count = 0;
            List<GeneDef> genes = null;
            try
            {
                if (xenotypeDialog != null)
                {
                    genes = Traverse.Create(xenotypeDialog)
                                    .Field("selectedGenes")
                                    .GetValue<List<GeneDef>>();
                }
                else if (xenogermDialog != null)
                {
                    genes = new List<GeneDef>();
                    var packs = Traverse.Create(xenogermDialog)
                                        .Field("selectedGenepacks")
                                        .GetValue<List<Genepack>>();
                    if (packs != null)
                        foreach (var gp in packs)
                            if (gp?.GeneSet?.GenesListForReading != null)
                                genes.AddRange(gp.GeneSet.GenesListForReading);
                }
                if (genes != null) count = genes.Count;
            }
            catch (Exception ex) { Log.Warning($"[XenoPreview] Could not fetch genes: {ex.Message}"); }
            return genes;
        }

        private void GenerateOrRefreshPawns(List<GeneDef> genes, bool forceNewUnlocked = false)
        {
            if (genes == null || genes.Count == 0)
            {
                Cleanup(); return;
            }

            CustomXenotype tmpXeno = new CustomXenotype
            {
                genes = new List<GeneDef>(genes),
                name = "TempPreviewXenotype"
            };

            // Female
            if (!femaleLocked || forceNewUnlocked)
            {
                if (!femaleLocked) DestroyPawn(ref femalePawn);
                femalePawn = GeneratePawn(Gender.Female, tmpXeno);
            }
            else // locked but genes changed: update genes only
                UpdatePreviewPawnGenes(femalePawn, genes);

            // Male
            if (!maleLocked || forceNewUnlocked)
            {
                if (!maleLocked) DestroyPawn(ref malePawn);
                malePawn = GeneratePawn(Gender.Male, tmpXeno);
            }
            else
                UpdatePreviewPawnGenes(malePawn, genes);
        }

        private Pawn GeneratePawn(Gender gender, CustomXenotype xeno)
        {
            try
            {
                var req = new PawnGenerationRequest(
                    kind: PawnKindDefOf.Colonist,
                    faction: Faction.OfPlayer,
                    context: PawnGenerationContext.NonPlayer,
                    fixedGender: gender,
                    forceGenerateNewPawn: true,
                    allowDead: false,
                    allowDowned: false,
                    canGeneratePawnRelations: false,
                    mustBeCapableOfViolence: false,
                    colonistRelationChanceFactor: 0f,
                    forceAddFreeWarmLayerIfNeeded: false,
                    allowGay: true,
                    allowFood: false,
                    allowAddictions: false,
                    inhabitant: false,
                    certainlyBeenInCryptosleep: false,
                    forceRedressWorldPawnIfFormerColonist: false,
                    worldPawnFactionDoesntMatter: false,
                    biocodeWeaponChance: 0f,
                    biocodeApparelChance: 0f,
                    relationWithExtraPawnChanceFactor: 0f,
                    fixedBiologicalAge: 25f,
                    fixedChronologicalAge: 25f,
                    forcedXenotype: null,
                    forcedCustomXenotype: xeno,
                    forceNoIdeo: true,
                    forceNoBackstory: true,
                    forbidAnyTitle: true
                );
                var p = PawnGenerator.GeneratePawn(req);
                p.needs?.AllNeeds?.Clear();
                if (p.mindState != null) p.mindState.Active = false;
                PortraitsCache.SetDirty(p);
                return p;
            }
            catch (Exception ex)
            {
                Log.Error($"[XenoPreview] Pawn generation failed: {ex}");
                return null;
            }
        }

        private void UpdatePreviewPawnGenes(Pawn pawn, List<GeneDef> genes)
        {
            if (pawn == null || genes == null) return;
            try
            {
                pawn.genes?.GenesListForReading?.Clear();
                foreach (var g in genes)
                    pawn.genes?.AddGene(g, true);
                PortraitsCache.SetDirty(pawn);
            }
            catch (Exception ex)
            {
                Log.Error($"[XenoPreview] Gene update failed: {ex}");
                needsPawnUpdate = true;
            }
        }

        private void DestroyPawn(ref Pawn p)
        {
            if (p != null && !p.Destroyed) p.Destroy(DestroyMode.Vanish);
            p = null;
        }
        private void Cleanup()
        {
            if (!femaleLocked) DestroyPawn(ref femalePawn);
            if (!maleLocked) DestroyPawn(ref malePawn);
        }
        #endregion
    }
}
