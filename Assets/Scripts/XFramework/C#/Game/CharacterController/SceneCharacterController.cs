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
        
        var sprite = AssetsManager.Instance.LoadAssets<Sprite>($"{AssetsPaths.CharacterSpinePath}{npcData.SceneSpinePath}.png");
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
          var dramaUI = UISystem.Instance.OpenUI<DramaUI>("DramaUI");
          dramaUI.StartDrama(npcData.PointerDialogue[Random.Range(0, npcData.PointerDialogue.Count)], () =>
          {
              if(npcData.FunctionType== FunctionGroup.Node)return;
              var ui = UISystem.Instance.OpenUI<CharacterFunctionUI>("CharacterFunctionUI");
              ui.SetData(npcData);
          });
      }

    }

    public void Release()
    {
        if (npcData != null)
        {
            AssetsManager.Instance.FreeAsset($"{AssetsPaths.CharacterSpinePath}{npcData.SceneSpinePath}.png");
        }
    }
}
