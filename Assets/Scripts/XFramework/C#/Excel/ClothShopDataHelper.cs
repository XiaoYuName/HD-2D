using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public static class ClothShopDataHelper
{
    private static List<ClothShopData> DataList;

    public static void InitData(string jsonStr)
    {
        DataList = JsonConvert.DeserializeObject < List <ClothShopData>>
        (jsonStr);
        if (DataList == null || DataList.Count == 0)
        {
            Debug.LogError("反序列化异常");
        }
    }

    public static List<ClothShopData> GetAll()
    {
        return DataList;
    }

    public static ClothShopData GetByIdx(int idx)
    {
        var info = GetByCondition(x => x.idx == idx);
        if (info == null || info.Count == 0)
        {
            return null;
        }

        return info[0];
    }

    public static List<ClothShopData> GetByCondition(Predicate<ClothShopData> predicate)
    {
        return DataList.FindAll(predicate);
    }

    public static ClothShopData GetOneByCondition(Predicate<ClothShopData> predicate)
    {
        var temp = GetByCondition(predicate);
        if (temp == null || temp.Count == 0)
        {
            return null;
        }
        return temp[0];
    }
}