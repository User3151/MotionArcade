using MotionArcade.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MotionArcade.UI
{
    // מחבר את סליידרי העוצמה בלוח ההגדרות ל-VolumeSettings
    public class VolumeSliders : MonoBehaviour
    {
        [SerializeField] private Slider master;
        [SerializeField] private Slider music;
        [SerializeField] private Slider sfx;

        private void OnEnable()
        {
            // הלוח נפתח: מציגים את הערכים השמורים. WithoutNotify כדי לא לכתוב אותם מחדש.
            VolumeSettings settings = VolumeSettings.Instance;
            if (settings != null)
            {
                master.SetValueWithoutNotify(settings.Get(VolumeChannel.Master));
                music.SetValueWithoutNotify(settings.Get(VolumeChannel.Music));
                sfx.SetValueWithoutNotify(settings.Get(VolumeChannel.Sfx));
            }
            master.onValueChanged.AddListener(SetMaster);
            music.onValueChanged.AddListener(SetMusic);
            sfx.onValueChanged.AddListener(SetSfx);
        }

        private void OnDisable()
        {
            master.onValueChanged.RemoveListener(SetMaster);
            music.onValueChanged.RemoveListener(SetMusic);
            sfx.onValueChanged.RemoveListener(SetSfx);
            if (VolumeSettings.Instance != null) VolumeSettings.Instance.Save();   // הלוח נסגר: שומרים לדיסק
        }

        private void SetMaster(float value) => Set(VolumeChannel.Master, value);
        private void SetMusic(float value) => Set(VolumeChannel.Music, value);
        private void SetSfx(float value) => Set(VolumeChannel.Sfx, value);

        private static void Set(VolumeChannel channel, float value)
        {
            if (VolumeSettings.Instance != null) VolumeSettings.Instance.Set(channel, value);
        }
    }
}
