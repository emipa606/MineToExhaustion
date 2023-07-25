using RimWorld;
using UnityEngine;
using Verse;

namespace MineItAll;

internal class Designator_MineAll : Designator_Mine
{
    public Designator_MineAll()
    {
        defaultLabel = "DesignatorMineAll".Translate();
        defaultDesc = "DesignatorMineAllDesc".Translate();
        icon = ContentFinder<Texture2D>.Get("UI/Designators/MineAll");
        useMouseIcon = true;
        soundDragSustain = SoundDefOf.Designate_DragStandard;
        soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
        soundSucceeded = SoundDefOf.Designate_Mine;
        tutorTag = "Mine";
    }

    protected override DesignationDef Designation => DefDatabase<DesignationDef>.GetNamed("MineAll");

    public override AcceptanceReport CanDesignateCell(IntVec3 c)
    {
        if (c.Fogged(Map))
        {
            return false;
        }

        return base.CanDesignateCell(c);
    }
}