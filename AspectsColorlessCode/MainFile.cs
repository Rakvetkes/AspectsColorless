using AspectsColorless.AspectsColorlessCode.Patches;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace AspectsColorless.AspectsColorlessCode;

//You're recommended but not required to keep all your code in this package and all your assets in the AspectsColorless folder.
[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "AspectsColorless"; //At the moment, this is used only for the Logger and harmony names.

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        // 注入自定义属性名，否则 SavedProperties 二进制封包序列化会崩
        SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(KeywordsPatch.CycleSavePlaceholder));

        Harmony harmony = new(ModId);
        harmony.PatchAll();
    }
}