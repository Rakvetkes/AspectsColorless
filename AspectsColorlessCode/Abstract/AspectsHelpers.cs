using AspectsColorless.AspectsColorlessCode.Enumerations;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace AspectsColorless.AspectsColorlessCode.Abstract;

public static class AspectsHelpers
{
    public static IHoverTip StaticHoverTip(AspectsTips tip, params DynamicVar[] vars)
    {
        string str = StringHelper.Slugify(tip.ToString());
        LocString title = new LocString("static_hover_tips", str + ".title");
        LocString description = new LocString("static_hover_tips", str + ".description");
        foreach (DynamicVar var in vars)
        {
            title.Add(var);
            description.Add(var);
        }
        return (IHoverTip) new HoverTip(title, description);
    }
}