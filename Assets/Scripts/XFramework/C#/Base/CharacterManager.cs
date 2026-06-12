using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

public class CharacterManager : MonoSingleton<CharacterManager>
{
    [FoldoutGroup("Configs"),LabelText("角色配置表")]
    public CharacterDataManager CharacterData;

   
}
