// DummyWorldManager.cs
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using HarmonyLib;
using Verse.Profile;

namespace XenoPreview
{
    [StaticConstructorOnStartup]
    public static class DummyWorldManager
    {
        private static bool active;
        private static bool _attemptedThisSession = false;

        private static Map dummyMap;
        private static MapParent dummyParent;

        private static readonly HashSet<string> _loggedMessages = new HashSet<string>();

        private static void LogMessageThisRun(string text) // Renamed for clarity
        {
            if (!_attemptedThisSession || _loggedMessages.Add(text)) // Log if first attempt or new message for current attempt
                Log.Message(text);
        }
        // Warning and Error can remain Log.Warning/Error as they are critical
        private static void LogWarningCritical(string text) { Log.Warning(text); }
        private static void LogErrorCritical(string text) { Log.Error(text); }


        private static readonly FieldInfo _playerFactionInternalFieldInfo = AccessTools.Field(typeof(Scenario), "playerFaction");
        private static readonly FieldInfo _scenarioPartsInternalFieldInfo = AccessTools.Field(typeof(Scenario), "parts");
        private static readonly FieldInfo _playerFactionPartDefFieldInfo = AccessTools.Field(typeof(ScenPart_PlayerFaction), "factionDef");

        public static bool EnsureDummyWorld()
        {
            if (active)
            {
                // LogSessionOnce(Log.Message, "EnsureDummyWorld_AlreadyActive", "[XenoPreview Session] Dummy world is already active.");
                return true;
            }
            if (_attemptedThisSession)
            {
                // LogSessionOnce(Log.Warning, "EnsureDummyWorld_AlreadyAttemptedFailed", "[XenoPreview Session] Dummy world creation previously attempted this session and failed. Skipping further attempts.");
                return false;
            }

            _attemptedThisSession = true;
            _loggedMessages.Clear(); // Clear for this new attempt

            LogMessageThisRun("[XenoPreview ATTEMPT] Starting dummy world creation process...");
            try
            {
                // Step 1: Initialize Game
                LogMessageThisRun("[XenoPreview ATTEMPT] Step 1: Ensuring Game instance...");
                if (Current.Game == null)
                {
                    LogMessageThisRun("[XenoPreview ATTEMPT] Current.Game is null. Creating new Game().");
                    Current.Game = new Game();
                }
                Current.Game.tickManager.gameStartAbsTick = GenTicks.TicksAbs;
                LogMessageThisRun($"[XenoPreview ATTEMPT] tickManager.gameStartAbsTick set to {Current.Game.tickManager.gameStartAbsTick}");
                Current.ProgramState = ProgramState.MapInitializing;
                LogMessageThisRun("[XenoPreview ATTEMPT] Step 1 DONE.");

                // Step 2: Scenario setup
                LogMessageThisRun("[XenoPreview ATTEMPT] Step 2: Scenario setup...");
                if (Current.Game.Scenario == null)
                {
                    ScenarioDef crashDef = ScenarioDefOf.Crashlanded;
                    if (crashDef?.scenario != null)
                    {
                        Current.Game.Scenario = crashDef.scenario.CopyForEditing();
                        if (Current.Game.Scenario.Category == ScenarioCategory.Undefined)
                            Current.Game.Scenario.Category = ScenarioCategory.CustomLocal;
                        LogMessageThisRun($"[XenoPreview ATTEMPT] Copied Crashlanded scenario '{Current.Game.Scenario.name}'.");
                        var pf = (ScenPart_PlayerFaction)_playerFactionInternalFieldInfo.GetValue(Current.Game.Scenario);
                        if (pf != null)
                        {
                            _playerFactionPartDefFieldInfo.SetValue(pf, FactionDefOf.PlayerColony);
                            LogMessageThisRun("[XenoPreview ATTEMPT] Patched playerFactionPart.factionDef.");
                        }
                        else LogWarningCritical("[XenoPreview ATTEMPT] Copied Crashlanded scenario missing playerFaction part."); // Use critical for warnings we need to see
                    }
                    else
                    {
                        LogErrorCritical("[XenoPreview ATTEMPT] Crashlanded ScenarioDef invalid. Creating minimal scenario fallback.");
                        Scenario minimal = new Scenario { name = "XenoDummy", summary = "", description = "", Category = ScenarioCategory.CustomLocal };
                        var parts = (List<ScenPart>)_scenarioPartsInternalFieldInfo.GetValue(minimal) ?? new List<ScenPart>();
                        _scenarioPartsInternalFieldInfo.SetValue(minimal, parts);
                        var pfPart = (ScenPart_PlayerFaction)ScenarioMaker.MakeScenPart(ScenPartDefOf.PlayerFaction);
                        _playerFactionPartDefFieldInfo.SetValue(pfPart, FactionDefOf.PlayerColony);
                        _playerFactionInternalFieldInfo.SetValue(minimal, pfPart);
                        parts.Add(pfPart);
                        parts.Add(ScenarioMaker.MakeScenPart(ScenPartDefOf.ConfigPage_ConfigureStartingPawns));
                        parts.Add(ScenarioMaker.MakeScenPart(ScenPartDefOf.PlayerPawnsArriveMethod));
                        Current.Game.Scenario = minimal;
                        LogMessageThisRun("[XenoPreview ATTEMPT] Minimal scenario created.");
                    }
                }
                LogMessageThisRun("[XenoPreview ATTEMPT] Step 2 DONE.");

                // Step 3: Storyteller
                LogMessageThisRun("[XenoPreview ATTEMPT] Step 3: Setting storyteller...");
                if (Current.Game.storyteller.def == null)
                {
                    var def = DefDatabase<StorytellerDef>.GetNamed("CassandraClassic", false)
                              ?? DefDatabase<StorytellerDef>.AllDefsListForReading.First();
                    Current.Game.storyteller.def = def;
                    LogMessageThisRun($"[XenoPreview ATTEMPT] Storyteller set to {def.defName}");
                }
                Current.Game.Info.permadeathMode = false;
                LogMessageThisRun("[XenoPreview ATTEMPT] Step 3 DONE.");

                // Step 3.5: Ensure GameInitData instance exists BEFORE WorldGenerator
                LogMessageThisRun("[XenoPreview ATTEMPT] Step 3.5: Ensuring GameInitData instance exists...");
                if (Current.Game.InitData == null)
                {
                    Current.Game.InitData = new GameInitData();
                    LogMessageThisRun("[XenoPreview ATTEMPT] New GameInitData() created and assigned to Current.Game.InitData.");
                }
                Current.Game.InitData.gameToLoad = "XenoPreviewDummyWorld_" + Rand.Int;
                Current.Game.InitData.permadeath = false;
                LogMessageThisRun("[XenoPreview ATTEMPT] Step 3.5 DONE.");

                // Step 4: Generate World
                LogMessageThisRun("[XenoPreview ATTEMPT] Step 4: Generating world...");
                if (Current.Game.World == null)
                {
                    var seed = Current.Game.InitData.gameToLoad;
                    LogMessageThisRun($"[XenoPreview ATTEMPT] Calling WorldGenerator.GenerateWorld with seed: {seed}");
                    Current.Game.World = WorldGenerator.GenerateWorld(0.02f, seed,
                        OverallRainfall.Normal, OverallTemperature.Normal,
                        OverallPopulation.AlmostNone, null, 0f);
                    LogMessageThisRun($"[XenoPreview ATTEMPT] World generated: {Current.Game.World.info.name}");
                }
                if (Faction.OfPlayer == null) { LogErrorCritical("[XenoPreview ATTEMPT] CRITICAL: Faction.OfPlayer is NULL after world gen!"); Cleanup(); return false; }
                LogMessageThisRun("[XenoPreview ATTEMPT] Step 4 DONE.");

                // Step 5: Settlement
                LogMessageThisRun("[XenoPreview ATTEMPT] Step 5: Ensuring settlement...");
                Settlement playerSettlement = (Settlement)Find.WorldObjects.Settlements.FirstOrDefault(s => s.Faction == Faction.OfPlayer);
                if (playerSettlement == null)
                {
                    LogMessageThisRun("[XenoPreview ATTEMPT] No player settlement found. Creating one...");
                    playerSettlement = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
                    if (Faction.OfPlayer == null) { LogErrorCritical("[XenoPreview ATTEMPT] Faction.OfPlayer became null before setting settlement faction!"); Cleanup(); return false; }
                    playerSettlement.SetFaction(Faction.OfPlayer);
                    int tile;
                    int? nullableTile = TileFinder.RandomStartingTile();
                    if (nullableTile.HasValue) tile = nullableTile.Value;
                    else tile = Find.World.grid.TilesCount > 0 ? Rand.Range(0, Find.World.grid.TilesCount - 1) : 0;
                    playerSettlement.Tile = tile;
                    playerSettlement.Name = SettlementNameGenerator.GenerateSettlementName(playerSettlement);
                    Find.WorldObjects.Add(playerSettlement);
                    LogMessageThisRun($"[XenoPreview ATTEMPT] Created settlement at {tile}");
                }
                else
                {
                    LogMessageThisRun($"[XenoPreview ATTEMPT] Player settlement found at tile {playerSettlement.Tile}");
                }
                LogMessageThisRun("[XenoPreview ATTEMPT] Step 5 DONE.");

                // Step 6: GameInitData
                LogMessageThisRun("[XenoPreview ATTEMPT] Step 6: Fully setting InitData for map gen...");
                // InitData instance should exist from Step 3.5
                Current.Game.InitData.mapSize = 40;
                Current.Game.InitData.startingTile = playerSettlement.Tile;
                // permadeath was set in 3.5, ensure it's consistent with Game.Info
                Current.Game.InitData.permadeath = Current.Game.Info.permadeathMode;
                Current.Game.InitData.startedFromEntry = true;
                Current.Game.InitData.startingAndOptionalPawns = new List<Pawn>();
                Current.Game.InitData.gameToLoad = null; // World exists, don't "load"
                LogMessageThisRun("[XenoPreview ATTEMPT] Step 6 DONE.");

                // DEBUG: Log details about player settlements BEFORE InitNewGame
                LogMessageThisRun("[XenoPreview ATTEMPT] --- Pre-InitNewGame Settlement Debug ---");
                List<Settlement> allSettlements = Find.WorldObjects.Settlements;
                LogMessageThisRun($"[XenoPreview ATTEMPT] Total settlements on world: {allSettlements.Count}");
                foreach (var sett in allSettlements)
                {
                    LogMessageThisRun($"[XenoPreview ATTEMPT] Settlement: Label='{sett.Label}', Faction='{sett.Faction?.Name ?? "NULL"}', Tile='{sett.Tile}', Type='{sett.GetType()}'");
                }
                Settlement foundPlayerSettlementForInit = null;
                for (int i = 0; i < allSettlements.Count; i++)
                {
                    if (allSettlements[i].Faction == Faction.OfPlayer)
                    {
                        foundPlayerSettlementForInit = allSettlements[i];
                        LogMessageThisRun($"[XenoPreview ATTEMPT] InitNewGame will likely pick this settlement: {foundPlayerSettlementForInit.Label} at tile {foundPlayerSettlementForInit.Tile}");
                        break;
                    }
                }
                if (foundPlayerSettlementForInit == null)
                {
                    LogWarningCritical("[XenoPreview ATTEMPT] NO PLAYER SETTLEMENT FOUND by iterating Find.WorldObjects.Settlements before InitNewGame. This is problematic.");
                }
                LogMessageThisRun($"[XenoPreview ATTEMPT] Current.Game.InitData.startingTile is set to: {Current.Game.InitData.startingTile}");
                LogMessageThisRun("[XenoPreview ATTEMPT] --- End Pre-InitNewGame Settlement Debug ---");


                // Step 7: InitNewGame
                LogMessageThisRun("[XenoPreview ATTEMPT] Step 7: Calling Current.Game.InitNewGame()...");
                Current.Game.InitNewGame();
                LogMessageThisRun($"[XenoPreview ATTEMPT] InitNewGame finished. gameStartAbsTick={Current.Game.tickManager.gameStartAbsTick}");
                LogMessageThisRun($"[XenoPreview ATTEMPT] Maps count AFTER InitNewGame: {Current.Game.Maps.Count}");

                dummyMap = Current.Game.CurrentMap;
                if (dummyMap == null) { LogErrorCritical("[XenoPreview ATTEMPT] CurrentMap null after InitNewGame. CRITICAL FAILURE."); Cleanup(); return false; }
                dummyParent = dummyMap.Parent ?? playerSettlement;
                LogMessageThisRun($"[XenoPreview ATTEMPT] dummyMap: {dummyMap.GetUniqueLoadID()}, dummyParent: {dummyParent?.GetUniqueLoadID() ?? "null"}");
                LogMessageThisRun("[XenoPreview ATTEMPT] Step 7 DONE.");

                // Step 8: Finalize
                LogMessageThisRun("[XenoPreview ATTEMPT] Step 8: Calling Current.Game.FinalizeInit()...");
                Current.Game.FinalizeInit();
                LogMessageThisRun("[XenoPreview ATTEMPT] Step 8 DONE.");

                active = true;
                LogMessageThisRun("[XenoPreview ATTEMPT] Dummy world creation successful. 'active' set to true."); // Use ATTEMPT here
                _attemptedThisSession = false; // Reset on success, so if window is closed and reopened, it can try again.
                return true;
            }
            catch (Exception ex)
            {
                LogErrorCritical($"[XenoPreview ATTEMPT] Exception during EnsureDummyWorld: {ex.ToString()}");
                Cleanup();
                return false;
            }
        }

        public static void Cleanup()
        {
            LogMessageThisRun("[XenoPreview Cleanup] Cleanup called."); // Use ATTEMPT prefix if this might be part of a failing run

            // Modified logic for when cleanup is called
            bool wasActive = active; // Store current active state
            active = false; // Immediately set active to false to prevent re-entry issues with logging

            try
            {
                if (!wasActive && !_attemptedThisSession)
                {
                    LogMessageThisRun("[XenoPreview Cleanup] Not previously active and no attempt registered this session. Minimal cleanup.");
                }
                else if (!wasActive && _attemptedThisSession)
                {
                    LogWarningCritical("[XenoPreview Cleanup] Cleanup called after a FAILED attempt.");
                }
                else
                { // wasActive was true
                    LogMessageThisRun("[XenoPreview Cleanup] Cleanup for a previously ACTIVE session.");
                }

                LogMessageThisRun("[XenoPreview Cleanup] Attempting to destroy dummyParent and map...");
                dummyParent?.Destroy();
                LogMessageThisRun("[XenoPreview Cleanup] dummyParent?.Destroy() called.");

                if (dummyMap != null && Current.Game?.Maps.Contains(dummyMap) == true)
                {
                    LogMessageThisRun("[XenoPreview Cleanup] Map still exists. De-initializing directly.");
                    Current.Game.DeinitAndRemoveMap(dummyMap, false);
                }

                dummyMap = null;
                dummyParent = null;
                LogMessageThisRun("[XenoPreview Cleanup] dummyMap and dummyParent nulled.");

                LogMessageThisRun("[XenoPreview Cleanup] Resetting static Find fields via reflection...");
                typeof(Find).GetField("currentMapInt", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
                typeof(Find).GetField("worldInt", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
                LogMessageThisRun("[XenoPreview Cleanup] Find fields reset.");

                if (Current.Game != null)
                {
                    LogMessageThisRun("[XenoPreview Cleanup] Nulling Current.Game.");
                    Current.Game = null;
                }
                LogMessageThisRun("[XenoPreview Cleanup] Setting ProgramState to Entry.");
                Current.ProgramState = ProgramState.Entry;
                LogMessageThisRun("[XenoPreview Cleanup] ProgramState set to Entry.");
            }
            catch (Exception ex)
            {
                LogWarningCritical($"[XenoPreview Cleanup] Cleanup error: {ex.ToString()}");
            }
            finally
            {
                // active is already false
                _attemptedThisSession = false; // Reset for a potential future full attempt
                _loggedMessages.Clear(); // Clear "session unique" logs, allowing them on next full cycle.
                Log.Message("[XenoPreview Session] Cleanup finished. Attempt/Session state reset."); // Use Log.Message for this final one.
            }
        }
    }
}