using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;

namespace AspectsColorless.AspectsColorlessCode.Patches;

[HarmonyPatch]
public static class GainEnergyPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.GainEnergy))]
    public static void GainEnergy_Prefix(Player player, out int __state)
    {
        __state = player.PlayerCombatState?.Energy ?? 0;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.GainEnergy))]
    public static void GainEnergy_Postfix(decimal amount, Player player, int __state, ref Task __result)
    {
        __result = GainEnergy_Postfix_Awaited(__result, player, __state);
    }

    private static async Task GainEnergy_Postfix_Awaited(Task originalTask, Player player, int energyBefore)
    {
        await originalTask;

        if (player.PlayerCombatState is not { } playerCombatState)
            return;

        int energyAfter = playerCombatState.Energy;
        int actualGained = energyAfter - energyBefore;

        if (actualGained > 0 && player.Creature.CombatState is { } combatState)
        {
            await AspectsHook.AfterEnergyGained(combatState, player, actualGained);
        }
    }
}
