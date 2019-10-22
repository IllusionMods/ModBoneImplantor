using BepInEx.Harmony;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace ModBoneImplantor
{
    /// <summary>
    /// Harmonyによるフック処理をまとめたクラス
    /// </summary>
    public static class Hooks
    {
        /// <summary>
        /// フックメソッドをインストールする
        /// </summary>
        public static void InstallHooks()
        {
            var harmony = new Harmony("com.rclcircuit.bepinex.modboneimplantor");

            HarmonyWrapper.PatchAll(typeof(Hooks));

            // 気になる点
            // 1) 属性指定のみで入れ子クラスをパッチできるのかどうか
            //    ↓をharmony.PatchAll()のように書きたい……
            // 2) コンパイラが分解したコルーチン用のメソッドにパッチしてもいいのかどうか
            //    Unity/Monoがコルーチンの処理で絶対に死なないかどうか
            harmony.Patch(
                AccessTools.Method(typeof(ChaControl).GetNestedType("<LoadCharaFbxDataAsync>c__Iterator13", BindingFlags.NonPublic | BindingFlags.Instance), "MoveNext"),
                null, null, new HarmonyMethod(typeof(Hooks), nameof(LoadCharaFbxDataAsyncTranspiler))
            );
        }

        /// <summary>
        /// InitializeControlLoadObjectのポストフィックス
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ChaControl), "InitializeControlLoadObject")]
        public static void InitializeControlLoadObjectPostfix(ChaControl __instance)
        {
            // 管理用コンポーネントを追加する
            var cim = __instance.gameObject.AddComponent<ChaImplantManager>();
        }

        /// <summary>
        /// LoadCharaFbxDataAsyncのトランスパイラ
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        public static IEnumerable<CodeInstruction> LoadCharaFbxDataAsyncTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var insts = new List<CodeInstruction>(instructions);

            var chaControl = AccessTools.Field(
                typeof(ChaControl).GetNestedType("<LoadCharaFbxDataAsync>c__Iterator13", BindingFlags.NonPublic | BindingFlags.Instance),
                "$this"
            );
            var locvar5 = AccessTools.Field(
                typeof(ChaControl).GetNestedType("<LoadCharaFbxDataAsync>c__Iterator13", BindingFlags.NonPublic | BindingFlags.Instance),
                "$locvar5"
            );
            var loadObj = AccessTools.Field(
                typeof(ChaControl).GetNestedType("<LoadCharaFbxDataAsync>c__Iterator13", BindingFlags.NonPublic | BindingFlags.Instance)
                                    .GetNestedType("<LoadCharaFbxDataAsync>c__AnonStorey20", BindingFlags.NonPublic | BindingFlags.Instance),
                "newObj"
            );
            var assetId = AccessTools.Field(
                typeof(ChaControl).GetNestedType("<LoadCharaFbxDataAsync>c__Iterator13", BindingFlags.NonPublic | BindingFlags.Instance),
                "id"
            );
            var assetName0 = AccessTools.Field(
                typeof(ChaControl).GetNestedType("<LoadCharaFbxDataAsync>c__Iterator13", BindingFlags.NonPublic | BindingFlags.Instance),
                "<assetName>__0"
            );
            var copyWeightsMode = AccessTools.Field(
                typeof(ChaControl).GetNestedType("<LoadCharaFbxDataAsync>c__Iterator13", BindingFlags.NonPublic | BindingFlags.Instance),
                "copyWeights"
            );
            var copyDB = AccessTools.Field(
                typeof(ChaControl).GetNestedType("<LoadCharaFbxDataAsync>c__Iterator13", BindingFlags.NonPublic | BindingFlags.Instance),
                "copyDynamicBone"
            );
            var methodImplantation = AccessTools.Method(typeof(Hooks), nameof(ExecuteImplantation));
            var methodTransfer = AccessTools.Method(typeof(Hooks), nameof(ExecuteRefTransfer));

            // ボーン移植処理の挿入
            for(var i = 1; i < insts.Count; i++)
            {
                var tmpInst = insts[i];
                if(tmpInst.opcode == OpCodes.Ldarg_0 && tmpInst.labels.Count > 0)
                {
                    var prevInst = insts[i - 1].ToString();
                    if(prevInst == "callvirt Void SetParent(UnityEngine.Transform, Boolean)" && insts.Count > i + 1)
                    {
                        insts.InsertRange(i + 1, new[]{
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
						});
                        break;
                    }
                }
            }

            // ボーン参照移し替え処理の挿入
            var idx = insts.FindIndex(
                inst => inst.ToString() == "callvirt Void AssignedWeightsAndSetBounds(UnityEngine.GameObject, System.String, Bounds, UnityEngine.Transform)");
            insts[idx] = new CodeInstruction(OpCodes.Call, methodTransfer);
            insts.InsertRange(idx, new[]{
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, chaControl)
            });

            return insts;
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
            if(copyWeights != 1 || bips == null || bips.Length < 1)
            {
                return copyDynamicBone;
            }

            var cim = cc.gameObject.GetComponent<ChaImplantManager>();
            var aaw = (AssignedAnotherWeights)AccessTools.Field(typeof(ChaControl), "aaWeightsBody").GetValue(cc);
            if(cim == null)
            {
                return copyDynamicBone;
            }

            // キャラエディットで1回読み込んだ後、再度読み込む場合に対応
            cim.DestroyAndClearImplants(id, assetName);

            // BoneImplantProcessによる移植処理
            foreach(var bip in bips)
            {
                // 何らかの理由で失敗したら終了
                if(!bip.Exec((src, dst) => cim.ImplantBones(id, assetName, src, dst, aaw.dictBone)))
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
            if(cim == null || bips == null || bips.Length < 1)
            {
                aaw.AssignedWeightsAndSetBounds(obj, delTopName, bounds, rootBone);
                return;
            }

            // SkinnedMeshRendererのボーン参照を移し替える
            cim.TransferBoneReference(obj, delTopName, bounds, rootBone, aaw.dictBone);
        }
    }
}
