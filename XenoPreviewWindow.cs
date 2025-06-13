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

        private Pawn femalePawn;
        private Pawn malePawn;
        private bool femaleLocked;
        private bool maleLocked;

        private Rot4 femaleRotation = Rot4.South;
        private Rot4 maleRotation = Rot4.South;

        private Color femaleNaturalHairColor;
        private Color maleNaturalHairColor;

        private int lastGeneCount = -1;
        private bool needsPawnUpdate = true;

        private int updateTicks;
        private const int UPDATE_INTERVAL = 15;

        private bool isMinimized = false;
        private static readonly Vector2 WindowSize = new Vector2(320f, 500f); // Increased size

        // Clothing and tattoo visibility
        private bool showClothing = true;
        private bool showTattoos = true;
        private static bool IdeologyActive => ModsConfig.IdeologyActive;

        // Button size constants
        private const float BUTTON_WIDTH = 120f;
        private const float BUTTON_HEIGHT = 30f;
        private const float SMALL_BUTTON_WIDTH = 60f;
        private static readonly Vector2 MinimizedSize = new Vector2(180f, 60f);
        #endregion

        #region Constructors
        public override Vector2 InitialSize => isMinimized ? MinimizedSize : WindowSize;

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

            Vector2 currentSize = isMinimized ? MinimizedSize : WindowSize;

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
                Mathf.Min(x, UI.screenWidth - currentSize.x),
                Mathf.Clamp(y, 0f, UI.screenHeight - currentSize.y),
                currentSize.x,
                currentSize.y
            );
        }

        private bool CanGeneratePawns()
        {
            return Current.Game != null && Current.Game.World != null;
        }
        #endregion

        #region RimWorld callbacks
        public override void DoWindowContents(Rect inRect)
        {
            if (
                (xenotypeDialog == null || !xenotypeDialog.IsOpen)
                && (xenogermDialog == null || !xenogermDialog.IsOpen)
            )
            {
                Close(false);
                return;
            }

            if (isMinimized)
            {
                DrawMinimizedWindow(inRect);
                return;
            }

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

            if (needsPawnUpdate && CanGeneratePawns())
            {
                GenerateOrRefreshPawns(currentGenes);
                needsPawnUpdate = false;
            }

            DrawLayout(inRect, currentGenes);
        }

        private void DrawMinimizedWindow(Rect inRect)
        {
            Rect buttonRect = new Rect(
                (inRect.width - BUTTON_WIDTH) / 2f,
                (inRect.height - BUTTON_HEIGHT) / 2f,
                BUTTON_WIDTH,
                BUTTON_HEIGHT
            );

            GUI.color = new Color(0.3f, 0.5f, 0.7f, 0.9f);
            GUI.DrawTexture(buttonRect, BaseContent.WhiteTex);
            GUI.color = Color.white;
            Widgets.DrawHighlight(buttonRect);

            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Small;

            if (Widgets.ButtonText(buttonRect, "Show Preview", true, true, Color.white))
            {
                isMinimized = false;
                UpdatePosition();
            }
            Text.Anchor = TextAnchor.UpperLeft;
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();

            if (
                (xenotypeDialog == null || !xenotypeDialog.IsOpen)
                && (xenogermDialog != null && !xenogermDialog.IsOpen)
                && IsOpen
            )
            {
                Close(false);
                return;
            }

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
            const float rotateButtonW = 30f;
            const float gap = 5f;

            float currentY = 5f;

            // Hide button in top left corner
            Rect hideButton = new Rect(5f, currentY, BUTTON_WIDTH, BUTTON_HEIGHT);
            if (Widgets.ButtonText(hideButton, "Hide Preview"))
            {
                isMinimized = true;
                UpdatePosition();
            }
            currentY += BUTTON_HEIGHT + gap;

            // Portrait area
            float portraitH = 200f; // Fixed height for better control
            float portraitW = (inRect.width - gap) / 2f;

            Rect femRect = new Rect(0, currentY, portraitW, portraitH);
            Rect maleRect = new Rect(portraitW + gap, currentY, portraitW, portraitH);

            DrawPawnPortrait(femalePawn, femRect, femaleRotation);
            DrawPawnPortrait(malePawn, maleRect, maleRotation);

            // Rotation buttons
            Rect femRotateRect = new Rect(
                femRect.x + femRect.width - rotateButtonW,
                femRect.y + 5f,
                rotateButtonW,
                buttonH
            );
            Rect maleRotateRect = new Rect(
                maleRect.x + maleRect.width - rotateButtonW,
                maleRect.y + 5f,
                rotateButtonW,
                buttonH
            );

            if (Widgets.ButtonText(femRotateRect, "↻"))
            {
                femaleRotation = femaleRotation.Rotated(RotationDirection.Clockwise);
            }

            if (Widgets.ButtonText(maleRotateRect, "↻"))
            {
                maleRotation = maleRotation.Rotated(RotationDirection.Clockwise);
            }

            currentY += portraitH + gap;

            // Gender labels
            Rect femLabel = new Rect(femRect.x, currentY, femRect.width, labelH);
            Rect maleLabel = new Rect(maleRect.x, currentY, maleRect.width, labelH);

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(femLabel, "Female");
            Widgets.Label(maleLabel, "Male");
            Text.Anchor = TextAnchor.UpperLeft;

            currentY += labelH + gap;

            // Lock buttons
            Rect femLock = new Rect(femLabel.x, currentY, femLabel.width, buttonH);
            Rect maleLock = new Rect(maleLabel.x, currentY, maleLabel.width, buttonH);

            if (Widgets.ButtonText(femLock, femaleLocked ? "Locked" : "Unlocked"))
            {
                femaleLocked = !femaleLocked;
                if (!femaleLocked && activeGenes != null)
                    needsPawnUpdate = true;
            }
            if (Widgets.ButtonText(maleLock, maleLocked ? "Locked" : "Unlocked"))
            {
                maleLocked = !maleLocked;
                if (!maleLocked && activeGenes != null)
                    needsPawnUpdate = true;
            }

            currentY += buttonH + gap;

            // Reroll button
            Rect reroll = new Rect(
                (inRect.width - 90f) / 2f,
                currentY,
                90f,
                buttonH
            );
            if (
                Widgets.ButtonText(reroll, "Reroll")
                && (!femaleLocked || !maleLocked)
            )
                GenerateOrRefreshPawns(activeGenes);

            currentY += buttonH + gap;

            // Clothing controls
            float buttonGroupWidth = inRect.width - 10f;
            float clothingButtonWidth = buttonGroupWidth * 0.6f;
            float rerollClothingWidth = buttonGroupWidth * 0.35f;

            Rect clothingToggle = new Rect(5f, currentY, clothingButtonWidth, buttonH);
            Rect rerollClothing = new Rect(
                clothingToggle.xMax + 5f,
                currentY,
                rerollClothingWidth,
                buttonH
            );

            if (Widgets.ButtonText(clothingToggle, showClothing ? "Hide Clothing" : "Show Clothing"))
            {
                showClothing = !showClothing;
                UpdateClothingVisibility();
            }

            if (Widgets.ButtonText(rerollClothing, "Reroll Clothes"))
            {
                RerollClothing();
            }

            currentY += buttonH + gap;

            // Tattoo controls (only if Ideology is active)
            if (IdeologyActive)
            {
                Rect tattooToggle = new Rect(5f, currentY, clothingButtonWidth, buttonH);
                Rect rerollTattoo = new Rect(
                    tattooToggle.xMax + 5f,
                    currentY,
                    rerollClothingWidth,
                    buttonH
                );

                if (
                    Widgets.ButtonText(
                        tattooToggle,
                        showTattoos ? "Hide Tattoos" : "Show Tattoos"
                    )
                )
                {
                    showTattoos = !showTattoos;
                    UpdateTattooVisibility();
                }

                if (Widgets.ButtonText(rerollTattoo, "Reroll Tattoos"))
                {
                    RerollTattoos();
                }
            }
        }

        private void DrawPawnPortrait(Pawn pawn, Rect rect, Rot4 rotation)
        {
            if (!CanGeneratePawns())
            {
                Root_Play.SetupForQuickTestPlay();
            }

            if (pawn != null)
            {
                try
                {
                    var tex = PortraitsCache.Get(pawn, rect.size, rotation, Vector3.zero);
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

        #region Clothing and Tattoo Management
        private void UpdateClothingVisibility()
        {
            if (femalePawn != null)
            {
                ApplyClothingVisibility(femalePawn);
            }
            if (malePawn != null)
            {
                ApplyClothingVisibility(malePawn);
            }
        }

        private void ApplyClothingVisibility(Pawn pawn)
        {
            if (pawn?.apparel?.WornApparel == null)
                return;

            if (!showClothing)
            {
                // Remove all apparel
                var apparelList = pawn.apparel.WornApparel.ToList();
                foreach (var apparel in apparelList)
                {
                    pawn.apparel.Remove(apparel);
                }
            }
            else
            {
                // If clothing was hidden and now we want to show it, regenerate
                if (pawn.apparel.WornApparel.Count == 0)
                {
                    GenerateApparelForPawn(pawn);
                }
            }

            PortraitsCache.SetDirty(pawn);
        }

        private void UpdateTattooVisibility()
        {
            if (!IdeologyActive)
                return;

            if (femalePawn != null)
            {
                ApplyTattooVisibility(femalePawn);
            }
            if (malePawn != null)
            {
                ApplyTattooVisibility(malePawn);
            }
        }

        private void ApplyTattooVisibility(Pawn pawn)
        {
            if (!IdeologyActive || pawn?.style == null)
                return;

            try
            {
                if (!showTattoos)
                {
                    // Clear tattoos
                    pawn.style.FaceTattoo = TattooDefOf.NoTattoo_Face;
                    pawn.style.BodyTattoo = TattooDefOf.NoTattoo_Body;
                }
                else
                {
                    // If tattoos were hidden and now we want to show them, regenerate
                    if (
                        pawn.style.FaceTattoo == TattooDefOf.NoTattoo_Face
                        && pawn.style.BodyTattoo == TattooDefOf.NoTattoo_Body
                    )
                    {
                        GenerateTattoosForPawn(pawn);
                    }
                }

                PortraitsCache.SetDirty(pawn);
            }
            catch (Exception ex)
            {
                Log.Warning($"[XenoPreview] Error applying tattoo visibility: {ex.Message}");
            }
        }

        private void RerollClothing()
        {
            if (femalePawn != null && showClothing)
            {
                GenerateApparelForPawn(femalePawn);
            }
            if (malePawn != null && showClothing)
            {
                GenerateApparelForPawn(malePawn);
            }
        }

        private void RerollTattoos()
        {
            if (!IdeologyActive)
                return;

            if (femalePawn != null && showTattoos)
            {
                GenerateTattoosForPawn(femalePawn);
            }
            if (malePawn != null && showTattoos)
            {
                GenerateTattoosForPawn(malePawn);
            }
        }

        private void GenerateApparelForPawn(Pawn pawn)
        {
            try
            {
                // Clear existing apparel
                var existingApparel = pawn.apparel.WornApparel.ToList();
                foreach (var apparel in existingApparel)
                {
                    pawn.apparel.Remove(apparel);
                }

                // Generate new apparel
                PawnApparelGenerator.GenerateStartingApparelFor(pawn, new PawnGenerationRequest());

                PortraitsCache.SetDirty(pawn);
            }
            catch (Exception ex)
            {
                Log.Warning($"[XenoPreview] Error generating apparel: {ex.Message}");
            }
        }

        private void GenerateTattoosForPawn(Pawn pawn)
        {
            if (!IdeologyActive || pawn?.style == null)
                return;

            try
            {
                // Use the game's style generation system
                var availableFaceTattoos = DefDatabase<TattooDef>
                    .AllDefs.Where(x => x.tattooType == TattooType.Face)
                    .ToList();
                var availableBodyTattoos = DefDatabase<TattooDef>
                    .AllDefs.Where(x => x.tattooType == TattooType.Body)
                    .ToList();

                if (availableFaceTattoos.Any())
                {
                    pawn.style.FaceTattoo = availableFaceTattoos.RandomElement();
                }

                if (availableBodyTattoos.Any())
                {
                    pawn.style.BodyTattoo = availableBodyTattoos.RandomElement();
                }

                PortraitsCache.SetDirty(pawn);
            }
            catch (Exception ex)
            {
                Log.Warning($"[XenoPreview] Error generating tattoos: {ex.Message}");
            }
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
                    genes = Traverse
                        .Create(xenotypeDialog)
                        .Field("selectedGenes")
                        .GetValue<List<GeneDef>>();
                else if (xenogermDialog != null)
                {
                    genes = new List<GeneDef>();
                    var packs = Traverse
                        .Create(xenogermDialog)
                        .Field("selectedGenepacks")
                        .GetValue<List<Genepack>>();
                    if (packs != null)
                        foreach (var gp in packs)
                            if (gp?.GeneSet?.GenesListForReading != null)
                                genes.AddRange(gp.GeneSet.GenesListForReading);
                }
                if (genes != null)
                    count = genes.Count;
            }
            catch (Exception ex)
            {
                Log.Warning($"[XenoPreview] Could not fetch genes: {ex.Message}");
            }
            return genes;
        }

        private void GenerateOrRefreshPawns(
            List<GeneDef> genes,
            bool forceNewUnlocked = false
        )
        {
            if (genes == null || genes.Count == 0 || !CanGeneratePawns())
            {
                Cleanup();
                return;
            }

            var tmpXeno = new CustomXenotype
            {
                genes = new List<GeneDef>(genes),
                name = "TempPreviewXenotype"
            };

            if (!femaleLocked || forceNewUnlocked)
            {
                if (!femaleLocked)
                    DestroyPawn(ref femalePawn);
                femalePawn = GeneratePawn(Gender.Female, tmpXeno);
                ApplyClothingVisibility(femalePawn);
                if (IdeologyActive)
                    ApplyTattooVisibility(femalePawn);
            }
            else
            {
                UpdatePreviewPawnGenes(femalePawn, genes);
            }

            if (!maleLocked || forceNewUnlocked)
            {
                if (!maleLocked)
                    DestroyPawn(ref malePawn);
                malePawn = GeneratePawn(Gender.Male, tmpXeno);
                ApplyClothingVisibility(malePawn);
                if (IdeologyActive)
                    ApplyTattooVisibility(malePawn);
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
                var request = new PawnGenerationRequest(
                    PawnKindDefOf.Colonist,
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

                if (gender == Gender.Female)
                    femaleNaturalHairColor = p.story.HairColor;
                else
                    maleNaturalHairColor = p.story.HairColor;

                p.needs?.AllNeeds.Clear();
                if (p.mindState != null)
                    p.mindState.Active = false;
                PortraitsCache.SetDirty(p);
                return p;
            }
            catch (Exception ex)
            {
                Log.Error($"[XenoPreview] Pawn generation failed: {ex}");
                return null;
            }
        }

        private void UpdatePreviewPawnGenes(Pawn pawn, List<GeneDef> selectedGenes)
        {
            if (pawn == null || selectedGenes == null)
                return;

            // remove deselected
            var toRemove = pawn.genes.GenesListForReading
                .Where(g => !selectedGenes.Contains(g.def))
                .ToList();
            foreach (var g in toRemove)
                pawn.genes.RemoveGene(g);

            // add new
            foreach (var def in selectedGenes)
                if (!pawn.genes.GenesListForReading.Any(g => g.def == def))
                    pawn.genes.AddGene(def, true);

            // restore hair color if needed
            bool hairGene = selectedGenes.Any(d => d.hairColorOverride.HasValue);
            if (!hairGene)
                pawn.story.HairColor =
                    pawn.gender == Gender.Female
                        ? femaleNaturalHairColor
                        : maleNaturalHairColor;

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
            if (!femaleLocked)
                DestroyPawn(ref femalePawn);
            if (!maleLocked)
                DestroyPawn(ref malePawn);
        }
        #endregion
    }
}