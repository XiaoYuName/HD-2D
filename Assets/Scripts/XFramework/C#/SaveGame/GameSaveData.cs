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
        [LabelText("玩家基本数据")]
        public PlayerData PlayerData;
        
        [LabelText("游戏角色背包")]
        public List<CharacterBag> CharacterBags = new List<CharacterBag>();

        [LabelText("游戏物品背包")]
        public List<ItemBag> itemBags = new List<ItemBag>();
        
        [LabelText("游戏内布料商店数据")]
        public List<ClothShopData> ClothShops = new List<ClothShopData>();
        
        [LabelText("对话历史记录")]
        public List<DialogueData> DialogueDataList = new List<DialogueData>();
    }
}

