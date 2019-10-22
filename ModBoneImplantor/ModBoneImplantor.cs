using BepInEx;

namespace ModBoneImplantor
{
	/// <summary>
	/// ModBoneImplantorプラグイン本体
	/// </summary>
	[BepInPlugin(GUID, "Mod Bone Implantor", Version)]
	public class ModBoneImplantor : BaseUnityPlugin
	{
        public const string GUID = "com.rclcircuit.bepinex.modboneimplantor";
        public const string Version = "0.2.2";

        /// <summary>
        /// コンストラクタではフックをインストールするだけ
        /// </summary>
        public ModBoneImplantor()
		{
			Hooks.InstallHooks();
		}
    }
}
