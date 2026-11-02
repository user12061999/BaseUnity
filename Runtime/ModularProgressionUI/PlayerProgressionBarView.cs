using BaseUnity.Core;
using BaseUnity.EconomyModules.UI;
using UnityEngine.UI;
namespace BaseUnity.Progression.UI
{
    public sealed class PlayerProgressionBarView : EconomyViewBase
    {
        public Text levelLabel,experienceLabel;
        public Slider progress;
        private PlayerProgressionService progression;
        protected override bool Bind(ServiceRegistry services) => services.TryGet(out progression);
        protected override void RefreshView()
        {
            if(levelLabel)levelLabel.text="Player level "+progression.Level;
            if(experienceLabel)experienceLabel.text=progression.IsMaximumLevel?"MAX LEVEL":"EXP "+progression.ExperienceIntoLevel+" / "+progression.ExperiencePerLevel;
            if(progress) { progress.minValue=0;progress.maxValue=1;progress.SetValueWithoutNotify(progression.IsMaximumLevel?1:(float)((double)progression.ExperienceIntoLevel/progression.ExperiencePerLevel)); }
        }
    }
}
