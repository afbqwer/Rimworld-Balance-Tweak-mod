using System.Collections.Generic;
using System.Linq;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;


[TweakFor(typeof(WeatherDef), SettingType.Weather)]
class WeatherData : TweakData<WeatherData>
{
    [TweakField()]
    public float? accuracyMultiplier = null;
    [TweakField()]
    public float? moveSpeedMultiplier = null;
    [TweakField()]
    public float? windSpeedFactor = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        this.def = def;
        id = new(def.defName, type);
        modPackName = def.modContentPack?.Name ?? "Unknown";
        this.tweaked = tweaked;
        SetProps(def, type);
        if (def is WeatherDef d)
        {
            label = d.LabelCap.NullOrEmpty() ? d.defName : d.LabelCap;
            defLabel ??= def?.label;
            desc = d.description;
            if (desc.NullOrEmpty()) { desc = d.defName; }
            searchString = label + d.defName;
            accuracyMultiplier ??= d.accuracyMultiplier;
            moveSpeedMultiplier ??= d.moveSpeedMultiplier;
            windSpeedFactor ??= d.windSpeedFactor;
        }
    }

    public override void Apply()
    {
        if (this.def is not WeatherDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        if (accuracyMultiplier.HasValue) { def.accuracyMultiplier = accuracyMultiplier.Value; }
        if (moveSpeedMultiplier.HasValue) { def.moveSpeedMultiplier = moveSpeedMultiplier.Value; }
        if (windSpeedFactor.HasValue) { def.windSpeedFactor = windSpeedFactor.Value; }
        if (defLabel != null) this.def.label = defLabel;
    }

    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(WeatherType).GetEnumNames().Select(s => "MST." + s).ToList();

    private enum WeatherType
    {
        Good,
        Bad,
    }

    public override int GetPropType()
    {
        if (this.def is WeatherDef def)
        {
            if (def.isBad)
            {
                return (int)WeatherType.Bad;
            }
            else
            {
                return (int)WeatherType.Good;
            }
        }
        return (int)WeatherType.Good;
    }
}
