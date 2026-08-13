using UnityEngine;
using UnityEngine.EventSystems;
using XFramework;

public class SceneCharacterController : GameBase,IPointerEnterHandler,IPointerExitHandler,IPointerClickHandler
{
    private SpriteRenderer spriteRenderer;
    public NpcData npcData { get; private set; }

    public void Init(NpcData npcData)
    {
        spriteRenderer = Get<SpriteRenderer>("CharacterSpriteRenderer");
        this.npcData = npcData;
        
        var sprite = AssetsManager.Instance.LoadAssets<Sprite>($"{npcData.TexturePath}");
        spriteRenderer.sprite = sprite;
        transform.localPosition = new Vector3( npcData.ScenePosition.X, npcData.ScenePosition.Y,0);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        
    }

    public void OnPointerClick(PointerEventData eventData)
    {
      if (eventData.button != PointerEventData.InputButton.Left) return;

      // 任务系统：点击NPC上报。用角色表 ID（npcData.Id 是场景摆放行，任务配表按角色配）
      QuestEventBus.ReportNpcClicked(npcData.CharacterData);

      //没有对话内容,但是有功能
      if (npcData.PointerDialogue.Count <= 0 && npcData.FunctionType != FunctionGroup.Node)
      {
          if(npcData.FunctionType== FunctionGroup.Node)return;
          var ui = UISystem.Instance.OpenUI<CharacterFunctionUI>("CharacterFunctionUI");
          ui.SetData(npcData);
          return;
      }

      if (npcData.PointerDialogue.Count > 0)
      {
          QuestEventBus.ReportNpcTalked(npcData.CharacterData); // 任务系统：与NPC对话上报（整段对话结束时一次）
      }

    }

    public void Release()
    {
        if (npcData != null)
        {
            AssetsManager.Instance.FreeAsset($"{npcData.TexturePath}");
        }
    }
}
