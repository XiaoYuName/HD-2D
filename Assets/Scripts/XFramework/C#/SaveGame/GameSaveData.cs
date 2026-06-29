using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using System;

namespace XFramework
{
    /// <summary>
    /// GameSave 框架核心
    /// </summary>
    [Serializable]
    public partial class GameSaveData
    {
        [LabelText("玩家基本数据")]
        public PlayerData PlayerData;
        
        [LabelText("游戏角色背包")]
        public List<CharacterBag> CharacterBags = new();

        [LabelText("游戏物品背包")]
        public List<ItemBag> itemBags = new ();
        public List<ItemInfo> itemList = new ();
        
        [LabelText("游戏内布料商店数据")]
        public List<ShopItemBag> ClothShops = new ();
        
        [LabelText("对话历史记录")]
        public List<DialogueData> DialogueDataList = new();

        public static GameSaveData Create()
        {
            return new();
        }
    }
}

