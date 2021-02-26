using System.Text;
using UnityEngine;

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
    }
}
