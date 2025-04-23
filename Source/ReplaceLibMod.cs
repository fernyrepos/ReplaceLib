using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml;
using HarmonyLib;
using RimWorld;
using Verse;
using static Verse.DirectXmlCrossRefLoader;

namespace ReplaceLib
{
	public class ReplaceLibMod : Mod
	{
		public ReplaceLibMod(ModContentPack pack) : base(pack)
		{
			new Harmony("ReplaceLibMod").PatchAll();
		}
	}


	[HarmonyPatch(typeof(Game), "FinalizeInit")]
	public class Game_FinalizeInit_Patch
	{
		public static void Postfix()
		{
			Utils.ProcessReplacerDefs();
			Utils.ProcessRecipes();
		}
	}

	[HarmonyPatch(typeof(DefGenerator), "GenerateImpliedDefs_PostResolve")]
	public class DefGenerator_GenerateImpliedDefs_PostResolve_Patch
	{
		public static void Postfix()
		{
			foreach (var def in Utils.defsToResolve)
			{
				def.ResolveReferences();
			}
		}
	}

	[HarmonyPatch(typeof(DirectXmlCrossRefLoader), "ResolveAllWantedCrossReferences")]
	public static class DirectXmlCrossRefLoader_ResolveAllWantedCrossReferences
	{
		public static bool processedDefs;
		public static void Prefix()
		{
			if (processedDefs is false)
			{
				Utils.ProcessReplacerDefs();
				processedDefs = true;
			}
			foreach (var wanted in wantedRefs)
			{
				if (wanted is WantedRefForObject wantedRefForObject)
				{
					var defType = wantedRefForObject.overrideFieldType ?? wantedRefForObject.fi.FieldType;
					if (typeof(ThingDef).IsAssignableFrom(defType))
					{
						wantedRefForObject.defName = Utils.GetMainThingDefName(wantedRefForObject.defName);
					}
					else if (typeof(TerrainDef).IsAssignableFrom(defType))
					{
						wantedRefForObject.defName = Utils.GetMainTerrainDefName(wantedRefForObject.defName);
					}
				}
				else
				{
					var genericTypeDefinition = wanted.GetType().GetGenericTypeDefinition();
					if (genericTypeDefinition == typeof(WantedRefForList<>))
					{
						var defType = wanted.GetType().GetGenericArguments()[0];
						if (typeof(ThingDef).IsAssignableFrom(defType))
						{
							var field = Traverse.Create(wanted).Field("defNames");
							var defNames = field.GetValue() as List<string>;
							var newDefNames = new List<string>();
							for (int i = 0; i < defNames.Count; i++)
							{
								string defName = defNames[i];
								newDefNames.Add(Utils.GetMainThingDefName(defName));
							}
							field.SetValue(newDefNames);
						}
						else if (typeof(TerrainDef).IsAssignableFrom(defType))
						{
							var field = Traverse.Create(wanted).Field("defNames");
							var defNames = field.GetValue() as List<string>;
							var newDefNames = new List<string>();
							for (int i = 0; i < defNames.Count; i++)
							{
								string defName = defNames[i];
								newDefNames.Add(Utils.GetMainTerrainDefName(defName));
							}
							field.SetValue(newDefNames);
						}
					}
					else if (genericTypeDefinition == typeof(WantedRefForDictionary<,>))
					{
						var key = wanted.GetType().GetGenericArguments()[0];
						var value = wanted.GetType().GetGenericArguments()[1];
						bool keyIsDef = typeof(BuildableDef).IsAssignableFrom(key);
						bool valueIsDef = typeof(BuildableDef).IsAssignableFrom(value);
						if (keyIsDef || valueIsDef)
						{
							var field = Traverse.Create(wanted).Field("wantedDictRefs");
							var wantedDictRefs = field.GetValue() as List<XmlNode>;
							foreach (var wantedDictRef in wantedDictRefs)
							{
								if (keyIsDef)
								{
									XmlNode xmlNode = wantedDictRef["key"];
									string text = xmlNode.InnerText;
									xmlNode.InnerText = typeof(ThingDef).IsAssignableFrom(key) ? Utils.GetMainThingDefName(text)
										: Utils.GetMainTerrainDefName(text);
								}
								if (valueIsDef)
								{
									XmlNode xmlNode2 = wantedDictRef["value"];
									string text = xmlNode2.InnerText;
									xmlNode2.InnerText = typeof(ThingDef).IsAssignableFrom(value) ? Utils.GetMainThingDefName(text)
										: Utils.GetMainTerrainDefName(text);
								}
							}
						}
					}
				}
			}
		}
	}

	[HarmonyPatch(typeof(ThingDef), "Named")]
	public static class ThingDef_Named
	{
		public static void Postfix(ref ThingDef __result)
		{
			if (__result != null)
			{
				__result = Utils.GetMainDef(__result);
			}
		}
	}

	[HarmonyPatch(typeof(TerrainDef), "Named")]
	public static class TerrainDef_Named
	{
		public static void Postfix(ref TerrainDef __result)
		{
			if (__result != null)
			{
				__result = Utils.GetMainDef(__result);
			}
		}
	}

	[HarmonyPatch(typeof(GenDefDatabase), "GetDef")]
	public static class GenDefDatabase_GetDef
	{
		public static void Postfix(ref Def __result)
		{
			__result = Utils.GetMainDef(__result);
		}
	}

	[HarmonyPatch(typeof(GenDefDatabase), "GetDefSilentFail")]
	public static class GenDefDatabase_GetDefSilentFail
	{
		public static void Postfix(ref Def __result)
		{
			__result = Utils.GetMainDef(__result);
		}
	}

	[HarmonyPatch(typeof(ThingDefCountClass), MethodType.Constructor, new Type[] { typeof(ThingDef), typeof(int) })]
	public static class ThingDefCountClass_GetDefSilentFail
	{
		public static void Prefix(ref ThingDef thingDef)
		{
			thingDef = thingDef.GetMainDef();
		}
	}

	[HarmonyPatch(typeof(ThingMaker), "MakeThing")]
	public static class ThingMaker_MakeThing
	{
		public static void Prefix(ref ThingDef def, ref ThingDef stuff)
		{
			if (def != null)
			{
				def = def.GetMainDef();
			}
			if (stuff != null)
			{
				stuff = stuff.GetMainDef();
			}
		}
	}

	[HarmonyPatch(typeof(BackCompatibility), "BackCompatibleDefName")]
	public static class BackCompatibility_BackCompatibleDefName
	{
		public static void Postfix(Type defType, string defName, ref string __result)
		{
			if (typeof(ThingDef).IsAssignableFrom(defType))
			{
				__result = Utils.GetMainThingDefName(defName);
			}
			else if (typeof(TerrainDef).IsAssignableFrom(defType))
			{
				__result = Utils.GetMainTerrainDefName(defName);
			}
		}
	}

	[HarmonyPatch(typeof(BackCompatibility), "BackCompatibleThingDefWithShortHash_Force")]
	public class BackCompatibility_BackCompatibleThingDefWithShortHash_Force_Patch
	{
		public static void Postfix(ref ThingDef __result, ushort hash, int major, int minor)
		{
			if (Utils.thingDefsByShortHash.TryGetValue(hash, out var thingDef))
			{
				if (thingDef.GetMainDef() != thingDef)
				{
					__result = thingDef.GetMainDef();
				}
			}
		}
	}

	[HarmonyPatch(typeof(BackCompatibility), "BackCompatibleTerrainWithShortHash")]
	public class BackCompatibility_BackCompatibleTerrainWithShortHash_Patch
	{
		public static void Postfix(ref TerrainDef __result, ushort hash)
		{
			if (Utils.terrainDefsByShortHash.TryGetValue(hash, out var terrainDef))
			{
				if (terrainDef.GetMainDef() != terrainDef)
				{
					__result = terrainDef.GetMainDef();
				}
			}
		}
	}

	[HarmonyPatch(typeof(DirectXmlCrossRefLoader), "RegisterObjectWantsCrossRef",
 		new Type[] { typeof(object), typeof(FieldInfo), typeof(string), typeof(string), typeof(string), typeof(Type) })]
	public class DirectXmlCrossRefLoader_RegisterObjectWantsCrossRef_PatchOne
	{
		public static void Prefix(object wanter, FieldInfo fi, ref string targetDefName, string mayRequireMod = null, string mayRequireAnyMod = null, Type assumeFieldType = null)
		{
			var type = assumeFieldType ?? fi.FieldType;
			if (typeof(ThingDef).IsAssignableFrom(type))
			{
				targetDefName = Utils.GetMainThingDefName(targetDefName);
			}
			else if (typeof(TerrainDef).IsAssignableFrom(type))
			{
				targetDefName = Utils.GetMainTerrainDefName(targetDefName);
			}
		}
	}

	[HarmonyPatch(typeof(DirectXmlCrossRefLoader), "RegisterObjectWantsCrossRef",
 		new Type[] { typeof(object), typeof(string), typeof(string), typeof(string), typeof(string), typeof(Type) })]
	public class DirectXmlCrossRefLoader_RegisterObjectWantsCrossRef_PatchTwo
	{
		public static void Prefix(object wanter, string fieldName, ref string targetDefName, Type overrideFieldType = null)
		{
			var type = overrideFieldType ?? wanter.GetType().GetField(fieldName, AccessTools.all).FieldType;
			if (typeof(ThingDef).IsAssignableFrom(type))
			{
				targetDefName = Utils.GetMainThingDefName(targetDefName);
			}
			else if (typeof(TerrainDef).IsAssignableFrom(type))
			{
				targetDefName = Utils.GetMainTerrainDefName(targetDefName);
			}
		}
	}

	[HarmonyPatch(typeof(DirectXmlCrossRefLoader), "RegisterObjectWantsCrossRef",
 		new Type[] { typeof(object), typeof(string), typeof(XmlNode), typeof(string), typeof(string), typeof(Type) })]
	public class DirectXmlCrossRefLoader_RegisterObjectWantsCrossRef_PatchThree
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var codes = instructions.ToList();
			var getName = AccessTools.PropertyGetter(typeof(XmlNode), "Name");
			foreach (var code in codes)
			{
				yield return code;
				if (code.Calls(getName))
				{
					yield return new CodeInstruction(OpCodes.Ldarg_0);
					yield return new CodeInstruction(OpCodes.Ldarg_1);
					yield return new CodeInstruction(OpCodes.Ldarg_S, 5);
					yield return new CodeInstruction(OpCodes.Call,
						AccessTools.Method(typeof(DirectXmlCrossRefLoader_RegisterObjectWantsCrossRef_PatchThree), nameof(ReturnName)));
				}
			}
		}

		public static string ReturnName(string name, object wanter, string fieldName, Type overrideFieldType = null)
		{
			var type = overrideFieldType ?? wanter.GetType().GetField(fieldName, AccessTools.all).FieldType;
			if (typeof(ThingDef).IsAssignableFrom(type))
			{
				name = Utils.GetMainThingDefName(name);
			}
			else if (typeof(TerrainDef).IsAssignableFrom(type))
			{
				name = Utils.GetMainTerrainDefName(name);
			}
			return name;
		}
	}
}