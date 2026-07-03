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
        public List<NpcSpawnSaveData> NpcSpawnSaveDataList = new List<NpcSpawnSaveData>();
        
        [LabelText("游戏角色背包")]
        public List<CharacterBag> CharacterBags = new();

        [LabelText("游戏物品背包")]
        public List<ItemInfo> itemList = new ();
        
        [LabelText("游戏内布料商店数据")]
        public List<ShopItemBag> ClothShops = new ();
        
        [LabelText("游戏内超市商店数据")]
        public List<ShopItemBag> SuperMarketShops = new List<ShopItemBag>();
        
        [LabelText("游戏内果蔬店数据")]
        public List<ShopItemBag> FruitShops = new List<ShopItemBag>();
        
        [LabelText("情趣用品店数据")]
        public List<ShopItemBag> SexToShops = new List<ShopItemBag>();
        
        [LabelText("钓鱼商店数据")]
        public List<ShopItemBag> FishShops = new List<ShopItemBag>();
        
        [LabelText("对话历史记录")]
        public List<DialogueData> DialogueDataList = new();
        
        [LabelText("娃娃图鉴背包")]
        public List<GuideBag> DollGuideDataList = new();

        [LabelText("娃娃机数据")] 
        public ClawMachineGameData ClawMachineGameData;

        public static GameSaveData Create()
        {
            return new();
        }
    }
}

