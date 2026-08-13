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

        [LabelText("角色临时驻场覆盖")]
        public Dictionary<long, CharacterSceneOverride> CharacterSceneOverrides;

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

        /// <summary>
        /// 剧情台词的历史记录（Log）。滚动保留最近
        /// <see cref="DramaHistory.DefaultCapacity"/> 条。
        /// </summary>
        [LabelText("剧情对话历史")]
        public List<DramaHistoryEntry> DramaHistoryList;

        /// <summary>
        /// 完整播完过的剧情ID。任务 / 条件系统的「做过某段剧情」查它（<c>DramaManager.HasDrama</c>）。
        ///
        /// <b>跟着存档槽走</b>，和跨存档共享的「已读」不是一回事 ——
        /// 二周目该重做的任务，不能因为一周目看过就直接算完成。
        /// </summary>
        [LabelText("已播完的剧情")]
        public List<long> FinishedDramaIds;

        [LabelText("剧情进度")]
        public DramaRestorePoint DramaProgress;

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

