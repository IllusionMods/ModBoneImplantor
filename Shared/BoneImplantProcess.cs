using BepInEx.Logging;
using System;
using UnityEngine;

namespace ModBoneImplantor
{
    /// <summary>
    /// Definition class for bone transplanting process.
    /// It is supposed to be attached to the top layer GameObject of the mod's model
    /// (can be done in SB3U and Unity Editor by referencing this .dll)
    /// ボーン移植処理用の本体のクラス
    /// MODモデルアセットの最上層のGameObjectにアタッチすることを想定している
    /// </summary>
    public class BoneImplantProcess : MonoBehaviour
    {
        /// <summary>
        /// Bone to be transplanted
        /// Do not change field name or references in mods will break
        /// 移植対象のボーン
        /// </summary>
        public Transform trfSrc;
        /// <summary>
        /// Parent bone to transplant to
        /// Do not change field name or references in mods will break
        /// 移植先の親ボーン
        /// </summary>
        public Transform trfDst;

        /// <summary>
        /// Transplant execution
        /// 移植実行
        /// </summary>
        /// <param name="implantMain">
        /// Actually executed delegate
        /// 実際の処理のデリゲート
        /// </param>.
        public bool Exec(Func<Transform, Transform, bool> implantMain)
        {
            if (implantMain == null)
            {
                return false;
            }

            if (trfSrc == null || trfDst == null || trfSrc == trfDst)
            {
                ModBoneImplantor.Logger.Log(LogLevel.Error, $"Your BoneImplantProcess is invalid. trfSrc is {(trfSrc != null ? trfSrc.name : "NULL")} and trfDst is {(trfDst != null ? trfDst.name : "NULL")}.");
                ModBoneImplantor.Logger.Log(LogLevel.Error, $"1) You must specify both trfSrc and trfDst.");
                ModBoneImplantor.Logger.Log(LogLevel.Error, $"2) trfSrc must be different from trfDst.");
                return false;
            }

            return implantMain(trfSrc, trfDst);
        }
    }
}
