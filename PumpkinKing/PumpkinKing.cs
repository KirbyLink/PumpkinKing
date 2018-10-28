using Harmony;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;
using System.Collections.Generic;

namespace PumpkinKing
{
    /// <summary>The mod entry point.</summary>
    public class ModEntry : Mod
    {
        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            //Harmony patcher
            //https://github.com/KirbyLink/PumpkinKing
            var harmony = HarmonyInstance.Create("com.github.kirbylink.pumpkinking");
            var plantOriginal = typeof(HoeDirt).GetMethod("plant");
            var plantPrefix = helper.Reflection.GetMethod(typeof(PumpkinKing.PlantNearPumpkinKing), "Prefix").MethodInfo;
            var plantPostfix = helper.Reflection.GetMethod(typeof(PumpkinKing.PlantNearPumpkinKing), "Postfix").MethodInfo;
            var removeOriginal = typeof(Tool).GetMethod("DoFunction");
            var removePrefix = helper.Reflection.GetMethod(typeof(PumpkinKing.PickUpPumpkinKing), "Prefix").MethodInfo;
            var removePostfix = helper.Reflection.GetMethod(typeof(PumpkinKing.PickUpPumpkinKing), "Postfix").MethodInfo;
            harmony.Patch(plantOriginal, new HarmonyMethod(plantPrefix), new HarmonyMethod(plantPostfix));
            harmony.Patch(removeOriginal, new HarmonyMethod(removePrefix), new HarmonyMethod(removePostfix));

        }
    }

    public static class PlantNearPumpkinKing
    {
        
        /* Check if planting fertilizer or if the crops has no seasons to grow in or not pumpkins */
        static void Prefix(HoeDirt __instance, bool isFertilizer, int index, int tileX, int tileY, ref bool __state)
        {
            if (__instance != null)
            {
                Crop crop = new Crop(index, tileX, tileX);
                __state = isFertilizer || crop.seasonsToGrowIn.Count == 0 || index != 490;
            }
        }

        static void Postfix(HoeDirt __instance, int index, int tileX, int tileY, Farmer who, ref bool __state, bool __result)
        {
            /*  
             *  Add an additional check to see if there is a Pumpkin King scarecrow in the area.
             *  If so, cut phaseDays by 25%.
            */

            //If state is true, we didn't make it past first two returns in plant() or this crop isn't a pumpkin
            //If result is false, we couldn't plant the seed for some reason
            //If result is true, we have already updated this HoeDirt.crop
            if (!__state && __result)
            {
                //Is the Pumpkin King Scarecrow in the area?
                Farm farm = Game1.getFarm();
                List<Vector2> vector2List = new List<Vector2>();
                foreach(KeyValuePair<Vector2, StardewValley.Object> pair in farm.objects.Pairs)
                {
                    if (pair.Value.bigCraftable.Value && pair.Value.Name.Contains("The Pumpkin King"))
                        vector2List.Add(pair.Key);
                }

                if (vector2List == null || vector2List.Count == 0)
                {
                    return;
                }

                //Reduce phaseDays by 25%
                Crop crop = __instance.crop;
                Vector2 tileVector = new Vector2(tileX, tileY);
                foreach (Vector2 scarecrow in vector2List)
                {
                    if ((double)Vector2.Distance(scarecrow, tileVector) < 9.0)
                    {
                        /* Code from HoeDirt.plant() */
                        int numGrowthDays = 0;
                        for (int index1 = 0; index1 < crop.phaseDays.Count - 1; index1++)
                            numGrowthDays += crop.phaseDays[index1];
                        bool hasOtherModifier = __instance.fertilizer.Value == 465 || __instance.fertilizer.Value == 466 || who.professions.Contains(5);
                        int growthDayChange = (hasOtherModifier ? 3 : 4);
                        int initialOffset = 0;
                        //Set initialOffset based on last phaseDay modified by fertilizer/skill combo
                        if (hasOtherModifier)
                        {
                            if (__instance.fertilizer.Value == 465)
                            {
                                initialOffset = (who.professions.Contains(5) ? 4 : 3);
                            }
                            else if (__instance.fertilizer.Value == 466)
                            {
                                initialOffset = (who.professions.Contains(5) ? 3 : 2);
                            }
                            else
                                initialOffset = 3;
                        }

                        for (int i1 = 0; growthDayChange > 0 && i1 < 3; ++i1)
                        {
                            int i2 = (i1 == 0 ? initialOffset : 0);
                            while (i2 < crop.phaseDays.Count - 1)
                            {
                                if (crop.phaseDays[i2] > 1)
                                {
                                    crop.phaseDays[i2]--;
                                    --growthDayChange;
                                }
                                if (growthDayChange <= 0 || (i2 == crop.phaseDays.Count - 2 && crop.phaseDays[i2] == 1))
                                {
                                    break;
                                }
                                i2++;
                            }
                        }
                    }
                }
            }
        }
    }

    public static class PickUpPumpkinKing
    {

        /* Check if item being removed is The Pumpkin King */
        static void Prefix(Tool __instance, GameLocation location, int x, int y, ref bool __state)
        {
            if (__instance != null && location.IsFarm)
            {
                Vector2 vector = new Vector2((float)(x / 64), (float)(y / 64));
                StardewValley.Object vectorObject = null;
                if (location.objects.ContainsKey(vector))
                {
                    vectorObject = location.objects[vector];
                }
                __state = vectorObject != null && vectorObject.Name.Contains("The Pumpkin King");
            }
        }

        static void Postfix(GameLocation location, int x, int y, Farmer who, bool __state)
        {
            /*  
             *  Remove 25% growth bonus from surrounding pumpkins
            */

            //If state is true, we removed a Pumpkin King on the farm
            if (__state)
            {
                //Find all HoeDirt within radius
                foreach (KeyValuePair<Vector2, TerrainFeature> pair in location.terrainFeatures.Pairs)
                {
                    TerrainFeature pairTerrain = pair.Value;
                    Vector2 scarecrowVector = new Vector2((float)(x / 64), (float)(y / 64));
                    bool isHoeDirt = pairTerrain is HoeDirt;
                    bool isPumpkin = (isHoeDirt ? ((pairTerrain as HoeDirt).crop != null && (pairTerrain as HoeDirt).crop.indexOfHarvest.Value == 276) : false);
                    bool isWithinRange = Vector2.Distance(scarecrowVector, pair.Key) < 9.0;
                    //Increase phaseDays by 25%
                    if (isPumpkin && isWithinRange)
                    {
                        HoeDirt hoeDirt = (pairTerrain as HoeDirt);
                        Crop crop = hoeDirt.crop;
                        bool hadOtherModifier = hoeDirt.fertilizer.Value == 465 || hoeDirt.fertilizer.Value == 466 || who.professions.Contains(5);
                        int phaseDayCount = crop.phaseDays.Count - 1;                        
                        //Reset phaseDays based on last phaseDay modified by fertilizer/skill combo
                        if (hadOtherModifier)
                        {
                            if (hoeDirt.fertilizer.Value == 465 || who.professions.Contains(5))
                            {
                                if (hoeDirt.fertilizer.Value == 465 && who.professions.Contains(5))
                                {
                                    crop.phaseDays[phaseDayCount - 1] = 3;
                                    crop.phaseDays[phaseDayCount - 2] = 3;
                                    crop.phaseDays[phaseDayCount - 3] = 2;
                                }
                                else
                                {
                                    crop.phaseDays[phaseDayCount - 1] = 3;
                                    crop.phaseDays[phaseDayCount - 2] = 4;
                                    crop.phaseDays[phaseDayCount - 3] = 2;
                                }
                            }
                            else 
                            {
                                if (who.professions.Contains(5))
                                {
                                    crop.phaseDays[phaseDayCount - 1] = 2;
                                    crop.phaseDays[phaseDayCount - 2] = 3;
                                }
                                else
                                {
                                    crop.phaseDays[phaseDayCount - 1] = 2;
                                    crop.phaseDays[phaseDayCount - 2] = 3;
                                    crop.phaseDays[phaseDayCount - 3] = 2;
                                }
                            }
                        }
                        else
                        {
                            for(int i = 1; i < crop.phaseDays.Count - 1; i++)
                            {
                                switch (i)
                                {
                                    case 1:
                                        crop.phaseDays[i] = 2;
                                        break;
                                    case 3:
                                        crop.phaseDays[i] = 4;
                                        break;
                                    case 2:
                                    case 4:
                                        crop.phaseDays[i] = 3;
                                        break;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
