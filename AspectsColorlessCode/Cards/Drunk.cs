using AspectsColorless.AspectsColorlessCode.Abstract;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;

namespace AspectsColorless.AspectsColorlessCode.Cards;

[Pool(typeof(StatusCardPool))]
public class Drunk() : AspectsCardModel(-1, CardType.Status, CardRarity.Status, TargetType.None)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal, CardKeyword.Unplayable];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<StrengthPower>(1)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<StrengthPower>()];

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card == this)
        {
            await PowerCmd.Apply<StrengthPower>(choiceContext, this.Owner.Creature, this.DynamicVars.Strength.BaseValue, this.Owner.Creature, null);
        }
    }
    
    protected override void OnUpgrade()
    {
        this.DynamicVars.Strength.UpgradeValueBy(1);
    }
}