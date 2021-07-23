using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ModBoneImplantor
{
    [BepInPlugin(GUID, "Mod Bone Implantor", Version)]
    [BepInDependency("com.deathweasel.bepinex.uncensorselector", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(KKAPI.KoikatuAPI.GUID, KKAPI.KoikatuAPI.VersionConst)]
    public partial class ModBoneImplantor : BaseUnityPlugin
    {
        public const string GUID = "com.rclcircuit.bepinex.modboneimplantor";
        public const string Version = "1.1";

        internal static new ManualLogSource Logger;

        private void Awake()
        {
            Logger = base.Logger;

            var hi = new Harmony(GUID);
            AssignedAnotherWeightsHooks.InstallHooks(hi);
            UncensorSelectorSupport.InstallHooks(hi);
        }

        private static ImplantedBoneInfo TryImplantBones(GameObject loadedObj, Dictionary<string, GameObject> existingBoneDict)
        {
            var implants = loadedObj.GetComponentsInChildren<BoneImplantProcess>();
            if (implants.Length == 0) return null;

            // Implant extra bones from the object into base body skeleton based on BoneImplantProcess components attached to the object
            var implantedBones = new List<Transform>(implants.Length);
            var implantedColliders = new List<DynamicBoneCollider>(implants.Length);
            foreach (var implantInfo in implants)
            {
                var boneToImplant = implantInfo.trfSrc;
                var targetParentBone = implantInfo.trfDst;
                if (boneToImplant == null || targetParentBone == null || boneToImplant == targetParentBone)
                {
                    Logger.Log(LogLevel.Error, $"Your BoneImplantProcess is invalid. trfSrc is {(boneToImplant != null ? boneToImplant.name : "NULL")} and trfDst is {(targetParentBone != null ? targetParentBone.name : "NULL")}.");
                    Logger.Log(LogLevel.Error, "1) You must specify both trfSrc and trfDst.");
                    Logger.Log(LogLevel.Error, "2) trfSrc must be different from trfDst.");
                }
                // Find a bone in the body skeleton with the same name as the trfDst bone in the BoneImplantProcess
                else if (existingBoneDict.TryGetValue(targetParentBone.name, out var targetDstObj))
                {
                    boneToImplant.SetParent(targetDstObj.transform, false);
                    implantedBones.AddRange(boneToImplant.GetComponentsInChildren<Transform>(true));
                    implantedColliders.AddRange(boneToImplant.GetComponentsInChildren<DynamicBoneCollider>(true));
                }
                else
                {
                    Logger.Log(LogLevel.Error, $"Your BoneImplantProcess is invalid: trfDst wasn't found in the body bones. trfDst is {targetParentBone.name}.");
                    Logger.Log(LogLevel.Error, "trfDst must be a bone stored in the same structure as official body skeleton (name must match).");
                    Logger.Log(LogLevel.Error, "trfDst cannot be set to original bone or placeholder object such as cf_o_root.");
                }
            }

            Logger.LogDebug($"Found {implants.Length} instances of BoneImplantProcess. In total {implantedBones.Count} bones were implanted.");

            return new ImplantedBoneInfo(implantedBones, implantedColliders);
        }
    }
}
