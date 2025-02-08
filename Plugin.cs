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
        const string PLUGIN_GUID = "butterystancakes.lethalcompany.weedkillerfixes", PLUGIN_NAME = "Weed Killer Fixes", PLUGIN_VERSION = "1.1.2";
        internal static new ManualLogSource Logger;

        void Awake()
        {
            Logger = base.Logger;

            new Harmony(PLUGIN_GUID).PatchAll();

            Logger.LogInfo($"{PLUGIN_NAME} v{PLUGIN_VERSION} loaded");
        }
    }

    [HarmonyPatch]
    class WeedKillerFixesPatches
    {
        static MoldSpreadManager moldSpreadManager;
        static MoldSpreadManager MoldSpreadManager
        {
            get
            {
                if (moldSpreadManager == null)
                    moldSpreadManager = Object.FindAnyObjectByType<MoldSpreadManager>();

                return moldSpreadManager;
            }
        }
        static VehicleController vehicleController;

        static readonly MethodInfo MOLD_SPREAD_MANAGER_INSTANCE = AccessTools.DeclaredPropertyGetter(typeof(WeedKillerFixesPatches), nameof(MoldSpreadManager));
        static readonly FieldInfo VEHICLE_CONTROLLER_INSTANCE = AccessTools.Field(typeof(WeedKillerFixesPatches), nameof(vehicleController));

        [HarmonyPatch(typeof(SprayPaintItem), nameof(SprayPaintItem.TrySprayingWeedKillerBottle))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> SprayPaintItem_Trans_TrySprayingWeedKillerBottle(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();

            MethodInfo compareTag = AccessTools.Method(typeof(GameObject), nameof(GameObject.CompareTag));
            MethodInfo timeDeltaTime = AccessTools.DeclaredPropertyGetter(typeof(Time), nameof(Time.deltaTime));
            for (int i = 2; i < codes.Count - 3; i++)
            {
                if (codes[i].opcode == OpCodes.Call)
                {
                    string methodName = codes[i].operand.ToString();
                    if (methodName.Contains("Raycast") && codes[i - 2].opcode == OpCodes.Ldc_I4)
                    {
                        codes[i - 2].operand = (int)codes[i - 2].operand & ~(1 << LayerMask.NameToLayer("Room"));
                        Plugin.Logger.LogDebug("Transpiler (SprayPaintItem.TrySprayingWeedKillerBottle): Simplify layer mask");
                    }
                    else if ((MethodInfo)codes[i].operand == timeDeltaTime)
                    {
                        codes[i].opcode = OpCodes.Ldfld;
                        codes[i].operand = AccessTools.Field(typeof(SprayPaintItem), nameof(SprayPaintItem.sprayIntervalSpeed));
                        codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_0));
                        Plugin.Logger.LogDebug("Transpiler (SprayPaintItem.TrySprayingWeedKillerBottle): Fix addVehicleHPInterval time");
                    }
                    else if (methodName.Contains("VehicleController"))
                    {
                        codes[i].opcode = OpCodes.Ldsfld;
                        codes[i].operand = VEHICLE_CONTROLLER_INSTANCE;
                        Plugin.Logger.LogDebug($"Transpiler (SprayPaintItem.TrySprayingWeedKillerBottle): Cache Cruiser script");
                    }
                }
                else if (codes[i].opcode == OpCodes.Ldstr && (string)codes[i].operand == "MoldSporeCollider" && codes[i + 1].opcode == OpCodes.Callvirt && (MethodInfo)codes[i + 1].operand == compareTag)
                {
                    codes[i].operand = "MoldSpore";
                    Plugin.Logger.LogDebug("Transpiler (SprayPaintItem.TrySprayingWeedKillerBottle): Fix wrong tag being checked");
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(SprayPaintItem), nameof(SprayPaintItem.KillWeedClientRpc))]
        [HarmonyPatch(typeof(SprayPaintItem), nameof(SprayPaintItem.LateUpdate))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> CacheMoldSpreadManager(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            List<CodeInstruction> codes = instructions.ToList();

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Call)
                {
                    string methodName = codes[i].operand.ToString();
                    if (methodName.Contains("FindObjectOfType") && methodName.Contains("MoldSpreadManager"))
                    {
                        codes[i].operand = MOLD_SPREAD_MANAGER_INSTANCE;
                        Plugin.Logger.LogDebug($"Transpiler ({__originalMethod.DeclaringType}.{__originalMethod.Name}): Cache weed script");
                    }
                }
            }

            //Plugin.Logger.LogWarning($"{__originalMethod.Name} transpiler failed");
            return codes;
        }

        [HarmonyPatch(typeof(VehicleController), "Awake")]
        [HarmonyPostfix]
        static void VehicleController_Post_Awake(VehicleController __instance)
        {
            if (vehicleController == null)
                vehicleController = __instance;
        }
    }
}