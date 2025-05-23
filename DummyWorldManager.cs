using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace XenoPreview
{
    [StaticConstructorOnStartup]
    public static class DummyWorldManager
    {
        private static bool isWorldActive = false;
        private static bool isActivelyManaging = false;

        private static ProgramState originalProgramState;
        private static Game originalGame;
        private static GameInitData originalGameInitDataInstance;
        private static List<Map> originalMapsCache; // To store maps from originalGame if it existed
        private static Map originalCurrentMap;
        private static Faction originalPlayerFaction;

        private static readonly HashSet<string> _loggedMessages = new HashSet<string>();
        private static readonly HashSet<int> _loggedErrorHashes = new HashSet<int>();

        private static Faction dummyPlayerFaction;
        private static Settlement dummyMapParent;

        static DummyWorldManager()
        {
            MessageOnce("DummyWorldManager static constructor called.");
        }

        private static void MessageOnce(string text)
        {
            if (_loggedMessages.Add($"[DWM] {text}"))
                Log.Message($"[XenoPreview DEBUG] {text}");
        }

        private static void WarningOnce(string text, int keyOverride = 0)
        {
            int warningKey = keyOverride != 0 ? keyOverride : text.GetHashCode();
            if (_loggedErrorHashes.Add(warningKey + 1000000))
                Log.Warning($"[XenoPreview WARNING] {text}");
        }

        private static void ErrorOnce(string text, int keyOverride = 0)
        {
            int errorKey = keyOverride != 0 ? keyOverride : text.GetHashCode();
            if (_loggedErrorHashes.Add(errorKey))
                Log.Error($"[XenoPreview ERROR] {text}");
        }

        public static bool EnsureDummyWorld()
        {
            MessageOnce("EnsureDummyWorld called.");
            if (isActivelyManaging) { WarningOnce("EnsureDummyWorld called while already actively managing. Skipping."); return isWorldActive; }
            if (isWorldActive)
            {
                MessageOnce("Dummy world is already active.");
                if (Current.ProgramState != ProgramState.Playing)
                {
                    MessageOnce($"ProgramState was {Current.ProgramState}, setting to Playing for active dummy world.");
                    Current.ProgramState = ProgramState.Playing;
                }
                return true;
            }

            isActivelyManaging = true;
            MessageOnce("Starting dummy world creation process...");

            try
            {
                originalProgramState = Current.ProgramState;
                MessageOnce($"Original ProgramState '{originalProgramState}' stored.");
                originalGame = Current.Game;
                MessageOnce($"Original Current.Game reference stored (is null: {originalGame == null}).");

                if (originalGame != null)
                {
                    originalMapsCache = originalGame.Maps?.ListFullCopy() ?? new List<Map>();
                    originalCurrentMap = originalGame.CurrentMap;
                    originalGameInitDataInstance = originalGame.InitData;
                }
                else
                {
                    originalMapsCache = new List<Map>();
                    originalCurrentMap = null;
                    originalGameInitDataInstance = null;
                }

                MessageOnce("Step 0: Nullifying Current.Game to ensure a completely fresh start for 'Find' system.");
                Current.Game = null;

                MessageOnce("Creating NEW Game instance for Current.Game.");
                Current.Game = new Game();

                // --- NEW DEBUGGING BLOCK ---
                MessageOnce($"DEBUG: After 'Current.Game = new Game()':");
                MessageOnce($"DEBUG: Current.Game is null? {Current.Game == null}");
                if (Current.Game != null)
                {
                    MessageOnce($"DEBUG: Current.Game.World (Property) is null? {Current.Game.World == null}");
                    // The following line might cause a compiler error if 'factionManager' field isn't visible
                    // If it does, comment it out for this test run.
                    // MessageOnce($"DEBUG: Current.Game.factionManager (FIELD) is null? {Current.Game.factionManager == null}");


                    FieldInfo worldIntField = typeof(Game).GetField("worldInt", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (worldIntField != null)
                    {
                        object worldIntVal = worldIntField.GetValue(Current.Game);
                        MessageOnce($"DEBUG: Current.Game.worldInt (internal field) is null? {worldIntVal == null}");
                    }
                    else
                    {
                        MessageOnce($"DEBUG: Could not find Current.Game.worldInt field via reflection.");
                    }
                }

                MessageOnce($"DEBUG: Attempting to access Find.World directly...");
                World foundWorld = null;
                try
                {
                    foundWorld = Find.World;
                    MessageOnce($"DEBUG: Find.World access attempt finished. Find.World is null? {foundWorld == null}");
                    if (foundWorld != null)
                    {
                        // The following line might cause a compiler error if 'factionManager' field on World isn't visible
                        // If it does, comment it out for this test run.
                        // MessageOnce($"DEBUG: Find.World.factionManager (FIELD) is null? {foundWorld.factionManager == null}");
                    }
                }
                catch (Exception exFindWorld)
                {
                    ErrorOnce($"DEBUG: EXCEPTION during Find.World access: {exFindWorld.GetType().Name} - {exFindWorld.Message}");
                }

                MessageOnce($"DEBUG: Attempting to access Find.FactionManager directly (this is where it usually crashes)...");
                FactionManager fm = null;
                try
                {
                    fm = Find.FactionManager;
                    MessageOnce($"DEBUG: Find.FactionManager access attempt finished. Find.FactionManager is null? {fm == null}");
                }
                catch (Exception exFindFM)
                {
                    ErrorOnce($"DEBUG: EXCEPTION during Find.FactionManager access: {exFindFM.GetType().Name} - {exFindFM.Message}");
                }
                // --- END NEW DEBUGGING BLOCK ---

                if (Find.FactionManager == null)
                { // This was the original crash point in the log for this specific error
                    ErrorOnce("ULTRA CRITICAL: Find.FactionManager is STILL NULL after setting up Current.Game.");
                    isActivelyManaging = false; throw new Exception("Find.FactionManager is null and could not be initialized.");
                }
                MessageOnce("Current.Game is set, and Find.FactionManager is believed to be correctly initialized.");

                Current.Game.InitData = new GameInitData();
                MessageOnce("Current.Game.InitData created/reset.");

                MessageOnce("Storing potentially re-evaluated originalPlayerFaction...");
                originalPlayerFaction = Faction.OfPlayerSilentFail;
                MessageOnce($"Original player faction after Current.Game setup: '{originalPlayerFaction?.Name ?? "null"}'.");

                MessageOnce("Preparing Current.Game further for dummy world operations...");
                Current.Game.tickManager.gameStartAbsTick = GenTicks.TicksAbs;
                Current.Game.tickManager.DebugSetTicksGame(0);
                MessageOnce("Core game components prepared for dummy world, tickManager.gameStartAbsTick set.");

                Current.ProgramState = ProgramState.Entry;
                MessageOnce("ProgramState set to Entry.");
                LongEventHandler.ClearQueuedEvents();

                MessageOnce("Step 4: Setting up Scenario...");
                Scenario scenario = new Scenario();
                scenario.name = "XenoPreviewDummyScenario";
                scenario.Category = ScenarioCategory.Undefined;
                ScenPart_PlayerFaction playerFactionPart = new ScenPart_PlayerFaction();
                playerFactionPart.Randomize();
                MessageOnce("ScenPart_PlayerFaction randomized.");

                FieldInfo playerFactionFieldInfo = typeof(Scenario).GetField("playerFaction", BindingFlags.Instance | BindingFlags.NonPublic);
                if (playerFactionFieldInfo != null) playerFactionFieldInfo.SetValue(scenario, playerFactionPart);
                else { ErrorOnce("Reflection failed for Scenario.playerFaction field."); throw new Exception("Reflection error for Scenario.playerFaction."); }

                Current.Game.Scenario = scenario;
                Current.Game.Scenario.PreConfigure();
                MessageOnce("Scenario configured.");

                MessageOnce("Step 5: Setting Storyteller and GameInfo...");
                Current.Game.storyteller.def = StorytellerDefOf.Cassandra;
                Current.Game.storyteller.difficultyDef = DifficultyDefOf.Easy;
                Current.Game.Info.permadeathMode = false;
                MessageOnce("Storyteller and GameInfo configured.");

                Current.Game.InitData.playerFaction = null;
                Current.Game.InitData.startingAndOptionalPawns = new List<Pawn>();
                Current.Game.InitData.startingTile = -1;

                MessageOnce("Step 6: Generating World...");
                List<FactionDef> factionsToGenerate = new List<FactionDef>() { FactionDefOf.PlayerColony };
                Current.Game.World = WorldGenerator.GenerateWorld(
                    planetCoverage: 0.05f, seedString: "XenoPreviewWorld",
                    overallRainfall: OverallRainfall.Normal, overallTemperature: OverallTemperature.Normal,
                    population: OverallPopulation.AlmostNone, factions: factionsToGenerate, pollution: 0f);
                MessageOnce("WorldGenerator.GenerateWorld finished.");

                dummyPlayerFaction = Faction.OfPlayer;
                if (dummyPlayerFaction == null)
                {
                    ErrorOnce("CRITICAL: Faction.OfPlayer is NULL after world gen.");
                    dummyPlayerFaction = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f => f.IsPlayer);
                    if (dummyPlayerFaction == null) throw new Exception("Player faction not created and not findable.");
                    WarningOnce("Manually fetched player faction from FactionManager.");
                }
                MessageOnce($"Player faction '{dummyPlayerFaction.Name}' established.");
                Current.Game.InitData.playerFaction = dummyPlayerFaction;

                FieldInfo temporaryField = typeof(Faction).GetField("temporary", BindingFlags.Instance | BindingFlags.NonPublic);
                if (temporaryField != null) temporaryField.SetValue(dummyPlayerFaction, true);
                else WarningOnce("Could not mark dummyPlayerFaction as temporary via reflection.");
                MessageOnce("World generation step complete.");

                MessageOnce("Step 7: Generating a minimal Map...");
                Current.ProgramState = ProgramState.MapInitializing;
                int tileIdSync = -1;
                if (Current.Game.Info != null && Current.Game.Info.startingTile >= 0)
                {
                    tileIdSync = Current.Game.Info.startingTile;
                    MessageOnce($"Game.Info supplied starting tile: {tileIdSync}");
                }
                else
                {
                    tileIdSync = TileFinder.RandomStartingTile();
                    if (tileIdSync < 0) tileIdSync = TileFinder.RandomSettlementTileFor(dummyPlayerFaction, false);
                    if (tileIdSync < 0)
                    {
                        for (int i = 0; i < Find.WorldGrid.TilesCount; i++)
                        {
                            if (Find.WorldGrid[i].biome != BiomeDefOf.Ocean && Find.WorldGrid[i].biome != BiomeDefOf.Lake)
                            {
                                tileIdSync = i; break;
                            }
                        }
                        if (tileIdSync < 0) { ErrorOnce("Failed to find ANY valid tile for map gen."); throw new Exception("No valid tile for map."); }
                        MessageOnce($"Used absolute fallback to find tile {tileIdSync}.");
                    }
                }
                Current.Game.InitData.startingTile = tileIdSync;

                dummyMapParent = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
                dummyMapParent.SetFaction(dummyPlayerFaction);
                dummyMapParent.Tile = tileIdSync;
                dummyMapParent.Name = SettlementNameGenerator.GenerateSettlementName(dummyMapParent, dummyPlayerFaction.def.playerInitialSettlementNameMaker);
                Find.WorldObjects.Add(dummyMapParent);
                MessageOnce($"MapParent (Settlement) '{dummyMapParent.LabelCap}' created at tile {tileIdSync}.");

                Map newMapSync = MapGenerator.GenerateMap(new IntVec3(50, 1, 50), dummyMapParent, dummyMapParent.MapGeneratorDef, dummyMapParent.ExtraGenStepDefs, null);
                Current.Game.AddMap(newMapSync);
                Current.Game.CurrentMap = newMapSync;
                dummyMapParent.forceRemoveWorldObjectWhenMapRemoved = true;
                Find.CameraDriver.SetRootPosAndSize(newMapSync.Center.ToVector3(), (float)Math.Sqrt(newMapSync.Area));
                Current.Game.tickManager.DebugSetTicksGame(0);
                MessageOnce("Minimal map generated and set as CurrentMap.");

                MessageOnce("Step 8: Finalizing game state...");
                Current.ProgramState = ProgramState.Playing;
                if (Current.Game.researchManager == null || (originalGame != null && Current.Game.researchManager == originalGame.researchManager))
                {
                    Current.Game.researchManager = new ResearchManager();
                }

                foreach (Map map in Current.Game.Maps) { map.FinalizeLoading(); }
                MessageOnce("Dummy world setup complete. ProgramState set to Playing.");
                isWorldActive = true;
                return true;
            }
            catch (Exception e)
            {
                ErrorOnce($"Dummy world creation failed: {e.ToString()}", e.GetHashCode());
                Cleanup();
                isWorldActive = false;
                return false;
            }
            finally
            {
                isActivelyManaging = false;
            }
        }

        // ... (Cleanup and GetPlayerFaction method remain the same as the last complete version I provided) ...
        public static void Cleanup()
        {
            MessageOnce("Cleanup called.");
            if (isActivelyManaging) { WarningOnce("Cleanup called while already actively managing. Skipping."); return; }
            if (!isWorldActive && originalGame == null && (Current.Game == null || Current.Game == originalGame))
            {
                MessageOnce("Cleanup: No significant state to restore or world not made active.");
                if (originalProgramState != default && Current.ProgramState != originalProgramState)
                {
                    Current.ProgramState = originalProgramState;
                }
                else if (Current.ProgramState != ProgramState.Entry && originalProgramState == default && originalGame == null)
                {
                    Current.ProgramState = ProgramState.Entry;
                }
                isActivelyManaging = false;
                return;
            }

            isActivelyManaging = true;
            Game gameBeingCleanedUp = Current.Game;

            try
            {
                MessageOnce("Starting cleanup process...");

                if (gameBeingCleanedUp != originalGame && gameBeingCleanedUp != null)
                {
                    MessageOnce("Cleaning up maps and world objects from the DUMMY Game instance...");
                    foreach (Map map in gameBeingCleanedUp.Maps.ToList())
                    {
                        MessageOnce($"De-initializing dummy map: {map.uniqueID} ({map.info.parent?.LabelCap ?? "NoParent"}).");
                        MapDeiniter.Deinit(map, false);
                    }
                    // Clear the maps list from the dummy game AFTER deiniting them.
                    gameBeingCleanedUp.Maps.Clear();

                    if (dummyMapParent != null && gameBeingCleanedUp.World != null && gameBeingCleanedUp.World.worldObjects.Contains(dummyMapParent))
                    {
                        MessageOnce($"Removing dummyMapParent: {dummyMapParent.LabelCap} from dummy world's objects.");
                        gameBeingCleanedUp.World.worldObjects.Remove(dummyMapParent);
                    }
                }
                dummyMapParent = null;


                if (originalProgramState != default && Current.ProgramState != originalProgramState)
                {
                    Current.ProgramState = originalProgramState; MessageOnce($"Restored ProgramState to {originalProgramState}.");
                }
                else if (Current.ProgramState != ProgramState.Entry && originalProgramState == default && originalGame == null)
                {
                    Current.ProgramState = ProgramState.Entry; MessageOnce("Restored ProgramState to Entry.");
                }

                if (Current.Game != originalGame)
                {
                    MessageOnce($"Restoring Current.Game to original (original was null: {originalGame == null}).");
                    Current.Game = originalGame;
                }
                else
                {
                    MessageOnce("Current.Game was already the originalGame reference. No change to Current.Game itself.");
                }

                if (originalGame != null && originalGameInitDataInstance != null && originalGame.InitData != originalGameInitDataInstance)
                {
                    originalGame.InitData = originalGameInitDataInstance;
                    MessageOnce("Restored original Game.InitData instance on the original game object.");
                }


                if (Current.Game == null)
                {
                    MessageOnce("Current.Game is now null (restored from null original).");
                }
                else
                {
                    // If originalGame was not null, we expect its maps to be restored when Current.Game = originalGame happened.
                    // If originalMapsCache was used (which it should have been if originalGame existed),
                    // we might need to explicitly re-add them if Current.Game = originalGame doesn't restore the list perfectly,
                    // though it should if originalGame.Maps was the source.
                    // For now, rely on Current.Game = originalGame to restore maps.
                    MessageOnce($"Current.Game restored. Maps count: {Current.Game.Maps.Count}, CurrentMap: {(Current.Game.CurrentMap?.info.parent.LabelCap ?? "null")}.");
                }

                if (dummyPlayerFaction != null)
                {
                    MessageOnce($"Cleared dummyPlayerFaction reference (was: {dummyPlayerFaction.Name}).");
                    dummyPlayerFaction = null;
                }

                LongEventHandler.ClearQueuedEvents();
                MessageOnce("Cleared LongEventHandler queue.");

                isWorldActive = false;
                originalGame = null; originalProgramState = default; originalGameInitDataInstance = null;
                originalMapsCache = null; originalCurrentMap = null; originalPlayerFaction = null;
                dummyMapParent = null;
                MessageOnce("Cleanup finished.");
            }
            catch (Exception e)
            {
                ErrorOnce($"Error during cleanup: {e.ToString()}", e.GetHashCode() + 1);
                isWorldActive = false;
                if (Current.Game != originalGame) Current.Game = originalGame;
            }
            finally
            {
                isActivelyManaging = false;
            }
        }

        public static Faction GetPlayerFaction()
        {
            MessageOnce("GetPlayerFaction called.");
            if (!isWorldActive || dummyPlayerFaction == null)
            {
                WarningOnce("GetPlayerFaction: Dummy world not active or faction is null. Attempting to ensure world...");
                if (!EnsureDummyWorld())
                {
                    ErrorOnce("GetPlayerFaction: Failed to ensure dummy world.");
                    return null;
                }
                if (dummyPlayerFaction == null)
                {
                    ErrorOnce("GetPlayerFaction: EnsureDummyWorld ran, but dummyPlayerFaction is STILL null!");
                    return null;
                }
            }
            MessageOnce($"GetPlayerFaction returning: {dummyPlayerFaction?.Name ?? "null"}");
            return dummyPlayerFaction;
        }
    }
}