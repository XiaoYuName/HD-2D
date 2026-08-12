using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace XFramework
{
    /// <summary>
    /// 剧情的存档点，同时也是读档时的恢复点 —— 存和读用的是同一个结构，中间不做映射。
    ///
    /// <b>只记"走到哪儿了"，不记"屏幕上是什么"。</b>
    /// 背景 / BGM / 立绘站位 / 对话框皮肤 / 遮罩，全是前面每条指令堆出来的累积状态；
    /// 与其一个个快照下来（每加一种指令就多一处要维护，漏一个就是读档后少个效果，
    /// 而且极难发现），不如读档时从剧本开头<b>静默重放</b>到这一条，让它自己堆回去。
    /// 重放靠 <see cref="Drama.Runtime.Flow.EDramaPlaybackMode.Restoring"/>，
    /// 那个模式下所有等待归零、台词不等输入。
    ///
    /// 重放唯一救不回来的是<b>选项</b>：走到选项节点时不能弹面板问玩家，
    /// 所以当年选的那些要按顺序记在 <see cref="ChoicePath"/> 里喂回去。
    /// </summary>
    [Serializable]
    public class DramaRestorePoint
    {
        [LabelText("剧本ID")]
        public long DramaId;

        /// <summary>
        /// 停在哪条指令上。<b>只会是台词</b> —— 台词等玩家点击是整段剧情唯一的空闲时刻，
        /// 别处存下来读档会落在一条正在跑的动画中间。
        /// </summary>
        [LabelText("指令下标")]
        public int ActionIndex = -1;

        [LabelText("已走过的选项")]
        public List<int> ChoicePath = new List<int>();

        public bool IsValid => DramaId > 0 && ActionIndex >= 0;

        /// <summary>
        /// 下标是<b>当前导出产物</b>的编号，剧本重导后会变，这里不做任何迁移。
        /// 对不上时 <see cref="Drama.Runtime.Flow.DramaPlayer"/> 会退回从头正常播并打一条警告。
        /// </summary>
        public static DramaRestorePoint Capture(long dramaId, int actionIndex, IReadOnlyList<int> choicePath)
        {
            var point = new DramaRestorePoint
            {
                DramaId = dramaId,
                ActionIndex = actionIndex,
            };

            if (choicePath != null)
            {
                point.ChoicePath.AddRange(choicePath);
            }

            return point;
        }
    }
}
