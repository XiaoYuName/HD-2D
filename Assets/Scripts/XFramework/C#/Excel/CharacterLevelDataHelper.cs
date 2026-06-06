using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public static class CharacterLevelDataHelper
{
    private static List<CharacterLevelData> DataList;

    public static void InitData(string jsonStr)
    {
        DataList = JsonConvert.DeserializeObject < List <CharacterLevelData>>
        (jsonStr);
        if (DataList == null || DataList.Count == 0)
        {
            Debug.LogError("反序列化异常");
        }
    }

    public static List<CharacterLevelData> GetAll()
    {
        return DataList;
    }

    public static CharacterLevelData GetByIdx(int idx)
    {
        var info = GetByCondition(x => x.idx == idx);
        if (info == null || info.Count == 0)
        {
            return null;
        }

        return info[0];
    }

    public static List<CharacterLevelData> GetByCondition(Predicate<CharacterLevelData> predicate)
    {
        return DataList.FindAll(predicate);
    }

    public static CharacterLevelData GetOneByCondition(Predicate<CharacterLevelData> predicate)
    {
        var temp = GetByCondition(predicate);
        if (temp == null || temp.Count == 0)
        {
            return null;
        }
        return temp[0];
    }
}