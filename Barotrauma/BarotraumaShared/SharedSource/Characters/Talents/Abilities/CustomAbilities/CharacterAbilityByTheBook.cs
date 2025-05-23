﻿using System.Collections.Generic;
using System.Linq;

namespace Barotrauma.Abilities
{
    class CharacterAbilityByTheBook : CharacterAbility
    {
        private readonly int moneyAmount;
        private readonly int experienceAmount;
        private readonly int max;

        public CharacterAbilityByTheBook(CharacterAbilityGroup characterAbilityGroup, ContentXElement abilityElement) : base(characterAbilityGroup, abilityElement)
        {
            moneyAmount = abilityElement.GetAttributeInt("moneyamount", 0);
            experienceAmount = abilityElement.GetAttributeInt("experienceamount", 0);
            max = abilityElement.GetAttributeInt("max", 0);
        }

        protected override void ApplyEffect()
        {
            IEnumerable<Character> enemyCharacters = Character.CharacterList.Where(c => !Character.IsFriendly(c));

            int timesGiven = 0;
            foreach (Character enemyCharacter in enemyCharacters)
            {
                if (!enemyCharacter.IsHuman) { continue; }
                if (enemyCharacter.Submarine == null || 
                    (Submarine.MainSub != null && enemyCharacter.Submarine != Submarine.MainSub)) 
                {
                    continue;
                }
                if (enemyCharacter.IsDead) { continue; }
                if (!enemyCharacter.LockHands) { continue; }
                Character.GiveMoney(moneyAmount);
                GameAnalyticsManager.AddMoneyGainedEvent(moneyAmount, GameAnalyticsManager.MoneySource.Ability, CharacterTalent.Prefab.Identifier.Value);
                foreach (Character character in Character.GetFriendlyCrew(Character))
                {
                    character.Info.GiveExperience(experienceAmount);
                }
                timesGiven++;
                if (max > 0 && timesGiven >= max) { break; }
            }

        }
    }
}
