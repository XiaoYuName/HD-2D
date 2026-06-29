using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Components;
using XFramework;

public class WordSceneItem : GameBase,IPointerEnterHandler,IPointerExitHandler,IPointerClickHandler
{
    [LabelText("当前场景数据"),ReadOnly]
    public WordMapSceneData gameSceneItemData;

    [FoldoutGroup("数据"),LabelText("场景ID")]
    public long SceneID;
    
    private Tweener movYTweener;
    
    public void Init()
    {
        gameSceneItemData = GameSceneManager.Instance.GetWordMapSceneData(SceneID);
        movYTweener?.Kill();
        movYTweener = transform.DOMoveY(transform.localPosition.y + 0.05f,0.8f).SetLoops(-1,LoopType.Yoyo);
    }


    private void OnDestroy()
    {
        movYTweener?.Kill();
        movYTweener = null;
        scaleTweener?.Kill();
        scaleTweener = null;
    }


    private Tweener scaleTweener;

    public void OnPointerExit(PointerEventData eventData)
    {
        scaleTweener?.Kill();
        scaleTweener = transform.DOScale(Vector3.one,0.2f);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (gameSceneItemData != null)
        {
            if (gameSceneItemData.SubScenes.Count > 1)
            {
                UISystem.Instance.OpenUI<WordMapInfoUI>("WordMapInfoUI").ShowData(gameSceneItemData);
            }
            else
            {
                GameSceneManager.Instance.EnterGameScene(gameSceneItemData.ID,gameSceneItemData.SubScenes[0]);
            }
        }
        
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        scaleTweener?.Kill();
        scaleTweener = transform.DOScale(Vector3.one * 1.1f,0.2f);
    }
}
