using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace XFramework
{
    public class SceneLoader
    {
        protected string Key;
        private bool isLoader;
        private AsyncOperationHandle<SceneInstance> _handle;
        private LoadSceneMode _mode;
        
        public SceneLoader(string key, LoadSceneMode mode)
        {
            this.Key = key;
            this._mode = mode;
        }

        /// <summary>
        /// 异步加载场景
        /// </summary>
        /// <param name="OnComplete">加载场景前一秒调用</param>
        public virtual void LoadSceneAsync(Action OnComplete)
        {
            if (isLoader)
            {
                if (_handle.IsDone)
                {
                    OnComplete?.Invoke();
                    _handle.Result.ActivateAsync().completed += (res) =>
                    {
                        if (res.isDone)
                        {
                            SceneManager.SetActiveScene(_handle.Result.Scene);
                        }
                    };
                }
                else
                {
                    _handle.Completed += (result) =>
                    {
                        if (result.Status == AsyncOperationStatus.Succeeded)
                        {
                            OnComplete?.Invoke();
                            _handle.Result.ActivateAsync().completed += (res) =>
                            {
                                if (res.isDone)
                                {
                                    SceneManager.SetActiveScene(_handle.Result.Scene);
                                }
                            };
                        }
                        else
                        {
                            Debug.LogError($"资源下载失败Key : {Key} ,类型为: {typeof(Scene)}");
                        }
                    };
                }
            }
            else
            {
                isLoader = true;
                _handle = Addressables.LoadSceneAsync(Key, _mode,false);
                _handle.Completed += (result) =>
                {
                    if (result.Status == AsyncOperationStatus.Succeeded)
                    {
                        OnComplete?.Invoke();
                        _handle.Result.ActivateAsync().completed += (res) =>
                        {
                            if (res.isDone)
                            {
                                SceneManager.SetActiveScene(_handle.Result.Scene);
                            }
                        };
                        
                    }
                    else
                    {
                        Debug.LogError($"资源下载失败Key : {Key} ,类型为: {typeof(Scene)}");
                    }
                };
            }
        }

        /// <summary>
        /// 携程加载Scenen
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerator LoadSceneCoroutine()
        {
            if (isLoader)
            {
                if (_handle.IsDone)
                {
                    var handle = _handle.Result.ActivateAsync();
                    yield return handle;
                    if (handle.isDone)
                    {
                        SceneManager.SetActiveScene(_handle.Result.Scene);
                    }
                }
            }
            else
            {
                isLoader = true;
                _handle = Addressables.LoadSceneAsync(Key, _mode, false);
                yield return _handle;
                if (_handle.Status == AsyncOperationStatus.Succeeded)
                {
                    var handle = _handle.Result.ActivateAsync();
                    yield return handle;
                    if (handle.isDone)
                    {
                        SceneManager.SetActiveScene(_handle.Result.Scene);
                    }
                }
                else
                {
                    Debug.LogError($"资源下载失败Key : {Key} ,类型为: {typeof(Scene)}");
                }
            }
        }

        /// <summary>
        /// UniTask加载Scene
        /// </summary>
        public async UniTask LoadSceneUniTask()
        {
            if (isLoader)
            {
                if (_handle.IsDone)
                {
                    var handle = _handle.Result.ActivateAsync();
                    await handle.ToUniTask();
                    if (handle.isDone)
                    {
                        SceneManager.SetActiveScene(_handle.Result.Scene);
                    }
                }
            }
            else
            {
                isLoader = true;
                _handle = Addressables.LoadSceneAsync(Key, _mode, false);
                await _handle.ToUniTask();
                if (_handle.Status == AsyncOperationStatus.Succeeded)
                {
                    var handle = _handle.Result.ActivateAsync();
                    await handle.ToUniTask();
                    if (handle.isDone)
                    {
                        SceneManager.SetActiveScene(_handle.Result.Scene);
                    }
                }
                else
                {
                    Debug.LogError($"资源下载失败Key : {Key} ,类型为: {typeof(Scene)}");
                }
            }
        }
        
        /// <summary>
        /// UniTask加载Scene
        /// </summary>
        public async UniTask LoadSceneUniTask(IProgress<float> progress)
        {
            if (isLoader)
            {
                if (_handle.IsDone)
                {
                    var handle = _handle.Result.ActivateAsync();
                    await handle.ToUniTask(progress);
                    if (handle.isDone)
                    {
                        SceneManager.SetActiveScene(_handle.Result.Scene);
                    }
                }
            }
            else
            {
                isLoader = true;
                _handle = Addressables.LoadSceneAsync(Key, _mode, false);
                await _handle.ToUniTask();
                if (_handle.Status == AsyncOperationStatus.Succeeded)
                {
                    var handle = _handle.Result.ActivateAsync();
                    await handle.ToUniTask();
                    if (handle.isDone)
                    {
                        SceneManager.SetActiveScene(_handle.Result.Scene);
                    }
                }
                else
                {
                    Debug.LogError($"资源下载失败Key : {Key} ,类型为: {typeof(Scene)}");
                }
            }
        }

        /// <summary>
        /// 卸载当前场景
        /// </summary>
        public virtual void ULoadSceneAsync()
        {
            if (isLoader)
            {
                if (_handle.IsDone)
                {
                    Addressables.UnloadSceneAsync(_handle.Result,false);
                }
                else
                {
                    _handle.Completed += (result) =>
                    {
                        if (result.Status == AsyncOperationStatus.Succeeded)
                        {
                            Addressables.UnloadSceneAsync(_handle.Result, false);
                        }
                        else
                        {
                            Debug.LogError($"资源下载失败Key : {Key} ,类型为: {typeof(Scene)}");
                        }
                    };
                }
            }
            else
            {
                Debug.LogError($"场景卸载失败Key : {Key} ,该场景尚未加载,但却试图卸载它:{typeof(Scene)}");
            }
        }

        /// <summary>
        /// 协程卸载场景
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerator ULoadSceneCoroutine()
        {
            if (isLoader)
            {
                if (_handle.IsDone)
                {
                    AsyncOperationHandle<SceneInstance> operationHandle = Addressables.UnloadSceneAsync(_handle.Result, false);
                    yield return operationHandle.Task;
                }
                else
                {
                    yield return _handle.Task;
                    AsyncOperationHandle<SceneInstance> operationHandle = Addressables.UnloadSceneAsync(_handle.Result, false);
                    yield return operationHandle.Task;
                }
            }
            else
            {
                Debug.LogError($"场景卸载失败Key : {Key} ,该场景尚未加载,但却试图卸载它:{typeof(Scene)}");
            }
        }
        
        /// <summary>
        /// UniTask 卸载场景
        /// </summary>
        public async UniTask ULoadSceneUniTask()
        {
            if (isLoader)
            {
                if (_handle.IsDone)
                {
                    var operationHandle = Addressables.UnloadSceneAsync(_handle.Result, false);
                    await operationHandle.ToUniTask();
                }
            }
        }

        /// <summary>
        /// 释放当前场景资源
        /// </summary>
        public virtual void Release()
        {
            if (this.isLoader)
            {
                this.isLoader = false;
                if (_handle.IsValid() && _handle.Status == AsyncOperationStatus.Succeeded)
                {
                    Addressables.Release(_handle);
                }
            }
        }
    }
}
