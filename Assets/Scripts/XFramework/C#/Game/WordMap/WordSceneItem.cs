using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using XFramework;

public class WordSceneItem : GameBase
{
    [LabelText("当前场景数据"),ReadOnly]
    public GameSceneData gameSceneItemData;

    private SpriteRenderer _spriteRenderer;
    private TextMeshPro labelText;
    
    public void Initialize(GameSceneData gameSceneData)
    {
        gameSceneItemData = gameSceneData;
        _spriteRenderer = Get<SpriteRenderer>("");
        labelText = Get<TextMeshPro>("LabelText");
    }


    public void OnMouseUp()
    {
        GameDataManager.Instance.EnterGameScene(gameSceneItemData.scene_id);
    }
}
