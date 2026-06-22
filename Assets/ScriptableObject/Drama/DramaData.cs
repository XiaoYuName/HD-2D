using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

[CreateAssetMenu(fileName = "DramaData", menuName = "Configs/Drama/DramaData")]
public class DramaData : SerializedScriptableObject
{
    [LabelText("是否展示场景物体")]
    public bool IsShowEnvironment;
    [LabelText("命令列表")]
    public List<DramaCommand> Commands = new List<DramaCommand>();
}
