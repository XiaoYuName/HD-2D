using UnityEngine;
using UnityEngine.EventSystems;
using XFramework;

public class SceneCharacterController : GameBase,IPointerEnterHandler,IPointerExitHandler,IPointerClickHandler
{
    private SpriteRenderer spriteRenderer;
    public CharacterData characterData { get; private set; }
    public ShowingData currentShowingData { get; private set; }

    public void Init(CharacterData characterData,ShowingData showingData)
    {
        spriteRenderer = Get<SpriteRenderer>("CharacterSpriteRenderer");
        this.characterData = characterData;
        spriteRenderer.sprite = characterData.CharacterSceneIcon;
        transform.localPosition = showingData.FixedSceneData.Position;
        currentShowingData = showingData;

    }

    public void Init(CharacterData characterData, ShowingData showingData,SceneData sceneData)
    {
        spriteRenderer = Get<SpriteRenderer>("CharacterSpriteRenderer");
        this.characterData = characterData;
        spriteRenderer.sprite = characterData.CharacterSceneIcon;
        transform.localPosition = sceneData.Position;
        currentShowingData = showingData;
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
      if (currentShowingData.DialogueIds <= 0) return;
      var dramaUI = UISystem.Instance.OpenUI<DramaUI>("DramaUI");
      dramaUI.StartDrama(currentShowingData.DialogueIds);
    }
}
