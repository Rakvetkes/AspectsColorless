using AspectsColorless.AspectsColorlessCode.Patches;
using MegaCrit.Sts2.Core.Models;

namespace AspectsColorless.AspectsColorlessCode.Commands;

public static class CardPoolCmd
{
    public static CardPoolModel? ResolvePool(Type poolType)
    {
        foreach (var pool in ModelDb.AllCardPools)
        {
            if (pool.GetType() == poolType)
                return pool;
        }
        return null;
    }

    public static void AddToCardPool(CardModel card, Type poolType)
    {
        var pool = ResolvePool(poolType);
        if (pool != null)
            CardPoolModStore.AddToCardPool(card, pool);
    }

    public static void RemoveFromCardPool(CardModel card, Type poolType)
    {
        var pool = ResolvePool(poolType);
        if (pool != null)
            CardPoolModStore.RemoveFromCardPool(card, pool);
    }

    public static void ClearAll() => CardPoolModStore.ClearAll();

    public static CardPoolModel GetCardPool(CardModel card) => card.VisualCardPool;
}
