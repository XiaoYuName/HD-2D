using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        recipeImage.SetIcon(recipe.IconPath);
        recipeNameText.text = recipe.Name;
        // recipeDescText.text = recipe.Desc;

        newRecipeText.text = "配方";
        newRecipeText.text = $"{items[0].Name}";

        for(int i = 1; i < items.Length; i++)
            newRecipeText.text += $" + {items[i].Name}";

        newRecipeText.text += $" = {recipe.Name}";

        foodImage.SetIcon(food.IconPath);
        foodNameText.text = food.Name;

        for(int i = 0; i < qualityImages.Length; i++)
            qualityImages[i].gameObject.SetActive(false);

        gameObject.SetActive(true);
        RegisterClick();
    }

    void RegisterClick()
    {
        if(isClickRegistered)
            return;

        PlayerInputManager.St.OnClick += OnClickClose;
        isClickRegistered = true;
    }

    void UnregisterClick()
    {
        if(!isClickRegistered)
            return;

        PlayerInputManager.St.OnClick -= OnClickClose;
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
