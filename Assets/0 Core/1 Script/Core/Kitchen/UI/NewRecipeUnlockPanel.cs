using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XFramework
{
    public class NewRecipeUnlockPanel : MonoBehaviour
    {
        [SerializeField] Image recipeImage, foodImage;
        [SerializeField] TextMeshProUGUI recipeNameText, foodNameText;
        [SerializeField] TextMeshProUGUI recipeDescText, newRecipeText;
        [SerializeField] TextMeshProUGUI titleText, closeTipText;
        [SerializeField] Image[] qualityImages;

        public event Action OnClose;

        bool isClickRegistered;
        Coroutine registerClickCt;

        public void Init(ItemInfo recipe, ItemInfo food, ItemInfo[] items)
        {
            recipeImage.SetIcon(GamePathTools.CombinationItemIconPath(recipe.GetIconName()));
            recipeNameText.text = recipe.GetName();

            newRecipeText.text = $"{items[0].GetName()}";
            for(int i = 1; i < items.Length; i++)
                newRecipeText.text += $" + {items[i].GetName()}";
            newRecipeText.text += $" = {recipe.GetName()}";

            foodImage.SetIcon(GamePathTools.CombinationItemIconPath(food.GetIconName()));
            foodNameText.text = food.GetName();

            titleText.text = LanguageManager.Instance.GetLocalizedString(LocTableSet.Kitchen, LocVarSet.MiniGame1CookGame.NewRecipeUnlockTitle);
            closeTipText.text = LanguageManager.Instance.GetLocalizedString(LocTableSet.Kitchen, LocVarSet.MiniGame1CookGame.NewRecipeCloseTip);

            for(int i = 0; i < qualityImages.Length; i++)
                qualityImages[i].gameObject.SetActive(false);

            gameObject.SetActive(true);

            // 打开面板所依赖的输入（点击/空格）与关闭监听若同帧注册，会被同一次输入立刻关闭；延后一帧再注册
            if(registerClickCt != null)
                StopCoroutine(registerClickCt);
            registerClickCt = StartCoroutine(RegisterClickNextFrame());
        }

        IEnumerator RegisterClickNextFrame()
        {
            yield return null;
            RegisterClick();
        }

        void RegisterClick()
        {
            if(isClickRegistered)
                return;

            PlayerInputManager.Instance.OnClick += OnClickClose;
            isClickRegistered = true;
        }

        void UnregisterClick()
        {
            if(!isClickRegistered)
                return;

            PlayerInputManager.Instance.OnClick -= OnClickClose;
            isClickRegistered = false;
        }

        void OnClickClose()
        {
            UnregisterClick();
            gameObject.SetActive(false);
            OnClose?.Invoke();
        }

        void OnDisable()
        {
            if(registerClickCt != null)
            {
                StopCoroutine(registerClickCt);
                registerClickCt = null;
            }
            UnregisterClick();
        }
    }
}
