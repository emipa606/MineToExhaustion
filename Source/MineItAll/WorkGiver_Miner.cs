using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MineItAll;

internal class WorkGiver_Miner : RimWorld.WorkGiver_Miner
{
    public DesignationDef MineAll => DefDatabase<DesignationDef>.GetNamed("MineAll");
    public string NoPathTrans => "NoPath".Translate();

    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        foreach (var designation in pawn.Map.designationManager.AllDesignations)
        {
            if (designation.def != DesignationDefOf.Mine && designation.def != MineAll)
            {
                continue;
            }

            var mayBeAccessible = false;
            for (var i = 0; i < 8; i++)
            {
                var adjacentCell = designation.target.Cell + GenAdj.AdjacentCells[i];
                if (!adjacentCell.InBounds(pawn.Map) || !adjacentCell.Walkable(pawn.Map))
                {
                    continue;
                }

                mayBeAccessible = true;
                break;
            }

            if (!mayBeAccessible)
            {
                continue;
            }

            var mineable = designation.target.Cell.GetFirstMineable(pawn.Map);
            if (mineable != null)
            {
                yield return mineable;
            }
        }
    }

    public override bool ShouldSkip(Pawn pawn, bool forced = false)
    {
        return base.ShouldSkip(pawn, forced) && !pawn.Map.designationManager.AnySpawnedDesignationOfDef(MineAll);
    }

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        if (!t.def.mineable)
        {
            return null;
        }

        var designationManager = pawn.Map.designationManager;
        if (designationManager.DesignationAt(t.Position, DesignationDefOf.Mine) == null &&
            designationManager.DesignationAt(t.Position, MineAll) == null)
        {
            return null;
        }

        if (!pawn.CanReserve(t, 1, -1, null, forced))
        {
            return null;
        }

        var reachable = false;
        for (var i = 0; i < 8; i++)
        {
            var adjacentCell = t.Position + GenAdj.AdjacentCells[i];
            if (!adjacentCell.InBounds(pawn.Map) || !adjacentCell.Standable(pawn.Map) ||
                !ReachabilityImmediate.CanReachImmediate(adjacentCell, t, pawn.Map, PathEndMode.Touch, pawn))
            {
                continue;
            }

            reachable = true;
            break;
        }

        if (reachable)
        {
            return new Job(JobDefOf.Mine, t, 20000, true);
        }

        for (var i = 0; i < 8; i++)
        {
            var adjacentCell = t.Position + GenAdj.AdjacentCells[i];
            if (!adjacentCell.InBounds(t.Map))
            {
                continue;
            }

            if (!ReachabilityImmediate.CanReachImmediate(adjacentCell, t, pawn.Map, PathEndMode.Touch, pawn))
            {
                continue;
            }

            if (!adjacentCell.Walkable(t.Map) || adjacentCell.Standable(t.Map))
            {
                continue;
            }

            Thing haulableThing = null;
            var thingList = adjacentCell.GetThingList(t.Map);
            foreach (var thing in thingList)
            {
                if (!thing.def.designateHaulable ||
                    thing.def.passability != Traversability.PassThroughOnly)
                {
                    continue;
                }

                haulableThing = thing;
                break;
            }

            if (haulableThing == null)
            {
                continue;
            }

            var job = HaulAIUtility.HaulAsideJobFor(pawn, haulableThing);
            if (job != null)
            {
                return job;
            }

            JobFailReason.Is(NoPathTrans);
            return null;
        }

        JobFailReason.Is(NoPathTrans);
        return null;
    }
}