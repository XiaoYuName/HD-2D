using UnityEngine;
using UnityEngine.EventSystems;
using XFramework;

public class SceneCharacterController : GameBase,IPointerEnterHandler,IPointerExitHandler,IPointerClickHandler
{
    private SpriteRenderer spriteRenderer;
    public CharacterData characterData { get; private set; }
    public NpcData npcData { get; private set; }
    public CharacterShowRuleData showRuleData { get; private set; }

    public void Init(CharacterData characterData,CharacterShowRuleData showRuleData)
    {
        spriteRenderer = Get<SpriteRenderer>("CharacterSpriteRenderer");
        this.characterData = characterData;
        this.npcData = CharacterManager.Instance.GetNpcDataByID(characterData.NpcID);
        this.showRuleData = showRuleData;
        
        var sprite = AssetsManager.Instance.LoadAssets<Sprite>($"{AssetsPaths.CharacterSpinePath}{npcData.SceneSpinePath}.png");
        spriteRenderer.sprite = sprite;
        transform.localPosition = new Vector3( showRuleData.ScenePosition.X, showRuleData.ScenePosition.Y,0);
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
      if (characterData.DailyDialogue.Count <= 0 && characterData.FunctionType != FunctionGrpup.Node)
      {
          if(characterData.FunctionType== FunctionGrpup.Node)return;
          var ui = UISystem.Instance.OpenUI<CharacterFunctionUI>("CharacterFunctionUI");
          ui.SetData(characterData);
          return;
      }

      if (characterData.DailyDialogue.Count > 0)
      {
          var dramaUI = UISystem.Instance.OpenUI<DramaUI>("DramaUI");
          dramaUI.StartDrama(characterData.DailyDialogue[Random.Range(0, characterData.DailyDialogue.Count)], () =>
          {
              if(characterData.FunctionType== FunctionGrpup.Node)return;
              var ui = UISystem.Instance.OpenUI<CharacterFunctionUI>("CharacterFunctionUI");
              ui.SetData(characterData);
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
