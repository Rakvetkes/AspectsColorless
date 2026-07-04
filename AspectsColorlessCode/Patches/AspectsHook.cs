using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace AspectsColorless.AspectsColorlessCode.Patches;

public static class AspectsHook
{
    /// <summary>
    /// <see cref="M:MegaCrit.Sts2.Core.Hooks.Hook.IterateCombatHookListeners"/> 的复制
    /// </summary>
    private static IEnumerable<AbstractModel> IterateCombatHookListeners(ICombatState combatState)
    {
        if (!CombatManager.Instance.IsOverOrEnding || CombatManager.Instance.IsStarting)
        {
            foreach (AbstractModel iterateHookListener in combatState.IterateHookListeners())
                yield return iterateHookListener;
        }
    }

    /// <summary>
    /// 注入 <see cref="M:MegaCrit.Sts2.Core.Commands.PlayerCmd.GainEnergy"/> 末尾的补充钩子，实际获得能量时触发。
    /// 从当前正在执行的 GameAction 身上偷它的 PlayerChoiceContext 以暂停当前正在执行的 action。
    /// GainEnergy 只应在卡牌/药水执行期间被调用，偷不到 context 说明出现了不应该发生的调用路径。
    /// </summary>
    public static async Task AfterEnergyGained(ICombatState combatState, Player player, decimal amount)
    {
        ulong? netId = LocalContext.NetId;
        if (!netId.HasValue)
        {
            return;
        }

        PlayerChoiceContext ctx = RunManager.Instance.ActionExecutor.CurrentlyRunningAction switch
        {
            PlayCardAction pca => pca.PlayerChoiceContext,
            UsePotionAction upa => upa.PlayerChoiceContext,
            GenericHookGameAction gha => gha.ChoiceContext,
            _ => null
        } ?? throw new InvalidOperationException(
            $"AfterEnergyGained called with no stealable context. CurrentlyRunningAction: {RunManager.Instance.ActionExecutor.CurrentlyRunningAction}");

        foreach (AbstractModel model in IterateCombatHookListeners(combatState))
        {
            if (model is Abstract.AspectsPowerModel aspectsPower)
            {
                ctx.PushModel(model);
                await aspectsPower.AfterEnergyGained(ctx, player, amount);
                model.InvokeExecutionFinished();
                ctx.PopModel(model);
            }
        }
    }
}
