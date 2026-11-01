using System;
using System.Threading.Tasks;
using BaseUnity.Flow;
using NUnit.Framework;
namespace BaseUnity.Tests
{
    public sealed class PortableGameFlowTests
    {
        [Test] public async Task FullLoopGuardsResultsAndFinalLevel()
        {
            var flow=new GameFlowService(2);
            Assert.IsFalse(flow.Win());Assert.IsFalse(await flow.NextAsync());
            Assert.IsTrue(await flow.HomeAsync());Assert.IsTrue(await flow.PlayAsync());
            Assert.IsTrue(flow.Pause());Assert.IsFalse(flow.Win());Assert.IsFalse(flow.CanPlay);
            Assert.IsTrue(flow.Resume());Assert.IsTrue(flow.Lose());Assert.IsFalse(flow.Win());
            Assert.IsTrue(await flow.RetryAsync());Assert.AreEqual(1,flow.Level);
            Assert.IsTrue(flow.Win());Assert.IsFalse(flow.Lose());Assert.IsTrue(await flow.NextAsync());
            Assert.AreEqual(2,flow.Level);Assert.IsTrue(flow.Win());Assert.IsFalse(flow.CanNext);
            Assert.IsFalse(await flow.NextAsync());Assert.IsTrue(await flow.HomeAsync());Assert.AreEqual(2,flow.Level);
        }
        [Test] public async Task BusyLoadRejectsRepeatedCommandsAndFailureRestoresState()
        {
            var pending=new TaskCompletionSource<bool>();
            var flow=new GameFlowService(2,home=>home?Task.CompletedTask:pending.Task);
            await flow.HomeAsync();var play=flow.PlayAsync();
            Assert.AreEqual(GamePhase.Loading,flow.Phase);Assert.IsFalse(await flow.PlayAsync());Assert.IsFalse(await flow.HomeAsync());
            pending.SetException(new InvalidOperationException("load failed"));
            try { await play;Assert.Fail("Must propagate failure"); } catch(InvalidOperationException) { }
            Assert.AreEqual(GamePhase.Home,flow.Phase);Assert.AreEqual(1,flow.Level);
        }
        [Test] public async Task FailedNextDoesNotAdvanceAndCanRetry()
        {
            bool fail=false;var flow=new GameFlowService(3,home=>fail?Task.FromException(new Exception("failed")):Task.CompletedTask);
            await flow.HomeAsync();await flow.PlayAsync();flow.Win();fail=true;
            Assert.ThrowsAsync<Exception>(async ()=> { await flow.NextAsync(); });
            Assert.AreEqual(1,flow.Level);Assert.AreEqual(GamePhase.Won,flow.Phase);
            fail=false;Assert.IsTrue(await flow.NextAsync());Assert.AreEqual(2,flow.Level);
        }
        [Test] public async Task DisposedPendingLoadCannotPublishNewState()
        {
            var pending=new TaskCompletionSource<bool>();var flow=new GameFlowService(1,home=>pending.Task);
            var task=flow.HomeAsync();flow.Dispose();pending.SetResult(true);
            Assert.IsFalse(await task);Assert.IsFalse(await flow.HomeAsync());Assert.IsFalse(flow.Win());
        }
        [Test] public async Task ObserverFailuresDoNotCorruptFlow()
        {
            int calls=0;var flow=new GameFlowService();flow.Changed+=()=>throw new Exception("observer");flow.Changed+=()=>calls++;
            await flow.HomeAsync();Assert.AreEqual(GamePhase.Home,flow.Phase);Assert.AreEqual(2,calls);
        }
    }
}
