﻿using Barotrauma.Networking;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Barotrauma
{
    partial class CharacterInfo
    {
        private readonly Dictionary<Identifier, float> prevSentSkill = new Dictionary<Identifier, float>();

        /// <summary>
        /// The client opted to create a new character and discard this one
        /// </summary>
        public bool Discarded;

        public void ApplyDeathEffects()
        {
            RespawnManager.ReduceCharacterSkillsOnDeath(this);
            RemoveSavedStatValuesOnDeath();
            CauseOfDeath = null;
        }

        partial void OnSkillChanged(Identifier skillIdentifier, float prevLevel, float newLevel, bool forceNotification)
        {
            if (Character == null || Character.Removed) { return; }
            if (!prevSentSkill.ContainsKey(skillIdentifier))
            {
                prevSentSkill[skillIdentifier] = prevLevel;
            }
            if (Math.Abs(prevSentSkill[skillIdentifier] - newLevel) > 0.1f || forceNotification)
            {
                GameMain.NetworkMember.CreateEntityEvent(Character, new Character.UpdateSkillsEventData(skillIdentifier, forceNotification));
                prevSentSkill[skillIdentifier] = newLevel;
            }            
        }

        partial void OnExperienceChanged(int prevAmount, int newAmount)
        {
            if (Character == null || Character.Removed) { return; }
            if (prevAmount != newAmount)
            {
                GameServer.Log($"{GameServer.CharacterLogName(Character)} has gained {newAmount - prevAmount} experience ({prevAmount} -> {newAmount})", ServerLog.MessageType.Talent);
                GameMain.NetworkMember.CreateEntityEvent(Character, new Character.UpdateExperienceEventData());
            }
        }

        partial void OnPermanentStatChanged(StatTypes statType)
        {
            if (Character == null || Character.Removed) { return; }
            GameMain.NetworkMember.CreateEntityEvent(Character, new Character.UpdatePermanentStatsEventData(statType));
        }

        public void ServerWrite(IWriteMessage msg)
        {
            msg.WriteUInt16(ID);
            msg.WriteString(Name);
            msg.WriteString(OriginalName);
            msg.WriteBoolean(RenamingEnabled);
            msg.WriteByte((byte)BotStatus);
            msg.WriteInt32(Salary);
            msg.WriteByte((byte)Head.Preset.TagSet.Count);
            foreach (Identifier tag in Head.Preset.TagSet)
            {
                msg.WriteIdentifier(tag);
            }
            msg.WriteByte((byte)Head.HairIndex);
            msg.WriteByte((byte)Head.BeardIndex);
            msg.WriteByte((byte)Head.MoustacheIndex);
            msg.WriteByte((byte)Head.FaceAttachmentIndex);
            msg.WriteColorR8G8B8(Head.SkinColor);
            msg.WriteColorR8G8B8(Head.HairColor);
            msg.WriteColorR8G8B8(Head.FacialHairColor);
            
            msg.WriteIdentifier(HumanPrefabIds.NpcIdentifier);
            msg.WriteIdentifier(MinReputationToHire.factionId);
            if (!MinReputationToHire.factionId.IsEmpty)
            {
                msg.WriteSingle(MinReputationToHire.reputation);
            }
            if (Job != null)
            {
                msg.WriteUInt32(Job.Prefab.UintIdentifier);
                msg.WriteByte((byte)Job.Variant);

                var skills = Job.GetSkills().OrderBy(s => s.Identifier);
                msg.WriteByte((byte)skills.Count());
                foreach (var skill in skills)
                {
                    msg.WriteIdentifier(skill.Identifier);
                    msg.WriteSingle(skill.Level);
                }
            }
            else
            {
                msg.WriteUInt32((uint)0);
                msg.WriteByte((byte)0);
            }

            msg.WriteInt32(ExperiencePoints);
            msg.WriteRangedInteger(AdditionalTalentPoints, 0, MaxAdditionalTalentPoints);
            msg.WriteBoolean(PermanentlyDead);
            msg.WriteInt32(TalentRefundPoints);
            msg.WriteInt32(TalentResetCount);
        }
    }
}
