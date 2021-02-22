using System;
using HarmonyLib;
using System.Collections.Generic;
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
            __instance.gameObject.AddComponent<ChaImplantManager>();
        }

        public static IEnumerable<CodeInstruction> LoadCharaFbxDataAsyncTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var chaControl = AccessTools.Field(_iteratorContainerType, "$this");
            var locvar5 = AccessTools.Field(_iteratorContainerType, "$locvar5");
#if KK
            var loadObj = AccessTools.Field(GetNestedType(_iteratorContainerType, "<LoadCharaFbxDataAsync>c__AnonStorey20"), "newObj");
#elif EC
            var loadObj = AccessTools.Field(GetNestedType(_iteratorContainerType, "<LoadCharaFbxDataAsync>c__AnonStorey21"), "newObj");
#endif
            var assetId = AccessTools.Field(_iteratorContainerType, "id");
            var assetName0 = AccessTools.Field(_iteratorContainerType, "<assetName>__0");
            var copyWeightsMode = AccessTools.Field(_iteratorContainerType, "copyWeights");
            var copyDB = AccessTools.Field(_iteratorContainerType, "copyDynamicBone");

            var methodImplantation = AccessTools.Method(typeof(Hooks), nameof(ExecuteImplantation));
            var methodTransfer = AccessTools.Method(typeof(Hooks), nameof(ExecuteRefTransfer));

            var methodSetParent = AccessTools.Method(typeof(Transform), nameof(Transform.SetParent), new[] { typeof(Transform), typeof(bool) });
            var methodAWSP = AccessTools.Method(typeof(AssignedAnotherWeights), nameof(AssignedAnotherWeights.AssignedWeightsAndSetBounds));
            
            return new CodeMatcher(instructions)
                // Attach the ExecuteImplantation method
                .MatchForward(true, new CodeMatch(OpCodes.Callvirt, methodSetParent), new CodeMatch(OpCodes.Ldarg_0))
                .Advance(1)
                .Insert(
                    // Ldarg_0(ラベル付き) ← Stfldの解決に使う
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, chaControl),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, locvar5),
                    new CodeInstruction(OpCodes.Ldfld, loadObj),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, assetId),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, assetName0),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, copyWeightsMode),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, copyDB),
                    new CodeInstruction(OpCodes.Call, methodImplantation),
                    new CodeInstruction(OpCodes.Stfld, copyDB),
                    new CodeInstruction(OpCodes.Ldarg_0) // オリジナルの処理の補完
                )
                // Attach the ExecuteRefTransfer method
                .Start()
                .MatchForward(false, new CodeMatch(OpCodes.Callvirt, methodAWSP))
                .SetInstruction(new CodeInstruction(OpCodes.Call, methodTransfer))
                .Insert(
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, chaControl))
                .Instructions();
        }

        /// <summary>
        /// LoadCharaFbxDataAsyncTranspilerで注入されるメソッド。移植処理のメイン部分を実行する
        /// </summary>
        /// <param name="cc">モデルをロードしたキャラクタの情報</param>
        /// <param name="obj">モデルの最上位のGameObject</param>
        /// <param name="id">モデルのID</param>
        /// <param name="assetName">モデルのアセット名</param>
        /// <param name="copyWeights">0が髪型やアクセサリ、1が衣服</param>
        /// <param name="copyDynamicBone">オリジナルの処理がDynamicBoneをコピーするか否か</param>
        /// <returns>DynamicBoneのコピー処理がまだ必要か否か</returns>
        public static bool ExecuteImplantation(ChaControl cc, GameObject obj, int id, string assetName, byte copyWeights, bool copyDynamicBone)
        {
            // カテゴリが衣服ではない、またはBoneImplantProcessが無ければ終了
            var bips = obj.GetComponentsInChildren<BoneImplantProcess>(true);
            if (copyWeights != 1 || bips == null || bips.Length < 1)
            {
                return copyDynamicBone;
            }

            var cim = cc.gameObject.GetComponent<ChaImplantManager>();
            var aaw = (AssignedAnotherWeights)AccessTools.Field(typeof(ChaControl), "aaWeightsBody").GetValue(cc);
            if (cim == null)
            {
                return copyDynamicBone;
            }

            // キャラエディットで1回読み込んだ後、再度読み込む場合に対応
            cim.DestroyAndClearImplants(id, assetName);

            // BoneImplantProcessによる移植処理
            foreach (var bip in bips)
            {
                // 何らかの理由で失敗したら終了
                if (!bip.Exec((src, dst) => cim.ImplantBones(id, assetName, src, dst, aaw.dictBone)))
                {
                    return copyDynamicBone;
                }
            }

            // 衣服カテゴリかつボーン移植に成功したとき、copyDynamicBoneがfalseの場合を無視してDynamicBoneを再設定する
            cim.ForceRescueDynamicBone(cc, obj, aaw.dictBone);

            // オリジナルのDynamicBone再設定処理を実行しない
            return false;
        }

        /// <summary>
        /// LoadCharaFbxDataAsyncTranspilerで注入されるメソッド。主にボーン参照の移し替えを実行する
        /// </summary>
        /// <param name="aaw">ボーン共有用の情報が含まれるインスタンス</param>
        /// <param name="obj">読み込まれたモデルの最上位のGameObject</param>
        /// <param name="delTopName">削除されるツリーの最上位のGameObject</param>
        /// <param name="bounds">境界情報のインスタンス</param>
        /// <param name="rootBone">SkinnedMeshRendererのrootBoneに設定するTransform</param>
        /// <param name="cc">モデルを読み込んだキャラのChaControlコンポーネント</param>
        public static void ExecuteRefTransfer(AssignedAnotherWeights aaw, GameObject obj, string delTopName, Bounds bounds, Transform rootBone, ChaControl cc)
        {
            // BoneImplantProcessが無ければオリジナルの処理を実行して終了
            var cim = cc.gameObject.GetComponent<ChaImplantManager>();
            var bips = obj.GetComponentsInChildren<BoneImplantProcess>(true);
            if (cim == null || bips == null || bips.Length < 1)
            {
                aaw.AssignedWeightsAndSetBounds(obj, delTopName, bounds, rootBone);
                return;
            }

            // SkinnedMeshRendererのボーン参照を移し替える
            cim.TransferBoneReference(obj, delTopName, bounds, rootBone, aaw.dictBone);
        }
    }
}
