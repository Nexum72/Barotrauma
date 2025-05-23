#nullable enable

namespace Barotrauma.Abilities
{
    internal sealed class CharacterAbilityGiveTalentPointsToAllies : CharacterAbility
    {
        private readonly int amount;

        public CharacterAbilityGiveTalentPointsToAllies(CharacterAbilityGroup characterAbilityGroup, ContentXElement abilityElement) : base(characterAbilityGroup, abilityElement)
        {
            amount = abilityElement.GetAttributeInt("amount", 0);
            if (amount == 0)
            {
                DebugConsole.ThrowError($"Error in talent {CharacterTalent.DebugIdentifier}, amount of talent points to give is 0.",
                    contentPackage: abilityElement.ContentPackage);
            }
        }

        public override void InitializeAbility(bool addingFirstTime)
        {
            if (!addingFirstTime) { return; }

            foreach (Character character in Character.GetFriendlyCrew(Character))
            {
                character.Info.AdditionalTalentPoints += amount;
            }
        }
    }
}