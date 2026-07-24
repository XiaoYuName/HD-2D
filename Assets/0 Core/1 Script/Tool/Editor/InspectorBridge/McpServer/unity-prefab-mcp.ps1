<#
.SYNOPSIS
    MCP stdio server for token-efficient Unity Prefab inspection and binding.

.DESCRIPTION
    Unity must have this project open. The server translates MCP tool calls to the
    project's localhost-only InspectorBridge HTTP service. Stdout is reserved for
    newline-delimited JSON-RPC messages required by MCP.
#>
param(
    [string]$ProjectPath = "",
    [int]$Port = 58732,
    [int]$TimeoutSeconds = 25
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $cursor = Get-Item -LiteralPath $PSScriptRoot
    while ($null -ne $cursor -and
        (-not (Test-Path -LiteralPath (Join-Path $cursor.FullName "Assets")) -or
         -not (Test-Path -LiteralPath (Join-Path $cursor.FullName "ProjectSettings")))) {
        $cursor = $cursor.Parent
    }
    if ($null -eq $cursor) {
        throw "Cannot locate the Unity project root above $PSScriptRoot. Pass -ProjectPath explicitly."
    }
    $ProjectPath = $cursor.FullName
}

$ProjectPath = [System.IO.Path]::GetFullPath($ProjectPath).TrimEnd('\', '/')
$Endpoint = "http://127.0.0.1:$Port/"

function Write-McpMessage {
    param([Parameter(Mandatory = $true)]$Message)
    $json = $Message | ConvertTo-Json -Depth 40 -Compress
    [Console]::Out.WriteLine($json)
    [Console]::Out.Flush()
}

function New-JsonRpcResponse {
    param($Id, $Result)
    return [ordered]@{ jsonrpc = "2.0"; id = $Id; result = $Result }
}

function New-JsonRpcError {
    param($Id, [int]$Code, [string]$Message)
    return [ordered]@{
        jsonrpc = "2.0"
        id = $Id
        error = [ordered]@{ code = $Code; message = $Message }
    }
}

function Get-ToolDefinitions {
    $tools = @(
        [ordered]@{
            name = "unity_prefab_status"
            description = "Check that the correct Unity project is open and its Prefab bridge is ready."
            inputSchema = [ordered]@{ type = "object"; properties = [ordered]@{}; additionalProperties = $false }
        },
        [ordered]@{
            name = "refresh_unity_assets"
            description = "Ask Unity to refresh AssetDatabase and request script compilation. The request returns before refresh starts; poll get_unity_compile_status afterward."
            inputSchema = [ordered]@{
                type = "object"
                properties = [ordered]@{
                    refreshOnly = @{ type = "boolean"; default = $false; description = "Refresh assets without explicitly requesting script compilation." }
                }
                additionalProperties = $false
            }
        },
        [ordered]@{
            name = "capture_unity_screenshot"
            description = "Capture the focused Unity Editor window as a downscaled JPEG image. Returns MCP image content directly to keep screenshot inspection token-efficient. Use captureTarget=custom for an explicit desktop rectangle."
            inputSchema = [ordered]@{
                type = "object"
                properties = [ordered]@{
                    captureTarget = @{ type = "string"; enum = @("focusedWindow", "custom"); default = "focusedWindow" }
                    x = @{ type = "integer"; description = "Custom rectangle left coordinate in desktop pixels." }
                    y = @{ type = "integer"; description = "Custom rectangle top coordinate in desktop pixels." }
                    widthPixels = @{ type = "integer"; minimum = 1; description = "Custom rectangle width." }
                    heightPixels = @{ type = "integer"; minimum = 1; description = "Custom rectangle height." }
                    maxWidth = @{ type = "integer"; minimum = 64; maximum = 4096; default = 1600 }
                    maxHeight = @{ type = "integer"; minimum = 64; maximum = 4096; default = 1200 }
                    jpegQuality = @{ type = "integer"; minimum = 20; maximum = 95; default = 75 }
                }
                additionalProperties = $false
            }
        },
        [ordered]@{
            name = "get_unity_compile_status"
            description = "Read current compilation state and the persisted errors/warnings from the latest script compilation, including after an assembly reload."
            inputSchema = [ordered]@{
                type = "object"
                properties = [ordered]@{
                    excludeMessages = @{ type = "boolean"; default = $false; description = "Return counts only." }
                    maxResults = @{ type = "integer"; minimum = 1; maximum = 200; default = 50; description = "Maximum errors/warnings to return." }
                }
                additionalProperties = $false
            }
        },
        [ordered]@{
            name = "get_prefab_mcp_settings"
            description = "Read the project-shared ScriptableObject settings, including write policy, backups, limits, folders, and server port."
            inputSchema = [ordered]@{ type = "object"; properties = [ordered]@{}; additionalProperties = $false }
        },
        [ordered]@{
            name = "find_prefabs"
            description = "Find Prefab assets without reading their YAML. Returns compact asset paths and GUIDs."
            inputSchema = [ordered]@{
                type = "object"
                properties = [ordered]@{
                    query = @{ type = "string"; description = "Unity AssetDatabase search text, usually a prefab name fragment." }
                    searchFolders = @{ type = "array"; items = @{ type = "string" }; description = "Optional Assets/... folders to search." }
                    maxResults = @{ type = "integer"; minimum = 1; maximum = 200; default = 20 }
                }
                additionalProperties = $false
            }
        },
        [ordered]@{
            name = "get_prefab_tree"
            description = "Get a compact Prefab hierarchy. objectId is sibling-index based and should be used by later calls."
            inputSchema = [ordered]@{
                type = "object"
                properties = [ordered]@{
                    prefabPath = @{ type = "string"; description = "Assets/.../*.prefab path." }
                    rootObjectId = @{ type = "string"; description = "Optional subtree root objectId. Returned objectIds remain relative to the full Prefab root." }
                    maxDepth = @{ type = "integer"; minimum = 1; maximum = 64; default = 4 }
                    includeComponents = @{ type = "boolean"; default = $true }
                    compact = @{ type = "boolean"; default = $false; description = "Omit hierarchyPath, full component type names, and true/default flags to reduce tokens." }
                    nameFilter = @{ type = "string"; description = "Optional case-insensitive node-name filter." }
                    componentTypeFilter = @{ type = "string"; description = "Optional short or full component-type filter." }
                    maxResults = @{ type = "integer"; minimum = 1; maximum = 1000; default = 100 }
                }
                required = @("prefabPath")
                additionalProperties = $false
            }
        },
        [ordered]@{
            name = "get_component_fields"
            description = "List only the top-level Unity-serialized fields of one component, including compact current values."
            inputSchema = [ordered]@{
                type = "object"
                properties = [ordered]@{
                    prefabPath = @{ type = "string" }
                    objectId = @{ type = "string"; description = "objectId returned by get_prefab_tree; root is 0." }
                    componentIndex = @{ type = "integer"; minimum = 0 }
                    propertyPath = @{ type = "string"; description = "Optional: expand the children of this property instead of listing top-level fields. Works for nested structs and arrays (arrays return size + elements). Use returned propertyPaths with edit_prefab setValue." }
                    fieldNameFilter = @{ type = "string"; description = "Optional case-insensitive property/display-name filter." }
                    onlyObjectReferences = @{ type = "boolean"; default = $false }
                    onlyUnassigned = @{ type = "boolean"; default = $false }
                }
                required = @("prefabPath", "objectId", "componentIndex")
                additionalProperties = $false
            }
        },
        [ordered]@{
            name = "find_binding_candidates"
            description = "Find type-compatible GameObjects or Components inside a Prefab for one serialized object-reference field."
            inputSchema = [ordered]@{
                type = "object"
                properties = [ordered]@{
                    prefabPath = @{ type = "string" }
                    objectId = @{ type = "string" }
                    componentIndex = @{ type = "integer"; minimum = 0 }
                    propertyPath = @{ type = "string"; description = "SerializedProperty.propertyPath returned by get_component_fields." }
                    candidateRootObjectId = @{ type = "string"; description = "Optional subtree root objectId." }
                    maxResults = @{ type = "integer"; minimum = 1; maximum = 200; default = 30 }
                }
                required = @("prefabPath", "objectId", "componentIndex", "propertyPath")
                additionalProperties = $false
            }
        },
        [ordered]@{
            name = "assign_object_reference"
            description = "Validate or save a Prefab-internal drag-and-drop assignment. Call first with apply=false; set apply=true only after choosing the exact candidate. Use sourceComponentIndex=-1 for a GameObject."
            inputSchema = [ordered]@{
                type = "object"
                properties = [ordered]@{
                    prefabPath = @{ type = "string" }
                    objectId = @{ type = "string"; description = "Target objectId." }
                    componentIndex = @{ type = "integer"; minimum = 0; description = "Target component index." }
                    propertyPath = @{ type = "string" }
                    sourceObjectId = @{ type = "string"; description = "Required when clear=false; pass 0 explicitly for the Prefab root." }
                    sourceComponentIndex = @{ type = "integer"; minimum = -1; description = "Candidate component index, or -1 for GameObject." }
                    clear = @{ type = "boolean"; default = $false; description = "Clear the field; source fields are ignored." }
                    apply = @{ type = "boolean"; default = $false; description = "false validates only; true writes and saves the Prefab." }
                }
                required = @("prefabPath", "objectId", "componentIndex", "propertyPath", "apply")
                additionalProperties = $false
            }
        },
        [ordered]@{
            name = "find_asset_candidates"
            description = "Find project asset candidates compatible with a serialized object-reference field, including ScriptableObjects, sprites, prefabs, and prefab components."
            inputSchema = [ordered]@{
                type = "object"
                properties = [ordered]@{
                    prefabPath = @{ type = "string" }
                    objectId = @{ type = "string" }
                    componentIndex = @{ type = "integer"; minimum = 0 }
                    propertyPath = @{ type = "string" }
                    query = @{ type = "string"; description = "Optional AssetDatabase name/search text." }
                    searchFolders = @{ type = "array"; items = @{ type = "string" }; description = "Defaults to the SO-configured asset folders." }
                    maxResults = @{ type = "integer"; minimum = 1; maximum = 500; default = 50 }
                }
                required = @("prefabPath", "objectId", "componentIndex", "propertyPath")
                additionalProperties = $false
            }
        },
        [ordered]@{
            name = "assign_asset_reference"
            description = "Validate or save an asset drag-and-drop assignment using an exact candidate returned by find_asset_candidates. Call with apply=false first."
            inputSchema = [ordered]@{
                type = "object"
                properties = [ordered]@{
                    prefabPath = @{ type = "string" }
                    objectId = @{ type = "string" }
                    componentIndex = @{ type = "integer"; minimum = 0 }
                    propertyPath = @{ type = "string" }
                    assetPath = @{ type = "string" }
                    assetName = @{ type = "string" }
                    assetType = @{ type = "string" }
                    assetLocalId = @{ type = "integer"; description = "Exact local file ID returned by find_asset_candidates." }
                    assetObjectId = @{ type = "string"; description = "For prefab GameObject/Component candidates." }
                    assetComponentIndex = @{ type = "integer"; minimum = -1; description = "For prefab Component candidates." }
                    apply = @{ type = "boolean"; default = $false }
                }
                required = @("prefabPath", "objectId", "componentIndex", "propertyPath", "assetPath", "apply")
                additionalProperties = $false
            }
        },
        [ordered]@{
            name = "create_ui_element"
            description = "Validate or create a configured UI element under a RectTransform in a Prefab. Supported types: container, image, button, tmpText, verticalLayout, scrollView."
            inputSchema = [ordered]@{
                type = "object"
                properties = [ordered]@{
                    prefabPath = @{ type = "string" }
                    parentObjectId = @{ type = "string"; description = "Parent objectId; root is 0." }
                    elementType = @{ type = "string"; enum = @("container", "image", "button", "tmpText", "verticalLayout", "scrollView") }
                    elementName = @{ type = "string" }
                    label = @{ type = "string"; description = "Button label or TMP text content." }
                    width = @{ type = "number"; exclusiveMinimum = 0; description = "Defaults to the SO UI width." }
                    height = @{ type = "number"; exclusiveMinimum = 0; description = "Defaults to the SO UI height." }
                    apply = @{ type = "boolean"; default = $false }
                }
                required = @("prefabPath", "parentObjectId", "elementType", "apply")
                additionalProperties = $false
            }
        },
        [ordered]@{
            name = "edit_prefab"
            description = "Run an ordered, transactional batch of prefab edits in ONE call: rename, setActive, reparent, setSiblingIndex, delete, duplicate, createObject, instantiatePrefab, addComponent, removeComponent, removeMissingScripts, setValue. Any failing op aborts the batch and nothing is saved. apply=false dry-runs every op in memory and reports per-op results. objectIds are evaluated as the batch mutates the tree, so later ops must use ids valid after earlier structural ops; every result returns the node's up-to-date objectId. Prefer one batch over many single-op calls."
            inputSchema = [ordered]@{
                type = "object"
                properties = [ordered]@{
                    prefabPath = @{ type = "string"; description = "Assets/.../*.prefab path." }
                    apply = @{ type = "boolean"; default = $false; description = "false dry-runs the whole batch; true backs up once, runs, and saves once." }
                    operations = @{
                        type = "array"
                        minItems = 1
                        items = [ordered]@{
                            type = "object"
                            properties = [ordered]@{
                                op = @{ type = "string"; enum = @("rename", "setActive", "reparent", "setSiblingIndex", "delete", "duplicate", "createObject", "instantiatePrefab", "addComponent", "removeComponent", "removeMissingScripts", "setValue") }
                                objectId = @{ type = "string"; description = "Target node id from get_prefab_tree; root is 0." }
                                parentObjectId = @{ type = "string"; description = "reparent/createObject/instantiatePrefab: parent node id; root is 0." }
                                newName = @{ type = "string"; description = "rename (required) / duplicate / createObject / instantiatePrefab (optional)." }
                                active = @{ type = "string"; enum = @("true", "false"); description = "setActive only; pass as string." }
                                siblingIndex = @{ type = "string"; description = "Optional 0-based child index as a string, e.g. '2'. Required by setSiblingIndex." }
                                componentType = @{ type = "string"; description = "addComponent: short or full type name; ambiguous short names are rejected with the full-name list." }
                                componentIndex = @{ type = "integer"; description = "setValue/removeComponent: component index from get_prefab_tree. Transform (index 0) cannot be removed." }
                                propertyPath = @{ type = "string"; description = "setValue: SerializedProperty path, e.g. m_AnchoredPosition, m_SizeDelta, items.Array.data[2].label, items.Array.size." }
                                value = @{ type = "string"; description = "setValue only, always a string. Formats: numbers/strings literal; bool true|false; enum name or int; Color #RRGGBBAA; Vector2 x,y; Vector3 x,y,z; Vector4/Quaternion x,y,z,w (Quaternion also accepts euler x,y,z); Rect x,y,w,h; object reference: null | asset:Assets/path[#subAssetName] | object:<objectId>[#componentIndex] (-1 = GameObject)." }
                                sourcePrefabPath = @{ type = "string"; description = "instantiatePrefab: Assets/.../*.prefab to nest under parentObjectId." }
                            }
                            required = @("op")
                            additionalProperties = $false
                        }
                    }
                }
                required = @("prefabPath", "operations", "apply")
                additionalProperties = $false
            }
        },
        [ordered]@{
            name = "validate_prefab"
            description = "Report missing scripts and unassigned top-level object references. By default, reference checks only inspect project scripts under Assets to avoid Unity UI/internal-field noise."
            inputSchema = [ordered]@{
                type = "object"
                properties = [ordered]@{
                    prefabPath = @{ type = "string" }
                    maxResults = @{ type = "integer"; minimum = 1; maximum = 500; default = 100 }
                    includeUnityComponents = @{ type = "boolean"; default = $false; description = "Also inspect package and Unity-provided MonoBehaviour components; this can be noisy." }
                }
                required = @("prefabPath")
                additionalProperties = $false
            }
        }
    )

    # Inject targetMode/sceneRootName into every target-aware tool and drop prefabPath from
    # required (only prefabAsset needs it; the server validates per targetMode). Keeps schemas DRY.
    $targetAware = @(
        "get_prefab_tree", "get_component_fields", "find_binding_candidates",
        "assign_object_reference", "find_asset_candidates", "assign_asset_reference",
        "create_ui_element", "edit_prefab", "validate_prefab"
    )
    foreach ($tool in $tools) {
        if ($targetAware -notcontains $tool.name) { continue }
        $props = $tool.inputSchema.properties
        if ($props.Contains("prefabPath")) {
            $props["prefabPath"] = @{ type = "string"; description = "Assets/.../*.prefab path. Required when targetMode=prefabAsset (the default)." }
        }
        $props["targetMode"] = @{ type = "string"; enum = @("prefabAsset", "prefabStage", "openScene"); default = "prefabAsset"; description = "Edit target. prefabAsset (default) reads/writes the on-disk prefab and supports apply=false dry-run; prefabStage uses the currently open Prefab stage; openScene uses sceneRootName in the active scene. Live stage/scene targets mutate real objects and mark the scene dirty (save with Ctrl+S)." }
        $props["sceneRootName"] = @{ type = "string"; description = "targetMode=openScene only: name of a root GameObject in the active scene. objectId 0 refers to that root object." }
        if ($tool.inputSchema.Contains("required")) {
            $tool.inputSchema["required"] = @($tool.inputSchema["required"] | Where-Object { $_ -ne "prefabPath" })
        }
    }
    return $tools
}

function Invoke-UnityBridge {
    param([string]$Action, $Arguments)

    $request = [ordered]@{
        action = $Action
        expectedProjectPath = $ProjectPath
    }
    if ($null -ne $Arguments) {
        foreach ($property in $Arguments.PSObject.Properties) {
            $request[$property.Name] = $property.Value
        }
    }

    $body = $request | ConvertTo-Json -Depth 30 -Compress
    return Invoke-RestMethod -Uri $Endpoint -Method Post -Body $body `
        -ContentType "application/json; charset=utf-8" -TimeoutSec $TimeoutSeconds
}

# JsonUtility emits every field (empty strings/arrays, unused defaults).
# Recursively strip empty values to keep MCP responses token-efficient.
# NOTE: keep this file ASCII-only; PowerShell 5.1 reads BOM-less files as ANSI.
function Remove-EmptyValues {
    param($Value)
    if ($null -eq $Value) { return $null }
    if ($Value -is [string]) { return $Value }
    if ($Value -is [System.Collections.IDictionary]) {
        $result = [ordered]@{}
        foreach ($key in @($Value.Keys)) {
            $cleaned = Remove-EmptyValues $Value[$key]
            if ($null -eq $cleaned) { continue }
            if ($cleaned -is [string] -and $cleaned.Length -eq 0) { continue }
            if ($cleaned -is [System.Collections.IList] -and $cleaned.Count -eq 0) { continue }
            $result[$key] = $cleaned
        }
        return $result
    }
    if ($Value -is [System.Management.Automation.PSCustomObject]) {
        $result = [ordered]@{}
        foreach ($property in $Value.PSObject.Properties) {
            $cleaned = Remove-EmptyValues $property.Value
            if ($null -eq $cleaned) { continue }
            if ($cleaned -is [string] -and $cleaned.Length -eq 0) { continue }
            if ($cleaned -is [System.Collections.IList] -and $cleaned.Count -eq 0) { continue }
            $result[$property.Name] = $cleaned
        }
        return $result
    }
    if ($Value -is [System.Collections.IEnumerable]) {
        $items = New-Object System.Collections.ArrayList
        foreach ($item in $Value) { [void]$items.Add((Remove-EmptyValues $item)) }
        return ,$items # unary comma keeps single-element collections as arrays
    }
    return $Value
}

function ConvertTo-CompactPrefabNodes {
    param($Nodes)

    $items = New-Object System.Collections.ArrayList
    foreach ($node in @($Nodes)) {
        $item = [ordered]@{
            objectId = $node.objectId
            name = $node.name
            depth = $node.depth
        }
        if (-not $node.activeSelf) {
            $item.activeSelf = $false
        }
        if ($null -ne $node.components -and $node.components.Count -gt 0) {
            $components = New-Object System.Collections.ArrayList
            foreach ($component in @($node.components)) {
                $componentItem = [ordered]@{
                    componentIndex = $component.componentIndex
                    shortType = $component.shortType
                }
                if ($component.missing) {
                    $componentItem.missing = $true
                }
                [void]$components.Add($componentItem)
            }
            $item.components = $components
        }
        [void]$items.Add($item)
    }
    return ,$items
}

function Invoke-McpTool {
    param([string]$Name, $Arguments)

    $actions = @{
        unity_prefab_status = "prefab.status"
        capture_unity_screenshot = "editor.screenshot"
        refresh_unity_assets = "unity.refresh"
        get_unity_compile_status = "unity.compileStatus"
        get_prefab_mcp_settings = "prefab.settings"
        find_prefabs = "prefab.find"
        get_prefab_tree = "prefab.tree"
        get_component_fields = "prefab.fields"
        find_binding_candidates = "prefab.candidates"
        assign_object_reference = "prefab.assign"
        find_asset_candidates = "prefab.assetCandidates"
        assign_asset_reference = "prefab.assignAsset"
        create_ui_element = "prefab.createUi"
        edit_prefab = "prefab.edit"
        validate_prefab = "prefab.validate"
    }

    if (-not $actions.ContainsKey($Name)) {
        throw "Unknown tool: $Name"
    }

    try {
        $response = Invoke-UnityBridge -Action $actions[$Name] -Arguments $Arguments
    }
    catch {
        return [ordered]@{
            content = @(@{ type = "text"; text = "Unity bridge request failed. Keep this project open in Unity and wait for script compilation to finish. $($_.Exception.Message)" })
            isError = $true
        }
    }

    if (-not $response.ok) {
        $errorPayload = [ordered]@{ error = $response.error }
        if ($Name -eq "edit_prefab" -and $response.edit) {
            $errorPayload.edit = $response.edit
        }
        $text = (Remove-EmptyValues $errorPayload) | ConvertTo-Json -Depth 30 -Compress
        return [ordered]@{
            content = @(@{ type = "text"; text = $text })
            isError = $true
        }
    }

    if ($Name -eq "capture_unity_screenshot") {
        $meta = [ordered]@{
            message = $response.message
            width = $response.imageWidth
            height = $response.imageHeight
            mimeType = $response.imageMimeType
        }
        $metaText = (Remove-EmptyValues $meta) | ConvertTo-Json -Depth 10 -Compress
        return [ordered]@{
            content = @(
                @{ type = "image"; data = $response.imageBase64; mimeType = $response.imageMimeType },
                @{ type = "text"; text = $metaText }
            )
            isError = $false
        }
    }

    # JsonUtility emits every response field, including unrelated empty arrays.
    # Select only the payload for this tool to keep MCP responses token-efficient.
    $treeNodes = if ($Name -eq "get_prefab_tree" -and $Arguments.compact) {
        ConvertTo-CompactPrefabNodes $response.nodes
    } else {
        @($response.nodes)
    }

    $compact = switch ($Name) {
        "unity_prefab_status" { [ordered]@{ message = $response.message; unityVersion = $response.unityVersion; projectPath = $response.projectPath; compiling = $response.compiling } }
        "refresh_unity_assets" { [ordered]@{ message = $response.message } }
        "get_unity_compile_status" { [ordered]@{ message = $response.message; compileStatus = $response.compileStatus } }
        "get_prefab_mcp_settings" { [ordered]@{ message = $response.message; settings = $response.settings } }
        "find_prefabs" { [ordered]@{ message = $response.message; prefabs = @($response.prefabs) } }
        "get_prefab_tree" { [ordered]@{ message = $response.message; nodes = $treeNodes } }
        "get_component_fields" { [ordered]@{ message = $response.message; fields = @($response.fields) } }
        "find_binding_candidates" { [ordered]@{ message = $response.message; candidates = @($response.candidates) } }
        "assign_object_reference" { [ordered]@{ message = $response.message; assignment = $response.assignment } }
        "find_asset_candidates" { [ordered]@{ message = $response.message; assetCandidates = @($response.assetCandidates) } }
        "assign_asset_reference" { [ordered]@{ message = $response.message; assignment = $response.assignment } }
        "create_ui_element" { [ordered]@{ message = $response.message; creation = $response.creation } }
        "edit_prefab" { [ordered]@{ message = $response.message; edit = $response.edit } }
        "validate_prefab" { [ordered]@{ message = $response.message; issues = @($response.issues) } }
    }
    $text = (Remove-EmptyValues $compact) | ConvertTo-Json -Depth 30 -Compress

    return [ordered]@{
        content = @(@{ type = "text"; text = $text })
        isError = $false
    }
}

while ($null -ne ($line = [Console]::In.ReadLine())) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }

    try {
        $request = $line | ConvertFrom-Json
    }
    catch {
        Write-McpMessage (New-JsonRpcError -Id $null -Code -32700 -Message "Parse error")
        continue
    }

    $id = $request.id
    try {
        switch ($request.method) {
            "initialize" {
                $requestedVersion = $request.params.protocolVersion
                $version = if ($requestedVersion) { $requestedVersion } else { "2024-11-05" }
                $result = [ordered]@{
                    protocolVersion = $version
                    capabilities = [ordered]@{ tools = [ordered]@{ listChanged = $false } }
                    serverInfo = [ordered]@{ name = "unity-prefab-mcp"; version = "0.5.0" }
                }
                Write-McpMessage (New-JsonRpcResponse -Id $id -Result $result)
            }
            "notifications/initialized" { }
            "ping" {
                Write-McpMessage (New-JsonRpcResponse -Id $id -Result ([ordered]@{}))
            }
            "tools/list" {
                Write-McpMessage (New-JsonRpcResponse -Id $id -Result ([ordered]@{ tools = @(Get-ToolDefinitions) }))
            }
            "tools/call" {
                $result = Invoke-McpTool -Name $request.params.name -Arguments $request.params.arguments
                Write-McpMessage (New-JsonRpcResponse -Id $id -Result $result)
            }
            default {
                if ($null -ne $id) {
                    Write-McpMessage (New-JsonRpcError -Id $id -Code -32601 -Message "Method not found: $($request.method)")
                }
            }
        }
    }
    catch {
        if ($null -ne $id) {
            Write-McpMessage (New-JsonRpcError -Id $id -Code -32603 -Message $_.Exception.Message)
        }
    }
}
