using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace WeedKillerFixes
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        const string PLUGIN_GUID = "butterystancakes.lethalcompany.weedkillerfixes", PLUGIN_NAME = "Weed Killer Fixes", PLUGIN_VERSION = "1.0.0";
        internal static new ManualLogSource Logger;

        void Awake()
        {
            Logger = base.Logger;

            new Harmony(PLUGIN_GUID).PatchAll();

            Logger.LogInfo($"{PLUGIN_NAME} v{PLUGIN_VERSION} loaded");
        }
    }

    [HarmonyPatch]
    class WeedKillerFixes
    {
        [HarmonyPatch(typeof(SprayPaintItem), "TrySprayingWeedKillerBottle")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> TransTrySprayingWeedKillerBottle(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();

            MethodInfo gameObjectLayer = AccessTools.DeclaredPropertyGetter(typeof(GameObject), nameof(GameObject.layer));
            MethodInfo timeDeltaTime = AccessTools.DeclaredPropertyGetter(typeof(Time), nameof(Time.deltaTime));
            for (int i = 2; i < codes.Count - 3; i++)
            {
                if (codes[i].opcode == OpCodes.Call)
                {
                    if (codes[i].operand.ToString().Contains("Raycast") && codes[i - 2].opcode == OpCodes.Ldc_I4)
                    {
                        codes[i - 2].operand = (int)codes[i - 2].operand & ~(1 << LayerMask.NameToLayer("Room"));
                        Plugin.Logger.LogDebug("Transpiler: Simplify layer mask");
                    }
                    else if ((MethodInfo)codes[i].operand == timeDeltaTime)
                    {
                        codes[i].opcode = OpCodes.Ldfld;
                        codes[i].operand = AccessTools.Field(typeof(SprayPaintItem), nameof(SprayPaintItem.sprayIntervalSpeed));
                        codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_0));
                        Plugin.Logger.LogDebug("Transpiler: Fix addVehicleHPInterval time");
                    }
                }
                else if (codes[i].opcode == OpCodes.Callvirt && (MethodInfo)codes[i].operand == gameObjectLayer)
                {
                    codes.InsertRange(i + 3, [
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldflda, AccessTools.Field(typeof(SprayPaintItem), "sprayHit")),
                        new CodeInstruction(OpCodes.Call, AccessTools.DeclaredPropertyGetter(typeof(RaycastHit), nameof(RaycastHit.collider))),
                        new CodeInstruction(OpCodes.Ldstr, "MoldSpore"),
                        new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Component), nameof(Component.CompareTag))),
                        new CodeInstruction(OpCodes.Brfalse, codes[i + 2].operand)
                    ]);
                    Plugin.Logger.LogDebug("Transpiler: Check weed tag before killing it");
                }
            }

            return codes;
        }
    }
}