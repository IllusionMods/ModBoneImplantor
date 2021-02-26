using HarmonyLib;
using UnityEngine;

namespace ModBoneImplantor
{
    public partial class ModBoneImplantor
    {
        internal static class Hooks
        {
            public static void InstallHooks()
            {
                Harmony.CreateAndPatchAll(typeof(Hooks), GUID);
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(AssignedAnotherWeights), nameof(AssignedAnotherWeights.AssignedWeightsAndSetBounds))]
            private static bool AssignedWeightsAndSetBoundsHook(AssignedAnotherWeights __instance, GameObject obj, string delTopName, Bounds bounds, Transform rootBone)
            {
#if DEBUG
                Console.WriteLine("AssignedWeightsAndSetBoundsHook");
#endif
                return !AssignWeightsAndImplantBones(__instance, obj, delTopName, bounds, rootBone);
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(AssignedAnotherWeights), nameof(AssignedAnotherWeights.AssignedWeights))]
            private static bool AssignedWeightsHook(AssignedAnotherWeights __instance, GameObject obj, string delTopName, Transform rootBone)
            {
#if DEBUG
                Console.WriteLine("AssignedWeightsHook");
#endif
                return !AssignWeightsAndImplantBones(__instance, obj, delTopName, default(Bounds), rootBone);
            }
        }
    }
}