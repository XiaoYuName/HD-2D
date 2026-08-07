namespace XFramework
{
    public static class QuestObjExtension
    {
        public static bool HasComplete(this QuestObjInfoBase questObj)
        {
            return questObj != null && questObj.IsComplete;
        }
    }
}
