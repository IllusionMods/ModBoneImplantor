# ModBoneImplantor
Plugin for Koikatsu and EmotionCreators that allows clothing, accessory and uncensor mods to add additional bones to the character body. This can be used to add physics with dynamic bones, or with other plugins that manipulate these bones like BetterPenetration. If you saw any clothes that are broken (stretched into the ground) you probably were missing this plugin.

The original version of the plugin (0.2.1 and older) was made by rclcircuit. Since then it has been updated by others and posted [here in releases](https://github.com/IllusionMods/ModBoneImplantor/releases).

## How to install (users)
1. Install [BepInEx 5.1](https://github.com/BepInEx/BepInEx/releases) or newer. 
2. Download the [latest release](../../releases) for your game.
3. Extract the archive into your game directory. The plugin .dll should end up inside your 'BepinEx\Plugins' folder.
4. If there were any clothes that "sunk into the ground" they should now work properly.

## How to use (modders)
In short, you want to add BoneImplantProcess components to the bone structure in your mod, and use these components to specify which bones should be copied to the body skeleton.
1. If you are using [KoikatsuModdingTools](https://github.com/IllusionMods/KoikatsuModdingTools), you can find BoneImplantProcess in the component list.
If you are using SB3U, get [this](./BoneImplantProcessForSB3U.unity3d) file and copy the BoneImplantProcess MB from the included animator into your own bundle.
2. Attach these components on your bones for each of the added bones (either on root bone or on the last bone that exists in the game model). If you are adding a bone that has child bones, you should only add BoneImplantProcess for the topmost bone and not for the child bones. The child bones will be moved together with the topmost bone.
3. Add and configure any additional components to your extra bones like for example dynamic bones. Your extra bones will be cut out and added to the body bones, so all components you add will be preserved.

- If your extra bones appear to be locked at 0,0,0 then most likely something went wrong with the setup. Check game log for ModBoneImplantor messages, as long as the components are detected it should tell you what went wrong.
- You can see a more detailed SB3U tutorial [here](https://github.com/xm007/Koikatsu-Modding/blob/master/Index/X.%20Useful%20links%20for%20quick%20search.md) (and many other modding tutorials).
- If you want to add bones to accessories you have to use the [AccessoryClothes](https://github.com/DeathWeasel1337/KK_Plugins) plugin. For adding bones to bodies/uncensors you have to use [UncensorSelector](https://github.com/DeathWeasel1337/KK_Plugins).
