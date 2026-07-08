using AspectsColorless.AspectsColorlessCode.Abstract;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace AspectsColorless.AspectsColorlessCode.Powers;

public class GiftPower : AspectsPowerModel
{
    private string EmptyText
    {
        get
        {
            var key = $"{this.Id.Entry}.emptyText";
            var loc = new LocString("powers", key);
            return loc.GetFormattedText();
        }
    }
    
    private class Data
    {
        public List<CardModel> StoredCards = new();
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new StringVar("CardNames", EmptyText)];

    protected override object InitInternalData()
    {
        return new Data();
    }

    public void SetStoredCards(List<CardModel> cards)
    {
        GetInternalData<Data>().StoredCards = cards;
        
        var storedCards = cards.Count == 0 ? EmptyText : string.Join(", ", cards.Select(c => c.Title));
        ((StringVar)this.DynamicVars["CardNames"]).StringValue = storedCards;
    }

    public override async Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    {
        // Check if our owner was among the targets hit
        bool ownerWasHit = command.Results.Any(hit =>
            hit.Any(result => result.Receiver == this.Owner));

        if (!ownerWasHit) return;

        int newAmount = this.Amount - 1;
        if (newAmount <= 0)
        {
            // Trigger: add copies of stored cards to attacker's hand
            var data = GetInternalData<Data>();
            Player? attackerPlayer = command.Attacker?.Player;
            if (attackerPlayer != null && data.StoredCards.Count > 0)
            {
                this.Flash();
                foreach (var storedCard in data.StoredCards)
                {
                    var creator = storedCard.Owner;
                    storedCard.GiveToAnotherPlayer(attackerPlayer);
                    await CardPileCmd.AddGeneratedCardToCombat(storedCard, PileType.Hand, creator);
                }
            }

            await PowerCmd.Remove(this);
        }
        else
        {
            this.SetAmount(newAmount);
        }
    }

    public override Task AfterSideTurnStart(CombatSide side,
        IReadOnlyList<Creature> creatures, ICombatState combatState)
    {
        if (creatures.Contains(this.Owner))
        {
            this.SetAmount(3);
        }
        return Task.CompletedTask;
    }
}
