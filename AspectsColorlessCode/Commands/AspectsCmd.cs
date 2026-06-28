using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;

namespace AspectsColorless.AspectsColorlessCode.Commands;

public static class AspectsCmd
{
    public static async Task<IEnumerable<CardPileAddResult?>> Transmute(CardModel card, Player performer)
    {
        var result = new List<CardPileAddResult?>();
        var replacement = CardFactory.GetDistinctForCombat(performer,
            performer.Character.CardPool.GetUnlockedCards(performer.UnlockState, performer.RunState.CardMultiplayerConstraint),
            1, performer.RunState.Rng.CombatCardGeneration).First();
        
        var deckVersion = card.DeckVersion;
        result.Add(await CardCmd.Transform(card, replacement));
        if (deckVersion is { IsTransformable: true })
        {
            var transformed = result.First();
            var deckReplacement = deckVersion.Owner.RunState.CloneCard(replacement);
            result.Add(await CardCmd.Transform(deckVersion, deckReplacement));
            if (transformed != null)
            {
                transformed.Value.cardAdded.DeckVersion = deckReplacement;
            }
        }
        
        return result;
    }
}