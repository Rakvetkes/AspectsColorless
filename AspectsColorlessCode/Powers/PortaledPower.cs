using AspectsColorless.AspectsColorlessCode.Abstract;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace AspectsColorless.AspectsColorlessCode.Powers;

public class PortaledPower : AspectsPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    /// <summary>
    /// Flip and swap the contents of the player's DrawPile and DiscardPile with flying card VFX.
    /// Each pile's order is reversed before moving to the other pile.
    /// Calling twice restores the original state (flip is its own inverse).
    /// </summary>
    private async Task SwapPiles()
    {
        var player = this.Owner.Player;
        if (player?.PlayerCombatState == null) return;

        var drawCards = player.PlayerCombatState.DrawPile.Cards.ToList();
        var discardCards = player.PlayerCombatState.DiscardPile.Cards.ToList();

        // Flip: reverse each pile's order
        drawCards.Reverse();
        discardCards.Reverse();

        // Move flipped draw → discard (with flying card VFX)
        if (drawCards.Count > 0)
            await CardPileCmd.Add(drawCards, PileType.Discard);

        // Move flipped discard → draw (with flying card VFX)
        if (discardCards.Count > 0)
            await CardPileCmd.Add(discardCards, PileType.Draw);
    }

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        await SwapPiles();
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != this.Owner.Player) return;
        await SwapPiles(); // Swap back
        await PowerCmd.Remove(this);
    }
}
