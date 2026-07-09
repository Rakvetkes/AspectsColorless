using AspectsColorless.AspectsColorlessCode.Abstract;
using AspectsColorless.AspectsColorlessCode.Commands;
using AspectsColorless.AspectsColorlessCode.Enumerations;
using AspectsColorless.AspectsColorlessCode.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;

namespace AspectsColorless.AspectsColorlessCode.Cards;

[Pool(typeof(ColorlessCardPool))]
public class TwilightSpill() : AspectsCardModel(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    private const int SwapCount = 30;

    private static readonly Dictionary<IntentType, Type> IntentColorMap = new()
    {
        // Aggressive → Red (Ironclad)
        { IntentType.Attack,    typeof(IroncladCardPool) },
        { IntentType.DeathBlow, typeof(IroncladCardPool) },

        // Defensive → Blue (Defect)
        { IntentType.Defend,    typeof(DefectCardPool) },

        // Buff → Orange (Regent)
        { IntentType.Buff,      typeof(RegentCardPool) },
        { IntentType.Heal,      typeof(RegentCardPool) },

        // Debuff → Purple (Necrobinder)
        { IntentType.Debuff,        typeof(NecrobinderCardPool) },
        { IntentType.DebuffStrong,  typeof(NecrobinderCardPool) },
        { IntentType.CardDebuff,    typeof(NecrobinderCardPool) },
        { IntentType.StatusCard,    typeof(NecrobinderCardPool) },

        // Other → Green (Silent)
        { IntentType.Escape,    typeof(SilentCardPool) },
        { IntentType.Hidden,    typeof(SilentCardPool) },
        { IntentType.Summon,    typeof(SilentCardPool) },
        { IntentType.Sleep,     typeof(SilentCardPool) },
        { IntentType.Stun,      typeof(SilentCardPool) },
        { IntentType.Unknown,   typeof(SilentCardPool) },
    };

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("MinDamage", 0m),
        new SensitiveDamageVar("MaxDamage", 32m, ValueProp.Move, 3),
        new DynamicVar("SwapCount", SwapCount),
    ];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [
        ResourceHelpers.StaticHoverTip(AspectsTips.AspectsPalette)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        // 1. Capture target enemy's intents BEFORE attack (avoid reading from a dead enemy)
        List<Type> coloredPoolTypes = [];
        if (cardPlay.Target.Monster?.NextMove.Intents is { } intents)
        {
            coloredPoolTypes = intents
                .Select(i => i.IntentType)
                .Where(IntentColorMap.ContainsKey)
                .Select(it => IntentColorMap[it])
                .Distinct()
                .ToList();
        }

        // 2. Damage
        var combatRng = this.Owner.RunState.Rng.CombatTargets;
        DamageModifiers modifiers = DamageModifierCmd.Sniff(this, cardPlay.Target, ValueProp.Move);
        int damage = combatRng.NextInt(
            this.DynamicVars["MinDamage"].IntValue,
            ((SensitiveDamageVar) this.DynamicVars["MaxDamage"]).Resolve(modifiers) + 1);

        await DamageCmd.Attack(DamageModifierCmd.Solve(damage, modifiers))
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
            .Execute(choiceContext);

        if (coloredPoolTypes.Count == 0) return;

        // 3. Collect all unlocked cards from matching colored pools
        var combinedColored = new List<(CardModel Card, Type PoolType)>();
        var unlockState = this.Owner.UnlockState;
        var multiplayerConstraint = this.Owner.RunState.CardMultiplayerConstraint;
        foreach (var poolType in coloredPoolTypes)
        {
            var pool = CardPoolCmd.ResolvePool(poolType)!;
            foreach (var card in pool.GetUnlockedCards(unlockState, multiplayerConstraint))
                combinedColored.Add((card, poolType));
        }

        // 4. Shuffle and take SwapCount from each side
        var rng = this.Owner.RunState.Rng.CombatCardSelection;
        var selectedColored = combinedColored
            .OrderBy(_ => rng.NextInt(int.MaxValue))
            .Take(SwapCount)
            .ToList();
        var selectedColorless = CardPoolCmd.ResolvePool(typeof(ColorlessCardPool))!
            .GetUnlockedCards(unlockState, multiplayerConstraint)
            .OrderBy(_ => rng.NextInt(int.MaxValue))
            .Take(SwapCount)
            .ToList();

        // 5. Pair and swap
        for (int i = 0; i < Math.Min(selectedColored.Count, selectedColorless.Count); i++)
        {
            var (coloredCard, coloredPool) = selectedColored[i];
            var colorlessCard = selectedColorless[i];
            CardPoolCmd.RemoveFromCardPool(coloredCard, coloredPool);
            CardPoolCmd.AddToCardPool(coloredCard, typeof(ColorlessCardPool));
            CardPoolCmd.RemoveFromCardPool(colorlessCard, typeof(ColorlessCardPool));
            CardPoolCmd.AddToCardPool(colorlessCard, coloredPool);
        }
    }

    protected override void OnUpgrade()
    {
        this.DynamicVars["MaxDamage"].UpgradeValueBy(10m);
    }

}