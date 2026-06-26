using System;
using Coffee.UIEffects;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using XFramework;

public class CharacterPortraitController : MonoBehaviour
{
    private Image image;
    private UIEffect uiEffect;
    public NpcData npcData { get; private set; }
    
    private Tweener tweener;

    public void Awake()
    {
        image = GetComponent<Image>();
        uiEffect = GetComponent<UIEffect>();
    }

    public void SetData(NpcData npcData)
    {
        this.npcData = npcData;
        string path = $"{AssetsPaths.DialogueTexturePath}{npcData.MiniImg}.png";
        image.sprite = AssetsManager.Instance.LoadAssets<Sprite>(path);
        tweener?.Kill();
        tweener = DOTween.To(() => uiEffect.colorIntensity, x => uiEffect.colorIntensity = x, 0, 0.2f);
    }

    public void SetMask()
    {
        
        tweener?.Kill();
        tweener = DOTween.To(() => uiEffect.colorIntensity, x => uiEffect.colorIntensity = x, 1, 0.2f);
    }

    public void Release()
    {
        tweener?.Kill();
        uiEffect.colorIntensity = 0;
        if (npcData != null)
        {
            string path = $"{AssetsPaths.DialogueTexturePath}{npcData.MiniImg}.png";
            AssetsManager.Instance.FreeAsset(path);
        }
    }
}
