using RimWorld;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Verse;

namespace ReplaceLib
{
	public static class Utils
	{
		public static HashSet<BuildableDef> processedDefs = new HashSet<BuildableDef>();
		public static Dictionary<string, string> thingGroupsByDefNames = new();
		public static Dictionary<ThingDef, ThingDef> thingGroupsByDefs = new();
		public static Dictionary<string, string> terrainGroupsByDefNames = new();
		public static Dictionary<TerrainDef, TerrainDef> terrainGroupsByDefs = new();
		public static Dictionary<ushort, ThingDef> thingDefsByShortHash = new Dictionary<ushort, ThingDef>();
		public static Dictionary<ushort, TerrainDef> terrainDefsByShortHash = new Dictionary<ushort, TerrainDef>();
		public static List<BuildableDef> defsToResolve = new List<BuildableDef>();
		public static void ProcessReplacerDefs()
		{
			foreach (var replacerDef in DefDatabase<ReplacerDef>.AllDefs)
			{
				foreach (var replaceData in replacerDef.replacers)
				{
					if (replaceData.replace != null && replaceData.with != null)
					{
						thingGroupsByDefNames[replaceData.replace] = replaceData.with;
						var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(replaceData.with);
						if (thingDef != null)
						{
							if (thingGroupsByDefs.TryGetValue(thingDef, out var replace) is false)
							{
								replace = DefDatabase<ThingDef>.GetNamedSilentFail(replaceData.replace);
								if (replace != null)
								{
									thingGroupsByDefs[replace] = thingDef;
								}
							}
						}
					}
				}
			}
			
			var defsToProcess = thingGroupsByDefs.Keys.OfType<BuildableDef>().Where(x => x.IsSpawnable()).ToList().ToList();

			foreach (var def in defsToProcess)
			{
				if (def is TerrainDef terrainDef)
				{
					if (terrainDef != terrainDef.GetMainDef())
					{
						if (DefDatabase<TerrainDef>.AllDefsListForReading.Contains(terrainDef))
						{
							defsToResolve.Add(terrainDef);
							DefDatabase<TerrainDef>.Remove(terrainDef);
							DefDatabase<TerrainDef>.defsByName[terrainDef.defName] = terrainDef.GetMainDef();
							terrainDef.shortHash = (ushort)(GenText.StableStringHash(terrainDef.defName) % 65535);
							terrainDefsByShortHash[terrainDef.shortHash] = terrainDef;
						}
					}
				}
				else if (def is ThingDef thingDef)
				{
					if (thingDef != thingDef.GetMainDef())
					{
						if (DefDatabase<ThingDef>.AllDefsListForReading.Contains(thingDef))
						{
							defsToResolve.Add(thingDef);
							DefDatabase<ThingDef>.Remove(thingDef);
							DefDatabase<ThingDef>.defsByName[thingDef.defName] = thingDef.GetMainDef();
							thingDef.shortHash = (ushort)(GenText.StableStringHash(thingDef.defName) % 65535);
							thingDefsByShortHash[thingDef.shortHash] = thingDef;
						}
					}
				}
			}
		}

		public static void ProcessRecipes()
		{
			var defs = DefDatabase<RecipeDef>.AllDefsListForReading.ListFullCopy();
			var processedRecipes = new HashSet<RecipeDef>();
			foreach (var originalRecipe in defs)
			{
				if (processedRecipes.Any(x => x.label == originalRecipe.label && x.products.Count == 1 && originalRecipe.products.Count == 1
					&& x.ProducedThingDef == originalRecipe.ProducedThingDef && x.products[0].count == originalRecipe.products[0].count))
				{
					DefDatabase<RecipeDef>.Remove(originalRecipe);
					originalRecipe.ClearRemovedRecipesFromRecipeUsers();
				}
				processedRecipes.Add(originalRecipe);
			}
		}

		public static void ClearRemovedRecipesFromRecipeUsers(this RecipeDef recipeDef)
		{
			if (recipeDef.recipeUsers != null)
			{
				foreach (var recipeUser in recipeDef.recipeUsers)
				{
					if (recipeUser.allRecipesCached != null)
					{
						for (int i = recipeUser.allRecipesCached.Count - 1; i >= 0; i--)
						{
							if (recipeUser.allRecipesCached[i] == recipeDef)
							{
								recipeUser.allRecipesCached.RemoveAt(i);
							}
						}
					}
				}
			}
		}

		public static Dictionary<BuildableDef, bool> cachedSpawnableResults = new Dictionary<BuildableDef, bool>();
		public static bool IsSpawnable(this BuildableDef def)
		{
			if (!cachedSpawnableResults.TryGetValue(def, out bool result))
			{
				cachedSpawnableResults[def] = result = IsSpawnableInt(def);
			}
			return result;
		}

		public static bool IsSpawnableInt(this BuildableDef def)
		{
			if (def is TerrainDef terrainDef)
			{
				return true;
			}
			else if (def is ThingDef thingDef)
			{
				return IsSpawnableInt(thingDef);
			}
			return false;
		}

		public static bool IsSpawnableInt(ThingDef def)
		{
			try
			{
				if (def.forceDebugSpawnable)
				{
					return true;
				}
				if (def.DerivedFrom(typeof(Corpse)) || def.IsBlueprint || def.IsFrame || def.DerivedFrom(typeof(ActiveTransporter))
					|| def.DerivedFrom(typeof(MinifiedThing)) || def.DerivedFrom(typeof(MinifiedTree)) || def.DerivedFrom(typeof(UnfinishedThing))
					|| def.DerivedFrom(typeof(SignalAction)) || def.destroyOnDrop)
				{
					return false;
				}
				if (def.category == ThingCategory.Item || def.category == ThingCategory.Plant || def.category == ThingCategory.Pawn
					|| def.category == ThingCategory.Building)
				{
					return true;
				}
			}
			catch (Exception ex)
			{
				Log.Error("Caught error processing " + def + ": " + ex.ToString());
			}
			return false;
		}
		public static bool DerivedFrom(this ThingDef thingDef, Type type)
		{
			return type.IsAssignableFrom(thingDef.thingClass);
		}

		public static Def GetMainDef(Def __result)
		{
			if (__result is ThingDef thingDef)
			{
				return thingDef.GetMainDef();
			}
			else if (__result is TerrainDef terrainDef)
			{
				return terrainDef.GetMainDef();
			}
			return __result;
		}
		
		public static ThingDef GetMainDef(this ThingDef def)
		{
			if (def == null) return null;

			if (!thingGroupsByDefs.TryGetValue(def, out var mainDef))
			{
				foreach (var replacerDef in DefDatabase<ReplacerDef>.AllDefs)
				{
					foreach (var replace in replacerDef.replacers)
					{
						if (replace.replace == def.defName)
						{
							var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(replace.with);
							if (thingDef != null)
							{
								thingGroupsByDefs[def] = thingDef;
								mainDef = thingDef;
								break;
							}
						}
					}
					if (mainDef != null) break;
				}
			}

			if (mainDef != null && mainDef != def)
			{
				return mainDef;
			}
			return def;
		}

		public static TerrainDef GetMainDef(this TerrainDef def)
		{
			if (def == null) return null;

			if (!terrainGroupsByDefs.TryGetValue(def, out var mainDef))
			{
				foreach (var replacerDef in DefDatabase<ReplacerDef>.AllDefs)
				{
					foreach (var replace in replacerDef.replacers)
					{
						if (replace.replace == def.defName)
						{
							var terrainDef = DefDatabase<TerrainDef>.GetNamedSilentFail(replace.with);
							if (terrainDef != null)
							{
								terrainGroupsByDefs[def] = terrainDef;
								mainDef = terrainDef;
								break;
							}
						}
					}
					if (mainDef != null) break;
				}
			}

			if (mainDef != null && mainDef != def)
			{
				return mainDef;
			}
			return def;
		}

		public static string GetMainThingDefName(string defName)
		{
			if (defName == null) return null;

			if (!thingGroupsByDefNames.TryGetValue(defName, out var mainDefName))
			{
				foreach (var replacerDef in DefDatabase<ReplacerDef>.AllDefs)
				{
					foreach (var replace in replacerDef.replacers)
					{
						if (replace.replace == defName)
						{
							mainDefName = replace.with;
							thingGroupsByDefNames[defName] = mainDefName;
							break;
						}
					}
					if (mainDefName != null) break;
				}
			}

			if (mainDefName != null && mainDefName != defName)
			{
				return mainDefName;
			}
			return defName;
		}

		public static string GetMainTerrainDefName(string defName)
		{
			if (defName == null) return null;

			if (!terrainGroupsByDefNames.TryGetValue(defName, out var mainDefName))
			{
				foreach (var replacerDef in DefDatabase<ReplacerDef>.AllDefs)
				{
					foreach (var replace in replacerDef.replacers)
					{
						if (replace.replace == defName)
						{
							mainDefName = replace.with;
							terrainGroupsByDefNames[defName] = mainDefName;
							break;
						}
					}
					if (mainDefName != null) break;
				}
			}

			if (mainDefName != null && mainDefName != defName)
			{
				return mainDefName;
			}
			return defName;
		}
	}
}
