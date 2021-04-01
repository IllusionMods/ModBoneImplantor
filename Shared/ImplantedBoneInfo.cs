using System.Collections.Generic;
using UnityEngine;

namespace ModBoneImplantor
{
    public sealed class ImplantedBoneInfo
    {
        public readonly List<Transform> ImplantedBones;
        public readonly List<DynamicBoneCollider> ImplantedColliders;
        public readonly HashSet<SkinnedMeshRenderer> Usages = new HashSet<SkinnedMeshRenderer>();

        public ImplantedBoneInfo(List<Transform> implantedBones, List<DynamicBoneCollider> implantedColliders)
        {
            ImplantedBones = implantedBones;
            ImplantedColliders = implantedColliders;
        }
    }
}