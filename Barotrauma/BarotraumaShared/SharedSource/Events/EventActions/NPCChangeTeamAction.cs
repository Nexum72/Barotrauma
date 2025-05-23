﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Barotrauma
{
    /// <summary>
    /// Changes the team of an NPC. Most common use cases are adding a character to the crew, or turning an NPC hostile to the crew by changing their team to a hostile one.
    /// </summary>
    class NPCChangeTeamAction : EventAction
    {
        [Serialize("", IsPropertySaveable.Yes, description: "Tag of the NPC(s) whose team to change.")]
        public Identifier NPCTag { get; set; }

        [Serialize(CharacterTeamType.None, IsPropertySaveable.Yes, description: "The team to move the NPC to. None = unspecified, Team1 = player crew, Team2 = the team opposing Team1 (= hostile to player crew), FriendlyNPC = friendly to all other teams.")]
        public CharacterTeamType TeamID { get; set; }

        [Serialize(false, IsPropertySaveable.Yes, description: "Should the NPC be added to the player crew?")]
        public bool AddToCrew { get; set; }

        [Serialize(false, IsPropertySaveable.Yes, description: "Should the NPC be removed from the player crew?")]
        public bool RemoveFromCrew { get; set; }

        private bool isFinished = false;

        public NPCChangeTeamAction(ScriptedEvent parentEvent, ContentXElement element) : base(parentEvent, element) 
        {
            //backwards compatibility
            TeamID = element.GetAttributeEnum("teamtag", element.GetAttributeEnum<CharacterTeamType>("team", TeamID));

            var enums = Enum.GetValues(typeof(CharacterTeamType)).Cast<CharacterTeamType>();
            if (!enums.Contains(TeamID))
            {
                DebugConsole.ThrowError($"Error in {nameof(NPCChangeTeamAction)} in the event {ParentEvent.Prefab.Identifier}. \"{TeamID}\" is not a valid Team ID. Valid values are {string.Join(',', Enum.GetNames(typeof(CharacterTeamType)))}.",
                    contentPackage: element.ContentPackage);
            }
        }

        private List<Character> affectedNpcs = null;

        public override void Update(float deltaTime)
        {
            if (isFinished) { return; }

            bool isPlayerTeam = TeamID is CharacterTeamType.Team1 or CharacterTeamType.Team2;

            affectedNpcs = ParentEvent.GetTargets(NPCTag).OfType<Character>().ToList();
            foreach (Character npc in affectedNpcs)
            {
                // characters will still remain on friendlyNPC team for rest of the tick
                npc.SetOriginalTeamAndChangeTeam(TeamID);
                foreach (Item item in npc.Inventory.AllItems)
                {
                    var idCard = item.GetComponent<Items.Components.IdCard>();
                    if (idCard != null)
                    {
                        idCard.TeamID = TeamID;
                        if (isPlayerTeam)
                        {
                            idCard.SubmarineSpecificID = 0;
                        }
                    }
                }
                if (GameMain.GameSession.CrewManager is CrewManager crewManager)
                {
                    if (AddToCrew && isPlayerTeam)
                    {
                        if (npc.Info is CharacterInfo info)
                        {
                            info.StartItemsGiven = true;
                            crewManager.AddCharacter(npc);
                        }
                        else
                        {
                            DebugConsole.AddWarning($"Attempted to change the team of a character ({npc.Name}) that doesn't have Character Info. Can't add to the crew.");
                        }
                        ChangeItemTeam(Submarine.MainSub ?? Submarine.Loaded.FirstOrDefault(s => s.TeamID == TeamID), allowStealing: true);
                        if (GameMain.NetworkMember != null && GameMain.NetworkMember.IsServer)
                        {
                            GameMain.NetworkMember.CreateEntityEvent(npc, new Character.AddToCrewEventData(TeamID, npc.Inventory.FindAllItems(recursive: true)));
                        }                  
                    }
                    else if (RemoveFromCrew && npc.TeamID is CharacterTeamType.Team1 or CharacterTeamType.Team2)
                    {
                        if (npc.Info is CharacterInfo info)
                        {
                            info.StartItemsGiven = true;
                            crewManager.RemoveCharacter(npc, removeInfo: true);
                        }
                        else
                        {
                            DebugConsole.AddWarning($"Attempted to change the team of a character ({npc.Name}) that doesn't have Character Info. Can't remove from the crew.");
                        }
                        Submarine sub = Submarine.Loaded.FirstOrDefault(s => s.TeamID == TeamID);
                        ChangeItemTeam(sub, false);
                        if (GameMain.NetworkMember != null && GameMain.NetworkMember.IsServer)
                        {
                            GameMain.NetworkMember.CreateEntityEvent(npc, new Character.RemoveFromCrewEventData(TeamID, npc.Inventory.FindAllItems(recursive: true)));
                        }
                    }   
                }

                void ChangeItemTeam(Submarine sub, bool allowStealing)
                {
                    foreach (Item item in npc.Inventory.FindAllItems(recursive: true))
                    {
                        item.AllowStealing = allowStealing;
                        if (item.GetComponent<Items.Components.WifiComponent>() is { } wifiComponent)
                        {
                            wifiComponent.TeamID = TeamID;
                        }
                        if (item.GetComponent<Items.Components.IdCard>() is { } idCard)
                        {
                            idCard.SubmarineSpecificID = 0;
                        }
                    }
                    WayPoint subWaypoint =
                        WayPoint.WayPointList.Find(wp => wp.Submarine == sub && wp.SpawnType == SpawnType.Human && wp.AssignedJob == npc.Info?.Job?.Prefab) ??
                        WayPoint.WayPointList.Find(wp => wp.Submarine == sub && wp.SpawnType == SpawnType.Human);
                    if (subWaypoint != null)
                    {
                        npc.GiveIdCardTags(subWaypoint, createNetworkEvent: true);
                    }
                }
            }
            isFinished = true;
        }

        public override bool IsFinished(ref string goTo)
        {
            return isFinished;
        }

        public override void Reset()
        {
            isFinished = false;
        }

        public override string ToDebugString()
        {
            return $"{ToolBox.GetDebugSymbol(isFinished)} {nameof(NPCChangeTeamAction)} -> (NPCTag: {NPCTag.ColorizeObject()})";
        }
    }
}