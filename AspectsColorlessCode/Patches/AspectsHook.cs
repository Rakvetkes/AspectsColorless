using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
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
    /// 优先从当前正在执行的 GameAction 身上偷它的 PlayerChoiceContext；
    /// 如果没有正在执行的 action（如回合开始时遗物触发的能量获取），则仿照 Hook 做法原地创建 HookPlayerChoiceContext。
    /// </summary>
    public static async Task AfterEnergyGained(ICombatState combatState, Player player, decimal amount)
    {
        ulong? netId = LocalContext.NetId;
        if (!netId.HasValue)
        {
            return;
        }

        PlayerChoiceContext? ctx = RunManager.Instance.ActionExecutor.CurrentlyRunningAction switch
        {
            PlayCardAction pca => pca.PlayerChoiceContext,
            UsePotionAction upa => upa.PlayerChoiceContext,
            GenericHookGameAction gha => gha.ChoiceContext,
            _ => null
        };

        if (ctx != null)
        {
            // 有正在执行的 GameAction，直接偷它的 context
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
        else
        {
            // 没有正在执行的 GameAction（如回合开始时 HappyFlower 等遗物触发），
            // 仿照 Hook.BeforeFlush 的做法：为每个 model 原地创建 HookPlayerChoiceContext
            foreach (AbstractModel model in IterateCombatHookListeners(combatState))
            {
                if (model is Abstract.AspectsPowerModel aspectsPower)
                {
                    HookPlayerChoiceContext hookCtx = new HookPlayerChoiceContext(model, netId.Value, combatState, GameActionType.Combat);
                    Task task = aspectsPower.AfterEnergyGained(hookCtx, player, amount);
                    await hookCtx.AssignTaskAndWaitForPauseOrCompletion(task);
                    model.InvokeExecutionFinished();
                }
            }
        }
    }
}
