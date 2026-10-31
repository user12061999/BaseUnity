using BaseUnity.Core;
namespace BaseUnity.Save
{
    public sealed class SaveModule : ModuleInstaller
    {
        public override int Priority => -100;
        public override void Install(ModuleBootstrap bootstrap) { bootstrap.Services.Register<ISaveStore>(new JsonSaveStore(BuildEnvironment.SaveDirectory)); }
    }
}
