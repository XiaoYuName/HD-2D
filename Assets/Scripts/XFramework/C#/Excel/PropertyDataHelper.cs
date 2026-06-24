using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public static class PropertyDataHelper
{
    private static List<PropertyData> DataList;

    public static void InitData(string jsonStr)
    {
        DataList = JsonConvert.DeserializeObject < List <PropertyData>>
        (jsonStr);
        if (DataList == null || DataList.Count == 0)
        {
            Debug.LogError("反序列化异常");
        }
    }

    public static List<PropertyData> GetAll()
    {
        return DataList;
    }

    public static PropertyData GetByIdx(int idx)
    {
        var info = GetByCondition(x => x.idx == idx);
        if (info == null || info.Count == 0)
        {
            return null;
        }

        return info[0];
    }

    public static List<PropertyData> GetByCondition(Predicate<PropertyData> predicate)
    {
        return DataList.FindAll(predicate);
    }

    public static PropertyData GetOneByCondition(Predicate<PropertyData> predicate)
    {
        var temp = GetByCondition(predicate);
        if (temp == null || temp.Count == 0)
        {
            return null;
        }
        return temp[0];
    }
}