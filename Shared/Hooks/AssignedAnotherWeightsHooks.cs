using System.Linq;
using HarmonyLib;
using IllusionUtility.GetUtility;
using UniRx;
using UniRx.Triggers;
using UnityEngine;

namespace ModBoneImplantor
{
    public partial class ModBoneImplantor
    {
        internal static class AssignedAnotherWeightsHooks
        {
            public static void InstallHooks(Harmony hi)
            {
                hi.PatchAll(typeof(AssignedAnotherWeightsHooks));
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(AssignedAnotherWeights), nameof(AssignedAnotherWeights.AssignedWeightsAndSetBounds))]
            private static bool AssignedWeightsAndSetBoundsHook(AssignedAnotherWeights __instance, GameObject obj, string delTopName, Bounds bounds, Transform rootBone)
            {
#if DEBUG
                Console.WriteLine("AssignedWeightsAndSetBoundsHook");
#endif
                return AssignWeightsAndImplantBones(__instance, obj, delTopName, bounds, rootBone);
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(AssignedAnotherWeights), nameof(AssignedAnotherWeights.AssignedWeights))]
            private static bool AssignedWeightsHook(AssignedAnotherWeights __instance, GameObject obj, string delTopName, Transform rootBone)
            {
#if DEBUG
                Console.WriteLine("AssignedWeightsHook");
#endif
                return AssignWeightsAndImplantBones(__instance, obj, delTopName, default(Bounds), rootBone);
            }

            /// <summary>
            /// The full functionality of the plugin pretty much.
            /// Replaces the AssignedWeights* methods from AssignedAnotherWeights.
            /// If no BoneImplantProcess instances are found then it returns true and lets the stock method run instead to improve compatibility and safety.
            /// </summary>
            private static bool AssignWeightsAndImplantBones(AssignedAnotherWeights aaw, GameObject obj, string delTopName, Bounds bounds, Transform rootBone)
            {
                var dictBone = aaw.dictBone;

                var info = TryImplantBones(obj, dictBone);
                if (info == null) return true;

                var implantedBones = info.ImplantedBones;
                var dbColliders = info.ImplantedColliders;

                var renderers = obj.GetComponentsInChildren<SkinnedMeshRenderer>();
                if (renderers.Length == 0)
                {
                    // This should never happen unless the mod is broken
                    Logger.LogWarning("Found some instances of BoneImplantProcess, but no SkinnedMeshRenderers were found so nothing will be done.\n" +
                                      $"Object: {obj.GetFullPath()}");
                    // Clean up since they won't be used
                    foreach (var implantedBone in implantedBones) Destroy(implantedBone);
                    return true;
                }

                // Remove implanted bones once all renderers that use them are destroyed
                var renderersAlive = renderers.ToList();
                void CleanupImplantedBones(SkinnedMeshRenderer renderer)
                {
                    renderersAlive.Remove(renderer);
                    if (renderersAlive.Count == 0)
                    {
                        Logger.LogDebug($"Removing {implantedBones.Count} no longer used implanted bones");
                        foreach (var implantedBone in implantedBones)
                        {
                            if (implantedBone != null)
                                Destroy(implantedBone.gameObject);
                        }
                    }
                }

                // Replacement of AssignedAnotherWeights functionality that we override (need to override it because the implanted bones would be replaced with nulls)
                var replaceBounds = bounds != default(Bounds);
                foreach (var meshRenderer in renderers)
                {
                    var boneCount = meshRenderer.bones.Length;
                    var reassignedBoneArr = new Transform[boneCount];
                    for (var i = 0; i < boneCount; i++)
                    {
                        var rendererBone = meshRenderer.bones[i];
                        if (rendererBone == null) // Extra safety check, should never happen
                            Logger.LogWarning($"Renderer has a null bone! BoneIndex: {i}  Renderer: {meshRenderer.GetFullPath()}");
                        else if (implantedBones.Contains(rendererBone)) // Copy implanted bones as they are
                            reassignedBoneArr[i] = rendererBone;
                        else if (dictBone.TryGetValue(rendererBone.name, out var baseBone)) // Use the equivalent bone from the body skeleton if found
                            reassignedBoneArr[i] = baseBone.transform;
                        else
                        {
                            Logger.LogWarning(
                                "Renderer is using a bone that is not in the base skeleton and is not implanted. It will be set to null. You need to add a BoneImplantProcess component to your object.\n" +
                                $"Renderer: {meshRenderer.GetFullPath()}\n" +
                                $"Bone: {rendererBone.GetFullPath()}\n" +
                                $"BoneIndex: {i}");
                        }
                    }

                    meshRenderer.bones = reassignedBoneArr;
                    if (replaceBounds) meshRenderer.localBounds = bounds;

                    // Same as unedited version
                    var clothCmp = meshRenderer.gameObject.GetComponent<Cloth>();
                    if (rootBone != null && clothCmp == null)
                        meshRenderer.rootBone = rootBone;
                    else if (meshRenderer.rootBone != null && dictBone.TryGetValue(meshRenderer.rootBone.name, out var baseBone))
                        meshRenderer.rootBone = baseBone.transform;

                    // Keep track of which renderers got destroyed to know when to remove the implanted bones
                    meshRenderer.OnDestroyAsObservable().Subscribe(_ => CleanupImplantedBones(meshRenderer));
                }

                // Base game code will in most cases clear the bone collider list and replace it with a fresh list of all colliders on the base body
                // so the custom colliders on the implanted bones need to be readded separately
                if (dbColliders.Count > 0)
                {
                    var dynamicBones = obj.GetComponentsInChildren<DynamicBone>(true);
                    foreach (var db in dynamicBones)
                        db.m_Colliders.AddRange(dbColliders.Except(db.m_Colliders));

                    Logger.LogDebug($"Found {dbColliders.Count} DynamicBoneColliders on the implanted bones. They were added to {dynamicBones.Length} DynamicBones (alongside colliders attached to the base body).");
                }

                obj.transform.FindLoop(delTopName)?.gameObject.FancyDestroy(false, true);

                return false;
            }
        }
    }
}