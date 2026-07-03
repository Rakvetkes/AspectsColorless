using AspectsColorless.AspectsColorlessCode.Abstract;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace AspectsColorless.AspectsColorlessCode.Powers;

public class TheRunePower : AspectsPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyEnergyGain(Player player, decimal amount)
    {
        if (amount > 0 && this.Owner.Player is { PlayerCombatState.Phase: PlayerTurnPhase.Play })
        {
            CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), this.Amount, this.Owner.Player);
        }

        return amount;
    }
}