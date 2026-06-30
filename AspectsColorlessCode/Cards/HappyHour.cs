using AspectsColorless.AspectsColorlessCode.Abstract;
using AspectsColorless.AspectsColorlessCode.Commands;
using AspectsColorless.AspectsColorlessCode.Enumerations;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace AspectsColorless.AspectsColorlessCode.Cards;

[Pool(typeof(ColorlessCardPool))]
public class HappyHour() : AspectsCardModel(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [
        AspectsHelpers.StaticHoverTip(AspectsTips.Transmute),
        HoverTipFactory.FromCard<Drunk>(this.IsUpgraded)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        List<CardModel> list = PileType.Hand.GetPile(this.Owner).Cards.ToList();
        foreach (CardModel card in list)
        {
            CardModel newCard = this.CombatState!.CreateCard<Drunk>(this.Owner);
            if (this.IsUpgraded)
            {
                CardCmd.Upgrade(newCard);
            }

            await AspectsCmd.Transmute(card, newCard);
        }

        if (this.DeckVersion != null)
        {
            await CardPileCmd.RemoveFromDeck(this.DeckVersion);
        }
    }

}