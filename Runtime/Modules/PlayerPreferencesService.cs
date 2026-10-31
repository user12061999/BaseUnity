using System;
using System.Linq;
using BaseUnity.Save;
using UnityEngine;
namespace BaseUnity.Features
{
    public sealed class PlayerPreferencesService
    {
        [Serializable] public sealed class Data
        {
            public string playerName = "PLAYER";
            public int avatar;
            public float controlScale = 1, sensitivity = 1, deadZone = .1f;
            public bool leftHanded;
            public Vector2 joystickOffset, actionOffset;
        }
        private readonly ISaveStore saves;
        private Data data = new Data();
        public string LoadError { get; private set; }
        public string LastError { get; private set; }
        public Data Current => JsonUtility.FromJson<Data>(JsonUtility.ToJson(data));
        public event Action Changed;
        public PlayerPreferencesService(ISaveStore saves)
        {
            this.saves = saves;
            try { data = ModulePersistence.Load(saves,"baseunity_player_preferences",Valid,new Data()); }
            catch(Exception error) { LoadError = LastError = error.Message; }
        }
        private static bool Range(float value,float min,float max) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;
        private static bool Valid(Data d) => d != null && !string.IsNullOrWhiteSpace(d.playerName) && d.playerName.Length <= 20 && !d.playerName.Any(char.IsControl) && !d.playerName.Contains("<") && !d.playerName.Contains(">") && d.avatar >= 0 && d.avatar < 4 && Range(d.controlScale,.7f,1.4f) && Range(d.sensitivity,.5f,2) && Range(d.deadZone,0,.4f) && Range(d.joystickOffset.x,-120,120) && Range(d.joystickOffset.y,-120,120) && Range(d.actionOffset.x,-120,120) && Range(d.actionOffset.y,-120,120);
        public bool Save(Data value)
        {
            if (LoadError != null) return false;
            var next = value == null ? null : JsonUtility.FromJson<Data>(JsonUtility.ToJson(value));
            if(next != null) next.playerName = next.playerName?.Trim();
            if(!Valid(next)) { LastError = "Invalid player preferences"; return false; }
            try { saves.Save("baseunity_player_preferences",next); }
            catch(Exception e) { LastError=e.Message; return false; }
            data=next; LastError=null; Changed?.Invoke(); return true;
        }
        public bool ResetControls() { var next=Current; next.controlScale=next.sensitivity=1; next.deadZone=.1f; next.leftHanded=false; next.joystickOffset=next.actionOffset=Vector2.zero; return Save(next); }
    }
}
