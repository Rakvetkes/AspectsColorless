using AspectsColorless.AspectsColorlessCode.Abstract;
using AspectsColorless.AspectsColorlessCode.Enumerations;
using AspectsColorless.AspectsColorlessCode.Powers;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace AspectsColorless.AspectsColorlessCode.Cards;

[Pool(typeof(ColorlessCardPool))]
public class LunariCharm() : AspectsCardModel(3, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [AspectsKeywords.Cycle];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int amountToApply = this.EnergyCost.GetResolved() % 2 == 0 ? 2 : 1;
        await PowerCmd.Apply<LunariCharmPower>(choiceContext, [this.Owner.Creature], amountToApply, this.Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        this.AddKeyword(CardKeyword.Innate);
    }
}