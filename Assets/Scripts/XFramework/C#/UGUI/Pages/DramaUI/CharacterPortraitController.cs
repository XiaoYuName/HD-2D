using System;
using Coffee.UIEffects;
using UnityEngine;
using UnityEngine.UI;
using XFramework;

public class CharacterPortraitController : MonoBehaviour
{
    private Image image;
    private UIEffect uiEffect;
    public NpcData npcData { get; private set; }

    public void Awake()
    {
        image = GetComponent<Image>();
        uiEffect = GetComponent<UIEffect>();
    }

    public void SetData(NpcData npcData)
    {
        this.npcData = npcData;
        image.sprite = AssetsManager.Instance.LoadAssets<Sprite>(npcData.MiniImg);
    }

    public void SetMask()
    {
        
    }

    public void Release()
    {
        if (npcData != null)
        {
            AssetsManager.Instance.FreeAsset(npcData.MiniImg);
        }
    }
}
