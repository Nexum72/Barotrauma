﻿using Barotrauma.Extensions;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Barotrauma
{
    class AIObjectiveReturn : AIObjective
    {
        public override Identifier Identifier { get; set; } = "return".ToIdentifier();
        public Submarine Target { get; }

        private AIObjectiveGoTo moveInsideObjective, moveOutsideObjective;
        private bool usingEscapeBehavior, isSteeringThroughGap;

        protected override bool AllowOutsideSubmarine => true;
        protected override bool AllowInAnySub => true;

        public AIObjectiveReturn(Character character, Character orderGiver, AIObjectiveManager objectiveManager, float priorityModifier = 1.0f) : base(character, objectiveManager, priorityModifier)
        {
            Target = GetReturnTarget(Submarine.MainSubs) ?? GetReturnTarget(Submarine.Loaded);
            if (Target == null)
            {
                if (GameMain.GameSession?.GameMode is not TestGameMode)
                {
                    DebugConsole.AddWarning($"({character.DisplayName}) No suitable return target found. Cannot return back to the main sub.");   
                }
                Abandon = true;
            }

            Submarine GetReturnTarget(IEnumerable<Submarine> subs)
            {
                var requiredTeamID = orderGiver?.TeamID ?? character?.TeamID;
                Submarine returnTarget = null;
                foreach (var sub in subs)
                {
                    if (sub == null) { continue; }
                    if (sub.TeamID != requiredTeamID) { continue; }
                    returnTarget = sub;
                    break;
                }
                return returnTarget;
            }
        }

        protected override float GetPriority()
        {
            if (!Abandon && !IsCompleted && objectiveManager.IsOrder(this))
            {
                Priority = objectiveManager.GetOrderPriority(this);
            }
            else
            {
                Priority = AIObjectiveManager.LowestOrderPriority - 1;
            }
            return Priority;
        }

        protected override void Act(float deltaTime)
        {
            if (Target == null)
            {
                Abandon = true;
                return;
            }
            bool shouldUseEscapeBehavior = false;
            if (character.CurrentHull != null || isSteeringThroughGap)
            {
                if (character.Submarine == null || !character.Submarine.IsConnectedTo(Target))
                {
                    // Character is on another sub that is not connected to the target sub, use the escape behavior to get them out
                    shouldUseEscapeBehavior = true;
                    if (!usingEscapeBehavior)
                    {
                        HumanAIController.ResetEscape();
                    }
                    isSteeringThroughGap = HumanAIController.Escape(deltaTime);
                    if (!isSteeringThroughGap && (HumanAIController.EscapeTarget == null || HumanAIController.IsCurrentPathUnreachable))
                    {
                        Abandon = true;
                    }
                }
                else if (character.Submarine != Target)
                {
                    // Character is on another sub that is connected to the target sub, create a Go To objective to reach the target sub
                    if (moveInsideObjective == null)
                    {
                        Hull targetHull = null;
                        foreach (var d in Target.ConnectedDockingPorts.Values)
                        {
                            if (!d.Docked) { continue; }
                            if (d.DockingTarget == null) { continue; }
                            if (d.DockingTarget.Item.Submarine != character.Submarine) { continue; }
                            targetHull = d.Item.CurrentHull;
                            break;
                        }
                        if (targetHull != null && !targetHull.IsAirlock)
                        {
                            // Target the closest airlock
                            float closestDist = 0;
                            Hull airlock = null;
                            foreach (Hull hull in Hull.HullList)
                            {
                                if (hull.Submarine != targetHull.Submarine) { continue; }
                                if (!hull.IsAirlock) { continue; }
                                float dist = Vector2.DistanceSquared(targetHull.Position, hull.Position);
                                if (airlock == null || closestDist <= 0 || dist < closestDist)
                                {
                                    airlock = hull;
                                    closestDist = dist;
                                }
                                
                            }
                            if (airlock != null)
                            {
                                targetHull = airlock;
                            }
                        }
                        if (targetHull != null)
                        {
                            RemoveSubObjective(ref moveOutsideObjective);
                            TryAddSubObjective(ref moveInsideObjective,
                                constructor: () => new AIObjectiveGoTo(targetHull, character, objectiveManager)
                                {
                                    AllowGoingOutside = true,
                                    endNodeFilter = n => n.Waypoint.Submarine == targetHull.Submarine
                                },
                                onCompleted: () => RemoveSubObjective(ref moveInsideObjective),
                                onAbandon: () => Abandon = true);
                        }
                        else
                        {
#if DEBUG
                            DebugConsole.ThrowError("Error with a Return objective: no suitable target for 'moveInsideObjective'");
#endif
                        }
                    }
                }
                else
                {
                    // Character is on the target sub, the objective is completed
                    IsCompleted = true;
                }
            }
            else if (!isSteeringThroughGap && moveOutsideObjective == null)
            {
                Hull targetHull = null;
                float targetDistanceSquared = float.MaxValue;
                bool targetIsAirlock = false;
                foreach (var hull in Target.GetHulls(false))
                {
                    bool hullIsAirlock = hull.IsAirlock;
                    if(hullIsAirlock || (!targetIsAirlock && hull.LeadsOutside(character)))
                    {
                        float distanceSquared = Vector2.DistanceSquared(character.WorldPosition, hull.WorldPosition);
                        if (targetHull == null || distanceSquared < targetDistanceSquared)
                        {
                            targetHull = hull;
                            targetDistanceSquared = distanceSquared;
                            targetIsAirlock = hullIsAirlock;
                        }
                    }
                }
                if (targetHull != null)
                {
                    RemoveSubObjective(ref moveInsideObjective);
                    TryAddSubObjective(ref moveOutsideObjective,
                        constructor: () => new AIObjectiveGoTo(targetHull, character, objectiveManager)
                        {
                            AllowGoingOutside = true
                        },
                        onCompleted: () => RemoveSubObjective(ref moveOutsideObjective),
                        onAbandon: () => Abandon = true);
                }
                else
                {
#if DEBUG
                    DebugConsole.ThrowError("Error with a Return objective: no suitable target for 'moveOutsideObjective'");
#endif
                }
            }
            usingEscapeBehavior = shouldUseEscapeBehavior;
        }

        protected override bool CheckObjectiveState()
        {
            if (Target == null)
            {
                Abandon = true;
                return false;
            }
            if (character.Submarine == Target)
            {
                IsCompleted = true;
            }
            return IsCompleted;
        }

        public override void Reset()
        {
            base.Reset();
            moveInsideObjective = null;
            moveOutsideObjective = null;
            usingEscapeBehavior = false;
            isSteeringThroughGap = false;
            HumanAIController.ResetEscape();
        }

        protected override void OnAbandon()
        {
            base.OnAbandon();
            SteeringManager?.Reset();
            if (character.IsOnPlayerTeam && objectiveManager.CurrentOrder == objectiveManager.CurrentObjective)
            {
                string msg = TextManager.Get("dialogcannotreturn").Value;
                if (!msg.IsNullOrEmpty())
                {
                    character.Speak(msg, identifier: "dialogcannotreturn".ToIdentifier(), minDurationBetweenSimilar: 5.0f);
                }
            }
        }
    }
}