using UnityEngine;
using XFramework;

public class SceneCharacterController : GameBase
{
    private SpriteRenderer spriteRenderer;
    
    public void Init(CharacterData characterData,SceneData sceneData)
    {
        spriteRenderer = Get<SpriteRenderer>("CharacterSpriteRenderer");
        spriteRenderer.sprite = characterData.CharacterSceneIcon;
        transform.localPosition = sceneData.Position;
    }
}
