using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Localization.Settings;
using XFramework;

namespace XFramework
{
    /// <summary>
    /// 多语言管理器
    /// </summary>
    public class LanguageManager : MonoSingleton<LanguageManager>,IGameInitialized
    {
        [LabelText("当前选中Index")]
        public int LanguageIndex { get; private set; }

        /// <summary>
        /// 初始化脚本函数
        /// </summary>
        /// <returns></returns>
        public async UniTask Initialized()
        {
            LanguageIndex = PlayerPrefs.GetInt("LanguageIndex",0);
            SetLocalization(LanguageIndex);
            await UniTask.CompletedTask;
        }

        /// <summary>
        /// 释放脚本函数
        /// </summary>
        public async UniTask Release()
        {
            await UniTask.CompletedTask;
        }


        public void SetLocalization(int index)
        {
            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[index];
            PlayerPrefs.SetInt("LanguageIndex",index);
        }
    }
}

