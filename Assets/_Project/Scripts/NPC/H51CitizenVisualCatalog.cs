using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hanger51.NPCs
{
    [CreateAssetMenu(menuName = "Hanger 51/NPC/Citizen Visual Catalog", fileName = "H51CitizenVisualCatalog")]
    public sealed class H51CitizenVisualCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string packId;
            public string archetype;
            public string description;
            public GameObject prefab;
        }

        public List<Entry> entries = new List<Entry>();
    }
}
