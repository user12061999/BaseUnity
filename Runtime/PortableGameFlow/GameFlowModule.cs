using System;
using System.Threading.Tasks;
using BaseUnity.Core;
using BaseUnity.Scenes;
using UnityEngine;
namespace BaseUnity.Flow
{
    public sealed class GameFlowModule : ModuleInstaller
    {
        [Min(1)] public int levelCount=10;
        [Tooltip("Leave empty for a single-scene game.")] public string homeScene="";
        public string ingameScene="";
        public bool startAtHome=true;
        public GameFlowService Flow { get; private set; }
        private SceneService scenes;
        private bool started;
        public override void Install(ModuleBootstrap bootstrap)
        {
            scenes=new SceneService(); bootstrap.Services.Register(scenes);
            Flow=new GameFlowService(Mathf.Max(1,levelCount),Load); Flow.ObserverError+=Log;
            bootstrap.Services.Register(Flow);
        }
        private Task Load(bool home)
        { string scene=home?homeScene:ingameScene; return string.IsNullOrWhiteSpace(scene)?Task.CompletedTask:scenes.LoadAsync(scene); }
        private void Log(Exception error) { Debug.LogException(error,this); }
        public override void Tick(float deltaTime,float unscaledDeltaTime)
        { if(started||!startAtHome)return; started=true; Home(); }
        public override void Focus(bool focused) { if(!focused)Flow?.Pause(); }
        // UnityEvent entry points. Awaited service methods remain available to game code.
        private async void Run(Func<Task<bool>> command) { try { await command(); } catch(Exception error) { Log(error); } }
        public void Home() { if(Flow!=null)Run(Flow.HomeAsync); }
        public void Play() { if(Flow!=null)Run(Flow.PlayAsync); }
        public void Retry() { if(Flow!=null)Run(Flow.RetryAsync); }
        public void Next() { if(Flow!=null)Run(Flow.NextAsync); }
        public void Pause() { Flow?.Pause(); }
        public void Resume() { Flow?.Resume(); }
        public void Win() { Flow?.Win(); }
        public void Lose() { Flow?.Lose(); }
    }
}
