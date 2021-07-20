using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using KKAPI.Chara;
using UnityEngine;

namespace ModBoneImplantor
{
    public partial class ModBoneImplantor
    {
        private static class UncensorSelectorSupport
        {
            private static readonly Dictionary<Transform, ImplantedBoneInfo> _implantedBones = new Dictionary<Transform, ImplantedBoneInfo>();

            public static void InstallHooks(Harmony hi)
            {
#if KK
                var typeName = "KK_Plugins.UncensorSelector, KK_UncensorSelector";
#elif EC
                var typeName = "KK_Plugins.UncensorSelector, EC_UncensorSelector";
#elif KKS
                var typeName = "KK_Plugins.UncensorSelector, KKS_UncensorSelector";
#endif
                var mi = Type.GetType(typeName, false)?
                    .GetNestedType("UncensorSelectorController", AccessTools.all)?
                    .GetMethod("TransferBones", AccessTools.all);

                if (mi == null)
                    Logger.LogWarning("Could not find UncensorSelectorController.TransferBones - Make sure your UncensorSelector is up to date!");
                else
                    hi.Patch(mi, new HarmonyMethod(typeof(UncensorSelectorSupport), nameof(TransferBonesOverride)));
            }

            private static bool TransferBonesOverride(CharaCustomFunctionController __instance,
                SkinnedMeshRenderer src, SkinnedMeshRenderer dst)
            {
                // Clean up no longer used implanted bones
                foreach (var kvp in _implantedBones.ToList())
                {
                    kvp.Value.Usages.Remove(dst);
                    kvp.Value.Usages.RemoveWhere(x => x == null);
                    if (kvp.Value.Usages.Count == 0)
                    {
                        if (kvp.Value.ImplantedBones != null)
                        {
                            Logger.LogDebug($"Removing {kvp.Value.ImplantedBones.Count} no longer used implanted bones");

                            foreach (var implantedBone in kvp.Value.ImplantedBones)
                            {
                                if (implantedBone != null)
                                    Destroy(implantedBone.gameObject);
                            }
                        }

                        _implantedBones.Remove(kvp.Key);
                    }
                }

                var bodyBoneDict = __instance.ChaControl.GetBodyBoneDict(); /* try dst.GetBoneDict() if there are any missing bones */

                // Figure out the root object of the instantiated uncensor object and use it to figure out which renderers should share the instantiated bones
                var topmostParent = src.GetTopmostParent();
                // Cache results of bone implanting and reuse them on renderers from the same instantiated uncensor object
                if (!_implantedBones.TryGetValue(topmostParent, out var implantedBonesData))
                {
                    implantedBonesData = TryImplantBones(topmostParent.gameObject, bodyBoneDict);
                    if (implantedBonesData == null) return true;
                    _implantedBones[topmostParent] = implantedBonesData;
                }

                implantedBonesData.Usages.Add(dst);

                if (implantedBonesData.ImplantedBones == null || implantedBonesData.ImplantedBones.Count == 0)
                    return true;

                var existingBoneDict = dst.GetBoneDict();
                var bodyBonesDict = (Dictionary<string, GameObject>)null;

                var boneCount = src.bones.Length;
                var reassignedBoneArr = new Transform[boneCount];
                for (var i = 0; i < boneCount; i++)
                {
                    var rendererBone = src.bones[i];
                    if (rendererBone == null) // Extra safety check, should never happen
                    {
                        Logger.LogWarning($"Renderer has a null bone! BoneIndex: {i}  Renderer: {src.GetFullPath()}");
                    }
                    else if (implantedBonesData.ImplantedBones.Contains(rendererBone)) // Copy implanted bones as they are
                    {
                        reassignedBoneArr[i] = rendererBone;
                    }
                    else if (existingBoneDict.TryGetValue(rendererBone.name, out var baseBone)) // Use the equivalent bone from the target renderer if found
                    {
                        reassignedBoneArr[i] = baseBone.transform;
                    }
                    else
                    {
                        // This branch shouldn't happen in most cases so do a lazy init to avoid the reflection cost
                        if (bodyBonesDict == null) bodyBonesDict = bodyBoneDict;
                        // Use the equivalent bone from the body skeleton if found
                        if (bodyBonesDict.TryGetValue(rendererBone.name, out var bodyBone))
                            reassignedBoneArr[i] = bodyBone.transform;
                        else
                        {
                            Logger.LogWarning(
                                "Renderer is using a bone that is not in the base skeleton and is not implanted. It will be set to null. You need to add a BoneImplantProcess component to your object.\n" +
                                $"Renderer: {src.GetFullPath()}\n" +
                                $"Bone: {rendererBone.GetFullPath()}\n" +
                                $"BoneIndex: {i}");
                        }
                    }
                }

                dst.bones = reassignedBoneArr;

                return false;
            }
        }
    }
}