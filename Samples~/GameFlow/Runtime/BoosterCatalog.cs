using System;
using UnityEngine;
namespace BaseUnity.GameFlow
{
    [Serializable] public sealed class BoosterDefinition
    {
        public string itemId, effectId, title;
        public Sprite icon;
        [Min(.01f)] public float amount = 15;
        [Min(0)] public float cooldownSeconds = 2;
        [Min(0)] public int maxUsesPerLevel = 3;
    }
    [CreateAssetMenu(menuName = "BaseUnity/Booster Catalog")]
    public sealed class BoosterCatalog : ScriptableObject
    {
        public bool enabled = true;
        public BoosterDefinition[] boosters = {
            new BoosterDefinition { itemId = "energy", effectId = "extra_time", title = "EXTRA TIME", amount = 15 },
            new BoosterDefinition { itemId = "ticket", effectId = "hint", title = "HINT", amount = 3 }
        };
    }
}
