using UnityEngine;
using Unity.GraphToolkit.Editor;

[System.Serializable]
public class DramaDialogueNode : Node
{
    protected override void OnDefinePorts(IPortDefinitionContext context)
    {
        context.AddInputPort<string>("Input").Build();
        context.AddOutputPort<string>("Output").Build();
    }
}
