using AspectsColorless.AspectsColorlessCode.Abstract;
using AspectsColorless.AspectsColorlessCode.Enumerations;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;

namespace AspectsColorless.AspectsColorlessCode.Cards;

[Pool(typeof(ColorlessCardPool))]
public class Interference() : AspectsCardModel(1, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [AspectsKeywords.LastResort, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("StrengthLoss", 3m)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<StrengthPower>()];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        // Remove all buffs from the target enemy
        var buffs = cardPlay.Target.Powers.Where(p => p.Type == PowerType.Buff).ToList();
        foreach (var buff in buffs)
        {
            if (buff is not MinionPower)
            {
                await PowerCmd.Remove(buff);
            }
        }

        // Apply negative Strength
        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            cardPlay.Target,
            -this.DynamicVars["StrengthLoss"].IntValue,
            this.Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        this.DynamicVars["StrengthLoss"].UpgradeValueBy(2m);
    }
}
