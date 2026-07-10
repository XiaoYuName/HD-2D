using System;
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
        [SerializeField] Image[] qualityImages;

        public event Action OnClose;

        bool isClickRegistered;

        public void Init(ItemInfo recipe, ItemInfo food, ItemInfo[] items)
        {
            recipeImage.SetIcon(GamePathTools.CombinationItemIconPath(recipe.GetIconName()));
            recipeNameText.text = recipe.GetNameKey();
            // recipeDescText.text = recipe.GetDesc();

            newRecipeText.text = "配方";
            newRecipeText.text = $"{items[0].GetNameKey()}";

            for(int i = 1; i < items.Length; i++)
                newRecipeText.text += $" + {items[i].GetNameKey()}";

            newRecipeText.text += $" = {recipe.GetNameKey()}";

            foodImage.SetIcon(GamePathTools.CombinationItemIconPath(food.GetIconName()));
            foodNameText.text = food.GetNameKey();

            for(int i = 0; i < qualityImages.Length; i++)
                qualityImages[i].gameObject.SetActive(false);

            gameObject.SetActive(true);
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
            UnregisterClick();
        }
    }
}
