using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 跨存档的引导完成记录，对应 <see cref="TutorialRepeatType.OnceGlobal"/>。
    ///
    /// <b>不进 GameSaveData</b>：存档是按槽走的，而"这个操作我早就会了"是玩家级别的事实 ——
    /// 开个新档不该再被教一遍怎么打开背包。做法和 <see cref="DramaReadMarks"/>（剧情已读）一致：
    /// 存档目录下单独一个文件，不经过 <see cref="SaveGameManager"/>。
    ///
    /// 反过来，"这一周目的流程引导"要配 <see cref="TutorialRepeatType.Once"/>，那种记在存档里。
    /// </summary>
    public sealed class TutorialGlobalMarks
    {
        /// <summary>文件格式版本。以后要改结构时靠它分辨。</summary>
        private const int FormatVersion = 1;

        private const string FileName = "TutorialFinished.json";

        private static string FilePath =>
            Path.Combine(Application.persistentDataPath, SaveGameManager.SaveFolderName, FileName);

        private readonly HashSet<long> finished = new HashSet<long>();

        /// <summary>文件读过了没有。第一次用到时才读，不跟游戏的初始化顺序纠缠。</summary>
        private bool loaded;

        /// <summary>有没有还没落盘的新记录。</summary>
        private bool dirty;

        public bool IsFinished(long tutorialID)
        {
            EnsureLoaded();
            return finished.Contains(tutorialID);
        }

        public void MarkFinished(long tutorialID)
        {
            if (tutorialID <= 0)
            {
                return;
            }

            EnsureLoaded();

            if (finished.Add(tutorialID))
            {
                dirty = true;
            }
        }

        /// <summary>调试用：把跨存档记录清掉，好让引导能重新播一遍。</summary>
        public void ClearAll()
        {
            EnsureLoaded();

            if (finished.Count == 0)
            {
                return;
            }

            finished.Clear();
            dirty = true;
        }

        public void SaveIfDirty()
        {
            if (!dirty)
            {
                return;
            }

            try
            {
                string directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                SaveFile file = new SaveFile
                {
                    Version = FormatVersion,
                    FinishedTutorialIds = new List<long>(finished),
                };

                File.WriteAllText(FilePath, JsonConvert.SerializeObject(file));
                dirty = false;
            }
            catch (Exception exception)
            {
                // 写不进去不该把游戏搞崩：最坏结果只是下次启动又教一遍
                Debug.LogError($"引导完成记录写入失败: {FilePath}\n{exception}");
            }
        }

        private void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }

            loaded = true;

            try
            {
                if (!File.Exists(FilePath))
                {
                    return;
                }

                SaveFile file = JsonConvert.DeserializeObject<SaveFile>(File.ReadAllText(FilePath));
                if (file?.FinishedTutorialIds == null)
                {
                    return;
                }

                foreach (long id in file.FinishedTutorialIds)
                {
                    finished.Add(id);
                }
            }
            catch (Exception exception)
            {
                // 文件坏了就当没有记录：宁可多播一次引导，也不能因为它开不了游戏
                Debug.LogError($"引导完成记录读取失败,按空记录处理: {FilePath}\n{exception}");
            }
        }

        [Serializable]
        private class SaveFile
        {
            public int Version;
            public List<long> FinishedTutorialIds;
        }
    }
}
