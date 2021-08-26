using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;
#if HS2 || AIS
using AIChara;
#endif

namespace ModBoneImplantor
{
    internal static class Extensions
    {
        public static void FancyDestroy(this GameObject self, bool useDestroyImmediate = false, bool detachParent = false)
        {
            if (self == null) return;
            if (detachParent) self.transform.SetParent(null);
            if (useDestroyImmediate) Object.DestroyImmediate(self);
            else Object.Destroy(self);
        }

        public static string GetFullPath(this GameObject self)
        {
            if (self == null) return string.Empty;

            var result = new StringBuilder();
            var first = true;
            var transform = self.transform;
            while (transform != null)
            {
                if (first) first = false;
                else result.Insert(0, "/");
                result.Insert(0, transform.gameObject.name);
                transform = transform.parent;
            }
            return result.ToString();
        }

        public static string GetFullPath(this Component self)
        {
            if (self == null) return string.Empty;
            return $"{self.gameObject.GetFullPath()} [{self.GetType().Name}]";
        }

        public static Dictionary<string, GameObject> GetBodyBoneDict(this ChaControl ctrl)
        {
            var aaw = Traverse.Create(ctrl).Field<AssignedAnotherWeights>("aaWeightsBody").Value;
            return aaw.dictBone;
        }

        public static Dictionary<string, GameObject> GetBoneDict(this SkinnedMeshRenderer dst)
        {
            return dst.bones.Where(x => x != null).ToDictionary(x => x.name, x => x.gameObject);
        }

        public static Transform GetTopmostParent(this Component src)
        {
            var topmostParent = src.transform;
            while (topmostParent.parent)
                topmostParent = topmostParent.parent;
            return topmostParent;
        }
    }
}
