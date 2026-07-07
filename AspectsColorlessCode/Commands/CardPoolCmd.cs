using AspectsColorless.AspectsColorlessCode.Patches;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace AspectsColorless.AspectsColorlessCode.Commands;

/// <summary>
/// 公共入口：运行时修改卡牌池。
/// 所有实现细节委托给 <see cref="CardPoolModStore"/>。
/// </summary>
public static class CardPoolCmd
{
    public static void AddToCardPool(Player player, CardModel card, string poolTypeName)
        => CardPoolModStore.AddToCardPool(player, card, poolTypeName);

    public static void RemoveFromCardPool(Player player, CardModel card, string poolTypeName)
        => CardPoolModStore.RemoveFromCardPool(player, card, poolTypeName);

    public static void ClearAll()
        => CardPoolModStore.ClearAll();
}
