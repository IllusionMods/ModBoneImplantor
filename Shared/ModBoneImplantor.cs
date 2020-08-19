using BepInEx;
using BepInEx.Logging;

namespace ModBoneImplantor
{
    /// <summary>
    /// ModBoneImplantorプラグイン本体
    /// </summary>
    [BepInPlugin(GUID, "Mod Bone Implantor", Version)]
    public class ModBoneImplantor : BaseUnityPlugin
    {
        public const string GUID = "com.rclcircuit.bepinex.modboneimplantor";
        public const string Version = "0.2.4";
        internal static new ManualLogSource Logger;

        /// <summary>
        /// コンストラクタではフックをインストールするだけ
        /// </summary>
        private void Awake()
        {
            Logger = base.Logger;
            Hooks.InstallHooks();
        }
    }
}
