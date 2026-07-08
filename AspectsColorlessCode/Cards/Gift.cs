using AspectsColorless.AspectsColorlessCode.Abstract;
using AspectsColorless.AspectsColorlessCode.Powers;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;

namespace AspectsColorless.AspectsColorlessCode.Cards;

[Pool(typeof(ColorlessCardPool))]
public class Gift() : AspectsCardModel(3, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(25, ValueProp.Move),
        new CardsVar(3)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
        HoverTipFactory.FromPower<GiftPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        // 1. Deal damage to target enemy
        await DamageCmd.Attack(this.DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
            .Execute(choiceContext);

        if (this.CombatState == null) return;

        // 2. Select up to N cards from hand to exhaust
        var selectedCards = (await CardSelectCmd.FromHand(
            choiceContext,
            this.Owner,
            new CardSelectorPrefs(this.SelectionScreenPrompt, 0,
                this.DynamicVars.Cards.IntValue),
            null,
            this)).ToList();

        // 3. Clone each selected card (before exhausting) and store for the power
        var storedCards = new List<CardModel>();
        foreach (var card in selectedCards)
        {
            var clone = card.CreateClone();
            storedCards.Add(clone);
            await CardCmd.Exhaust(choiceContext, card);
        }

        // 4. Apply GiftPower to the target enemy with 3 stacks
        if (storedCards.Any())
        {
            var giftPower = await PowerCmd.Apply<GiftPower>(
                choiceContext,
                cardPlay.Target!,
                3,
                this.Owner.Creature,
                this);

            giftPower?.SetStoredCards(storedCards);
        }
    }

    protected override void OnUpgrade()
    {
        this.DynamicVars.Damage.UpgradeValueBy(5);
        this.DynamicVars.Cards.UpgradeValueBy(2);
    }
}
