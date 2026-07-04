using AspectsColorless.AspectsColorlessCode.Abstract;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace AspectsColorless.AspectsColorlessCode.Powers;

public class LunariCharmPower : AspectsPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.Static(StaticHoverTip.Energy)];

    public override async Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    {
        if (command.Attacker == this.Owner && command.ModelSource is CardModel { Type: CardType.Attack } card)
        {
            var totalDamage = command.Results.Sum(resultList => resultList.Sum(result => result.TotalDamage));

            if ((totalDamage - this.Amount) % 2 == 0)
            {
                this.Flash();
                await PlayerCmd.GainEnergy(card.EnergyCost.GetResolved(), command.Attacker.Player!);
            }
        }
    }
    
}