using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace XFramework
{
    public partial class GameSaveData
    {
        [LabelText("厨房已解锁配方Id")] public List<long> unlockedFoodRecipeIds;
    }
}

