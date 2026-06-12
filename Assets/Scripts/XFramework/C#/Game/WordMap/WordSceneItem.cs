using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using XFramework;

public class WordSceneItem : GameBase
{
    [LabelText("当前场景数据"),ReadOnly]
    public GameSceneData gameSceneItemData;

    private SpriteRenderer _spriteRenderer;
    private LocalizeStringEvent labelText;
    
    public void Initialize(GameSceneData gameSceneData)
    {
        gameSceneItemData = gameSceneData;
        _spriteRenderer = Get<SpriteRenderer>("");
        _spriteRenderer.sprite = gameSceneItemData.word_icon;
        transform.localPosition = new Vector3(gameSceneData.WordPosition.x,gameSceneData.WordPosition.y,0);
        labelText = Get<LocalizeStringEvent>("LabelText");
        labelText.SetEntry(gameSceneData.scene_name);
        transform.DOMoveY(transform.localPosition.y + 0.05f,0.8f).SetLoops(-1,LoopType.Yoyo);
    }


    private Tweener scaleTweener;
    public void OnMouseEnter()
    {
        scaleTweener?.Kill();
        scaleTweener = transform.DOScale(Vector3.one * 1.1f,0.2f);
    }

    public void OnMouseExit()
    {
        scaleTweener?.Kill();
        scaleTweener = transform.DOScale(Vector3.one,0.2f);
    }

    public void OnMouseUp()
    {
        GameDataManager.Instance.EnterGameScene(gameSceneItemData.scene_id,gameSceneItemData.min_sceneList[0]);
    }
}
