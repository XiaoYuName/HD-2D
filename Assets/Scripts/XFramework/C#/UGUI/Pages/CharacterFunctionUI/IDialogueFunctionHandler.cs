using UnityEngine;
using XFramework;

public class DialogueFunctionHandler : ICharacterFunctionHandler
{
    public FunctionGrpup FunctionType => FunctionGrpup.Dialogue;

    public void Execute(CharacterData characterData)
    {
        var dramaUI = UISystem.Instance.OpenUI<DramaUI>("DramaUI");
        if (dramaUI != null)
        {
            //dramaUI.StartDrama(characterData.ShowingData.DialogueID[Random.Range(0, characterData.ShowingData.DialogueID.Count)]);
        }
    }
}
