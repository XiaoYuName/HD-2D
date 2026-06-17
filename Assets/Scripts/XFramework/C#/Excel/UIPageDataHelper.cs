using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public static class UIPageDataHelper
{
    private static List<UIPageData> DataList;

    public static void InitData(string jsonStr)
    {
        DataList = JsonConvert.DeserializeObject < List <UIPageData>>
        (jsonStr);
        if (DataList == null || DataList.Count == 0)
        {
            Debug.LogError("反序列化异常");
        }
    }

    public static List<UIPageData> GetAll()
    {
        return DataList;
    }

    public static UIPageData GetByIdx(int idx)
    {
        var info = GetByCondition(x => x.idx == idx);
        if (info == null || info.Count == 0)
        {
            return null;
        }

        return info[0];
    }

    public static List<UIPageData> GetByCondition(Predicate<UIPageData> predicate)
    {
        return DataList.FindAll(predicate);
    }

    public static UIPageData GetOneByCondition(Predicate<UIPageData> predicate)
    {
        var temp = GetByCondition(predicate);
        if (temp == null || temp.Count == 0)
        {
            return null;
        }
        return temp[0];
    }
}