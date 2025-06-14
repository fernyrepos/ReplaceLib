using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ReplaceLib
{
    [StaticConstructorOnStartup]
    public static class Startup
    {
        static Startup()
        {
            Utils.ProcessReplacerDefs();
            Debug();
        }

        public static void Debug()
        {
            foreach (var recipe in DefDatabase<RecipeDef>.AllDefs)
            {
                if (recipe.ProducedThingDef != null)
                {
                    var mainDef = recipe.ProducedThingDef.GetMainDef();
                    if (mainDef != recipe.ProducedThingDef)
                    {
                        Log.Error(recipe + " has a produced thing def not replaced: " + recipe.ProducedThingDef + ", main thing def: " + mainDef);
                    }
                }
                if (recipe.defaultIngredientFilter != null)
                {
                    DebugThingFilter(recipe, recipe.defaultIngredientFilter);
                }
                if (recipe.fixedIngredientFilter != null)
                {
                    DebugThingFilter(recipe, recipe.fixedIngredientFilter);
                }
                if (recipe.ingredients != null)
                {
                    foreach (var ingredient in recipe.ingredients)
                    {
                        DebugThingFilter(recipe, ingredient.filter);
                    }
                }
            }
        }

        private static void DebugThingFilter(RecipeDef recipe, ThingFilter thingFilter)
        {
            foreach (var thingDef in (thingFilter.thingDefs ?? new List<ThingDef>()))
            {
                var mainDef = thingDef.GetMainDef();
                if (mainDef != null)
                {
                    if (mainDef != thingDef)
                    {
                        Log.Error(recipe + " has an allowed thing def not replaced: " + thingDef + ", main thing def: " + mainDef);
                    }
                }
            }
        }
    }
}