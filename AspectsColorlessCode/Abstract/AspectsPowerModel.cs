using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace AspectsColorless.AspectsColorlessCode.Abstract;

public abstract class AspectsPowerModel : CustomPowerModel
{
    public override string CustomPackedIconPath => GetPowerImagePath(Id.Entry);
    public override string? CustomBigIconPath => GetPowerImagePath(Id.Entry);

    private static string GetPowerImagePath(string id)
    {
        var path = $"{id.RemovePrefix().ToLowerInvariant()}.png".PowerImagePath();
        return ResourceLoader.Exists(path) ? path : "power.png".PowerImagePath();
    }

    public virtual Task AfterEnergyGained(PlayerChoiceContext ctx, Player player, decimal amount)
    {
        return Task.CompletedTask;
    }
}