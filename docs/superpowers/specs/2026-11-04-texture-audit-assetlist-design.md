# 纹理专项检测并入 AssetList 设计

- 日期：2026-11-04
- 状态：已确认
- 范围：AssetStudio.GUI 的 Asset List 与现有“纹理专项检测”页
- 不在范围：改变纹理检测算法、增加新的纹理审计规则、调整导出格式、重构整个 MainForm

## 1. 背景与问题

当前程序为纹理审计维护了独立的“纹理专项检测”页。该页面提供五种检测模式，但这些能力本质上都是对 `Texture2D` 的固定过滤条件，与 Asset List 已有的类型、纹理格式、资源大小和搜索筛选属于同一类能力。

独立页面造成以下问题：

1. 同一批纹理维护两套列表、结果集合、排序逻辑和统计逻辑。
2. 五种检测方法重复遍历资源、创建 `AssetItem` 副本并填充相同列。
3. 副本没有完整继承原始 `AssetItem` 的场景节点、路径等上下文。
4. 专项页复用了硬编码到 Asset List 的右键事件，导致专项页右键菜单与选中项导出不能可靠工作。
5. Texture 使用独立专项页，而正在建设的 Mesh 筛选位于 Asset List 内，交互模式不统一。
6. “图片规范压缩失效”等名称大于实际判断能力，容易误导用户。

本次设计将纹理专项能力收敛到 Asset List，在不改变现有检测结果的前提下删除重复入口与重复状态。

## 2. 设计目标

1. 删除独立的“纹理专项检测”页。
2. 当 Asset List 的资源类型选为 `Texture2D` 时，显示纹理专属筛选控件。
3. 纹理检测规则采用单选，保持界面简洁。
4. 纹理规则可以与格式、通用 Size、名称搜索等现有条件组合。
5. 所有结果继续引用原始 `AssetItem`，复用 Asset List 的预览、排序、右键和导出链路。
6. Texture 与 Mesh 采用一致的“按资源类型显示专属筛选区”模式。
7. 检测原理在界面中可见，但不引入复杂规则框架。
8. 尽量减少对当前未提交 Mesh、APK 等工作的干扰。

## 3. 非目标

1. 不把纹理规则升级为可多选组合。
2. 不建立通用规则注册、插件或动态表单系统。
3. 不计算新的 GPU 显存、解码内存或精确包体指标。
4. 不扩展“疑似 NPOT 压缩退化”的判断范围。
5. 不改变“非块压缩格式”沿用的现有格式集合。
6. 不改造全部 ListView 的排序基础设施；仅删除纹理专项页带来的重复排序状态。
7. 不在本次工作中全面消除 `MainForm` 的控件索引访问，仅确保新控件使用明确字段引用。

## 4. 方案选择

### 4.1 采用方案：AssetList 类型上下文筛选栏

Asset List 保留通用筛选控件，并增加一个类型专属筛选区：

- 选择 `Texture2D`：显示纹理格式、纹理检测规则、规则说明及必要参数。
- 选择 `Mesh`：显示现有 Mesh 专属条件。
- 选择其他类型：隐藏类型专属控件。

该方案只保留一个结果列表和一条选择/导出链路，与现有代码方向一致，且不需要动态规则框架。

### 4.2 未采用方案

#### 固定纹理面板

纹理控件始终占据 Asset List 顶部空间，只在非纹理类型下禁用。实现较直接，但界面浪费空间，也无法统一 Texture 与 Mesh 的交互。

#### 通用规则注册系统

通过规则描述注册 Texture、Mesh 和未来资产类型。扩展性更高，但当前规则数量少，引入注册模型、参数描述和动态 UI 属于过度设计。

## 5. 用户界面设计

### 5.1 Asset List 筛选区

筛选能力在逻辑上分为通用条件和类型专属条件。结合当前 WinForms 布局，保留已有搜索框，并将顶部筛选区组织为三行紧凑控件：

```text
搜索 [____________________________________________]
类型 [Texture2D ▼]                         Total Size: ...
格式 [All ▼]  Size > [__] MB
检测 [全部纹理 ▼]  尺寸档位 [≥2048 ▼]  宽或高任一边达到所选尺寸
```

其中搜索框是 Asset List 已有控件，不计入新增筛选行；本次只把 `filterPanel` 从两行扩为三行。窗口较窄时优先保证下拉框可操作，说明文字占用剩余空间并允许截断显示完整 Tooltip。

设计要求：

- “检测”下拉框始终在 `Texture2D` 类型下显示。
- “尺寸档位”仅在检测规则为“大尺寸”时显示。
- 规则说明随规则变化，使用一行简短文字。
- 隐藏尺寸档位时，说明区域应自动承接其空间，避免留下明显空洞。
- 非 `Texture2D` 类型下隐藏全部纹理专属控件。
- 现有通用 `Size > N MB` 保留，因为它筛选资源存储大小；“大尺寸”筛选纹理宽高，两者含义不同。
- 不在说明区域显示优化结论、警告等级或操作按钮。

### 5.2 纹理规则列表

规则顺序固定为：

1. 全部纹理
2. 非块压缩格式
3. 大尺寸
4. Read/Write 已开启
5. Mipmaps 已开启
6. 疑似 NPOT 压缩退化

规则采用单选。切换规则立即刷新 Asset List。

### 5.3 大尺寸档位

可选档位：

- ≥1024
- ≥2048
- ≥4096

默认值为 ≥2048。档位只在“大尺寸”规则下影响结果。

## 6. 规则语义

### 6.1 组合方式

所有筛选条件按 AND 组合：

```text
资源类型
AND 纹理格式
AND 纹理检测规则
AND 通用 Size 条件
AND 名称、Container 或 PathID 搜索
```

纹理检测规则内部为单选，不允许多项叠加。

### 6.2 规则定义与说明

| 规则 | 判断条件 | 动态说明 |
|---|---|---|
| 全部纹理 | 不增加纹理属性条件 | 显示全部符合其他筛选条件的纹理 |
| 非块压缩格式 | `m_TextureFormat` 属于现有原始像素格式集合 | 使用原始像素格式，可能增加包体或内存占用 |
| 大尺寸 | `m_Width >= 阈值 || m_Height >= 阈值` | 宽或高任一边达到所选尺寸 |
| Read/Write 已开启 | `m_IsReadable == true` | Unity Read/Write Enabled 已开启，可能保留 CPU 端副本 |
| Mipmaps 已开启 | `m_MipMap || m_MipCount > 1` | 已启用或包含多个 Mipmap 层级 |
| 疑似 NPOT 压缩退化 | `RGBA32 && 有 Mipmaps && 宽或高至少一边不是 2 的幂` | RGBA32、包含 Mipmaps，且宽或高不是 2 的幂 |

### 6.3 非块压缩格式集合

本次沿用现有“未压缩纹理”检测集合，不改变命中结果：

- RGBA32
- ARGB32
- RGB24
- RGBA4444
- ARGB4444
- RGB565
- R16
- RGBAHalf
- RGBAFloat
- BGRA32
- RG16
- R8

界面名称改为“非块压缩格式”，用于更准确地表达这些格式没有采用 DXT、ETC、ASTC 等块压缩；不把低位深像素格式错误表述为完全未经优化。

### 6.4 NPOT 判断

正整数 `value` 为 2 的幂时满足：

```csharp
(value & (value - 1)) == 0
```

判断前应要求 `value > 0`。现有 Unity 纹理宽高通常为正数，但辅助方法应具备完整输入语义。

## 7. 状态行为

1. 选择“大尺寸”后显示档位，默认 ≥2048。
2. 用户更改档位后切换到其他纹理规则，档位控件隐藏。
3. 再次选择“大尺寸”时，保留用户上次选择的档位。
4. 切换离开 `Texture2D` 时隐藏纹理控件，但保留当前纹理筛选状态。
5. 在同一批已加载资源中重新选择 `Texture2D` 时，恢复此前的纹理规则、格式和尺寸档位。
6. 加载新资源或执行 Reset 时：
   - 纹理规则恢复“全部纹理”；
   - 大尺寸档位恢复 ≥2048；
   - 纹理格式恢复 All；
   - 规则说明恢复“全部纹理”的说明。
7. 切换规则、格式或尺寸档位时，只刷新 `visibleAssets` 和 `assetListView.VirtualListSize`，不创建第二份 `AssetItem`。

## 8. 代码结构设计

### 8.1 UI 字段

在 `MainForm` 中为新增控件保留明确字段引用，避免通过 `Controls[index]` 获取：

- `textureRuleComboBox`
- `textureSizePresetComboBox`
- `textureRuleDescriptionLabel`

现有 `textureFormatComboBox` 继续复用。若当前 Designer 中纹理格式控件仍通过索引获取，应在本次修改覆盖到的区域内改为明确字段，避免新增更多隐式索引契约。

### 8.2 规则模型

使用局部、轻量的枚举表达规则，不建立通用规则注册框架：

```csharp
private enum TextureFilterRule
{
    All,
    NonBlockCompressed,
    Large,
    ReadWriteEnabled,
    MipmapsEnabled,
    SuspectedNpotCompressionFallback
}
```

大尺寸档位可以使用整数值 `1024`、`2048`、`4096`，无需额外类。

### 8.3 规则匹配入口

提取单一职责方法：

```csharp
private bool MatchesTextureRule(Texture2D texture)
```

职责：

- 读取当前单选规则和尺寸档位；
- 根据规则判断一个 `Texture2D` 是否命中；
- 不修改 UI；
- 不遍历资产；
- 不创建 `AssetItem`。

可辅以两个纯辅助方法：

```csharp
private static bool IsNonBlockCompressedFormat(TextureFormat format)
private static bool IsPowerOfTwo(int value)
```

规则说明更新由独立方法负责：

```csharp
private void UpdateTextureFilterControls()
```

它只处理尺寸档位可见性和说明文字，然后触发或由调用方触发一次筛选，避免 UI 状态与业务判断混在一起。

### 8.4 接入现有筛选

在 `FilterAssetList()` 对 `Texture2D` 的处理分支中组合：

1. 现有类型筛选；
2. 现有纹理格式筛选；
3. `MatchesTextureRule(texture)`；
4. 现有搜索条件；
5. 现有通用 Size 条件。

最终结果仍为原始 `exportableAssets` 中的 `AssetItem` 引用，写入 `visibleAssets`。

### 8.5 事件绑定

为三个纹理控件使用命名事件处理方法，避免使用无法正确解绑的临时 lambda：

- `textureFormatComboBox_SelectedIndexChanged`
- `textureRuleComboBox_SelectedIndexChanged`
- `textureSizePresetComboBox_SelectedIndexChanged`

事件只绑定一次。重新加载资源时更新 Items 和 SelectedIndex，不重复注册处理器。必要时使用已有的更新标志抑制批量重置过程中的中间刷新，避免一次 Reset 触发多次完整筛选。

### 8.6 删除的旧结构

删除以下纹理专项页专属内容：

- `textureDetectionComboBox`
- `uncompressedTexturesListView`
- `uncompressedTotalLabel`
- `uncompressedTextures`
- 专项页 Designer 布局和 TabPage
- 专项页虚拟列表取项分支
- 专项页列排序处理
- `PerformTextureDetection()`
- 五个重复检测/扫描方法
- 加载完成后自动调用 `PerformTextureDetection()` 的代码
- Reset 中专项结果集合和 VirtualListSize 的清理代码
- 只为专项列表存在的选择、右键或跳转分支

删除前应按引用逐项确认，避免误删 Asset List 或 AB 专项共用代码。

## 9. 数据流

```text
用户选择 Texture2D
  → 显示纹理专属筛选控件
  → 用户选择格式、规则或尺寸档位
  → 更新规则说明与参数可见性
  → FilterAssetList()
  → 对 exportableAssets 中的 Texture2D 执行组合条件
  → visibleAssets 保存原始 AssetItem 引用
  → 更新 assetListView.VirtualListSize
  → 现有预览、右键、导出和场景跳转继续工作
```

该流程只有一个结果集合和一个 ListView，不再生成纹理结果副本。

## 10. 错误与边界处理

1. 纹理对象读取失败时，沿用现有 Asset List 筛选的容错策略，不因单个资源终止全部筛选。
2. 未选择有效规则时按“全部纹理”处理，避免空结果或异常。
3. 未选择有效尺寸档位时回退到 2048。
4. 宽高小于等于 0 时不视为 2 的幂。
5. 对非 `Texture2D` 资产不执行纹理字段读取。
6. 更新下拉框选项期间避免重复筛选和事件累计。
7. 规则说明必须与实际谓词保持一致，不使用“必然错误”“一定浪费”等断言性文案。

## 11. 测试与验收标准

### 11.1 UI 验收

- “纹理专项检测”页不再出现。
- 选择 `Texture2D` 时出现格式、检测规则和说明。
- 选择其他类型时纹理专属控件隐藏。
- 只有“大尺寸”规则显示尺寸档位。
- 切换规则时说明文字正确更新。
- Texture 与 Mesh 专属筛选不会同时错误显示。

### 11.2 规则验收

- “非块压缩格式”的结果集合与旧“未压缩纹理”检测一致。
- 大尺寸默认结果与旧规则 `宽 >= 2048 || 高 >= 2048` 一致。
- ≥1024、≥2048、≥4096 三档边界使用包含关系。
- Read/Write 只匹配 `m_IsReadable == true`。
- Mipmaps 匹配 `m_MipMap == true` 或 `m_MipCount > 1`。
- NPOT 规则只匹配 `RGBA32 + Mipmaps + 至少一边非 2 的幂`。
- 1024、2048、4096 被识别为 2 的幂；1000、2047、3000 不被识别为 2 的幂。

### 11.3 组合筛选验收

- 纹理规则与具体 TextureFormat 同时生效。
- 纹理规则与通用 Size 条件同时生效。
- 纹理规则与名称、Container、PathID 搜索同时生效。
- “全部纹理”不增加额外限制。
- `Export Filtered` 导出当前 Asset List 的筛选结果。
- `Export Selected` 导出 Asset List 当前选中项。

### 11.4 状态验收

- 同一批资源内离开再返回 `Texture2D` 时保留规则和尺寸档位。
- 切出“大尺寸”再切回时保留档位。
- Reset 或加载新资源后恢复“全部纹理”和 ≥2048。
- 重新加载资源不会造成一次选择触发多次筛选。

### 11.5 回归验收

- 纹理预览和 RGBA 通道切换正常。
- 资产信息显示正常。
- Asset List 排序正常。
- 右键菜单、显示原始文件、场景跳转和导出正常。
- Mesh 专属筛选保持现有行为。
- AB 专项检测和重复资源列表不受影响。

## 12. 实施边界与改动安全

当前工作树已有 `MainForm.Designer.cs`、`MainForm.cs`、Mesh、Bundle、FileReader、APK 相关未提交修改。实施者必须：

1. 在现有工作树基础上做局部修改，不覆盖或回退无关改动。
2. 修改前检查 `git diff`，区分本功能与已有 Mesh/APK 工作。
3. 优先使用精确编辑，不整文件替换 Designer 或 MainForm。
4. 不因删除纹理专项页而改变 AB 专项页的页签、事件和结果模型。
5. 完成后按文件检查 diff，确认只包含本设计范围内的改动。

## 13. 成功定义

用户可以只在 Asset List 中完成纹理类型选择、格式过滤、专项规则筛选、原理理解、预览、排序和导出；独立纹理专项页及其重复数据流被完全删除；既有五项检测的命中语义除名称和可配置的大尺寸档位外保持一致。
