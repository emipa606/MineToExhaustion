using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MineItAll;

internal class JobDriver_Mine : RimWorld.JobDriver_Mine
{
    private readonly DesignationDef MineAllDef = DefDatabase<DesignationDef>.GetNamed("MineAll");
    protected Effecter effecter;
    protected int ticksToPickHit = -1000;

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        FailOnCellMissingMiningDesignation(TargetIndex.A);
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
        var mine = new Toil();
        mine.tickAction = delegate
        {
            var actor = mine.actor;
            var mineTarget = TargetThingA;

            if (ticksToPickHit < -100)
            {
                ResetTicksToPickHit();
            }

            if (actor.skills != null && (mineTarget.Faction != actor.Faction || actor.Faction == null))
            {
                actor.skills.Learn(SkillDefOf.Mining, 0.07f);
            }

            ticksToPickHit--;
            if (ticksToPickHit > 0)
            {
                return;
            }

            var position = mineTarget.Position;
            if (effecter == null)
            {
                effecter = EffecterDefOf.Mine.Spawn();
            }

            effecter.Trigger(actor, mineTarget);
            var damage = !mineTarget.def.building.isNaturalRock ? 40 : 80;
            var mineable = mineTarget as Mineable;
            var mineAll = false;
            if (mineable == null || mineTarget.HitPoints > damage)
            {
                mineTarget.TakeDamage(new DamageInfo(DamageDefOf.Mining, damage, 0f, -1f, mine.actor));
            }
            else
            {
                mineAll = actor.Map.designationManager.DesignationAt(position, MineAllDef) != null;
                mineable.Notify_TookMiningDamage(mineTarget.HitPoints, mine.actor);
                mineable.HitPoints = 0;
                mineable.DestroyMined(actor);
            }

            if (mineTarget.Destroyed)
            {
                if (mineAll)
                {
                    foreach (var direction in GenAdj.AdjacentCells)
                    {
                        var adjacentCell = position + direction;
                        if (!adjacentCell.InBounds(Map) || adjacentCell.Fogged(Map))
                        {
                            continue;
                        }

                        var edifice = adjacentCell.GetEdifice(Map);
                        if (edifice == null || edifice.def != mineTarget.def)
                        {
                            continue;
                        }

                        var designationManager = Map.designationManager;
                        if (designationManager.DesignationAt(adjacentCell, MineAllDef) != null)
                        {
                            continue;
                        }

                        designationManager.AddDesignation(new Designation(adjacentCell, MineAllDef));
                        designationManager.TryRemoveDesignation(adjacentCell, DesignationDefOf.SmoothWall);
                    }
                }

                actor.Map.mineStrikeManager.CheckStruckOre(position, mineTarget.def, actor);
                actor.records.Increment(RecordDefOf.CellsMined);
                if (pawn.Faction != Faction.OfPlayer)
                {
                    var thingList = position.GetThingList(Map);
                    foreach (var thing in thingList)
                    {
                        thing.SetForbidden(true, false);
                    }
                }
                else
                {
                    if (MineStrikeManager.MineableIsVeryValuable(mineTarget.def))
                    {
                        TaleRecorder.RecordTale(TaleDefOf.MinedValuable, pawn,
                            mineTarget.def.building.mineableThing);
                    }

                    if (MineStrikeManager.MineableIsValuable(mineTarget.def) && !pawn.Map.IsPlayerHome)
                    {
                        TaleRecorder.RecordTale(TaleDefOf.CaravanRemoteMining, pawn,
                            mineTarget.def.building.mineableThing);
                    }
                }

                ReadyForNextToil();
                return;
            }

            ResetTicksToPickHit();
        };
        mine.defaultCompleteMode = ToilCompleteMode.Never;
        mine.WithProgressBar(TargetIndex.A, () => 1f - ((float)TargetThingA.HitPoints / TargetThingA.MaxHitPoints));
        mine.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
        mine.activeSkill = () => SkillDefOf.Mining;
        yield return mine;
    }

    protected virtual void FailOnCellMissingMiningDesignation(TargetIndex index)
    {
        AddEndCondition(delegate
        {
            var actor = GetActor();
            var curJob = actor.jobs.curJob;
            if (curJob.ignoreDesignations)
            {
                return JobCondition.Ongoing;
            }

            var designationManager = actor.Map.designationManager;
            var cell = curJob.GetTarget(index).Cell;
            if (designationManager.DesignationAt(cell, DesignationDefOf.Mine) == null &&
                designationManager.DesignationAt(cell, MineAllDef) == null)
            {
                return JobCondition.Incompletable;
            }

            return JobCondition.Ongoing;
        });
    }

    protected virtual void ResetTicksToPickHit()
    {
        var speed = pawn.GetStatValue(StatDefOf.MiningSpeed);
        if (speed < 0.6f && pawn.Faction != Faction.OfPlayer)
        {
            speed = 0.6f;
        }

        ticksToPickHit = (int)Math.Round(100f / speed);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref ticksToPickHit, "ticksToPickHit");
    }
}