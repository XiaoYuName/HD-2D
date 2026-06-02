using System;
using System.Collections.Generic;
using System.IO;
using Sirenix.OdinInspector;
using UnityEngine;

namespace XFramework
{
    [CreateAssetMenu(fileName = "PageConfiguration", menuName = "XFramework/Configs/UIConfig"),]
    public class PageConfiguration : OdinScriptableManager<PageConfiguration>
    {
        [Title("UI Pages"),LabelText("UI列表"),Searchable]
        public List<UIPageItem> Pages = new List<UIPageItem>();
        
        /// <summary>
        /// 根据ID 获取对应的UIPage 数据
        /// </summary>
        /// <param name="TableName"></param>
        /// <returns></returns>
        public UIPageItem GetPage(string TableName)
        {
            return Pages.FindLast(x => x.PathID == TableName);
        }
    }
    
    
    [Serializable]
    public class UIPageItem
    {
        [LabelText("ID"),FoldoutGroup("Page"),HorizontalGroup("Page/Basic")]
        public string PathID;
        [LabelText("",SdfIconType.CloudArrowDownFill),GUIColor("GetPathColor"),
         FoldoutGroup("Page"),FilePath,HorizontalGroup("Page/Basic"),LabelWidth(50)]
        public string PagePath;
        [LabelText("适配层级"),FoldoutGroup("Page")]
        public UICanvasLayer UICanvas;
        [LabelText("子层级"),FoldoutGroup("Page")]
        public UIParentLayer UIParent;
        [LabelText("",SdfIconType.Play),Space,FoldoutGroup("Page"),Tooltip("选中OpenUI时会播放缩放动画")]
        public bool isTween;


        /// <summary>
        /// 根据路径是否存在返回颜色
        /// </summary>
        /// <returns></returns>
        public Color GetPathColor()
        {
            if (string.IsNullOrEmpty(PagePath))
            {
                return Color.red;
            }

            if (File.Exists(PagePath))
            {
                return Color.green;
            }
            return Color.yellow;
        }
    }
}

