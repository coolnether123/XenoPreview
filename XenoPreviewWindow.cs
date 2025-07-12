using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Noise;

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

        private bool femaleShowClothes = true;
        private bool maleShowClothes = true;
        private bool femaleShowTattoos = true;
        private bool maleShowTattoos = true;

        private Rot4 femaleRotation = Rot4.South;
        private Rot4 maleRotation = Rot4.South;

        private Color femaleNaturalHairColor;
        private Color maleNaturalHairColor;

        private int lastGeneCount = -1;
        private bool needsPawnUpdate = true;

        private int updateTicks;
        private const int UPDATE_INTERVAL = 15;

        private bool isMinimized = false;
        private static Vector2 WindowSize
        {
            get
            {
                float height = 480f; 
                // If Ideology is not active, reduce height by the size of the tattoo buttons and the gap
                if (!ModsConfig.IdeologyActive)
                {
                    height -= (BUTTON_HEIGHT + UI_GAP); // BUTTON_HEIGHT = 30f, UI_GAP = 5f
                }
                return new Vector2(320f, height);
            }
        }

        // Button size constants - both buttons use these exact same dimensions
        private const float BUTTON_WIDTH = 120f;
        private const float BUTTON_HEIGHT = 30f;
        private const float PORTRAIT_HEIGHT = 260f; // Fixed height for pawn portraits
        private static readonly Vector2 MinimizedSize = new Vector2(180f, 60f); // Button + padding

        // UI Layout Constants
        private const float LABEL_HEIGHT = 20f;
        private const float ROTATE_BUTTON_WIDTH = 30f;
        private const float UI_GAP = 5f;
        private const float REROLL_BUTTON_WIDTH = 90f;
        private const float REROLL_BUTTON_OFFSET_Y = 15f; // Offset for labels below portraits
        #endregion

        #region Constructors
        public override Vector2 InitialSize => isMinimized ? MinimizedSize : WindowSize;

        public XenoPreviewWindow() : base() // Add null if 1.5 > but remove if for 1.4
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
            if ((xenotypeDialog == null || !xenotypeDialog.IsOpen)
             && (xenogermDialog == null || !xenogermDialog.IsOpen))
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
            // Make the button fill most of the window but maintain same size as hide button
            Rect buttonRect = new Rect(
                (inRect.width - BUTTON_WIDTH) / 2f,
                (inRect.height - BUTTON_HEIGHT) / 2f,
                BUTTON_WIDTH,
                BUTTON_HEIGHT
            );

            // Draw background matching the hide button style
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

            if ((xenotypeDialog == null || !xenotypeDialog.IsOpen)
             && (xenogermDialog != null && !xenogermDialog.IsOpen)
             && IsOpen)
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
            const float buttonH = BUTTON_HEIGHT; // Use consistent button height
            const float rotateButtonW = 30f;
            const float gap = 5f;

            // Hide button in top left corner - EXACT same size as Show Preview button
            Rect hideButton = new Rect(5f, 5f, BUTTON_WIDTH, BUTTON_HEIGHT);
            if (Widgets.ButtonText(hideButton, "Hide Preview"))
            {
                isMinimized = true;
                UpdatePosition();
            }

            // Adjust portrait area to account for hide button
            float portraitStartY = hideButton.yMax + gap;
            float portraitH = PORTRAIT_HEIGHT;
            float portraitW = (inRect.width - gap) / 2f;

            Rect femRect = new Rect(0, portraitStartY, portraitW, portraitH);
            Rect maleRect = new Rect(portraitW + gap, portraitStartY, portraitW, portraitH);

            DrawPawnPortrait(femalePawn, femRect, femaleRotation);
            DrawPawnPortrait(malePawn, maleRect, maleRotation);

            // Rotation buttons
            Rect femRotateRect = new Rect(femRect.x + femRect.width - rotateButtonW, femRect.y + 5f, rotateButtonW, buttonH);
            Rect maleRotateRect = new Rect(maleRect.x + maleRect.width - rotateButtonW, maleRect.y + 5f, rotateButtonW, buttonH);

            if (Widgets.ButtonText(femRotateRect, "↻"))
            {
                femaleRotation = femaleRotation.Rotated(RotationDirection.Clockwise);
            }

            if (Widgets.ButtonText(maleRotateRect, "↻"))
            {
                maleRotation = maleRotation.Rotated(RotationDirection.Clockwise);
            }

            Rect femLabel = new Rect(femRect.x, femRect.yMax - 15f, femRect.width, labelH);
            Rect maleLabel = new Rect(maleRect.x, maleRect.yMax - 15f, maleRect.width, labelH);

            // Reroll button
            Rect reroll = new Rect((inRect.width - 90f) / 2f, femLabel.yMax + gap, 90f, buttonH);
            if (Widgets.ButtonText(reroll, "Reroll") && (!femaleLocked || !maleLocked))
                GenerateOrRefreshPawns(activeGenes);

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(femLabel, "Female");
            Widgets.Label(maleLabel, "Male");
            Text.Anchor = TextAnchor.UpperLeft;

            float currentY = reroll.yMax + gap;

            // Lock buttons
            Rect femLock = new Rect(femLabel.x, currentY, femLabel.width, buttonH);
            Rect maleLock = new Rect(maleLabel.x, currentY, maleLabel.width, buttonH);

            if (Widgets.ButtonText(femLock, femaleLocked ? "Locked" : "Unlocked"))
            {
                femaleLocked = !femaleLocked;
            }
            if (Widgets.ButtonText(maleLock, maleLocked ? "Locked" : "Unlocked"))
            {
                maleLocked = !maleLocked;
            }

            currentY += buttonH + gap;

            // Show/Hide Clothes buttons
            Rect femClothes = new Rect(femLock.x, currentY, femLock.width, buttonH);
            Rect maleClothes = new Rect(maleLock.x, currentY, maleLock.width, buttonH);

            if (Widgets.ButtonText(femClothes, femaleShowClothes ? "Hide Clothes" : "Show Clothes"))
            {
                femaleShowClothes = !femaleShowClothes;
                PortraitsCache.SetDirty(femalePawn);
                femalePawn.Drawer.renderer.SetAllGraphicsDirty(); // for 1.5 >
                /* 1.4: Apparel manipulation for immediate update
                if (femalePawn != null)
                {
                    if (!femaleShowClothes) // Hiding clothes
                    {
                        // 1.4: Store and clear apparel
                        if (femalePawn.apparel != null && femalePawn.apparel.WornApparelCount > 0)
                        {
                            originalApparel[femalePawn] = femalePawn.apparel.WornApparel.ToList();
                            femalePawn.apparel.WornApparel.Clear();
                        }
                    }
                    else // Showing clothes
                    {
                        // 1.4: Restore apparel
                        if (originalApparel.TryGetValue(femalePawn, out var apparel))
                        {
                            femalePawn.apparel.WornApparel.AddRange(apparel);
                            originalApparel.Remove(femalePawn);
                        }
                    }
                    // femalePawn.Drawer.renderer.graphics.ResolveAllGraphics(); // for 1.4 - this line is replaced by SetAllGraphicsDirty() above
                }
                */
            }
            if (Widgets.ButtonText(maleClothes, maleShowClothes ? "Hide Clothes" : "Show Clothes"))
            {
                maleShowClothes = !maleShowClothes;
                PortraitsCache.SetDirty(malePawn);
                malePawn.Drawer.renderer.SetAllGraphicsDirty(); // for 1.5 >
                /* 1.4: Apparel manipulation for immediate update
                if (malePawn != null)
                {
                    if (!maleShowClothes) // Hiding clothes
                    {
                        // 1.4: Store and clear apparel
                        if (malePawn.apparel != null && malePawn.apparel.WornApparelCount > 0)
                        {
                            originalApparel[malePawn] = malePawn.apparel.WornApparel.ToList();
                            malePawn.apparel.WornApparel.Clear();
                        }
                    }
                    else // Showing clothes
                    {
                        // 1.4: Restore apparel
                        if (originalApparel.TryGetValue(malePawn, out var apparel))
                        {
                            malePawn.apparel.WornApparel.AddRange(apparel);
                            originalApparel.Remove(malePawn);
                        }
                    }
                    // malePawn.Drawer.renderer.graphics.ResolveAllGraphics(); // for 1.4 - this line is replaced by SetAllGraphicsDirty() above
                }
                */
            }

            currentY += buttonH + gap;

            // Show/Hide Tattoos buttons
            if (ModsConfig.IdeologyActive)
            {
                Rect femTattoos = new Rect(femClothes.x, currentY, femClothes.width, buttonH);
                Rect maleTattoos = new Rect(maleClothes.x, currentY, maleClothes.width, buttonH);

                if (Widgets.ButtonText(femTattoos, femaleShowTattoos ? "Hide Tattoos" : "Show Tattoos"))
                {
                    femaleShowTattoos = !femaleShowTattoos;
                    PortraitsCache.SetDirty(femalePawn);
                    femalePawn.Drawer.renderer.SetAllGraphicsDirty(); // for 1.5 >
                    /* 1.4: Tattoo manipulation for immediate update
                    if (femalePawn != null && femalePawn.style != null)
                    {
                        if (!femaleShowTattoos) // Hiding tattoos
                        {
                            // 1.4: Store and clear tattoos
                            originalTattoos[femalePawn] = (femalePawn.style.FaceTattoo, femalePawn.style.BodyTattoo);
                            if (femalePawn.style.FaceTattoo != null) femalePawn.style.FaceTattoo = TattooDefOf.NoTattoo_Face;
                            if (femalePawn.style.BodyTattoo != null) femalePawn.style.BodyTattoo = TattooDefOf.NoTattoo_Body;
                        }
                        else // Showing tattoos
                        {
                            // 1.4: Restore tattoos
                            if (originalTattoos.TryGetValue(femalePawn, out var tattoos))
                            {
                                femalePawn.style.FaceTattoo = tattoos.face;
                                femalePawn.style.BodyTattoo = tattoos.body;
                                originalTattoos.Remove(femalePawn);
                            }
                        }
                        // femalePawn.Drawer.renderer.graphics.ResolveAllGraphics(); // for 1.4 - this line is replaced by SetAllGraphicsDirty() above
                    }
                    */
                }
                if (Widgets.ButtonText(maleTattoos, maleShowTattoos ? "Hide Tattoos" : "Show Tattoos"))
                {
                    maleShowTattoos = !maleShowTattoos;
                    PortraitsCache.SetDirty(malePawn);
                    malePawn.Drawer.renderer.SetAllGraphicsDirty(); // for 1.5 >
                    /* 1.4: Tattoo manipulation for immediate update
                    if (malePawn != null && malePawn.style != null)
                    {
                        if (!maleShowTattoos) // Hiding tattoos
                        {
                            // 1.4: Store and clear tattoos
                            originalTattoos[malePawn] = (malePawn.style.FaceTattoo, malePawn.style.BodyTattoo);
                            if (malePawn.style.FaceTattoo != null) malePawn.style.FaceTattoo = TattooDefOf.NoTattoo_Face;
                            if (malePawn.style.BodyTattoo != null) malePawn.style.BodyTattoo = TattooDefOf.NoTattoo_Body;
                        }
                        else // Showing tattoos
                        {
                            // 1.4: Restore tattoos
                            if (originalTattoos.TryGetValue(malePawn, out var tattoos))
                            {
                                malePawn.style.FaceTattoo = tattoos.face;
                                malePawn.style.BodyTattoo = tattoos.body;
                                originalTattoos.Remove(malePawn);
                            }
                        }
                        // malePawn.Drawer.renderer.graphics.ResolveAllGraphics(); // for 1.4 - this line is replaced by SetAllGraphicsDirty() above
                    }
                    */
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
                bool showClothes = pawn.gender == Gender.Female ? femaleShowClothes : maleShowClothes;
                bool showTattoos = pawn.gender == Gender.Female ? femaleShowTattoos : maleShowTattoos;

                // 1.4: Logic moved to button handlers.
                // 1.5+: Needed for other pre-rendering adjustments.
                PreparePawnForPortrait(pawn, showClothes, showTattoos);

                try
                {
                    var tex = PortraitsCache.Get(pawn, rect.size, rotation, Vector3.zero);  
                    Widgets.DrawTextureFitted(rect, tex, 1f); // for 1.5 and under
                    // Widgets.DrawTextureFitted(rect, (Texture)tex, 1f, new Vector2(tex.width, tex.height), new Rect(0f, 0f, 1f, 1f), angle: 0f, mat: null, alpha: 1f); // for 1.6
                }
                catch (Exception ex)
                {
                    Log.Error($"[XenoPreview] Portrait draw error: {ex}");
                    DrawPlaceholder(rect, "Portrait error");
                }
                finally
                {
                    // 1.4: Logic moved to button handlers.
                // 1.5+: Needed for other pre-rendering adjustments.
                RestorePawnAfterPortrait(pawn);
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
        private Dictionary<Pawn, List<Apparel>> originalApparel = new Dictionary<Pawn, List<Apparel>>(); // 1.5: Used by PreparePawnForPortrait/RestorePawnAfterPortrait
        private Dictionary<Pawn, (TattooDef face, TattooDef body)> originalTattoos = new Dictionary<Pawn, (TattooDef, TattooDef)>(); // 1.5: Used by PreparePawnForPortrait/RestorePawnAfterPortrait

        private void PreparePawnForPortrait(Pawn pawn, bool showClothes, bool showTattoos)
        {
            if (!showClothes && pawn.apparel != null && pawn.apparel.WornApparelCount > 0)
            {
                originalApparel[pawn] = pawn.apparel.WornApparel.ToList();
                pawn.apparel.WornApparel.Clear();
            }

            if (!showTattoos && pawn.style != null)
            {
                originalTattoos[pawn] = (pawn.style.FaceTattoo, pawn.style.BodyTattoo);
                if (pawn.style.FaceTattoo != null) pawn.style.FaceTattoo = TattooDefOf.NoTattoo_Face;
                if (pawn.style.BodyTattoo != null) pawn.style.BodyTattoo = TattooDefOf.NoTattoo_Body;
            }
        }

        // 1.4: This method is not needed as its logic is now in the button handlers.
        // 1.5+: This method might be used for other pre-rendering adjustments if needed.
        
        private void RestorePawnAfterPortrait(Pawn pawn)
        {
            if (originalApparel.TryGetValue(pawn, out var apparel))
            {
                pawn.apparel.WornApparel.AddRange(apparel);
                originalApparel.Remove(pawn);
            }

            if (originalTattoos.TryGetValue(pawn, out var tattoos))
            {
                pawn.style.FaceTattoo = tattoos.face;
                pawn.style.BodyTattoo = tattoos.body;
                originalTattoos.Remove(pawn);
            }
        }
        
        private List<GeneDef> TryGetCurrentGenes(out int count)
        {
            count = 0;
            List<GeneDef> genes = null;
            try
            {
                // "selectedGenes" is from Dialog_CreateXenotype
                if (xenotypeDialog != null)
                    genes = Traverse.Create(xenotypeDialog).Field("selectedGenes").GetValue<List<GeneDef>>();
                // "selectedGenepacks" is from Dialog_CreateXenogerm
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
                if (!femaleLocked) DestroyPawn(ref femalePawn);
                femalePawn = GeneratePawn(Gender.Female, tmpXeno);
            }
            else
            {
                UpdatePreviewPawnGenes(femalePawn, genes);
            }

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
                //Log.Message("Generating Pawn");
                var p = PawnGenerator.GeneratePawn(request);
                //p.story.favoriteColor = UnityEngine.Random.ColorHSV();
                if (ModsConfig.IdeologyActive)
                {
                    p.style.FaceTattoo = DefDatabase<TattooDef>.AllDefsListForReading.Where(x => x.tattooType == TattooType.Face).RandomElement();
                    p.style.BodyTattoo = DefDatabase<TattooDef>.AllDefsListForReading.Where(x => x.tattooType == TattooType.Body).RandomElement();
                }
                //Log.Message("Finished Generating Pawn");
                if (gender == Gender.Female)
                    femaleNaturalHairColor = p.story.HairColor;
                else
                    maleNaturalHairColor = p.story.HairColor;

                p.needs?.AllNeeds.Clear();
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

        private void UpdatePreviewPawnGenes(Pawn pawn, List<GeneDef> selectedGenes)
        {
            if (pawn == null || selectedGenes == null) return;

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
                pawn.story.HairColor = pawn.gender == Gender.Female
                    ? femaleNaturalHairColor
                    : maleNaturalHairColor;

            PortraitsCache.SetDirty(pawn);
        }

        private void DestroyPawn(ref Pawn p)
        {
            if (p != null)
            {
                // 1.4: These are not needed as their logic is now in the button handlers.
                // 1.5+: Needed if originalApparel and originalTattoos are used.
                originalApparel.Remove(p);
                originalTattoos.Remove(p);
                if (!p.Destroyed)
                    p.Destroy(DestroyMode.Vanish);
            }
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