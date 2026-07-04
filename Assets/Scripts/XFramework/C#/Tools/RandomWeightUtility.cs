using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XFramework
{
    public static class RandomWeightUtility
    {
        /// <summary>
        /// 加权随机
        /// </summary>
        /// <typeparam name="T">随机对象类型</typeparam>
        /// <param name="list">随机列表</param>
        /// <param name="getWeight">获取权重的方法</param>
        /// <returns>随机出来的对象</returns>
        public static T GetRandomByWeight<T>(IList<T> list, Func<T, int> getWeight)
        {
            if (list == null || list.Count == 0)
            {
                Debug.LogError("加权随机失败：列表为空");
                return default;
            }

            int totalWeight = 0;

            for (int i = 0; i < list.Count; i++)
            {
                int weight = getWeight(list[i]);

                if (weight > 0)
                {
                    totalWeight += weight;
                }
            }

            if (totalWeight <= 0)
            {
                Debug.LogError("加权随机失败：总权重小于等于 0");
                return default;
            }

            int randomValue = UnityEngine.Random.Range(0, totalWeight);

            int currentWeight = 0;

            for (int i = 0; i < list.Count; i++)
            {
                int weight = getWeight(list[i]);

                if (weight <= 0)
                {
                    continue;
                }

                currentWeight += weight;

                if (randomValue < currentWeight)
                {
                    return list[i];
                }
            }

            return list[list.Count - 1];
        }
    }
}
