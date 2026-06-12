using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

[CreateAssetMenu(fileName = "DramaData", menuName = "ScriptableObjects/DramaData")]
public class DramaData : SerializedScriptableObject
{
    public List<DramaCommand> Commands = new List<DramaCommand>();
}
