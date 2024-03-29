﻿using UnityEngine;

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
        /// Bone to be transplanted (the first custom bone in the chain to be added to the game).
        /// Do not change field name or references in mods will break.
        /// 移植対象のボーン
        /// </summary>
        public Transform trfSrc;
        /// <summary>
        /// Parent bone to transplant to (an existing vanilla game bone that should become the parent of the trfSrc bone).
        /// Do not change field name or references in mods will break.
        /// 移植先の親ボーン
        /// </summary>
        public Transform trfDst;
    }
}
