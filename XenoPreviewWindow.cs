using HarmonyLib; // Assuming you are using Harmony for some parts of your mod, as per previous discussions.
using RimWorld;
using RimWorld.Planet; // Needed for MapParent, WorldGenerator, etc.
using System;
using System.Collections.Generic;
using System.Linq; // For LINQ queries like .Where(), .ToList(), .Any()
using UnityEngine; // For Rect, Vector2, Mathf, GUI, Color
using Verse; // Core RimWorld types like Window, Pawn, Log, Find, Current, DefDatabase, Widgets, Text, Rand

namespace XenoPreview
{
    public class XenoPreviewWindow : Window
    {
        #region Fields and constants
        private Dialog_CreateXenotype xenotypeDialog;
        private Dialog_CreateXenogerm xenogermDialog;

        // NEW: track whether we spun up a dummy world for this window instance.
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
        private const int UPDATE_INTERVAL = 15; // Original was 15, previous response changed to 10. Reverted to 15.

        private static readonly Vector2 WindowSize = new Vector2(280f, 330f);
        #endregion

        #region Constructors
        public override Vector2 InitialSize => WindowSize;

        public XenoPreviewWindow() : base()
        {
            closeOnCancel = closeOnAccept = closeOnClickedOutside = false;
            absorbInputAroundWindow = false; // Original code had this as 'false'
            draggable = false;               // Original code had this as 'false'
            resizeable = false;
            drawShadow = true;
            forcePause = preventCameraMotion = false; // Original code had forcePause as 'false'
            doCloseX = false;
            layer = WindowLayer.Super;
            soundAppear = soundClose = null;

            // Optional: If you had a specific title, add it here for clarity in debugging.
            // optionalTitle = "Xenotype Preview"; 

            // Your original code might have had UpdatePosition() here or elsewhere for initial placement.
            // UpdatePosition() will be called when SetDialog/SetXenogermDialog is called.

            // Log.Message("[XenoPreview] Window initialized."); // Your specific log message if needed.
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
            // --- NEW: Ensure a dummy world and game environment are set up if none exists. ---
            // This is called every frame, but DummyWorldManager.EnsureDummyWorld() is idempotent.
            if (!ownsDummyWorld)
                ownsDummyWorld = DummyWorldManager.EnsureDummyWorld();

            // --- Original auto-close logic if associated dialogs are gone ---
            if ((xenotypeDialog == null || !xenotypeDialog.IsOpen)
             && (xenogermDialog == null || !xenogermDialog.IsOpen))
            {
                Close(false);
                return;
            }

            // --- Detect gene changes and trigger pawn updates ---
            int count;
            // TryGetCurrentGenes() needs to be correctly implemented to retrieve genes from dialogs.
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

            // --- Regenerate or refresh pawns if needed ---
            if (needsPawnUpdate)
            {
                GenerateOrRefreshPawns(currentGenes);
                needsPawnUpdate = false;
            }

            // --- Draw the UI layout including pawn portraits ---
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
            base.PreClose(); // Call base method first

            // --- NEW: Tear down our dummy world if *this* window instance created it ---
            if (ownsDummyWorld)
                DummyWorldManager.Cleanup();

            // Restore original pawn cleanup logic
            femaleLocked = maleLocked = false; // Unlock pawns
            Cleanup(); // Call the pawn-specific cleanup method in XenoPreviewWindow
            if (XenoPreview.PreviewWindowInstance == this)
                XenoPreview.PreviewWindowInstance = null; // Clear static instance if this was it
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
        // This method retrieves the currently selected genes from the active gene dialogs.
        // It uses Harmony's Traverse to access private fields of Dialog_CreateXenotype/Xenogerm.
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

        // This method generates or refreshes the preview pawns based on the active genes.
        private void GenerateOrRefreshPawns(List<GeneDef> genes, bool forceNewUnlocked = false)
        {
            if (genes == null || genes.Count == 0)
            {
                Cleanup(); // Cleanup pawn references
                return;
            }

            // Create a temporary CustomXenotype to pass to PawnGenerationRequest.
            var tmpXeno = new CustomXenotype
            {
                genes = new List<GeneDef>(genes),
                name = "TempPreviewXenotype"
            };

            // Generate or update female pawn.
            if (!femaleLocked || forceNewUnlocked)
            {
                if (!femaleLocked) DestroyPawn(ref femalePawn); // Destroy existing pawn if not locked
                femalePawn = GeneratePawn(Gender.Female, tmpXeno);
            }
            else
            {
                // If locked, just update genes on the existing pawn.
                UpdatePreviewPawnGenes(femalePawn, genes);
            }

            // Generate or update male pawn.
            if (!maleLocked || forceNewUnlocked)
            {
                if (!maleLocked) DestroyPawn(ref malePawn); // Destroy existing pawn if not locked
                malePawn = GeneratePawn(Gender.Male, tmpXeno);
            }
            else
            {
                // If locked, just update genes on the existing pawn.
                UpdatePreviewPawnGenes(malePawn, genes);
            }
        }

        // This method generates a single pawn with the specified gender and xenotype.
        private Pawn GeneratePawn(Gender gender, CustomXenotype xeno)
        {
            try
            {
                // Create a PawnGenerationRequest. Crucially, Faction.OfPlayer must exist here.
                var request = new PawnGenerationRequest(
                    PawnKindDefOf.Colonist, // Using Colonist pawn kind, can be adjusted.
                    Faction.OfPlayer,       // Requires Faction.OfPlayer to be initialized by DummyWorldManager.
                    PawnGenerationContext.All, // Context for pawn generation (usually World or Map for title screen)
                    forceGenerateNewPawn: true,
                    canGeneratePawnRelations: false,
                    colonistRelationChanceFactor: 0f,
                    fixedGender: gender,
                    fixedBiologicalAge: 25f, // Fixed age for consistent preview
                    fixedChronologicalAge: 25f,
                    forcedCustomXenotype: xeno, // Apply the custom xenotype
                    forceNoIdeo: true,          // No ideology for preview pawns
                    forceNoBackstory: true,     // No backstory for preview pawns
                    forbidAnyTitle: true        // No titles
                );

                var p = PawnGenerator.GeneratePawn(request);

                // Store natural hair color if no gene overrides it.
                if (p.story != null) // Ensure story is not null
                {
                    if (gender == Gender.Female)
                        femaleNaturalHairColor = p.story.HairColor;
                    else
                        maleNaturalHairColor = p.story.HairColor;
                }

                p.needs?.AllNeeds.Clear(); // Clear needs for a static preview pawn.
                if (p.mindState != null) p.mindState.Active = false; // Deactivate mind state.
                PortraitsCache.SetDirty(p); // Mark portrait as dirty to re-render.
                return p;
            }
            catch (Exception ex)
            {
                Log.Error($"[XenoPreview] Pawn generation failed: {ex}");
                return null;
            }
        }

        // This method updates an existing pawn's genes without regenerating the whole pawn.
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
            if (!hairGene && pawn.story != null) // Ensure story is not null before accessing
            {
                pawn.story.HairColor = pawn.gender == Gender.Female
                    ? femaleNaturalHairColor
                    : maleNaturalHairColor;
            }

            PortraitsCache.SetDirty(pawn); // Mark portrait as dirty to reflect gene changes.
        }

        // Helper to safely destroy a pawn.
        private void DestroyPawn(ref Pawn p)
        {
            if (p != null && !p.Destroyed)
                p.Destroy(DestroyMode.Vanish); // Destroy the pawn.
            p = null; // Nullify the reference.
        }

        // General cleanup for preview pawns.
        private void Cleanup()
        {
            if (!femaleLocked) DestroyPawn(ref femalePawn);
            if (!maleLocked) DestroyPawn(ref malePawn);
        }
        #endregion
    }
}