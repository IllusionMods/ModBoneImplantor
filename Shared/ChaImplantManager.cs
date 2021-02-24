using BepInEx.Logging;
using IllusionUtility.GetUtility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ModBoneImplantor
{
    public class ChaImplantManager : MonoBehaviour
    {
        private readonly List<ImplantInfo> _implantInfos = new List<ImplantInfo>();

        /// <summary>
        /// Attaches extra bones defined in BoneImplantProcess to the body bone structure to be used for later.
        /// Returns whether the port was successful or not 移植成功したかどうか
        /// </summary>
        public bool ImplantBones(int id, string assetName, BoneImplantProcess boneImplantProcess, AssignedAnotherWeights assignedAnotherWeights)
        {
            if (boneImplantProcess == null) throw new ArgumentNullException(nameof(boneImplantProcess));
            if (assignedAnotherWeights == null) throw new ArgumentNullException(nameof(assignedAnotherWeights));

            if (boneImplantProcess.trfSrc == null || boneImplantProcess.trfDst == null || boneImplantProcess.trfSrc == boneImplantProcess.trfDst)
            {
                ModBoneImplantor.Logger.Log(LogLevel.Error, $"Your BoneImplantProcess is invalid. trfSrc is {(boneImplantProcess.trfSrc != null ? boneImplantProcess.trfSrc.name : "NULL")} and trfDst is {(boneImplantProcess.trfDst != null ? boneImplantProcess.trfDst.name : "NULL")}.");
                ModBoneImplantor.Logger.Log(LogLevel.Error, "1) You must specify both trfSrc and trfDst.");
                ModBoneImplantor.Logger.Log(LogLevel.Error, "2) trfSrc must be different from trfDst.");
                return false;
            }

            // Find a bone in the body skeleton with the same name as the trfDst bone in the BoneImplantProcess
            if (assignedAnotherWeights.dictBone.TryGetValue(boneImplantProcess.trfDst.name, out var targetDstObj))
            {
                boneImplantProcess.trfSrc.SetParent(targetDstObj.transform, false);
            }
            else
            {
                ModBoneImplantor.Logger.Log(LogLevel.Error, $"Your BoneImplantProcess is invalid: trfDst wasn't found in the body bones. trfDst is {boneImplantProcess.trfDst.name}.");
                ModBoneImplantor.Logger.Log(LogLevel.Error, "trfDst must be the bone stored in the same structure as official body skeleton.");
                ModBoneImplantor.Logger.Log(LogLevel.Error, "You cannot set your original bone or placeholder object such as cf_o_root for trfDst.");
                return false;
            }

            // Mark the implanted bones for later to be cleaned up
            foreach (var trf in boneImplantProcess.trfSrc.GetComponentsInChildren<Transform>(true))
                trf.gameObject.AddComponent<BoneWasImplantedMarker>();

            _implantInfos.Add(new ImplantInfo(id, assetName, boneImplantProcess.trfSrc));

            return true;
        }

        /// <summary>
        /// Clear porting information associated with the specified asset name
        /// 指定したアセット名に関連する移植情報をクリア
        /// </summary>
        /// <param name="id">Model's ID モデルのID</param>
        /// <param name="assetName">Model's asset name モデルのアセット名</param>
        public void DestroyAndClearImplants(int id, string assetName)
        {
            foreach (var ii in _implantInfos)
            {
                if (ii.implantId == id && ii.implantName == assetName && ii.implantSrc != null)
                {
                    ii.implantSrc.SetParent(null);
                    Destroy(ii.implantSrc.gameObject);
                }
            }

            _implantInfos.RemoveAll(ii => ii.implantName == assetName);
        }

        /// <summary>
        /// Reconfigure DynamicBone. Almost a copy and paste of the original process.
        /// DynamicBoneを再設定する。ほぼオリジナルの処理のコピペ
        /// </summary>
        public void ForceRescueDynamicBone(ChaControl cc, GameObject obj, Dictionary<string, GameObject> dictBone)
        {
            //var dbcs = cc.objBodyBone.GetComponentsInChildren<DynamicBoneCollider>(true);
            var dbs = obj.GetComponentsInChildren<DynamicBone>(true);
            foreach (var db in dbs)
            {
                if (db.m_Root != null && !BoneWasImplantedMarker.IsMarked(db.m_Root))
                {
                    if (dictBone.TryGetValue(db.m_Root.name, out var value))
                        db.m_Root = value?.transform;
                }

                if (!db.m_Exclusions.IsNullOrEmpty())
                {
                    for (var i = 0; i < db.m_Exclusions.Count; i++)
                    {
                        var dbMExclusion = db.m_Exclusions[i];
                        if (dbMExclusion == null || BoneWasImplantedMarker.IsMarked(dbMExclusion)) continue;

                        if (dictBone.TryGetValue(dbMExclusion.name, out var value))
                            db.m_Exclusions[i] = value.transform;
                    }
                }

                if (!db.m_notRolls.IsNullOrEmpty())
                {
                    for (var i = 0; i < db.m_notRolls.Count; i++)
                    {
                        var dbMNotRoll = db.m_notRolls[i];
                        if (dbMNotRoll == null || BoneWasImplantedMarker.IsMarked(dbMNotRoll)) continue;

                        if (dictBone.TryGetValue(dbMNotRoll.name, out var value))
                            db.m_notRolls[i] = value.transform;
                    }
                }
            }
        }

        /// <summary>
        /// Transpose bone references; Replacement of AssignedAnotherWeights.AssignedWeightsAndSetBounds
        /// ボーン参照を移し替える。AssignedAnotherWeights.AssignedWeightsAndSetBoundsの置き換え
        /// </summary>
        public void TransferBoneReference(GameObject obj, string delTopName, Bounds bounds, Transform rootBone, Dictionary<string, GameObject> dictBone)
        {
            if (_implantInfos.IsNullOrEmpty() || obj == null || dictBone.IsNullOrEmpty())
                return;

            TransferBoneReferenceLoop(obj.transform, bounds, rootBone, dictBone);

            obj.transform.FindLoop(delTopName).Destroy(false, true);
        }

        /// <summary>
        /// Actual processing to transfer bone references; Replacement of AssignedAnotherWeights.AssignedWeightsAndSetBoundsLoop
        /// ボーン参照を移し替える実処理。AssignedAnotherWeights.AssignedWeightsAndSetBoundsLoopの置き換え
        /// </summary>
        private void TransferBoneReferenceLoop(Transform t, Bounds bounds, Transform rootBone, Dictionary<string, GameObject> dictBone)
        {
            var meshRenderer = t.GetComponent<SkinnedMeshRenderer>();
            if (meshRenderer != null)
            {
                var bonesLength = meshRenderer.bones.Length;
                var tmpBoneArr = new Transform[bonesLength];
                for (var i = 0; i < bonesLength; i++)
                {
                    if (BoneWasImplantedMarker.IsMarked(meshRenderer.bones[i]))
                        tmpBoneArr[i] = meshRenderer.bones[i];
                    else if (dictBone.TryGetValue(meshRenderer.bones[i].name, out var obj))
                        tmpBoneArr[i] = obj.transform;
                }

                meshRenderer.bones = tmpBoneArr;
                meshRenderer.localBounds = bounds;

                var cloth = meshRenderer.gameObject.GetComponent<Cloth>();
                if (rootBone != null && cloth == null)
                    meshRenderer.rootBone = rootBone;
                else if (meshRenderer.rootBone != null && dictBone.TryGetValue(meshRenderer.rootBone.name, out var obj))
                    meshRenderer.rootBone = obj.transform;
            }

            foreach (Transform childTr in t.gameObject.transform)
                TransferBoneReferenceLoop(childTr, bounds, rootBone, dictBone);
        }

        /// <summary>
        /// Class of bone porting information
        /// ボーン移植情報のクラス
        /// </summary>
        private class ImplantInfo
        {
            /// <summary>
            /// ID of porting information. Specify model ID
            /// 移植情報のID。 モデルのIDを指定
            /// </summary>
            public int implantId { get; }

            /// <summary>
            /// The name of the porting information. Specify the asset name of the model
            /// 移植情報の名前。モデルのアセット名を指定
            /// </summary>
            public string implantName { get; }

            /// <summary>
            /// The starting bone to be ported
            /// 移植対象の起点のボーン
            /// </summary>
            public Transform implantSrc { get; }

            public ImplantInfo(int newId, string newImplantName, Transform newImplantSrc)
            {
                implantId = newId;
                implantName = newImplantName;
                implantSrc = newImplantSrc;
            }
        }

        /// <summary>
        /// Class for marking the bones to be ported.
        /// 移植対象のボーンにマークするためのクラス
        /// </summary>
        private class BoneWasImplantedMarker : MonoBehaviour
        {
            public static bool IsMarked(Transform trf)
            {
                return trf != null && trf.gameObject.GetComponent<BoneWasImplantedMarker>();
            }
        }
    }
}
