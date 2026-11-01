using System;
using UnityEngine;
namespace BaseUnity.Localization
{
    [Serializable] public sealed class LocalizedEntry { public string key,english,vietnamese; }
    [CreateAssetMenu(menuName="BaseUnity/Modules/Localization Table")]
    public sealed class LocalizationTable : ScriptableObject { public LocalizedEntry[] entries=new LocalizedEntry[0]; }
}
