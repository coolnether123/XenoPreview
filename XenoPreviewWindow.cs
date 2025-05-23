using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace XenoPreview
{
    public class XenoPreviewWindow : Window
    {
        #region Fields and constants
        private Dialog_CreateXenotype xenotypeDialog;
        private Dialog_CreateXenogerm xenogermDialog;

        // Track whether we spun up a dummy world for this window instance.
        private bool ownsDummyWorld;

        private Pawn femalePawn;
        private Pawn malePawn;
        private bool femaleLocked;
        private bool maleLocked;

        private Color femaleNaturalHairColor;
        private Color maleNaturalHairColor;

        private int lastGeneCount = -1;
        private bool needsPawnUpdate = true;

        private int updateTicks;
        private const int UPDATE_INTERVAL = 15;

        private static readonly Vector2 WindowSize = new Vector2(280f, 330f);
        #endregion

        #region Constructors
        public override Vector2 InitialSize => WindowSize;

        private static readonly HashSet<string> _loggedMessages = new HashSet<string>();

        private static void MessageOnce(string text)
        {
            if (_loggedMessages.Add(text))
                Log.Message(text);
        }

        public XenoPreviewWindow() : base()
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
        #endregion

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

            if (!ok) return;

            windowRect = new Rect(
                Mathf.Min(x, UI.screenWidth - WindowSize.x),
                Mathf.Clamp(y, 0f, UI.screenHeight - WindowSize.y),
                WindowSize.x,
                WindowSize.y
            );
        }
        #endregion

        #region RimWorld callbacks
        public override void DoWindowContents(Rect inRect)
        {
            // Ensure a dummy world and game environment are set up if none exists.
            if (!ownsDummyWorld)
                ownsDummyWorld = DummyWorldManager.EnsureDummyWorld();

            // Auto-close logic if associated dialogs are gone
            if ((xenotypeDialog == null || !xenotypeDialog.IsOpen)
             && (xenogermDialog == null || !xenogermDialog.IsOpen))
            {
                Close(false);
                return;
            }

            // Detect gene changes and trigger pawn updates
            int count;
            var currentGenes = TryGetCurrentGenes(out count);

            if (count != lastGeneCount)
            {
                if (femaleLocked && femalePawn != null)
                    UpdatePreviewPawnGenes(femalePawn, currentGenes);
                else
                    needsPawnUpdate = true;

                if (maleLocked && malePawn != null)
                    UpdatePreviewPawnGenes(malePawn, currentGenes);
                else
                    needsPawnUpdate = true;

                lastGeneCount = count;
            }

            // Regenerate or refresh pawns if needed
            if (needsPawnUpdate)
            {
                GenerateOrRefreshPawns(currentGenes);
                needsPawnUpdate = false;
            }

            // Draw the UI layout including pawn portraits
            DrawLayout(inRect, currentGenes);
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();

            // Auto-close if dialog went away
            if ((xenotypeDialog == null || !xenotypeDialog.IsOpen)
             && (xenogermDialog == null || !xenogermDialog.IsOpen)
             && IsOpen)
            {
                Close(false);
                return;
            }

            // Throttle gene-watch checks to avoid excessive CPU usage
            if (++updateTicks >= UPDATE_INTERVAL)
            {
                updateTicks = 0;
                int cnt;
                TryGetCurrentGenes(out cnt);
                if (cnt != lastGeneCount)
                    needsPawnUpdate = true;
            }
        }

        public override void PreClose()
        {
            base.PreClose();

            // Tear down our dummy world if this window instance created it
            if (ownsDummyWorld)
                DummyWorldManager.Cleanup();

            // Restore original pawn cleanup logic
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
            const float gap = 5f;

            float portraitH = inRect.height - (labelH + buttonH + buttonH + 3 * gap);
            float portraitW = (inRect.width - gap) / 2f;

            Rect femRect = new Rect(0, 0, portraitW, portraitH);
            Rect maleRect = new Rect(portraitW + gap, 0, portraitW, portraitH);

            DrawPawnPortrait(femalePawn, femRect);
            DrawPawnPortrait(malePawn, maleRect);

            Rect femLabel = new Rect(femRect.x, femRect.yMax + gap, femRect.width, labelH);
            Rect maleLabel = new Rect(maleRect.x, maleRect.yMax + gap, maleRect.width, labelH);

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(femLabel, "Female");
            Widgets.Label(maleLabel, "Male");
            Text.Anchor = TextAnchor.UpperLeft;

            Rect femLock = new Rect(femLabel.x, femLabel.yMax + gap, femLabel.width, buttonH);
            Rect maleLock = new Rect(maleLabel.x, maleLabel.yMax + gap, maleLabel.width, buttonH);

            if (Widgets.ButtonText(femLock, femaleLocked ? "Locked" : "Unlocked"))
            {
                femaleLocked = !femaleLocked;
                if (!femaleLocked && activeGenes != null) needsPawnUpdate = true;
            }
            if (Widgets.ButtonText(maleLock, maleLocked ? "Locked" : "Unlocked"))
            {
                maleLocked = !maleLocked;
                if (!maleLocked && activeGenes != null) needsPawnUpdate = true;
            }

            Rect reroll = new Rect((inRect.width - 90f) / 2f, femLock.yMax + gap, 90f, buttonH);
            if (Widgets.ButtonText(reroll, "Reroll") && (!femaleLocked || !maleLocked))
                GenerateOrRefreshPawns(activeGenes);
        }

        private void DrawPawnPortrait(Pawn pawn, Rect rect)
        {
            if (pawn != null)
            {
                try
                {
                    var tex = PortraitsCache.Get(pawn, rect.size, Rot4.South, Vector3.zero);
                    Widgets.DrawTextureFitted(rect, tex, 1f);
                }
                catch (Exception ex)
                {
                    Log.Error($"[XenoPreview] Portrait draw error: {ex}");
                    DrawPlaceholder(rect, "Portrait error");
                }
            }
            else
            {
                DrawPlaceholder(rect, "Add genes to see preview");
            }
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
                    genes = Traverse.Create(xenotypeDialog).Field("selectedGenes").GetValue<List<GeneDef>>();
                else if (xenogermDialog != null)
                {
                    genes = new List<GeneDef>();
                    var packs = Traverse.Create(xenogermDialog).Field("selectedGenepacks").GetValue<List<Genepack>>();
                    if (packs != null)
                        foreach (var gp in packs)
                            if (gp?.GeneSet?.GenesListForReading != null)
                                genes.AddRange(gp.GeneSet.GenesListForReading);
                }
                if (genes != null) count = genes.Count;
            }
            catch (Exception ex)
            {
                Log.Warning($"[XenoPreview] Could not fetch genes: {ex.Message}");
            }
            return genes;
        }

        private void GenerateOrRefreshPawns(List<GeneDef> genes, bool forceNewUnlocked = false)
        {
            if (genes == null || genes.Count == 0)
            {
                Cleanup();
                return;
            }

            var tmpXeno = new CustomXenotype
            {
                genes = new List<GeneDef>(genes),
                name = "TempPreviewXenotype"
            };

            // Generate or update female pawn.
            if (!femaleLocked || forceNewUnlocked)
            {
                if (!femaleLocked) DestroyPawn(ref femalePawn);
                femalePawn = GeneratePawn(Gender.Female, tmpXeno);
            }
            else
            {
                UpdatePreviewPawnGenes(femalePawn, genes);
            }

            // Generate or update male pawn.
            if (!maleLocked || forceNewUnlocked)
            {
                if (!maleLocked) DestroyPawn(ref malePawn);
                malePawn = GeneratePawn(Gender.Male, tmpXeno);
            }
            else
            {
                UpdatePreviewPawnGenes(malePawn, genes);
            }
        }

        private Pawn GeneratePawn(Gender gender, CustomXenotype xeno)
        {
            try
            {
                MessageOnce("[XenoPreview DEBUG] Attempting pawn generation...");

                Faction faction = DummyWorldManager.GetPlayerFaction();

                if (faction == null)
                {
                    MessageOnce("[XenoPreview DEBUG] No faction available, attempting factionless generation...");

                    // Create a very basic request without faction
                    var basicRequest = new PawnGenerationRequest(
                        PawnKindDefOf.Colonist,
                        null, // No faction
                        PawnGenerationContext.NonPlayer,
                        forceGenerateNewPawn: true,
                        canGeneratePawnRelations: false,
                        colonistRelationChanceFactor: 0f,
                        fixedGender: gender,
                        fixedBiologicalAge: 25f,
                        fixedChronologicalAge: 25f,
                        forcedCustomXenotype: xeno,
                        forceNoIdeo: true,
                        forceNoBackstory: true,
                        forbidAnyTitle: true
                    );

                    var p = PawnGenerator.GeneratePawn(basicRequest);

                    if (p != null)
                    {
                        // Store natural hair color
                        if (p.story != null)
                        {
                            if (gender == Gender.Female)
                                femaleNaturalHairColor = p.story.HairColor;
                            else
                                maleNaturalHairColor = p.story.HairColor;
                        }

                        // Clean up the pawn for preview purposes
                        p.needs?.AllNeeds?.Clear();
                        if (p.mindState != null) p.mindState.Active = false;

                        // Clear any unwanted components that might cause issues
                        p.drafter = null;
                        p.playerSettings = null;

                        PortraitsCache.SetDirty(p);
                        MessageOnce("[XenoPreview DEBUG] Factionless pawn generated successfully.");
                    }

                    return p;
                }
                else
                {
                    // Use the faction-based approach
                    var request = new PawnGenerationRequest(
                        PawnKindDefOf.Colonist,
                        faction,
                        PawnGenerationContext.NonPlayer,
                        forceGenerateNewPawn: true,
                        canGeneratePawnRelations: false,
                        colonistRelationChanceFactor: 0f,
                        fixedGender: gender,
                        fixedBiologicalAge: 25f,
                        fixedChronologicalAge: 25f,
                        forcedCustomXenotype: xeno,
                        forceNoIdeo: true,
                        forceNoBackstory: true,
                        forbidAnyTitle: true
                    );

                    var p = PawnGenerator.GeneratePawn(request);

                    if (p?.story != null)
                    {
                        if (gender == Gender.Female)
                            femaleNaturalHairColor = p.story.HairColor;
                        else
                            maleNaturalHairColor = p.story.HairColor;
                    }

                    p?.needs?.AllNeeds?.Clear();
                    if (p?.mindState != null) p.mindState.Active = false;
                    p.drafter = null;
                    p.playerSettings = null;
                    PortraitsCache.SetDirty(p);

                    MessageOnce("[XenoPreview DEBUG] Faction-based pawn generated successfully.");
                    return p;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[XenoPreview] Pawn generation failed: {ex}");
                return null;
            }
        }

        private void UpdatePreviewPawnGenes(Pawn pawn, List<GeneDef> selectedGenes)
        {
            if (pawn == null || selectedGenes == null || pawn.genes == null) return;

            // Remove genes that are no longer selected.
            var toRemove = pawn.genes.GenesListForReading
                                  .Where(g => !selectedGenes.Contains(g.def))
                                  .ToList();
            foreach (var g in toRemove)
                pawn.genes.RemoveGene(g);

            // Add new genes that are now selected.
            foreach (var def in selectedGenes)
                if (!pawn.genes.GenesListForReading.Any(g => g.def == def))
                    pawn.genes.AddGene(def, true);

            // Restore hair color if no gene now overrides it.
            bool hairGene = selectedGenes.Any(d => d.hairColorOverride.HasValue);
            if (!hairGene && pawn.story != null)
            {
                pawn.story.HairColor = pawn.gender == Gender.Female
                    ? femaleNaturalHairColor
                    : maleNaturalHairColor;
            }

            PortraitsCache.SetDirty(pawn);
        }

        private void DestroyPawn(ref Pawn p)
        {
            if (p != null && !p.Destroyed)
                p.Destroy(DestroyMode.Vanish);
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