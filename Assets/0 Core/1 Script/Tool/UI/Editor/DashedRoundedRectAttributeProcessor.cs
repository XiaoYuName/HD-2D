using System;
using System.Collections.Generic;
using System.Reflection;
using Sirenix.OdinInspector.Editor;
using UnityEngine;

/// <summary>
/// 隐藏 <see cref="DashedRoundedRect"/> 从 MaskableGraphic 继承来的 m_OnCullStateChanged 事件字段。
/// 该 UnityEvent 只在被遮罩裁剪(RectMask2D)时才回调，本组件用不到，
/// 而 Odin 默认会把继承来的序列化成员全画出来，故在此给它补一个 HideInInspector 隐藏。
/// </summary>
public class DashedRoundedRectAttributeProcessor : OdinAttributeProcessor<DashedRoundedRect>
{
    public override void ProcessChildMemberAttributes(InspectorProperty parentProperty, MemberInfo member, List<Attribute> attributes)
    {
        if (member.Name == "m_OnCullStateChanged")
            attributes.Add(new HideInInspector());
    }
}
