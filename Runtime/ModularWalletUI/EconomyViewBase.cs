using System;
using BaseUnity.Core;
using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.EconomyModules.UI
{
    public abstract class EconomyViewBase : MonoBehaviour
    {
        public Text message;
        protected EconomyStore Store { get; private set; }
        private float nextRefresh;
        protected bool Ready { get; private set; }
        protected abstract bool Bind(ServiceRegistry services);
        protected abstract void RefreshView();
        private void OnEnable() { TryBind(); }
        private void TryBind()
        {
            var root=ModuleBootstrap.Instance;if(!root||!root.IsReady)return;
            EconomyStore store;if(!root.Services.TryGet(out store)||!Bind(root.Services))return;
            Store=store;Ready=true;Store.Changed+=Refresh;Refresh();
        }
        private void Update()
        { if(!Ready)TryBind();else if(UnityEngine.Time.unscaledTime>=nextRefresh) { nextRefresh=UnityEngine.Time.unscaledTime+.5f;Refresh(); } }
        private void OnDisable() { if(Store!=null)Store.Changed-=Refresh;Store=null;Ready=false; }
        private void Refresh() { if(Ready)RefreshView(); }
        protected void Execute(Func<TransactionResult> command)
        {
            if(!Ready)return;
            try { var result=command();if(message)message.text=result==TransactionResult.Success?"Success":result==TransactionResult.Duplicate?"Already received":"Not available"; }
            catch(Exception error) { if(message)message.text="Could not save. Please retry.";Debug.LogException(error,this); }
            Refresh();
        }
    }
}
