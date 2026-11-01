using BaseUnity.Core;
using BaseUnity.Save;
namespace BaseUnity.Localization
{
    public sealed class LocalizationModule : ModuleInstaller
    {
        public LocalizationTable table;
        public string defaultLanguage="en";
        public override int Priority => -70;
        public override void Install(ModuleBootstrap bootstrap) { bootstrap.Services.Register(new LocalizationService(bootstrap.Services.Get<ISaveStore>(),table?table.entries:null,defaultLanguage)); }
    }
}
