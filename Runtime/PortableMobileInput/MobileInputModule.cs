using BaseUnity.Core;
using BaseUnity.Input;
namespace BaseUnity.MobileInput
{
    public sealed class MobileInputModule : ModuleInstaller
    {
        private ManualInputSource manual;
        public override void Install(ModuleBootstrap bootstrap) { bootstrap.Services.Register(manual=new ManualInputSource()); bootstrap.Services.Register(new InputRouter(manual)); }
        public override void Focus(bool focused) { if(!focused) manual?.Reset(); }
    }
}
