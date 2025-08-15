using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace SurviveButFreezeRegrowables
{
    /// <summary>
    /// SMAPI mod: Crops survive season changes, regrowables freeze outside planted season.
    /// Fully configurable via config.json for year-round locations, winter kill, and debug logging.
    /// </summary>
    public class ModEntry : Mod
    {
        private const string KeyPlantedSeason = "StyStatic.PlantedSeason";

        private ModConfig Config;

        public override void Entry(IModHelper helper)
        {
            // Load config
            Config = helper.ReadConfig<ModConfig>();
             
            // Events
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.World.TerrainFeatureListChanged += OnTerrainFeatureListChanged;
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            // Backfill planted seasons for existing crops
            foreach (var loc in Game1.locations)
                TagMissingPlantedSeasons(loc, Game1.currentSeason);
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            foreach (var loc in Game1.locations)
            {
                foreach (var kvp in loc.terrainFeatures.Pairs)
                {
                    if (!(kvp.Value is HoeDirt dirt) || dirt.crop == null)
                        continue;

                    var crop = dirt.crop;

                    if (!crop.modData.ContainsKey(KeyPlantedSeason))
                        crop.modData[KeyPlantedSeason] = Game1.currentSeason;

                    string plantedSeason = crop.modData[KeyPlantedSeason];
                    bool outOfSeason = !string.Equals(Game1.currentSeason, plantedSeason, StringComparison.OrdinalIgnoreCase);

                    // Winter kill
                    if (Config.WinterKillsCrops && Game1.currentSeason == "winter" && !IsYearRoundLocation(loc))
                        continue;

                    // Make sure crop survives
                    crop.dead.Value = false;

                    // Only apply **extra growth** if out-of-season
                    if (outOfSeason)
                    {
                        // Increment day of phase manually
                        crop.dayOfCurrentPhase.Value++;

                        // If phase is complete, move to next
                        while (crop.dayOfCurrentPhase.Value >= crop.phaseDays[crop.currentPhase.Value])
                        {
                            crop.dayOfCurrentPhase.Value -= crop.phaseDays[crop.currentPhase.Value];
                            crop.currentPhase.Value++;
                        }

                        // For regrowables: stop at the last visual growth phase
                        if (crop.RegrowsAfterHarvest() && !IsYearRoundLocation(loc))
                        {
                            crop.currentPhase.Value = Math.Min(crop.currentPhase.Value, crop.phaseDays.Count - 2);
                        }

                        // Don’t mark fully grown yet, so it keeps growing
                        crop.fullyGrown.Value = false;
                    }
                    else
                    {
                        // In-season crops: let vanilla logic handle fully grown
                        if (crop.currentPhase.Value >= crop.phaseDays.Count - 1)
                            crop.fullyGrown.Value = true;
                    }
                }
            }
        }





        private void OnTerrainFeatureListChanged(object? sender, TerrainFeatureListChangedEventArgs e)
        {
            if (e.Added == null) return;

            foreach (var kvp in e.Added)
            {
                if (!(kvp.Value is HoeDirt dirt) || dirt.crop == null)
                    continue;

                if (!dirt.crop.modData.ContainsKey(KeyPlantedSeason))
                    dirt.crop.modData[KeyPlantedSeason] = Game1.currentSeason;
            }
        }

        private void TagMissingPlantedSeasons(GameLocation loc, string season)
        {
            foreach (var kvp in loc.terrainFeatures.Pairs)
            {
                if (!(kvp.Value is HoeDirt dirt) || dirt.crop == null)
                    continue;

                if (!dirt.crop.modData.ContainsKey(KeyPlantedSeason))
                    dirt.crop.modData[KeyPlantedSeason] = season;
            }
        }

        /// <summary>
        /// Determines if a location allows year-round crop production.
        /// Configurable via config.json.
        /// </summary>
        private bool IsYearRoundLocation(GameLocation loc)
        {
            if (loc == null) return false;

            string name = loc.NameOrUniqueName;

            foreach (string locName in Config.YearRoundLocations)
            {
                if (name.Equals(locName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Config class for SurviveButFreezeRegrowables
    /// </summary>
    public class ModConfig
    {
        /// <summary>List of locations where crops can grow year-round</summary>
        public List<string> YearRoundLocations { get; set; } = new List<string> { "Greenhouse", "IslandWest", "IslandNorth", "IslandFarm" };

        /// <summary>Set to true if winter should still kill crops</summary>
        public bool WinterKillsCrops { get; set; } = false;

        /// <summary>Enable debug logging for crop freezes</summary>
        public bool DebugLogging { get; set; } = false;
    }
}