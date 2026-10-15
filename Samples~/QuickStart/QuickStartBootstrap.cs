using System;
using BaseUnity.Core;
using BaseUnity.Diagnostics;
using BaseUnity.Save;
using BaseUnity.Time;
using UnityEngine;

namespace BaseUnity.Samples
{
    public sealed class QuickStartBootstrap : GameBootstrap
    {
        [Serializable]
        public sealed class PlayerData { public int coins; }
        private IDisposable subscription;
        private TimerService.TimerHandle timer;

        protected override void OnReady()
        {
            var saves = Services.Get<ISaveStore>();
            if (!saves.TryLoad<PlayerData>("player", out var data)) data = new PlayerData();
            GameLog.Info("Sample", $"Loaded {data.coins} coins.");
            subscription = Services.Get<EventBus>().Subscribe<int>(coins => GameLog.Info("Sample", $"Coins: {coins}"));
            timer = Services.Get<TimerService>().Schedule(1, () =>
            {
                data.coins++;
                saves.Save("player", data);
                Services.Get<EventBus>().Publish(data.coins);
            });
        }

        protected override void OnDestroy()
        {
            timer?.Dispose();
            subscription?.Dispose();
            base.OnDestroy();
        }
    }
}
