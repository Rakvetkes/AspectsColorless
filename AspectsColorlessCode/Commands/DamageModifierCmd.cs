using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace AspectsColorless.AspectsColorlessCode.Commands;

public readonly record struct DamageModifiers(decimal A, decimal M);

public static class DamageModifierCmd
{
    private static readonly decimal _divine = 999999999m;

    public static DamageModifiers Sniff(CardModel card, Creature? target, ValueProp props)
    {
        decimal d0 = 42;
        decimal fd0 = RunLinear(d0, card, target, props);
        while (fd0 <= 0 && d0 < _divine)
        {
            d0 *= 2;
            fd0 = RunLinear(d0, card, target, props);
        }
        if (fd0 <= 0) return default;

        decimal m = RunLinear(d0 + 1m, card, target, props) - fd0;
        if (m <= 0) return default;

        return new DamageModifiers(fd0 / m - d0, m);
    }

    public static decimal Solve(decimal targetV, DamageModifiers modifiers)
    {
        if (targetV <= 0) return 0;
        if (modifiers.M == 0)
            throw new InvalidOperationException($"Cannot resolve {targetV} with M=0.");
        return targetV / modifiers.M - modifiers.A;
    }

    private static decimal RunLinear(decimal baseDamage, CardModel card, Creature? target, ValueProp props)
    {
        return Hook.ModifyDamage(
            card.Owner.RunState, card.CombatState, target, card.Owner.Creature,
            baseDamage, props, card, null,
            ModifyDamageHookType.Additive | ModifyDamageHookType.Multiplicative,
            CardPreviewMode.None, out _);
    }
}