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
            if (CheckScene())
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
            else
            {
                UIUtility.ShowPopWindow("提示","场景未开放","确定");
            }
        }
        
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        scaleTweener?.Kill();
        scaleTweener = transform.DOScale(Vector3.one * 1.1f,0.2f);
    }

    private bool CheckScene()
    {
        
        for (int i = 0; i < gameSceneItemData.SubScenes.Count; i++)
        {
            GameSceneData sceneData = GameSceneManager.Instance.GetGameSceneData(gameSceneItemData.SubScenes[i]);
            if(sceneData.PermanentScene == PermanentSceneType.Permanent)break; //如果是常驻场景
            if (sceneData.UnlockConditionsID.Count <= 0) break;//如果解锁ID没有
            for (int j = 0; j < sceneData.UnlockConditionsID.Count; j++)
            {
                var ulockData = LubanManager.Instance.TbUnlockConditionsData.Get(sceneData.UnlockConditionsID[j]);
                if (ulockData.UnlockConditionsType.HasFlag(UnlockConditionsType.None)) break; //所有是Node

                if (ulockData.UnlockConditionsType.HasFlag(UnlockConditionsType.Prop))
                {
                    if (GameDataManager.Instance.GetProperty(ulockData.TbUlocakPropData.PropType).Value <
                        ulockData.TbUlocakPropData.Value)
                    {
                        return false;
                    }
                }

                if (ulockData.UnlockConditionsType.HasFlag(UnlockConditionsType.Item))
                {
                    if (InventoryManager.Instance.GetItemCount(ulockData.TbUlocakItemData.ItemID) <
                        ulockData.TbUlocakItemData.Value)
                    {
                        return false;
                    }
                }

                if (ulockData.UnlockConditionsType.HasFlag(UnlockConditionsType.Character))
                {
                    var characterBag =
                        CharacterManager.Instance.GetCharacterBag(ulockData.TbUlockCharacterData.CharacterID);
                    if (characterBag == null) return false;
                    if (ulockData.TbUlockCharacterData.CharacterType == CharacterPropType.Feeling)
                    {
                        if (characterBag.Feeling < ulockData.TbUlockCharacterData.Value)
                        {
                            return false;
                        }
                    }
                    else if (ulockData.TbUlockCharacterData.CharacterType == CharacterPropType.Goodwill)
                    {
                        if (characterBag.Favorability < ulockData.TbUlockCharacterData.Value)
                        {
                            return false;
                        }
                    }
                }

                if (ulockData.UnlockConditionsType.HasFlag(UnlockConditionsType.Date))
                {
                    if (!ulockData.TbUlockDateData.WeekFlag.HasFlag(GameDataManager.Instance.PlayerData.GetWeekType()))
                    {
                        return false;
                    }

                    if (!ulockData.TbUlockDateData.TimeFlag.HasFlag(GameDataManager.Instance.PlayerData.GetTimeType()))
                    {
                        return false;
                    }
                }
            }




        }
        return true;
    }
}
