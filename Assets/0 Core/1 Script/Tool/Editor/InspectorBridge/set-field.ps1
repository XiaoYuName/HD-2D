<#
.SYNOPSIS
    通过 Unity 编辑器里常驻的 InspectorBridgeServer（Assets/0 Core/1 Script/Tool/Editor/InspectorBridgeServer.cs）
    远程给某个组件的 Inspector 字段赋值/读值，相当于人工把物体拖进 Inspector 槽位。

.DESCRIPTION
    前提：Unity 编辑器必须开着（脚本已编译过一次），该服务会自动监听 127.0.0.1:58732。
    支持三种目标：
      - PrefabAsset : 直接修改 Prefab 资源并保存
      - PrefabStage : 当前双击进入的 Prefab 编辑模式
      - OpenScene   : 当前打开的场景（只标脏，需要手动 Ctrl+S 保存）

.PARAMETER Action
    setField | getField | listFields

.PARAMETER TargetMode
    prefabAsset | prefabStage | openScene

.PARAMETER TargetPath
    targetMode=prefabAsset 时，Prefab 的 Assets 相对路径，例如
    "Assets/AddressableAssets/Remote/Prefabs/UGUI/MiniGame1KitchenUI/CookSettlePanel.prefab"

.PARAMETER HierarchyPath
    目标物体相对路径（"/" 分隔），空字符串表示根节点本身。
    openScene 模式下第一段是场景里某个根物体的名字。

.PARAMETER Component
    要操作的组件类型名（类名即可，不用写命名空间）

.PARAMETER Field
    要赋值/读取的字段名，需和 [SerializeField] 的字段名完全一致

.PARAMETER ValueKind
    asset | objectInTarget | null | string | float | int | bool

.PARAMETER ValueAssetPath
    valueKind=asset 时：资源的 Assets 相对路径

.PARAMETER ValueHierarchyPath
    valueKind=objectInTarget 时：另一个物体相对同一个根的路径

.PARAMETER ValueComponent
    valueKind=objectInTarget 且字段类型是组件时：该物体上要取的组件类型名

.PARAMETER ValueString / ValueFloat / ValueInt / ValueBool
    对应 valueKind=string/float/int/bool 时的值

.EXAMPLE
    # 给 EatPanel 预制体上的 EatPanel 组件的 eatAloneSprite 字段赋值一张贴图
    ./set-field.ps1 -Action setField -TargetMode prefabAsset `
        -TargetPath "Assets/AddressableAssets/Remote/Prefabs/UGUI/.../EatPanel.prefab" `
        -Component EatPanel -Field eatAloneSprite `
        -ValueKind asset -ValueAssetPath "Assets/AddressableAssets/Remote/Texture2D/UI/Kitchen/EatPanel/xxx.png"

.EXAMPLE
    # 把 Prefab 内 "EatEndTip/EatEndTipAnim" 物体上的 Image 组件，赋给 curFoodItemSlotUI 字段
    ./set-field.ps1 -Action setField -TargetMode prefabAsset -TargetPath "Assets/.../EatPanel.prefab" `
        -Component EatPanel -Field curFoodItemSlotUI `
        -ValueKind objectInTarget -ValueHierarchyPath "EatEndTip" -ValueComponent ItemSlotUI

.EXAMPLE
    # 列出某组件所有可序列化字段名和类型，找不准字段名时先查一下
    ./set-field.ps1 -Action listFields -Component EatPanel -TargetMode prefabAsset -TargetPath "Assets/.../EatPanel.prefab"
#>
param(
    [Parameter(Mandatory = $true)][ValidateSet("setField", "getField", "listFields")][string]$Action,
    [ValidateSet("prefabAsset", "prefabStage", "openScene")][string]$TargetMode = "prefabAsset",
    [string]$TargetPath = "",
    [string]$HierarchyPath = "",
    [string]$Component = "",
    [string]$Field = "",
    [ValidateSet("asset", "objectInTarget", "null", "string", "float", "int", "bool")][string]$ValueKind = "asset",
    [string]$ValueAssetPath = "",
    [string]$ValueHierarchyPath = "",
    [string]$ValueComponent = "",
    [string]$ValueString = "",
    [float]$ValueFloat = 0,
    [int]$ValueInt = 0,
    [bool]$ValueBool = $false,
    [int]$Port = 58732
)

$body = @{
    action             = $Action
    targetMode         = $TargetMode
    targetPath         = $TargetPath
    hierarchyPath      = $HierarchyPath
    component          = $Component
    field              = $Field
    valueKind          = $ValueKind
    valueAssetPath     = $ValueAssetPath
    valueHierarchyPath = $ValueHierarchyPath
    valueComponent     = $ValueComponent
    valueString        = $ValueString
    valueFloat         = $ValueFloat
    valueInt           = $ValueInt
    valueBool          = $ValueBool
} | ConvertTo-Json -Compress

try {
    $response = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/" -Method Post -Body $body -ContentType "application/json; charset=utf-8"
}
catch {
    Write-Error "请求失败，确认 Unity 编辑器是否已打开该工程（服务未启动/端口不通）：$_"
    exit 1
}

if ($response.ok) {
    if ($response.info) { Write-Output $response.info } else { Write-Output "OK" }
    exit 0
}
else {
    Write-Error $response.error
    exit 1
}
