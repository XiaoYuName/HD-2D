using Unity.GraphToolkit.Editor;
using UnityEngine;

public class StartNode : DramaBaseNode
{
   protected override void OnDefinePorts(IPortDefinitionContext context)
   {
      // Start is a special node that has no input, so we don't call DefineCommonPorts
      context.AddOutputPort(EXECUTION_PORT_DEFAULT_NAME)
         .WithDisplayName(string.Empty)
         .WithConnectorUI(PortConnectorUI.Arrowhead)
         .Build();
      base.OnDefinePorts(context);
   }
}
