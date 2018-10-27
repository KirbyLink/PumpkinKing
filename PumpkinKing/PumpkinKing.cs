using Harmony;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.TerrainFeatures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace FestivalEndTimeTweak
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
            var original = typeof(HoeDirt).GetMethod("plant");
            var prefix = helper.Reflection.GetMethod(typeof(FestivalEndTimeTweak.ChangeFestivalEndTime), "Prefix").MethodInfo;
            var postfix = helper.Reflection.GetMethod(typeof(FestivalEndTimeTweak.ChangeFestivalEndTime), "Postfix").MethodInfo;
            harmony.Patch(original, new HarmonyMethod(prefix), new HarmonyMethod(postfix));

        }
    }

    public static class ChangeFestivalEndTime
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
                        for (int index1 = 0; index1 < crop.phaseDays.Count - 1; ++index1)
                            numGrowthDays += crop.phaseDays[index1];
                        bool hasOtherModifier = __instance.fertilizer.Value == 465 || __instance.fertilizer.Value == 466 || who.professions.Contains(5);
                        int growthDayChange = (hasOtherModifier ? 3 : 4);
                        for (int i1 = 0; growthDayChange > 0 && i1 < 3; ++i1)
                        {
                            for (int i2 = 0; i2 < crop.phaseDays.Count; ++i2)
                            {
                                if (i2 > 0 || crop.phaseDays[i2] > 1)
                                {
                                    crop.phaseDays[i2]--;
                                    --growthDayChange;
                                }
                                if (growthDayChange <= 0)
                                    break;
                            }
                        }
                    }
                }
            }
        }
    }
}
