using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ModBoneImplantor
{
    internal static class Extensions
    {

        public static void Destroy(this GameObject self, bool useDestroyImmediate = false, bool detachParent = false)
        {
            if (self == null) return;
            if (detachParent) self.transform.SetParent(null);
            if (useDestroyImmediate) Object.DestroyImmediate(self);
            else Object.Destroy(self);
        }
        
        public static bool IsNullOrEmpty(this ICollection self)
        {
            return self == null || self.Count == 0;
        }
    }
}
