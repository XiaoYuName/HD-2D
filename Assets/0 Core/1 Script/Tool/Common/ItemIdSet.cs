
public static class ItemIdSet
{
    public const long DefaRecipeFood = 120000;
    public const long Bait = 130000;
    
    public const long JunkRangeStart = 150000;// 垃圾的Id范围
    public const long JunkRangeEnd = 160000;
    public static bool IsJunk(long id) => id >= JunkRangeStart && id <= JunkRangeEnd;
}
