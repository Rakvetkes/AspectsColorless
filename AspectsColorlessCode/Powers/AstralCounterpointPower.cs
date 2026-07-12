using AspectsColorless.AspectsColorlessCode.Abstract;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace AspectsColorless.AspectsColorlessCode.Powers;

public class AstralCounterpointPower : AspectsPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
    {
        if (card.Owner.Creature != base.Owner)
        {
            return playCount;
        }

        // Look up the last card played this combat from history
        var lastPlayed = CombatManager.Instance.History.CardPlaysStarted
            .LastOrDefault(e => e.Actor == base.Owner && e.CardPlay.IsFirstInSeries);

        if (lastPlayed != null && lastPlayed.CardPlay.Card.VisualCardPool != card.VisualCardPool)
        {
            return playCount + base.Amount;
        }

        return playCount;
    }

    public override Task AfterModifyingCardPlayCount(CardModel card)
    {
        Flash();
        return Task.CompletedTask;
    }
}
