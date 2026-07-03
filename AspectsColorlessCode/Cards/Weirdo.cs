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
public class Weirdo() : AspectsCardModel(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [AspectsHelpers.StaticHoverTip(AspectsTips.Transmute)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel? card = PileType.Draw.GetPile(this.Owner).Cards.FirstOrDefault();
        if (card != null)
        {
            var result = (await AspectsCmd.TransmuteToRandom(card, this.Owner)).First()!.Value.cardAdded;
            
            if (this.IsUpgraded)
            {
                CardCmd.Upgrade(result);
                if (result.DeckVersion != null)
                {
                    CardCmd.Upgrade(result.DeckVersion);
                }
            }
            await CardPileCmd.Add(result, PileType.Hand);
        }
    }
}