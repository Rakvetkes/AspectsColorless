using AspectsColorless.AspectsColorlessCode.Abstract;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace AspectsColorless.AspectsColorlessCode.Powers;

public class TheRunePower : AspectsPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.Static(StaticHoverTip.Energy)];

    public override async Task AfterEnergyGained(PlayerChoiceContext ctx, Player player, decimal amount)
    {
        if (/* this.Owner.Player == player && */this.Owner.Player is { PlayerCombatState.Phase: PlayerTurnPhase.Play })
        {
            this.Flash();
            await CardPileCmd.Draw(ctx, this.Amount, this.Owner.Player);
        }
    }
    
}