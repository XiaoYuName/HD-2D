using UnityEngine;

public abstract class QuestObjInfoBase
{
    public long Id;
    public QuestObjState State;

    public virtual void Init(QuestObjData data)
    {

    }
    protected virtual void SubsEvents()
    {
    }

    protected virtual void UnsubsEvents()
    {
    }
}