using AspectsColorless.AspectsColorlessCode.Abstract;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;

namespace AspectsColorless.AspectsColorlessCode.Cards;

[Pool(typeof(ColorlessCardPool))]
public class GoldenLance() : AspectsCardModel(3, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(27, ValueProp.Move)];
    private int _playCountThisCombat = 0;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(this.DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).Targeting(cardPlay.Target!).WithHitFx("vfx/vfx_dramatic_stab", tmpSfx: "blunt_attack.mp3").Execute(choiceContext);
        ++_playCountThisCombat;
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource, CardPlay? cardPlay)
    {
        return cardSource == this && _playCountThisCombat == 1 ? 3M : 1M;
    }

    protected override void OnUpgrade()
    {
        this.DynamicVars.Damage.UpgradeValueBy(10);
    }
}