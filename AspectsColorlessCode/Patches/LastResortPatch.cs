using AspectsColorless.AspectsColorlessCode.Enumerations;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace AspectsColorless.AspectsColorlessCode.Patches;

[HarmonyPatch]
public static class LastResortPatch
{
    /// <summary>
    /// 在洗牌后将带有 LastResort 关键词的卡牌移至抽牌堆底部。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.ModifyShuffleOrder))]
    private static void ModifyShuffleOrder_Postfix(Player player, List<CardModel> cards, bool isInitialShuffle)
    {
        // 找出所有带 LastResort 关键词的牌，移到列表末尾（= 抽牌堆底）
        List<CardModel> lastResortCards = cards.Where(c => c.Keywords.Contains(AspectsKeywords.LastResort)).ToList();
        foreach (CardModel card in lastResortCards)
        {
            cards.Remove(card);
            cards.Add(card);
        }
    }
}
