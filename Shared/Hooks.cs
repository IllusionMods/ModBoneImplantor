using System;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace ModBoneImplantor
{
    internal static class Hooks
    {
        private static Type _iteratorContainerType;

        private static Type GetNestedType(Type baseType, string nestedTypeName)
        {
            return baseType.GetNestedType(nestedTypeName, AccessTools.all) ?? throw new MissingMemberException("Failed to find " + nestedTypeName);
        }

        public static void InstallHooks()
        {
            _iteratorContainerType = GetNestedType(typeof(ChaControl), "<LoadCharaFbxDataAsync>c__Iterator13");

            var harmony = Harmony.CreateAndPatchAll(typeof(Hooks), ModBoneImplantor.GUID);

            harmony.Patch(AccessTools.Method(_iteratorContainerType, "MoveNext"),
                transpiler: new HarmonyMethod(typeof(Hooks), nameof(LoadCharaFbxDataAsyncTranspiler)));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ChaControl), "InitializeControlLoadObject")]
        public static void InitializeControlLoadObjectPostfix(ChaControl __instance)
        {
            //todo This can create multiple copies? Should only one copy exist?
            __instance.gameObject.AddComponent<ChaImplantManager>();
        }

        private static IEnumerable<CodeInstruction> LoadCharaFbxDataAsyncTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var chaControlF = AccessTools.Field(_iteratorContainerType, "$this");
#if KK
            var loadObjF = AccessTools.Field(GetNestedType(_iteratorContainerType, "<LoadCharaFbxDataAsync>c__AnonStorey20"), "newObj");
#elif EC
            var loadObjF = AccessTools.Field(GetNestedType(_iteratorContainerType, "<LoadCharaFbxDataAsync>c__AnonStorey21"), "newObj");
#endif
            var copyDbF = AccessTools.Field(_iteratorContainerType, "copyDynamicBone");

            return new CodeMatcher(instructions)
                // Attach the ExecuteImplantation method
                .MatchForward(true,
                    new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(Transform), nameof(Transform.SetParent), new[] { typeof(Transform), typeof(bool) })),
                    new CodeMatch(OpCodes.Ldarg_0))
                .Advance(1)
                .Insert(
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, chaControlF),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(_iteratorContainerType, "$locvar5")),
                    new CodeInstruction(OpCodes.Ldfld, loadObjF),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(_iteratorContainerType, "id")),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(_iteratorContainerType, "<assetName>__0")),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(_iteratorContainerType, "copyWeights")),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, copyDbF),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Hooks), nameof(ExecuteImplantation))),
                    new CodeInstruction(OpCodes.Stfld, copyDbF), // Override the copyDynamicBone field with return of the method
                    new CodeInstruction(OpCodes.Ldarg_0) // Go back to a valid stack state
                )
                // Attach the ExecuteRefTransfer method
                .Start()
                .MatchForward(false, new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(AssignedAnotherWeights), nameof(AssignedAnotherWeights.AssignedWeightsAndSetBounds))))
                .SetInstruction(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Hooks), nameof(CustomAssignedWeightsAndSetBounds)))) // Replace first method call only
                .Insert( // Add instructions before the replaced method to feed it an extra ChaControl parameter
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, chaControlF))
                .Instructions();
        }

        private static bool ExecuteImplantation(ChaControl cc, GameObject obj, int id, string assetName, byte copyWeights, bool copyDynamicBone)
        {
            // copyWeights = 0 for accessories, 1 for clothes (cf_j_root), 2 for cf_J_N_FaceRoot
            if (copyWeights != 1)
                return copyDynamicBone;

            var cim = cc.gameObject.GetComponent<ChaImplantManager>();
            if (cim == null)
                return copyDynamicBone;

            var bips = obj.GetComponentsInChildren<BoneImplantProcess>(true);
            if (bips.IsNullOrEmpty())
                return copyDynamicBone;

            // Support for loading once and then loading again in character edit
            // キャラエディットで1回読み込んだ後、再度読み込む場合に対応
            cim.DestroyAndClearImplants(id, assetName);

            var aaw = (AssignedAnotherWeights)AccessTools.Field(typeof(ChaControl), "aaWeightsBody").GetValue(cc);

            foreach (var bip in bips)
            {
                if (!cim.ImplantBones(id, assetName, bip, aaw))
                    return copyDynamicBone;
            }

            // Ignore the case where copyDynamicBone is false and reconfigure DynamicBone when the garment category and bone transplant is successful.
            // 衣服カテゴリかつボーン移植に成功したとき、copyDynamicBoneがfalseの場合を無視してDynamicBoneを再設定する
            cim.ForceRescueDynamicBone(cc, obj, aaw.dictBone);

            // Do not execute the original DynamicBone reconfiguration process.
            // オリジナルのDynamicBone再設定処理を実行しない
            return false;
        }

        private static void CustomAssignedWeightsAndSetBounds(AssignedAnotherWeights aaw, GameObject obj, string delTopName, Bounds bounds, Transform rootBone, ChaControl cc)
        {
            var cim = cc.gameObject.GetComponent<ChaImplantManager>();
            // todo any better way than running GetComponentsInChildren?
            if (cim == null || obj.GetComponentsInChildren<BoneImplantProcess>(true).IsNullOrEmpty())
            {
                // Run the original method
                aaw.AssignedWeightsAndSetBounds(obj, delTopName, bounds, rootBone);
            }
            else
            {
                // Run our replacement method
                cim.TransferBoneReference(obj, delTopName, bounds, rootBone, aaw.dictBone);
            }
        }
    }
}
