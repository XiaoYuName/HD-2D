using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// UISystem 底层委托: 用于打开界面后的回调
    /// </summary>
    /// <typeparam name="T">打开界面后的T类型对象</typeparam>
    public delegate void Call<in T>(T cb);
    
    /// <summary>
    /// UI系统总管理器
    /// </summary>
    public class UISystem : MonoOdinSingleton<UISystem>,IGameInitialized
    {
        [BoxGroup("Initialized"),LabelText("配置路径"),FilePath]
        public string ConfigPath = "Assets/AddressableAssets/Configs/UIPage/PageConfiguration.asset";
        [BoxGroup("Initialized"),ShowInInspector,ShowIf("isShowingPageConfiguration"),LabelText("配置表数据"),ReadOnly]
        private PageConfiguration pageConfiguration;

        #region Initialized

        public async UniTask Initialized()
        {
            pageConfiguration =  await AssetsManager.Instance
                .LoadAssetTask<PageConfiguration>(ConfigPath);
            LoadCanvas();
        }

        public async UniTask Release()
        {
            await UniTask.CompletedTask;
        }

        private void LoadCanvas()
        {
            uiCanvasDictionary = new Dictionary<UICanvasLayer, 
                Dictionary<UIParentLayer, Transform>>();
            foreach (UICanvasLayer CanvasEnums in Enum.GetValues(typeof(UICanvasLayer)))
            {
                foreach (UIParentLayer Layer  in Enum.GetValues(typeof(UIParentLayer)))
                {
                    Transform canvasLayerTransform = transform.Find(CanvasEnums.ToString());
                    if (canvasLayerTransform == null)
                    {
                        Debug.LogError(CanvasEnums+" :对应类型的Canvas适配层级没有找到,将自动跳过");
                        continue;
                    }
                    Transform parentTransform = canvasLayerTransform.Find(Layer.ToString());
                    if (parentTransform == null)
                    {
#if UNITY_EDITOR
                        Debug.LogWarning("没有找到对应的子级层级,系统将自动创建对应的子层级");
#endif
                        GameObject parentObj = new GameObject(Layer.ToString());
                        parentObj.transform.parent = canvasLayerTransform;
                        parentObj.transform.localPosition = Vector3.zero;
                        AddUICanvasDictionary(CanvasEnums,Layer,parentObj.transform);
                    }
                    else
                    {
                        AddUICanvasDictionary(CanvasEnums,Layer,parentTransform);
                    }
                }
            }
        }
        
        /// <summary>
        /// 更新父级适配数据
        /// </summary>
        /// <param name="layer">Canvas 适配</param>
        /// <param name="parentLayer">子渲染层级</param>
        /// <param name="tran">坐标位置</param>
        private void AddUICanvasDictionary(UICanvasLayer layer,
            UIParentLayer parentLayer,Transform tran)
        {
            if (!uiCanvasDictionary.ContainsKey(layer))
            {
                uiCanvasDictionary.Add(layer,new Dictionary<UIParentLayer, Transform>());
            }
            if (!uiCanvasDictionary[layer].ContainsKey(parentLayer))
            {
                uiCanvasDictionary[layer].Add(parentLayer,tran);
            }
            uiCanvasDictionary[layer][parentLayer] = tran;
        }
        
        /// <summary>
        /// 获取UI的生成父级
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="parentLayer"></param>
        /// <returns></returns>
        public Transform GetUILayer(UICanvasLayer layer,UIParentLayer parentLayer)
        {
            if (uiCanvasDictionary.ContainsKey(layer))
            {
                if (uiCanvasDictionary[layer].ContainsKey(parentLayer))
                {
                    return uiCanvasDictionary[layer][parentLayer];
                }
            }

            return null;
        }

        #endregion

        #region Datas

        [ReadOnly,LabelText("UI列表"),BoxGroup("列表")]
        private Dictionary<string, GameObject> uiDictionary = new Dictionary<string, GameObject>();
        [ReadOnly,LabelText("UIRoots"),BoxGroup("列表")]
        private Dictionary<UICanvasLayer, Dictionary<UIParentLayer, Transform>> uiCanvasDictionary;
        
        #endregion

        #region 底层框架

        public void AddUI(string uiPage,UIBase uiBase)
        {
            if (!uiDictionary.ContainsKey(uiPage))
            {
                uiDictionary.Add(uiPage, uiBase.gameObject);
                return;
            }
           
        }

        /// <summary>
        /// 获取UI
        /// </summary>
        /// <param name="uiPage">UI Key</param>
        /// <typeparam name="T">UI 组件对象,该组件必须继承自UIBase</typeparam>
        /// <returns></returns>
        public T GetUI<T>(string uiPage) where T: UIBase
        {
            if (!uiDictionary.ContainsKey(uiPage))
            {
                return LoadUI(uiPage).GetComponent<T>();
            }
            return uiDictionary[uiPage].GetComponent<T>();
        }

        /// <summary>
        /// 异步获取UI
        /// </summary>
        /// <param name="uiPage"></param>
        /// <param name="action">回调函数</param>
        /// <typeparam name="T">组件对象,该对象必须继承UIBase</typeparam>
        public void GetUIAsync<T>(string uiPage,Call<T> action) where T:UIBase
        {
            if (!uiDictionary.ContainsKey(uiPage))
            {
                LoadUIAsync(uiPage,action);
            }
            else
            {
                action?.Invoke(uiDictionary[uiPage].GetComponent<T>());
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uiPage"></param>
        /// <param name="action"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public IEnumerator GetUICoroutine<T>(string uiPage, Call<T> action)
        {
            if (!uiDictionary.ContainsKey(uiPage))
            {
                yield return LoadUIEnumerator(uiPage, action);
            }
            else
            {
                action?.Invoke(uiDictionary[uiPage].GetComponent<T>());
            }
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="uiPage"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async UniTask<T> GetUIUniTask<T>(string uiPage) where T : UIBase
        {
            if (!uiDictionary.ContainsKey(uiPage))
            {
               return await loadUiUniTask<T>(uiPage);
            }
            return uiDictionary[uiPage].GetComponent<T>();
        }

        /// <summary>
        /// 打开UI界面
        /// </summary>
        /// <param name="uiPage">界面名称</param>
        public void OpenUI(string uiPage)
        {
            UIBase ui = GetUI<UIBase>(uiPage);
            if (ui == null) return;
            if (ui.isOpen) return;
            ui.Open();
            ui.transform.SetAsLastSibling();
        }

        /// <summary>
        /// 打开UI界面
        /// </summary>
        /// <param name="uiPage">界面</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T OpenUI<T>(string uiPage) where T : UIBase
        {
            UIBase ui = GetUI<UIBase>(uiPage);
            if (ui == null) return null;
            if (!ui.isOpen)
            {
                ui.Open();
            }
            ui.transform.SetAsLastSibling();
            return ui.GetComponent<T>();
        }
        
        /// <summary>
        /// 异步打开UI界面
        /// </summary>
        /// <param name="uiPage">界面名称</param>
        /// <param name="action">打开后的回调函数</param>
        /// <typeparam name="T">界面类型,该类型必须继承UIBase</typeparam>
        public void OpenUIAsync<T>(string uiPage, Call<T> action) where T:UIBase
        {
            GetUIAsync(uiPage, delegate(T cb)
            {
                if(!cb.isOpen)
                    cb.Open();
                cb.transform.SetAsLastSibling();
                action?.Invoke(cb);
            });
        }
        
        /// <summary>
        /// 异步打开UI界面
        /// </summary>
        /// <param name="uiPage">界面名称</param>
        /// <typeparam name="T">界面类型,该类型必须继承UIBase</typeparam>
        public void OpenUIAsync<T>(string uiPage) where T:UIBase
        {
            GetUIAsync(uiPage, delegate(T cb)
            {
                if(!cb.isOpen)
                    cb.Open();
                cb.transform.SetAsLastSibling();
            });
        }
        
        /// <summary>
        /// 协程打开UI
        /// </summary>
        /// <param name="uiPage">界面名称</param>
        /// <param name="action">打开后的回调函数</param>
        /// <typeparam name="T">界面类型,该类型必须继承UIBase</typeparam>
        /// <returns></returns>
        public IEnumerator OpenUICoroutine<T>(string uiPage, Call<T> action) where T:UIBase
        {
            yield return GetUICoroutine(uiPage, delegate(T cb)
            {
                if(!cb.isOpen)
                    cb.Open();
                cb.transform.SetAsLastSibling();
                action?.Invoke(cb);
            });
        }
        
        /// <summary>
        /// 关闭UI
        /// </summary>
        /// <param name="uiPage">ui名称</param>
        public void CloseUI(string uiPage)
        {
            if(!uiDictionary.ContainsKey(uiPage))return;
            UIBase Obj = uiDictionary[uiPage].GetComponent<UIBase>();
            Obj.transform.SetAsFirstSibling();
            Obj.Close();
        }


        /// <summary>
        /// 同步加载UI
        /// </summary>
        /// <param name="uiPage"></param>
        /// <returns></returns>
        private GameObject LoadUI(string uiPage)
        {
            UIPageItem tableData = pageConfiguration.GetPage(uiPage);
            if (tableData == null)
            {
                Debug.LogError("表中没有对应UITable: "+uiPage);
                return null;
            }
            GameObject Prefab = AssetsManager.Instance.LoadAssets<GameObject>(tableData.PagePath);
            var Obj = Instantiate(Prefab, 
                uiCanvasDictionary[tableData.UICanvas][tableData.UIParent]);
            UIBase uiBase = Obj.GetComponent<UIBase>();
            if (uiBase != null)
            {
                uiBase.uiname = uiPage;
                uiBase.isTween = tableData.isTween;
                uiBase.Init();
            }
            uiDictionary.Add(uiPage,Obj);
            return Obj;
        }
        
        /// <summary>
        /// 同步加载UI
        /// </summary>
        /// <param name="uiPage"></param>
        /// <returns></returns>
        private T LoadUI<T>(string uiPage) where T:UIBase
        {
            UIPageItem tableData = pageConfiguration.GetPage(uiPage);
            if (tableData == null)
            {
                Debug.LogError("表中没有对应UITable: "+uiPage);
                return null;
            }
            GameObject Prefab = AssetsManager.Instance.LoadAssets<GameObject>(tableData.PagePath);
            var Obj = Instantiate(Prefab, uiCanvasDictionary[tableData.UICanvas][tableData.UIParent]);
            T uiBase = Obj.GetComponent<T>();
            if (uiBase != null)
            {
                uiBase.uiname = uiPage;
                uiBase.isTween = tableData.isTween;
                uiBase.Init();
            }
            uiDictionary.Add(uiPage,Obj);
            return uiBase;
        }
        
        /// <summary>
        /// 异步加载UI
        /// </summary>
        /// <param name="uiPage"></param>
        /// <param name="action"></param>
        /// <typeparam name="T"></typeparam>
        private void LoadUIAsync<T>(string uiPage,Call<T> action)
        {
            UIPageItem tableData = pageConfiguration.GetPage(uiPage);
            if (tableData == null)
            {
                Debug.LogError("表中没有对应UITable: "+uiPage);
                return;
            }
            AssetsManager.Instance.LoadAssetsAsync(tableData.PagePath, delegate(GameObject prefab)
            {
                var Obj = Instantiate(prefab, uiCanvasDictionary[tableData.UICanvas][tableData.UIParent]);
                UIBase uiBase = Obj.GetComponent<UIBase>();
                if (uiBase != null)
                {
                    uiBase.uiname = uiPage;
                    uiBase.isTween = tableData.isTween;
                    uiBase.Init();
                }

                if (!uiDictionary.ContainsKey(uiPage))
                {
                    uiDictionary.Add(uiPage,Obj);
                }
                action?.Invoke(uiBase.GetComponent<T>());
            });
        }
        
        /// <summary>
        /// 协程加载UI
        /// </summary>
        /// <param name="uiPage"></param>
        /// <param name="action"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        private IEnumerator LoadUIEnumerator<T>(string uiPage,Call<T> action)
        {
            UIPageItem tableData = pageConfiguration.GetPage(uiPage);
            if (tableData == null)
            {
                Debug.LogError("表中没有对应UITable: "+uiPage);
                yield break;
            }
            yield return AssetsManager.Instance.LoadAssetsCoroutine(tableData.PagePath, delegate(GameObject prefab)
            {
                var Obj = Instantiate(prefab, uiCanvasDictionary[tableData.UICanvas][tableData.UIParent]);
                UIBase uiBase = Obj.GetComponent<UIBase>();
                if (uiBase != null)
                {
                    uiBase.uiname = uiPage;
                    uiBase.isTween = tableData.isTween;
                    uiBase.Init();
                }
                uiDictionary.Add(uiPage,Obj);
                action?.Invoke(uiBase.GetComponent<T>());
            });
        }
        
        /// <summary>
        /// 协程加载UI
        /// </summary>
        /// <param name="uiPage"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        private async UniTask<T> loadUiUniTask<T>(string uiPage) where T:UIBase
        {
            UIPageItem tableData = pageConfiguration.GetPage(uiPage);
            if (tableData == null)
            {
                Debug.LogError("表中没有对应UITable: "+uiPage);
                return null;
            }
            var prefab =  await AssetsManager.Instance.LoadAssetsUniTask<GameObject>(tableData.PagePath);
            var Obj = Instantiate(prefab, uiCanvasDictionary[tableData.UICanvas][tableData.UIParent]);
            T uiBase = Obj.GetComponent<T>();
            if (uiBase != null)
            {
                uiBase.uiname = uiPage;
                uiBase.isTween = tableData.isTween;
                uiBase.Init();
            }
            uiDictionary.Add(uiPage,Obj);
            return uiBase;
        }

        #endregion
        
        #region OdinFunction

        private bool isShowingPageConfiguration()
        {
            if (string.IsNullOrEmpty(ConfigPath))
            {
                return false;
            }

            if (!File.Exists(ConfigPath))
            {
                return false;
            }
            return true;
        }

        #endregion
    }
}

