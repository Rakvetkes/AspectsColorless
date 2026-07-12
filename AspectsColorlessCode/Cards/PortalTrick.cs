using AspectsColorless.AspectsColorlessCode.Abstract;
using AspectsColorless.AspectsColorlessCode.Enumerations;
using AspectsColorless.AspectsColorlessCode.Powers;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace AspectsColorless.AspectsColorlessCode.Cards;

[Pool(typeof(ColorlessCardPool))]
public class PortalTrick() : AspectsCardModel(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(4)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [
        ResourceHelpers.StaticHoverTip(AspectsTips.Portal)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 1. Discard all hand cards and draw
        await CardCmd.DiscardAndDraw(choiceContext, PileType.Hand.GetPile(this.Owner).Cards, this.DynamicVars.Cards.IntValue);

        // 2. Apply power and swap draw/discard piles
        await PowerCmd.Apply<PortaledPower>(choiceContext, this.Owner.Creature, 1, this.Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        this.DynamicVars.Cards.UpgradeValueBy(2m);
    }
}
