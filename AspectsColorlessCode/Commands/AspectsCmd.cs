using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace AspectsColorless.AspectsColorlessCode.Commands;

public static class AspectsCmd
{
    public static async Task<IEnumerable<CardPileAddResult?>> TransmuteToRandom(CardModel card, Player performer)
    {
        var result = new List<CardPileAddResult?>();
        var deckVersion = card.DeckVersion;
        result.Add(await CardCmd.TransformToRandom(card, performer.RunState.Rng.CombatCardSelection));
        if (deckVersion is { IsTransformable: true })
        {
            var transformed = result.First()!.Value.cardAdded;
            var deckReplacement = deckVersion.Owner.RunState.CloneCard(transformed);
            result.Add(await CardCmd.Transform(deckVersion, deckReplacement));
            transformed.DeckVersion = deckReplacement;
        }
        
        return result;
    }

    public static async Task<IEnumerable<CardPileAddResult?>> Transmute(CardModel card, CardModel replacement)
    {
        var result = new List<CardPileAddResult?>();
        var deckVersion = card.DeckVersion;
        result.Add(await CardCmd.Transform(card, replacement));
        if (deckVersion is { IsTransformable: true })
        {
            var transformed = result.First()!.Value.cardAdded;
            var deckReplacement = deckVersion.Owner.RunState.CloneCard(transformed);
            result.Add(await CardCmd.Transform(deckVersion, deckReplacement));
            transformed.DeckVersion = deckReplacement;
        }
        
        return result;
    }
}