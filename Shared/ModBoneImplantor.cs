using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using IllusionUtility.GetUtility;
using UniRx;
using UniRx.Triggers;
using UnityEngine;

namespace ModBoneImplantor
{
    [BepInPlugin(GUID, "Mod Bone Implantor", Version)]
    public partial class ModBoneImplantor : BaseUnityPlugin
    {
        public const string GUID = "com.rclcircuit.bepinex.modboneimplantor";
        public const string Version = "1.0";

        internal static new ManualLogSource Logger;

        private void Awake()
        {
            Logger = base.Logger;

            Hooks.InstallHooks();
        }

        /// <summary>
        /// The full functionality of the plugin pretty much.
        /// Replaces the AssignedWeights* methods from AssignedAnotherWeights.
        /// If no BoneImplantProcess instances are found then it returns false and lets the stock method run instead to improve compatibility and safety.
        /// Could potentially be used directly when manually spawning an object and trying to make it use base body bones
        /// </summary>
        private static bool AssignWeightsAndImplantBones(AssignedAnotherWeights aaw, GameObject obj, string delTopName, Bounds bounds, Transform rootBone)
        {
            var implants = obj.GetComponentsInChildren<BoneImplantProcess>();
            if (implants.Length == 0) return false;

            var renderers = obj.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (renderers.Length == 0)
            {
                Logger.LogWarning($"Found {implants.Length} instances of BoneImplantProcess, but no SkinnedMeshRenderers were found so nothing will be done.\n" +
                                  $"Object: {obj.GetFullPath()}");
                return false;
            }

            var replaceBounds = bounds != default(Bounds);
            var dictBone = aaw.dictBone;

            // Implant extra bones from the object into base body skeleton based on BoneImplantProcess components attached to the object
            var implantedBones = new List<Transform>(implants.Length); //todo use hashset? not enough iterations to matter?
            var dbColliders = new List<DynamicBoneCollider>(implants.Length);
            foreach (var implantInfo in implants)
            {
                var boneToImplant = implantInfo.trfSrc;
                var targetParentBone = implantInfo.trfDst;
                if (boneToImplant == null || targetParentBone == null || boneToImplant == targetParentBone)
                {
                    Logger.Log(LogLevel.Error, $"Your BoneImplantProcess is invalid. trfSrc is {(boneToImplant != null ? boneToImplant.name : "NULL")} and trfDst is {(targetParentBone != null ? targetParentBone.name : "NULL")}.");
                    Logger.Log(LogLevel.Error, "You must specify both trfSrc and trfDst; and trfSrc must be different from trfDst.");
                }
                // Find a bone in the body skeleton with the same name as the trfDst bone in the BoneImplantProcess
                else if (dictBone.TryGetValue(targetParentBone.name, out var targetDstObj))
                {
                    boneToImplant.SetParent(targetDstObj.transform, false);
                    implantedBones.AddRange(boneToImplant.GetComponentsInChildren<Transform>(true));
                    dbColliders.AddRange(boneToImplant.GetComponentsInChildren<DynamicBoneCollider>(true));
                }
                else
                {
                    Logger.Log(LogLevel.Error, $"Your BoneImplantProcess is invalid: trfDst wasn't found in the body bones. trfDst is {targetParentBone.name}.");
                    Logger.Log(LogLevel.Error, "trfDst must be a bone stored in the same structure as official body skeleton (name must match).");
                    Logger.Log(LogLevel.Error, "trfDst cannot be set to original bone or placeholder object such as cf_o_root.");
                }
            }

            Logger.LogDebug($"Found {implants.Length} instances of BoneImplantProcess and {renderers.Length} instances of SkinnedMeshRenderers. {implantedBones.Count} bones were implanted.");

            // Remove implanted bones once all renderers that use them are destroyed
            var renderersAlive = renderers.ToList();
            void CleanupImplantedBones(SkinnedMeshRenderer renderer)
            {
                renderersAlive.Remove(renderer);
                if (renderersAlive.Count == 0)
                {
                    Logger.LogDebug($"Removing {implantedBones.Count} no longer used implanted bones");
                    foreach (var implantedBone in implantedBones)
                        Destroy(implantedBone.gameObject);
                }
            }

            // Replacement of AssignedAnotherWeights functionality that we override (need to override it because the implanted bones would be replaced with nulls)
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

            if (delTopName != null)
                obj.transform.FindLoop(delTopName).FancyDestroy(detachParent: true);

            return true;
        }
    }
}
