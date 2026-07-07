using AspectsColorless.AspectsColorlessCode.Abstract;
using AspectsColorless.AspectsColorlessCode.Commands;
using AspectsColorless.AspectsColorlessCode.Enumerations;
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
        new DynamicVar("MinDamage", 5m),
        new DynamicVar("MaxDamage", 20m),
        new DynamicVar("SwapCount", SwapCount),
    ];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [
        AspectsHelpers.StaticHoverTip(AspectsTips.AspectsPalette)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        // 1. Target! (¿)
        var rng = this.Owner.RunState.Rng.CombatCardSelection;
        int minDamage = this.DynamicVars["MinDamage"].IntValue;
        int maxDamage = this.DynamicVars["MaxDamage"].IntValue;
        int damage = rng.NextInt(minDamage, maxDamage + 1);

        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
            .Execute(choiceContext);

        // 2. Collect enemy intent types
        var intentTypes = this.CombatState!.Enemies
            .Where(e => e.Monster != null)
            .SelectMany(e => e.Monster!.NextMove.Intents)
            .Select(i => i.IntentType)
            .Distinct()
            .ToList();

        // 3. Map to distinct colored pool types
        var coloredPoolTypes = intentTypes
            .Where(IntentColorMap.ContainsKey)
            .Select(it => IntentColorMap[it])
            .Distinct()
            .ToList();

        if (coloredPoolTypes.Count == 0) return;

        // 4. Combine all cards from all matching colored pools
        var combinedColored = new List<(CardModel Card, Type PoolType)>();
        var unlockState = this.Owner.UnlockState;
        var multiplayerConstraint = this.Owner.RunState.CardMultiplayerConstraint;
        foreach (var poolType in coloredPoolTypes)
        {
            var pool = GetCardPool(poolType);
            foreach (var card in pool.GetUnlockedCards(unlockState, multiplayerConstraint))
            {
                combinedColored.Add((card, poolType));
            }
        }

        // 5. Pick SwapCount random colored cards (no duplicates)
        var selectedColored = new List<(CardModel Card, Type PoolType)>();
        var availableColored = new List<(CardModel Card, Type PoolType)>(combinedColored);
        for (int i = 0; i < SwapCount && availableColored.Count > 0; i++)
        {
            int idx = rng.NextInt(availableColored.Count);
            selectedColored.Add(availableColored[idx]);
            availableColored.RemoveAt(idx);
        }

        // 6. Pick SwapCount random colorless cards (no duplicates)
        var colorlessPool = ModelDb.CardPool<ColorlessCardPool>();
        var availableColorless = colorlessPool.GetUnlockedCards(unlockState, multiplayerConstraint).ToList();
        var selectedColorless = new List<CardModel>();
        for (int i = 0; i < SwapCount && availableColorless.Count > 0; i++)
        {
            int idx = rng.NextInt(availableColorless.Count);
            selectedColorless.Add(availableColorless[idx]);
            availableColorless.RemoveAt(idx);
        }

        // 7. Swap: colored → colorless, colorless → the same colored pool
        int pairCount = Math.Min(selectedColored.Count, selectedColorless.Count);
        for (int i = 0; i < pairCount; i++)
        {
            var (coloredCard, fromPool) = selectedColored[i];
            var colorlessCard = selectedColorless[i];

            SwapCardPool(this.Owner, coloredCard, fromPool, typeof(ColorlessCardPool));
            SwapCardPool(this.Owner, colorlessCard, typeof(ColorlessCardPool), fromPool);
        }
    }

    protected override void OnUpgrade()
    {
        this.DynamicVars["MinDamage"].UpgradeValueBy(5m);
        this.DynamicVars["MaxDamage"].UpgradeValueBy(10m);
    }

    // -- Pool manipulation helpers --

    private static void SwapCardPool(Player player, CardModel card, Type fromPool, Type toPool)
    {
        CardPoolCmd.RemoveFromCardPool(player, card, fromPool.FullName!);
        CardPoolCmd.AddToCardPool(player, card, toPool.FullName!);
    }

    private static CardPoolModel GetCardPool(Type poolType)
        => ModelDb.AllCardPools.First(p => p.GetType() == poolType);
}