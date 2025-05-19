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
        private Pawn previewPawn;
        private int lastGeneCount = -1;
        private bool needsPawnUpdate = true;

        // Gender selection
        private Gender selectedGender = Gender.Male;
        private static readonly Texture2D MaleIcon = ContentFinder<Texture2D>.Get("UI/Icons/Gender/Male");
        private static readonly Texture2D FemaleIcon = ContentFinder<Texture2D>.Get("UI/Icons/Gender/Female");

        // Timer to periodically check for gene changes
        private int updateTicks = 0;
        private const int UPDATE_INTERVAL = 15; // Check more frequently

        private static readonly Vector2 WindowSize = new Vector2(230f, 320f); // Slightly taller to accommodate gender button

        public override Vector2 InitialSize => WindowSize;

        public XenoPreviewWindow()
        {
            closeOnCancel = false;
            closeOnAccept = false;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
            draggable = false;
            resizeable = false;
            drawShadow = true;
            forcePause = false;
            preventCameraMotion = false;
            doCloseX = false;

            // Use a higher layer to ensure we can click on this window even if it overlaps the main dialog
            layer = WindowLayer.Super;

            soundAppear = null;
            soundClose = null;
        }

        public void SetDialog(Dialog_CreateXenotype dialog)
        {
            this.xenotypeDialog = dialog;
            this.xenogermDialog = null;
            UpdatePosition();
        }

        public void SetXenogermDialog(Dialog_CreateXenogerm dialog)
        {
            this.xenotypeDialog = null;
            this.xenogermDialog = dialog;
            UpdatePosition();
        }

        public void UpdatePosition()
        {
            float xPos = 0;
            float yPos = 0;
            bool validPosition = false;

            if (xenotypeDialog != null && xenotypeDialog.IsOpen)
            {
                // Position window to the right of the dialog
                xPos = xenotypeDialog.windowRect.xMax + 10f;
                yPos = xenotypeDialog.windowRect.yMin;
                validPosition = true;
            }
            else if (xenogermDialog != null && xenogermDialog.IsOpen)
            {
                // Position window to the right of the dialog
                xPos = xenogermDialog.windowRect.xMax + 10f;
                yPos = xenogermDialog.windowRect.yMin;
                validPosition = true;
            }

            if (validPosition)
            {
                // Make sure it doesn't go off screen
                xPos = Mathf.Min(xPos, UI.screenWidth - WindowSize.x);
                yPos = Mathf.Clamp(yPos, 0f, UI.screenHeight - WindowSize.y);
                this.windowRect = new Rect(xPos, yPos, WindowSize.x, WindowSize.y);
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Check if either dialog is still open
            if ((xenotypeDialog == null || !xenotypeDialog.IsOpen) &&
                (xenogermDialog == null || !xenogermDialog.IsOpen))
            {
                this.Close(false);
                return;
            }

            // Get the current genes from the appropriate dialog
            List<GeneDef> selectedGenes = null;
            int currentGeneCount = 0;

            try
            {
                if (xenotypeDialog != null)
                {
                    var traverse = Traverse.Create(xenotypeDialog);
                    selectedGenes = traverse.Field("selectedGenes").GetValue<List<GeneDef>>();
                }
                else if (xenogermDialog != null)
                {
                    // For Dialog_CreateXenogerm, we need to extract genes from genepacks
                    selectedGenes = new List<GeneDef>();
                    var traverse = Traverse.Create(xenogermDialog);
                    var selectedGenepacks = traverse.Field("selectedGenepacks").GetValue<List<Genepack>>();

                    if (selectedGenepacks != null)
                    {
                        foreach (var genepack in selectedGenepacks)
                        {
                            if (genepack?.GeneSet?.GenesListForReading != null)
                            {
                                selectedGenes.AddRange(genepack.GeneSet.GenesListForReading);
                            }
                        }
                    }
                }

                if (selectedGenes != null)
                {
                    currentGeneCount = selectedGenes.Count;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[XenoPreviewWindow] Could not get genes: {ex.Message}");
            }

            // Check for gene changes
            if (currentGeneCount != lastGeneCount)
            {
                needsPawnUpdate = true;
                lastGeneCount = currentGeneCount;
            }

            // Attempt to generate pawn if needed
            if (needsPawnUpdate)
            {
                if (selectedGenes != null && currentGeneCount > 0)
                {
                    GeneratePreviewPawn(selectedGenes, selectedGender);
                }
                else
                {
                    Cleanup();
                }
                needsPawnUpdate = false;
            }

            // Draw gender selection button at the bottom
            Rect genderButtonRect = new Rect(inRect.width / 2 - 50f, inRect.height - 30f, 100f, 24f);
            if (Widgets.ButtonText(genderButtonRect, $"{selectedGender}"))
            {
                // Toggle gender
                selectedGender = selectedGender == Gender.Male ? Gender.Female : Gender.Male;
                needsPawnUpdate = true;
            }

            // Add gender icon
            Rect genderIconRect = new Rect(genderButtonRect.x + 5f, genderButtonRect.y + 2f, 20f, 20f);
            GUI.DrawTexture(genderIconRect, selectedGender == Gender.Male ? MaleIcon : FemaleIcon);

            // Draw pawn portrait above the gender button
            Rect portraitRect = new Rect(0, 0, inRect.width, inRect.height - 35f);
            if (previewPawn != null)
            {
                try
                {
                    Texture portrait = PortraitsCache.Get(previewPawn, portraitRect.size, Rot4.South, Vector3.zero, 1.0f);
                    if (portrait != null)
                    {
                        Widgets.DrawTextureFitted(portraitRect, portrait, 1f);
                    }
                    else
                    {
                        GUI.color = Color.gray;
                        Text.Anchor = TextAnchor.MiddleCenter;
                        Widgets.Label(portraitRect, "No portrait available");
                        Text.Anchor = TextAnchor.UpperLeft;
                        GUI.color = Color.white;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[XenoPreview] EXCEPTION while drawing portrait: {ex}");
                    GUI.color = Color.gray;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(portraitRect, "Portrait error");
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = Color.white;
                }
            }
            else
            {
                Rect labelRect = new Rect(0, portraitRect.height / 2 - 15f, portraitRect.width, 30f);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(labelRect, "Add genes to see preview");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();

            // Check if either dialog is still open
            if ((xenotypeDialog == null || !xenotypeDialog.IsOpen) &&
                (xenogermDialog == null || !xenogermDialog.IsOpen) &&
                this.IsOpen)
            {
                this.Close(false);
                XenoPreview.PreviewWindowInstance = null;
                return;
            }

            // Periodically check for gene changes
            updateTicks++;
            if (updateTicks >= UPDATE_INTERVAL)
            {
                updateTicks = 0;

                try
                {
                    List<GeneDef> selectedGenes = null;

                    if (xenotypeDialog != null && xenotypeDialog.IsOpen)
                    {
                        var traverse = Traverse.Create(xenotypeDialog);
                        selectedGenes = traverse.Field("selectedGenes").GetValue<List<GeneDef>>();
                    }
                    else if (xenogermDialog != null && xenogermDialog.IsOpen)
                    {
                        selectedGenes = new List<GeneDef>();
                        var traverse = Traverse.Create(xenogermDialog);
                        var selectedGenepacks = traverse.Field("selectedGenepacks").GetValue<List<Genepack>>();

                        if (selectedGenepacks != null)
                        {
                            foreach (var genepack in selectedGenepacks)
                            {
                                if (genepack?.GeneSet?.GenesListForReading != null)
                                {
                                    selectedGenes.AddRange(genepack.GeneSet.GenesListForReading);
                                }
                            }
                        }
                    }

                    if (selectedGenes != null)
                    {
                        int currentGeneCount = selectedGenes.Count;
                        if (currentGeneCount != lastGeneCount)
                        {
                            needsPawnUpdate = true;
                            lastGeneCount = currentGeneCount;
                        }
                    }
                }
                catch (Exception)
                {
                    // Ignore errors in update loop to avoid log spam
                }
            }
        }

        private void GeneratePreviewPawn(List<GeneDef> selectedGenes, Gender gender)
        {
            Cleanup();

            if (selectedGenes == null || selectedGenes.Count == 0)
            {
                previewPawn = null;
                return;
            }

            try
            {
                // Create a temporary CustomXenotype for pawn generation
                CustomXenotype tempXenotype = new CustomXenotype();
                tempXenotype.genes = new List<GeneDef>(selectedGenes);
                tempXenotype.name = "TempPreviewXenotype";

                PawnGenerationRequest request = new PawnGenerationRequest(
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
                    forcedCustomXenotype: tempXenotype,
                    forceNoIdeo: true,
                    forceNoBackstory: true,
                    forbidAnyTitle: true
                );
                previewPawn = PawnGenerator.GeneratePawn(request);

                if (previewPawn != null)
                {
                    // Optimize the pawn by disabling unnecessary systems
                    previewPawn.needs?.AllNeeds?.Clear();
                    if (previewPawn.mindState != null) previewPawn.mindState.Active = false;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[XenoPreviewWindow] EXCEPTION during GeneratePreviewPawn: {ex}");
                previewPawn = null;
            }
        }

        private void Cleanup()
        {
            if (previewPawn != null)
            {
                if (!previewPawn.Destroyed) previewPawn.Destroy(DestroyMode.Vanish);
                previewPawn = null;
            }
        }

        public override void PreClose()
        {
            base.PreClose();
            Cleanup();
            if (XenoPreview.PreviewWindowInstance == this)
            {
                XenoPreview.PreviewWindowInstance = null;
            }
        }
    }
}