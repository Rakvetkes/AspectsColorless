using AspectsColorless.AspectsColorlessCode.Abstract;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace AspectsColorless.AspectsColorlessCode.Cards;

[Pool(typeof(ColorlessCardPool))]
public class Penny() : AspectsCardModel(-1, CardType.Skill, CardRarity.Rare, TargetType.None)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];
    public override bool HasTurnEndInHandEffect => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Gold", 10m)];

    protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
    {
        await PlayerCmd.GainGold(this.DynamicVars["Gold"].IntValue, this.Owner);
    }

    protected override void OnUpgrade()
    {
        this.DynamicVars["Gold"].UpgradeValueBy(5m);
    }
}
