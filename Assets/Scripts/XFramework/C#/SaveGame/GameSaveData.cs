using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// GameSave 框架核心
    /// </summary>
    public class GameSaveData
    {
        [LabelText("游戏角色背包")]
        public List<CharacterBag> CharacterBags = new List<CharacterBag>();
    }
}

