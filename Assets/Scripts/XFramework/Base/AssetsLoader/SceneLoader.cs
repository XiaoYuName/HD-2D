using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace XFramework
{
    public class SceneLoader
    {
        protected string Key;
        private bool isLoader;
        private AsyncOperationHandle<SceneInstance> _handle;
        private LoadSceneMode _mode;
#if UNITY_EDITOR
        private Scene editorScene;
#endif
        
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
#if UNITY_EDITOR
            LoadSceneAsyncInEditor(OnComplete).Forget();
#else
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
#endif
        }

        public virtual void LoadScene()
        {
#if UNITY_EDITOR
            isLoader = true;
            string scenePath = GetSceneAssetPath(Key);
            editorScene = EditorSceneManager.LoadSceneInPlayMode(scenePath, new LoadSceneParameters(_mode));
            if (editorScene.IsValid())
            {
                SceneManager.SetActiveScene(editorScene);
                return;
            }

            throw new UnityException($"LoadSceneInPlayMode not isValid: {scenePath}");
#else
            isLoader = true;
            this._handle = Addressables.LoadSceneAsync(Key, _mode);
            if (_handle.IsValid())
            {
                _handle.WaitForCompletion();
                return;
            }
            throw new UnityException("WaitForCompletion not isValid");
#endif
        }

        /// <summary>
        /// 携程加载Scenen
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerator LoadSceneCoroutine()
        {
#if UNITY_EDITOR
            yield return LoadSceneCoroutineInEditor(null);
            yield break;
#else
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
#endif
        }

        /// <summary>
        /// UniTask加载Scene
        /// </summary>
        public async UniTask LoadSceneUniTask()
        {
#if UNITY_EDITOR
            await LoadSceneUniTaskInEditor(null);
#else
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
#endif
        }
        
        /// <summary>
        /// UniTask加载Scene
        /// </summary>
        public async UniTask LoadSceneUniTask(IProgress<float> progress)
        {
#if UNITY_EDITOR
            await LoadSceneUniTaskInEditor(progress);
#else
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
#endif
        }

        /// <summary>
        /// 同步卸载场景
        /// </summary>
        public virtual void ULoadScene()
        {
#if UNITY_EDITOR
            if (isLoader)
            {
                Scene scene = GetLoadedEditorScene();
                if (scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                    isLoader = false;
                    return;
                }
            }

            Debug.LogError($"场景卸载失败Key : {Key} ,该场景尚未加载,但却试图卸载它:{typeof(Scene)}");
#else
            if (isLoader)
            {
                if(_handle.IsDone)
                {
                    var Operation = Addressables.UnloadSceneAsync(_handle.Result,false);
                    Operation.WaitForCompletion();
                }
            }
            else
            {
                Debug.LogError($"场景卸载失败Key : {Key} ,该场景尚未加载,但却试图卸载它:{typeof(Scene)}");
            }
#endif
        }

        /// <summary>
        /// 卸载当前场景
        /// </summary>
        public virtual void ULoadSceneAsync()
        {
#if UNITY_EDITOR
            if (isLoader)
            {
                Scene scene = GetLoadedEditorScene();
                if (scene.IsValid() && scene.isLoaded)
                {
                    SceneManager.UnloadSceneAsync(scene);
                    isLoader = false;
                    return;
                }
            }

            Debug.LogError($"场景卸载失败Key : {Key} ,该场景尚未加载,但却试图卸载它:{typeof(Scene)}");
#else
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
#endif
        }

        /// <summary>
        /// 协程卸载场景
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerator ULoadSceneCoroutine()
        {
#if UNITY_EDITOR
            if (isLoader)
            {
                Scene scene = GetLoadedEditorScene();
                if (scene.IsValid() && scene.isLoaded)
                {
                    yield return SceneManager.UnloadSceneAsync(scene);
                    isLoader = false;
                    yield break;
                }
            }

            Debug.LogError($"场景卸载失败Key : {Key} ,该场景尚未加载,但却试图卸载它:{typeof(Scene)}");
            yield break;
#else
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
#endif
        }
        
        /// <summary>
        /// UniTask 卸载场景
        /// </summary>
        public async UniTask ULoadSceneUniTask()
        {
#if UNITY_EDITOR
            if (isLoader)
            {
                Scene scene = GetLoadedEditorScene();
                if (scene.IsValid() && scene.isLoaded)
                {
                    AsyncOperation operation = SceneManager.UnloadSceneAsync(scene);
                    if (operation != null)
                    {
                        await operation.ToUniTask();
                    }

                    isLoader = false;
                }
            }
#else
            if (isLoader)
            {
                if (_handle.IsDone)
                {
                    var operationHandle = Addressables.UnloadSceneAsync(_handle.Result, false);
                    await operationHandle.ToUniTask();
                }
            }
#endif
        }

        /// <summary>
        /// 释放当前场景资源
        /// </summary>
        public virtual void Release()
        {
            if (this.isLoader)
            {
                this.isLoader = false;
#if UNITY_EDITOR
                editorScene = default;
#else
                if (_handle.IsValid() && _handle.Status == AsyncOperationStatus.Succeeded)
                {
                    Addressables.Release(_handle);
                }
#endif
            }
        }

#if UNITY_EDITOR
        private async UniTaskVoid LoadSceneAsyncInEditor(Action onComplete)
        {
            await LoadSceneUniTaskInEditor(null, onComplete);
        }

        private IEnumerator LoadSceneCoroutineInEditor(Action onComplete)
        {
            if (isLoader)
            {
                Scene scene = GetLoadedEditorScene();
                if (scene.IsValid() && scene.isLoaded)
                {
                    onComplete?.Invoke();
                    SceneManager.SetActiveScene(scene);
                }

                yield break;
            }

            isLoader = true;
            string scenePath = GetSceneAssetPath(Key);
            AsyncOperation operation = EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(_mode));
            if (operation == null)
            {
                Debug.LogError($"资源下载失败Key : {Key} ,类型为: {typeof(Scene)}");
                yield break;
            }

            operation.allowSceneActivation = false;
            while (operation.progress < 0.9f)
            {
                yield return null;
            }

            onComplete?.Invoke();
            operation.allowSceneActivation = true;
            yield return operation;

            editorScene = GetLoadedEditorScene(scenePath);
            if (editorScene.IsValid() && editorScene.isLoaded)
            {
                SceneManager.SetActiveScene(editorScene);
            }
            else
            {
                Debug.LogError($"资源下载失败Key : {Key} ,类型为: {typeof(Scene)}");
            }
        }

        private async UniTask LoadSceneUniTaskInEditor(IProgress<float> progress, Action onComplete = null)
        {
            if (isLoader)
            {
                Scene scene = GetLoadedEditorScene();
                if (scene.IsValid() && scene.isLoaded)
                {
                    progress?.Report(1f);
                    onComplete?.Invoke();
                    SceneManager.SetActiveScene(scene);
                }

                return;
            }

            isLoader = true;
            string scenePath = GetSceneAssetPath(Key);
            AsyncOperation operation = EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(_mode));
            if (operation == null)
            {
                Debug.LogError($"资源下载失败Key : {Key} ,类型为: {typeof(Scene)}");
                return;
            }

            operation.allowSceneActivation = false;
            while (operation.progress < 0.9f)
            {
                progress?.Report(operation.progress);
                await UniTask.Yield();
            }

            progress?.Report(0.9f);
            onComplete?.Invoke();
            operation.allowSceneActivation = true;
            await operation.ToUniTask();

            editorScene = GetLoadedEditorScene(scenePath);
            if (editorScene.IsValid() && editorScene.isLoaded)
            {
                SceneManager.SetActiveScene(editorScene);
                progress?.Report(1f);
            }
            else
            {
                Debug.LogError($"资源下载失败Key : {Key} ,类型为: {typeof(Scene)}");
            }
        }

        private Scene GetLoadedEditorScene()
        {
            return GetLoadedEditorScene(GetSceneAssetPath(Key));
        }

        private static Scene GetLoadedEditorScene(string scenePath)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.path == scenePath)
                {
                    return scene;
                }
            }

            return default;
        }

        private static string GetSceneAssetPath(string sceneKey)
        {
            if (string.IsNullOrEmpty(sceneKey))
            {
                return string.Empty;
            }

            if (sceneKey.StartsWith("Assets/") || sceneKey.StartsWith("Packages/"))
            {
                return sceneKey;
            }

            string path = AssetDatabase.GUIDToAssetPath(sceneKey);
            return string.IsNullOrEmpty(path) ? sceneKey : path;
        }
#endif
    }
}
