using BaseLib.Abstracts;
using BaseLib.Extensions;
using Godot;

namespace AspectsColorless.AspectsColorlessCode.Abstract;

public abstract class AspectsPowerModel : CustomPowerModel
{
    public override string CustomPackedIconPath => GetPowerImagePath(Id.Entry);
    
    private static string GetPowerImagePath(string id)
    {
        var path = $"{id.RemovePrefix().ToLowerInvariant()}.png".PowerImagePath();
        return ResourceLoader.Exists(path) ? path : "power.png".PowerImagePath();
    }
}