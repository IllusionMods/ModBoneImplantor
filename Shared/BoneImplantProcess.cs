using BepInEx.Logging;
using System;
using UnityEngine;

namespace ModBoneImplantor
{
    /// <summary>
    /// ボーン移植処理用の本体のクラス
    /// </summary>
    /// <remarks>
    /// MODモデルアセットの最上層のGameObjectにアタッチすることを想定している
    /// </remarks>
    public class BoneImplantProcess : MonoBehaviour
    {
        /// <summary>
        /// 移植対象のボーン
        /// </summary>
        public Transform trfSrc;
        /// <summary>
        /// 移植先の親ボーン
        /// </summary>
        public Transform trfDst;

        /// <summary>
        /// 移植実行
        /// </summary>
        /// <param name="implantMain">実際の処理のデリゲート</param>
        public bool Exec(Func<Transform, Transform, bool> implantMain)
        {
            if (implantMain == null)
            {
                return false;
            }

            if (trfSrc == null || trfDst == null || trfSrc == trfDst)
            {
                //初期値がおかしければコンソールで伝えて終了
                ModBoneImplantor.Logger.Log(LogLevel.Error, $"Your BoneImplantProcess is invalid. trfSrc is {(trfSrc != null ? trfSrc.name : "NULL")} and trfDst is {(trfDst != null ? trfDst.name : "NULL")}.");
                ModBoneImplantor.Logger.Log(LogLevel.Error, $"1) You must specify both trfSrc and trfDst.");
                ModBoneImplantor.Logger.Log(LogLevel.Error, $"2) trfSrc must be different from trfDst.");
                return false;
            }

            return implantMain(trfSrc, trfDst);
        }
    }
}
