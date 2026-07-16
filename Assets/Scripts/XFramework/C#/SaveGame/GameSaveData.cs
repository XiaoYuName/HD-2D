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
        [LabelText("存档版本号")]
        public int Version = SaveVersion;
        
        public const int SaveVersion = 1;
        
        [LabelText("玩家基本数据")]
        public PlayerData PlayerData;
        [LabelText("场景数据")]
        public SceneData SceneData;

        [LabelText("随机时刻的场景数据")] 
        public List<NpcSpawnSaveData> NpcSpawnSaveDataList;

        [LabelText("游戏角色背包")] 
        public List<CharacterBag> CharacterBags;

        [LabelText("角色背包")]
        public List<ItemInfo> PlayerStack;

        [LabelText("物品解锁列表")] 
        public List<ItemUnlockSaveData> ItemUnlockSaveDataList;

        [LabelText("游戏内布料商店数据")] 
        public List<ShopItemBag> ClothShops;

        [LabelText("游戏内超市商店数据")] 
        public List<ShopItemBag> SuperMarketShops;

        [LabelText("游戏内果蔬店数据")] 
        public List<ShopItemBag> FruitShops;
        
        [LabelText("情趣用品店数据")]
        public List<ShopItemBag> SexToShops;
        
        [LabelText("钓鱼商店数据")]
        public List<ShopItemBag> FishShops;
        
        [LabelText("对话历史记录")]
        public List<DialogueData> DialogueDataList;

        [LabelText("娃娃机数据")] 
        public ClawMachineGameData ClawMachineGameData;
        
        [LabelText("线上消息列表")]
        public List<MessageData> MessageDataList;
        
        [LabelText("私信消息列表")]
        public List<PriavateMessageBag> PrivateMessageDataList;
        
        [LabelText("展会宣发背包")]
        public List<ExhibitionPromotionBag>  ExhibitionPromotionDataList;
        


        public static GameSaveData Create()
        {
            return new();
        }
    }
}

