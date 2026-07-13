using UnityEngine;
using UnityEngine.UI;

namespace XFramework
{
    /// <summary>
    /// 女主粉丝界面
    /// </summary>
    public class FanPageUI : UIBase
    {
        private ScrollRect mScrollRect;
        private Scrollbar mScrollbar;
        
        /// <summary>
        /// 初始化方法,一般不需要手动调用
        /// </summary>
        public override void Init()
        {
            mScrollRect = Get<ScrollRect>("mScrollRect");
            mScrollbar = Get<Scrollbar>("mScrollbar");
        }
    }
}

