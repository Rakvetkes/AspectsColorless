using AspectsColorless.AspectsColorlessCode.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace AspectsColorless.AspectsColorlessCode.Extensions;

public class SensitiveDamageVar(
    string name, decimal baseValue, ValueProp props, decimal multiplier)
    : DamageVar(name, baseValue, props)
{
    public decimal Multiplier { get; } = multiplier;

    // Actual Range.
    public int Resolve(DamageModifiers modifiers)
    {
        decimal ret = ((BaseValue + modifiers.A * Multiplier) * modifiers.M);
        return Math.Max((int)ret, 0);
    }

    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks)
    {
        decimal num = BaseValue;
        EnchantmentModel? enchantment = card.Enchantment;
        if (enchantment != null)
        {
            num += enchantment.EnchantDamageAdditive(num, Props);
            num *= enchantment.EnchantDamageMultiplicative(num, Props);
            if (!card.IsEnchantmentPreview)
                EnchantedValue = num;
        }

        if (runGlobalHooks)
        {
            var modifiers = DamageModifierCmd.Sniff(card, target, Props);
            decimal targetV = (BaseValue + modifiers.A * Multiplier) * modifiers.M;
            num = Hook.ModifyDamage(card.Owner.RunState, card.CombatState, target, card.Owner.Creature,
                DamageModifierCmd.Solve(targetV, modifiers), this.Props, card, null,
                ModifyDamageHookType.All, previewMode, out IEnumerable<AbstractModel> _);
        }

        PreviewValue = num;
    }
}
