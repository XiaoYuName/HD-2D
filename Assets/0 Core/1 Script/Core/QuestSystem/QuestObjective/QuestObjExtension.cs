using UnityEngine;

public static class QuestObjExtension
{
    public static bool HasComplete(this QuestObjInfoBase questObjBase)
    {
        return questObjBase.State == QuestObjState.Complete;
    }
}
