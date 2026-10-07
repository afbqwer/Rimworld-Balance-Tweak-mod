using System.Collections.Generic;
using Verse;

namespace BalanceTweak;

public class ProcessIngredientItem
{
    public ThingDef? thing;
    public ThingCategoryDef? thingCategory;
    public List<ThingDef>? disallowedThingDefs;
    public float countNeeded;
}