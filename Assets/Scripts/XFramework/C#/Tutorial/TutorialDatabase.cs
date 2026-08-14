using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 所有新手引导的配置资产。原来这套是两张 Luban 表，改成 SO 是因为引导的字段
    /// 高度互斥（选了「界面节点」就用不上锚点Key，选了「贴合目标」就用不上自定义尺寸），
    /// 表格里只能一排格子全摊开，编辑器里能按条件收起来，还能直接拖 Sprite、选节点路径。
    ///
    /// 资产走 Addressable 加载，见 <see cref="AssetKeys.TutorialDatabasePath"/>。
    /// </summary>
    [CreateAssetMenu(fileName = "TutorialDatabase", menuName = "Configs/Tutorial/TutorialDatabase")]
    public class TutorialDatabase : OdinScriptableManager<TutorialDatabase>
    {
        [ListDrawerSettings(ShowFoldout = true, ListElementLabelName = nameof(TutorialConfig.EditorTitle),
            DraggableItems = true, ShowIndexLabels = false)]
        [LabelText("引导列表")]
        public List<TutorialConfig> Tutorials = new List<TutorialConfig>();

        /// <summary>按ID取一段引导，没有返回 null。</summary>
        public TutorialConfig Get(long tutorialID)
        {
            return Tutorials?.FirstOrDefault(tutorial => tutorial.ID == tutorialID);
        }

#if UNITY_EDITOR
        /// <summary>
        /// 配置自检：ID 重复 / 没步骤 / 目标没填，这些都是运行时才会炸的错，
        /// 在这里先扫一遍，比进游戏点半天再看报错快。
        /// </summary>
        [Button("检查配置", ButtonSizes.Large), PropertyOrder(-1)]
        private void Validate()
        {
            List<string> problems = new List<string>();

            foreach (var group in Tutorials.GroupBy(tutorial => tutorial.ID).Where(g => g.Count() > 1))
            {
                problems.Add($"引导ID {group.Key} 重复了 {group.Count()} 条");
            }

            foreach (TutorialConfig tutorial in Tutorials)
            {
                if (tutorial.ID <= 0)
                {
                    problems.Add($"「{tutorial.Description}」没填引导ID");
                }

                if (tutorial.Steps == null || tutorial.Steps.Count == 0)
                {
                    problems.Add($"引导 {tutorial.ID} 一步都没有");
                    continue;
                }

                for (int i = 0; i < tutorial.Steps.Count; i++)
                {
                    TutorialStepConfig step = tutorial.Steps[i];
                    string where = $"引导 {tutorial.ID} 第 {i + 1} 步";

                    switch (step.TargetType)
                    {
                        case TutorialTargetType.UIPath:
                            if (string.IsNullOrEmpty(step.TargetPageID) || string.IsNullOrEmpty(step.TargetPath))
                            {
                                problems.Add($"{where}：目标界面或节点路径没填");
                            }

                            break;

                        case TutorialTargetType.Anchor:
                            if (string.IsNullOrEmpty(step.AnchorKey))
                            {
                                problems.Add($"{where}：锚点Key没填");
                            }

                            break;
                    }

                    if (step.FinishType == TutorialFinishType.ClickTarget && !step.NeedsTargetNode)
                    {
                        problems.Add($"{where}：完成条件是「点击目标」，但这一步没有目标节点" +
                                     "（屏幕固定位置的洞点不出按钮事件，改用「点击洞内区域」）");
                    }

                    if (step.FinishType == TutorialFinishType.ClickHole && !step.HasHole)
                    {
                        problems.Add($"{where}：完成条件是「点击洞内区域」，但这一步没挖洞");
                    }

                    if (step.FinishType == TutorialFinishType.UIOpen && string.IsNullOrEmpty(step.FinishPageID))
                    {
                        problems.Add($"{where}：完成条件是「等界面打开」，但没选界面");
                    }

                    if (step.FinishType == TutorialFinishType.Event && string.IsNullOrEmpty(step.FinishEventKey))
                    {
                        problems.Add($"{where}：完成条件是「等自定义事件」，但没填事件名");
                    }
                }
            }

            if (problems.Count == 0)
            {
                Debug.Log($"引导配置检查通过，共 {Tutorials.Count} 段引导。");
                return;
            }

            Debug.LogError($"引导配置有 {problems.Count} 处问题：\n" + string.Join("\n", problems));
        }
#endif
    }
}
