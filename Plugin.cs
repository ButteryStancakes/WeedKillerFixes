using BepInEx;
using BepInEx.Bootstrap;
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
    [BepInDependency(GUID_LOBBY_COMPATIBILITY, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        internal const string PLUGIN_GUID = "butterystancakes.lethalcompany.weedkillerfixes", PLUGIN_NAME = "Weed Killer Fixes", PLUGIN_VERSION = "1.2.0";
        internal static new ManualLogSource Logger;

        const string GUID_LOBBY_COMPATIBILITY = "BMX.LobbyCompatibility";

        void Awake()
        {
            Logger = base.Logger;

            if (Chainloader.PluginInfos.ContainsKey(GUID_LOBBY_COMPATIBILITY))
            {
                Logger.LogInfo("CROSS-COMPATIBILITY - Lobby Compatibility detected");
                LobbyCompatibility.Init();
            }

            new Harmony(PLUGIN_GUID).PatchAll();

            Logger.LogInfo($"{PLUGIN_NAME} v{PLUGIN_VERSION} loaded");
        }
    }

    [HarmonyPatch]
    static class WeedKillerFixesPatches
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
        static CadaverGrowthAI cadaverGrowthAI;

        static readonly MethodInfo MOLD_SPREAD_MANAGER_INSTANCE = AccessTools.DeclaredPropertyGetter(typeof(WeedKillerFixesPatches), nameof(MoldSpreadManager));
        static readonly FieldInfo VEHICLE_CONTROLLER_INSTANCE = AccessTools.Field(typeof(WeedKillerFixesPatches), nameof(vehicleController));
        static readonly FieldInfo CADAVER_GROWTH_AI_INSTANCE = AccessTools.Field(typeof(WeedKillerFixesPatches), nameof(cadaverGrowthAI));

        [HarmonyPatch(typeof(SprayPaintItem), nameof(SprayPaintItem.TrySprayingWeedKillerBottle))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> SprayPaintItem_Trans_TrySprayingWeedKillerBottle(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();

            MethodInfo timeDeltaTime = AccessTools.DeclaredPropertyGetter(typeof(Time), nameof(Time.deltaTime));
            for (int i = 2; i < codes.Count - 3; i++)
            {
                if (codes[i].opcode == OpCodes.Call)
                {
                    if ((MethodInfo)codes[i].operand == timeDeltaTime)
                    {
                        codes[i].opcode = OpCodes.Ldfld;
                        codes[i].operand = AccessTools.Field(typeof(SprayPaintItem), nameof(SprayPaintItem.sprayIntervalSpeed));
                        codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_0));
                        Plugin.Logger.LogDebug("Transpiler (SprayPaintItem.TrySprayingWeedKillerBottle): Fix addVehicleHPInterval time");
                    }
                    else
                    {
                        string methodName = codes[i].operand.ToString();
                        if (methodName.Contains("FindObjectOfType") && methodName.Contains("VehicleController"))
                        {
                            codes[i].opcode = OpCodes.Ldsfld;
                            codes[i].operand = VEHICLE_CONTROLLER_INSTANCE;
                            Plugin.Logger.LogDebug($"Transpiler (SprayPaintItem.TrySprayingWeedKillerBottle): Cache Cruiser script");
                        }
                    }
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(SprayPaintItem), nameof(SprayPaintItem.Start))]
        [HarmonyPatch(typeof(SprayPaintItem), nameof(SprayPaintItem.CheckForWeedsInSprayPath))]
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

        [HarmonyPatch(typeof(SprayPaintItem), nameof(SprayPaintItem.LateUpdate))]
        [HarmonyPatch(typeof(SprayPaintItem), nameof(SprayPaintItem.TrySprayingWeedKillerOnLocalPlayer))]
        [HarmonyPatch(typeof(SprayPaintItem), nameof(SprayPaintItem.CheckForCadaverPlantsInSprayPath))]
        [HarmonyPatch(typeof(SprayPaintItem), nameof(SprayPaintItem.KillCadaverPlantRpc))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> CacheCadaverGrowthAI(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            List<CodeInstruction> codes = instructions.ToList();

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Call)
                {
                    string methodName = codes[i].operand.ToString();
                    if (methodName.Contains("FindObjectOfType") && methodName.Contains("CadaverGrowthAI"))
                    {
                        codes[i].opcode = OpCodes.Ldsfld;
                        codes[i].operand = CADAVER_GROWTH_AI_INSTANCE;
                        Plugin.Logger.LogDebug($"Transpiler ({__originalMethod.DeclaringType}.{__originalMethod.Name}): Cache Cadaver script");
                    }
                }
            }

            //Plugin.Logger.LogWarning($"{__originalMethod.Name} transpiler failed");
            return codes;
        }

        [HarmonyPatch(typeof(VehicleController), nameof(VehicleController.Awake))]
        [HarmonyPostfix]
        static void VehicleController_Post_Awake(VehicleController __instance)
        {
            if (vehicleController == null)
                vehicleController = __instance;
        }

        [HarmonyPatch(typeof(CadaverGrowthAI), nameof(CadaverGrowthAI.Start))]
        [HarmonyPostfix]
        static void CadaverGrowthAI_Post_Start(CadaverGrowthAI __instance)
        {
            if (cadaverGrowthAI == null)
                cadaverGrowthAI = __instance;
        }
    }
}