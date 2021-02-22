using BepInEx.Logging;
using IllusionUtility.GetUtility;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ModBoneImplantor
{
    /// <summary>
    /// ボーン移植の管理用クラス
    /// </summary>
    public class ChaImplantManager : MonoBehaviour
    {
        /// <summary>
        /// ボーン移植情報のリスト
        /// </summary>
        private List<ImplantInfo> implantInfos;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public ChaImplantManager()
        {
            implantInfos = new List<ImplantInfo>();
        }

        /// <summary>
        /// ボーンを移植する
        /// </summary>
        /// <param name="id">モデルのID</param>
        /// <param name="assetName">モデルのアセット名</param>
        /// <param name="src">移植元の最上位のTransform</param>
        /// <param name="dst">移植先のTransform</param>
        /// <param name="dictBone">共有ボーンの辞書</param>
        /// <returns>移植成功したかどうか</returns>
        public bool ImplantBones(int id, string assetName, Transform src, Transform dst, Dictionary<string, GameObject> dictBone)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (dictBone == null) throw new ArgumentNullException(nameof(dictBone));

            if(dictBone.TryGetValue(dst.name, out GameObject objParent))
            {
                // ボーン辞書中のdstと同名のtransformを親に設定
                src.SetParent(objParent.transform, false);
            }
            else
            {
                //trfDstがおかしければコンソールで伝えて終了
                ModBoneImplantor.Logger.Log(LogLevel.Error, $"Your BoneImplantProcess is invalid. trfDst is {dst.name}.");
                ModBoneImplantor.Logger.Log(LogLevel.Error, $"trfDst must be the bone stored in the same structure as official body skeleton.");
                ModBoneImplantor.Logger.Log(LogLevel.Error, $"You cannot set your original bone or placeholder object such as cf_o_root for trfDst.");
                return false;
            }

            // 移植対象にマーカーを付与
            var bones = src.GetComponentsInChildren<Transform>(true);
            foreach(var trf in bones)
            {
                trf.gameObject.AddComponent<BoneImplantMarker>();
            }

            // リストに追加
            implantInfos.Add(new ImplantInfo(id, assetName, src));

            return true;
        }

        /// <summary>
        /// 指定したアセット名に関連する移植情報をクリア
        /// </summary>
        /// <param name="id">モデルのID</param>
        /// <param name="assetName">モデルのアセット名</param>
        public void DestroyAndClearImplants(int id, string assetName)
        {
            // アセット名に一致する移植情報をもとに移植済みのボーンを削除
            foreach(var ii in implantInfos)
            {
                if(ii.implantId == id && ii.implantName == assetName && ii.implantSrc != null)
                {
                    ii.implantSrc.SetParent(null);
                    Destroy(ii.implantSrc.gameObject);
                }
            }

            // 移植情報をクリア
            implantInfos.RemoveAll(ii => ii.implantName == assetName);
        }

        /// <summary>
        /// DynamicBoneを再設定する。ほぼオリジナルの処理のコピペ
        /// </summary>
        /// <param name="cc"></param>
        /// <param name="obj"></param>
        /// <param name="dictBone"></param>
        public void ForceRescueDynamicBone(ChaControl cc, GameObject obj, Dictionary<string, GameObject> dictBone)
        {
            var dbcs = cc.objBodyBone.GetComponentsInChildren<DynamicBoneCollider>(true);
            var dbs = obj.GetComponentsInChildren<DynamicBone>(true);
            foreach(var db in dbs)
            {
                if(db.m_Root != null && !BoneImplantMarker.IsMarked(db.m_Root))
                {
                    foreach(var kvp in dictBone)
                    {
                        if(kvp.Key == db.m_Root.name)
                        {
                            db.m_Root = kvp.Value.transform;
                            break;
                        }
                    }
                }
                if(db.m_Exclusions != null && db.m_Exclusions.Count > 0)
                {
                    for(int i = 0; i < db.m_Exclusions.Count; i++)
                    {
                        if(db.m_Exclusions[i] != null && !BoneImplantMarker.IsMarked(db.m_Exclusions[i]))
                        {
                            foreach(var kvp in dictBone)
                            {
                                if(kvp.Key == db.m_Exclusions[i].name)
                                {
                                    db.m_Exclusions[i] = kvp.Value.transform;
                                    break;
                                }
                            }
                        }
                    }
                }
                if(db.m_notRolls != null && db.m_notRolls.Count != 0)
                {
                    for(int i = 0; i < db.m_notRolls.Count; i++)
                    {
                        if(db.m_notRolls[i] != null && !BoneImplantMarker.IsMarked(db.m_notRolls[i]))
                        {
                            foreach(var kvp in dictBone)
                            {
                                if(kvp.Key == db.m_notRolls[i].name)
                                {
                                    db.m_notRolls[i] = kvp.Value.transform;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// ボーン参照を移し替える。AssignedAnotherWeights.AssignedWeightsAndSetBoundsの置き換え
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="delTopName"></param>
        /// <param name="bounds"></param>
        /// <param name="rootBone"></param>
        /// <param name="dictBone"></param>
        public void TransferBoneReference(GameObject obj, string delTopName, Bounds bounds, Transform rootBone, Dictionary<string, GameObject> dictBone)
        {
            if(implantInfos == null || implantInfos.Count < 1 || obj == null || dictBone == null || dictBone.Count < 1)
            {
                return;
            }

            TransferBoneReferenceLoop(obj.transform, bounds, rootBone, dictBone);

            var delObj = obj.transform.FindLoop(delTopName);
            if(delObj != null)
            {
                delObj.transform.SetParent(null);
                Destroy(delObj);
            }
        }

        /// <summary>
        /// ボーン参照を移し替える実処理。AssignedAnotherWeights.AssignedWeightsAndSetBoundsLoopの置き換え
        /// </summary>
        /// <param name="t"></param>
        /// <param name="bounds"></param>
        /// <param name="rootBone"></param>
        /// <param name="dictBone"></param>
        private void TransferBoneReferenceLoop(Transform t, Bounds bounds, Transform rootBone, Dictionary<string, GameObject> dictBone)
        {
            var smr = t.GetComponent<SkinnedMeshRenderer>();
            if(smr != null)
            {
                var num = smr.bones.Length;
                var tmpArray = new Transform[num];
                GameObject obj = null;
                for(var i = 0; i < num; i++)
                {
                    if(BoneImplantMarker.IsMarked(smr.bones[i]))
                    {
                        tmpArray[i] = smr.bones[i];
                    }
                    else if(dictBone.TryGetValue(smr.bones[i].name, out obj))
                    {
                        tmpArray[i] = obj.transform;
                    }
                }

                smr.bones = tmpArray;
                smr.localBounds = bounds;

                var c = smr.gameObject.GetComponent<Cloth>();
                if(rootBone != null && c == null)
                {
                    smr.rootBone = rootBone;
                }
                else if(smr.rootBone != null && dictBone.TryGetValue(smr.rootBone.name, out obj))
                {
                    smr.rootBone = obj.transform;
                }
            }

            IEnumerator enumerator = t.gameObject.transform.GetEnumerator();
            try
            {
                while(enumerator.MoveNext())
                {
                    var t2 = (Transform)enumerator.Current;
                    TransferBoneReferenceLoop(t2, bounds, rootBone, dictBone);
                }
            }
            finally
            {
                IDisposable disposable;
                if((disposable = (enumerator as IDisposable)) != null)
                {
                    disposable.Dispose();
                }
            }
        }

        /// <summary>
        /// ボーン移植情報のクラス
        /// </summary>
        private class ImplantInfo
        {
            /// <summary>
            /// 移植情報のID。 モデルのIDを指定
            /// </summary>
            public int implantId { get; private set; }

            /// <summary>
            /// 移植情報の名前。モデルのアセット名を指定
            /// </summary>
            public string implantName { get; private set; }

            /// <summary>
            /// 移植対象の起点のボーン
            /// </summary>
            public Transform implantSrc { get; private set; }

            /// <summary>
            /// コンストラクタ
            /// </summary>
            /// <param name="newId"></param>
            /// <param name="newImplantName"></param>
            /// <param name="newImplantSrc"></param>
            public ImplantInfo(int newId, string newImplantName, Transform newImplantSrc)
            {
                implantId = newId;
                implantName = newImplantName;
                implantSrc = newImplantSrc;
            }
        }

        /// <summary>
        /// 移植対象のボーンにマークするためのクラス
        /// </summary>
        private class BoneImplantMarker : MonoBehaviour
        {
            public static bool IsMarked(Transform trf)
            {
                if(trf == null)
                {
                    return false;
                }

                var c = trf.gameObject.GetComponent<BoneImplantMarker>();
                return c != null;
            }
        }
    }
}
