using Sirenix.OdinInspector;
using UnityEngine;

public class DialogueNode : DramaBaseNode
{
    public bool isSystemActorName = true;
    
    protected override void OnDefinePorts(IPortDefinitionContext context)
    {
        base.OnDefinePorts(context);
        context.AddInputPort<string>("ActorName")
            .WithDisplayName("说话人:")
            .WithDefaultValue(string.Empty)
            .Build();
        if (!isSystemActorName)
        {
            context.AddInputPort<string>("ActorName").WithDisplayName("说话人").Build();
        }
    }

    protected override void OnDefineOptions(IOptionDefinitionContext context)
    {
        base.OnDefineOptions(context);
        context.AddOption<bool>("isSystemActorName")
            .WithDefaultValue(true)
            .WithDisplayName("是否系统说话人");
        
    }
}
