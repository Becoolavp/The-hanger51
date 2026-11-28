using System.Collections.Generic;
using Hanger51.Inventory;
using UnityEngine;

namespace Hanger51.Weapons
{
    [DisallowMultipleComponent]
    public sealed class H51CombatItemRegistry : MonoBehaviour
    {
        [SerializeField] List<H51CombatItemDefinition> definitions =
            new List<H51CombatItemDefinition>();

        public IReadOnlyList<H51CombatItemDefinition> Definitions => definitions;

        public void Configure(List<H51CombatItemDefinition> configured)
        {
            definitions = configured ?? new List<H51CombatItemDefinition>();
        }

        public H51CombatItemDefinition Find(InventoryItemDefinition item)
        {
            if (!item || definitions == null)
                return null;

            for (int i=0;i<definitions.Count;i++)
            {
                H51CombatItemDefinition definition = definitions[i];
                if (definition && definition.InventoryItem == item)
                    return definition;
            }

            return null;
        }

        public H51CombatItemDefinition FindAmmo(H51Cartridge cartridge)
        {
            if (definitions == null)
                return null;

            for (int i=0;i<definitions.Count;i++)
            {
                H51CombatItemDefinition definition = definitions[i];
                if (definition &&
                    definition.Kind == H51CombatItemKind.Ammunition &&
                    definition.Cartridge == cartridge)
                    return definition;
            }

            return null;
        }

        public List<H51CombatItemDefinition> FindAllAmmo(H51Cartridge cartridge)
        {
            List<H51CombatItemDefinition> matches =
                new List<H51CombatItemDefinition>();

            if (definitions == null)
                return matches;

            for (int i=0;i<definitions.Count;i++)
            {
                H51CombatItemDefinition definition = definitions[i];

                if (definition &&
                    definition.Kind == H51CombatItemKind.Ammunition &&
                    definition.Cartridge == cartridge &&
                    definition.InventoryItem &&
                    !matches.Contains(definition))
                {
                    matches.Add(definition);
                }
            }

            return matches;
        }
    }
}
