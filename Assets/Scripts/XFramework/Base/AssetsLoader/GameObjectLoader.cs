using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// Unity 实例化GameObject 资源管理类
    /// </summary>
    public class GameObjectLoader: BaseLoader
    {
        /// <summary>
        /// 资源缓存列表
        /// </summary>
        private Stack<GameObject> caches = new Stack<GameObject>();

        /// <summary>
        /// 正在使用的列表
        /// </summary>
        private HashSet<GameObject> references = new HashSet<GameObject>();

        public GameObject prefab;

        public GameObjectLoader(string Key) : base(Key)
        {
            prefab = null;
        }

        public GameObjectLoader(GameObject prefab,string Key) : base(Key)
        {
            this.prefab = prefab;
            
        }

        public GameObject Instantiate(Transform parent)
        {
            GameObject obj = null;
            if (caches.Count > 0)
            {
                caches.Pop();
            }
            else
            {
                obj = Object.Instantiate(this.prefab) as GameObject;
                obj.name = this.key;
            }
            this.references.Add(obj);
            return obj;
        }

        public GameObject Instantiate()
        {
            if (caches.Count > 0)
            {
                var Obj = caches.Pop();
                this.references.Add(Obj);
                return Obj;
            }
            
            if (this.prefab != null)
            {
                var obj = Object.Instantiate(this.prefab) as GameObject;
                obj.name = key;
                references.Add(obj);
                return obj;
            }
            else
            {
                this.prefab = base.Load<GameObject>();
                var obj = Object.Instantiate(this.prefab) as GameObject;
                obj.name = key;
                base.Release();
                return obj;
            }
        }

        public void InstantiateAsync(LoadCallBack<GameObject> Call)
        {
            if (caches.Count > 0)
            {
                var Obj = caches.Pop();
                this.references.Add(Obj);
                Call?.Invoke(Obj);
                return;
            }
            
            if (prefab != null)
            {
                var obj = Object.Instantiate(this.prefab) as GameObject;
                obj.name = key;
                references.Add(obj);
                Call?.Invoke(obj);
                return;
            }

            base.LoadAsync<GameObject>((obj) =>
            {
                this.prefab = obj;
                var OBJ = Object.Instantiate(this.prefab) as GameObject;
                OBJ.name = key;
                base.Release();
                Call?.Invoke(OBJ);
            });
        }

        public void Free(GameObject obj)
        {
            this.caches.Push(obj);
            this.references.Remove(obj);
            obj.transform.SetParent(AssetsManager.Instance.PoolRoot);
        }

        public override void Release()
        {
            foreach (var obj in this.caches)
            {
                Object.Destroy(obj.gameObject);
            }
            if (this.references.Count <= 0)
            {
                base.Release();
            }
        }
    }
}
