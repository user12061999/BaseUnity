using System;
using System.Linq;
using UnityEngine;

namespace BaseUnity.GameFlow
{
    [CreateAssetMenu(menuName = "BaseUnity/Home Hub Presentation")]
    public sealed class HomeHubCatalog : ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            public string id, titleEnglish, titleVietnamese;
            [TextArea] public string descriptionEnglish, descriptionVietnamese;
            public Sprite icon;
        }
        public string playerName = "PLAYER";
        public Sprite avatar;
        [Tooltip("Presentation only. Prices and rewards belong to FeatureConfig; IDs must match those catalogs.")]
        public Entry[] entries = Array.Empty<Entry>();
        public Entry Find(string id) => (entries ?? Array.Empty<Entry>()).FirstOrDefault(item => item != null && item.id == id);
    }
}
