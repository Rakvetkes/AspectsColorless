using System.Runtime.CompilerServices;
using AspectsColorless.AspectsColorlessCode.Enumerations;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace AspectsColorless.AspectsColorlessCode.Patches;

[HarmonyPatch]
public static class KeywordsPatch
{
    private static readonly ConditionalWeakTable<CardModel, CycleData> CycleStore = new();

    private sealed class CycleData
    {
        public int Cost;
    }

    /// <summary>
    /// 仅用于向 SavedPropertiesTypeCache 注册 "AC_CycleCost" 属性名。
    /// </summary>
    internal sealed class CycleSavePlaceholder
    {
        [SavedProperty]
        public int AC_CycleCost { get; set; }
    }

    /// <summary>读取轮转当前费用，无数据时回退到 Canonical</summary>
    public static int GetCycleCost(CardModel card) =>
        CycleStore.TryGetValue(card, out var data) ? data.Cost : card.EnergyCost.Canonical;

    /// <summary>设置轮转费用并同步到 CardEnergyCost（自动触发 UI 刷新）</summary>
    public static void SetCycleCost(CardModel card, int cost)
    {
        var data = CycleStore.GetValue(card, _ => new CycleData());
        data.Cost = cost;
        card.EnergyCost.SetCustomBaseCost(cost);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.ToSerializable))]
    private static void ToSerializable_Postfix(CardModel __instance, ref SerializableCard __result)
    {
        if (!__instance.Keywords.Contains(AspectsKeywords.Cycle)) return;
        if (!CycleStore.TryGetValue(__instance, out var data)) return;

        __result.Props ??= new SavedProperties();
        __result.Props.ints ??= new List<SavedProperties.SavedProperty<int>>();
        // 先删旧的再添加，防止重复
        __result.Props.ints.RemoveAll(p => p.name == "AC_CycleCost");
        __result.Props.ints.Add(new SavedProperties.SavedProperty<int>("AC_CycleCost", data.Cost));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.FromSerializable))]
    private static void FromSerializable_Postfix(SerializableCard save, ref CardModel __result)
    {
        if (save.Props?.ints == null) return;
        foreach (var p in save.Props.ints)
        {
            if (p.name == "AC_CycleCost")
            {
                SetCycleCost(__result, p.value);
                break;
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
    public static void OnPlayWrapper_Postfix(CardModel __instance,
        PlayerChoiceContext choiceContext,
        Creature? target,
        bool isAutoPlay,
        ResourceInfo resources,
        bool skipCardPileVisuals,
        ref Task __result)
    {
        __result = OnPlayWrapper_Postfix_Awaited(__result, __instance);
    }

    private static async Task OnPlayWrapper_Postfix_Awaited(Task originalTask, CardModel card)
    {
        await originalTask;

        if (card.Keywords.Contains(AspectsKeywords.Cycle))
        {
            int canonical = card.EnergyCost.Canonical;
            int current = GetCycleCost(card);
            int next = (current + 1) % (canonical + 1);
            SetCycleCost(card, next);
        }

        CardModel? deckCard = card.DeckVersion;
        if (deckCard != null && deckCard.Keywords.Contains(AspectsKeywords.Cycle))
        {
            int canonical = deckCard.EnergyCost.Canonical;
            int current = GetCycleCost(deckCard);
            int next = (current + 1) % (canonical + 1);
            SetCycleCost(deckCard, next);
        }
    }
}