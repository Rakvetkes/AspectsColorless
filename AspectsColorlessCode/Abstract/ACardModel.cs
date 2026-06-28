using BaseLib.Abstracts;
using BaseLib.Extensions;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace AspectsColorless.AspectsColorlessCode.Abstract;

public abstract class ACardModel(int cost, CardType cardType, CardRarity cardRarity, TargetType targetType) :
    CustomCardModel(cost, cardType, cardRarity, targetType)
{
    public override string CustomPortraitPath => GetCardImagePath(Id.Entry);

    private static string GetCardImagePath(string cardId)
    {
        var path = $"{cardId.RemovePrefix().ToLowerInvariant()}.png".CardImagePath();
        return ResourceLoader.Exists(path) ? path : "card.png".CardImagePath();
    }
}