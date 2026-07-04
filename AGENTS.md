# AGENTS.md — AspectsColorless 卡牌添加指南

本文档为其他 AI Agent 提供向本项目添加新卡牌的标准化流程与规范。Agent 在接到"添加卡牌"类任务时应严格遵循以下步骤。

---

## 项目概述

本项目是 **Slay the Spire 2** 的无色卡牌 mod（Mod ID: `AspectsColorless`），基于：
- **Godot 4.5.1** + **.NET 9.0**（C#）
- **BaseLib** 模组框架（版本 ≥ 3.3.2）
- **Harmony** 补丁库

所有卡牌 C# 代码位于 `AspectsColorlessCode/`，游戏资源（本地化、图片）位于 `AspectsColorless/`。

游戏反编译代码位于 `.sts2_decompiled/`，需要查阅游戏原始 API 或机制时可参考该目录。

---

## ⚠️ 核心原则

### 原则一：先读代码，遵循已有范式

**在写任何一行代码之前，Agent 必须先通读项目中与任务相关的所有现有文件，理解已有代码的范式和约定。** 具体包括：

1. **必读文件清单**（每次添加卡牌都必须过一遍）：
   - `AspectsColorlessCode/Cards/` 下所有现有卡牌（最少读 2-3 个不同类型的）
   - `AspectsColorlessCode/Abstract/AspectsCardModel.cs` — 理解基类能力
   - `AspectsColorlessCode/Commands/AspectsCmd.cs` — 了解自定义命令
   - `AspectsColorlessCode/Enumerations/AspectsKeywords.cs` — 已有自定义关键词
   - `AspectsColorlessCode/Enumerations/AspectsTips.cs` — 已有自定义悬浮提示
   - `AspectsColorless/localization/eng/cards.json` — 本地化格式和 Key 规范

2. **最大化复用已有模式**：
   - 新卡牌的结构、命名、代码组织方式应尽可能接近已有卡牌（如 `GoldenLance` 做伤害牌参考、`LunariCharm` 做 Power 牌参考、`Drunk` 做状态牌参考）
   - 本地化的措辞风格、Key 格式、动态变量使用方式应沿用已有方式
   - using 语句、命名空间声明严格与现有文件对齐

3. **新范式必须特别确认**：
   - 如果需要引入**任何与现有代码不同的新范式**（例如：使用了一个现有卡牌都没用过的 API / 回调 / 设计模式 / 文件结构 / 依赖），**必须先逐项向用户说明**：
     - 现有代码是怎么做的（引用具体文件和行号）
     - 你打算怎么做、为什么需要不同
     - 获得用户明确同意后才能执行
   - 此规则覆盖但不限于：新的基类、新的 Harmony Patch、新的枚举类型、新的文件组织方式、新的 NuGet 包引用

### 原则二：不确定就问

**Agent 在任何细节不明确时必须主动询问用户，禁止猜测或自行脑补。** 这包括但不限于：

- 用户描述的卡牌效果存在多种可能的实现方式（例如"造成伤害"未说明是单体还是 AOE）
- 数值不确定（费用、伤害、格挡、层数等）
- 稀有度未指定
- 是否需要升级效果不明确
- 卡牌池归属不清晰（是无色可获取牌还是衍生状态牌）
- 效果中引用的其他卡牌或 Power 尚不存在
- 是否需要创建新的自定义关键词 / 悬浮提示
- 与现有卡牌机制存在潜在冲突

**询问方式**：将模糊点逐条列出，每个问题给出 2-3 个推荐选项，等用户确认后再动手写代码。宁可多问，不可瞎猜。

---

## 添加卡牌的标准流程

### 第 1 步：分析需求，确定卡牌参数

在编写代码之前，先明确以下属性。**如果用户未提供全部信息，逐项询问补齐：**

| 参数 | 说明 | 可选值示例 |
|------|------|-----------|
| **Card ID** | 唯一标识符（PascalCase） | `GoldenLance`, `HappyHour` |
| **Cost** | 能量消耗（整数） | `0`, `1`, `2`, `3`, `-1`（不可打出的状态牌） |
| **CardType** | 卡牌类型 | `Attack`, `Skill`, `Power`, `Status` |
| **CardRarity** | 稀有度 | `Basic`, `Common`, `Uncommon`, `Rare`, `Status` |
| **TargetType** | 目标类型 | `Self`, `AnyEnemy`, `AllEnemies`, `None` |
| **CardPool** | 卡牌池 | `ColorlessCardPool`, `StatusCardPool`（见下方说明） |

**卡牌池规则**：
- 普通可获取的无色牌 → `[Pool(typeof(ColorlessCardPool))]`
- 只在战斗中生成的衍生状态牌 → `[Pool(typeof(StatusCardPool))]`
- 如果不需要出现在正常奖励池中，可不加 `[Pool]` 或使用其他池

**类名规范**：类名应与 Card ID 一致（PascalCase），例如 `GoldenLance`、`HappyHour`。

**稀有度中英对照**（翻译时必须严格使用以下对应，禁止随意翻译）：

| 英文 | 中文 |
|------|------|
| `Basic` | 基础 |
| `Common` | 普通 |
| `Uncommon` | 罕见 |
| `Rare` | 稀有 |
| `Status` | 状态 |

---

### 第 2 步：创建卡牌 C# 文件

在 `AspectsColorlessCode/Cards/` 下创建 `{CardId}.cs`。

**基类**：所有卡牌继承 `AspectsCardModel`（位于 `AspectsColorless.AspectsColorlessCode.Abstract`）。

#### 2.1 最小模板

```csharp
using AspectsColorless.AspectsColorlessCode.Abstract;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace AspectsColorless.AspectsColorlessCode.Cards;

[Pool(typeof(ColorlessCardPool))]
public class MyCard() : AspectsCardModel(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    // 构造函数中依次传入：cost, cardType, rarity, targetType
}
```

#### 2.2 常用可重写成员

```csharp
// 关键词（如虚无、消耗、保留等）
public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal, CardKeyword.Exhaust];

// 动态变量（伤害、格挡、力量等），用于本地化字符串中的 {} 占位
protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6, ValueProp.Move)];

// 额外悬浮提示（引用其他卡牌、自定义提示等）
protected override IEnumerable<IHoverTip> ExtraHoverTips => [
    HoverTipFactory.FromCard<SomeOtherCard>(upgraded: false),
    AspectsHelpers.StaticHoverTip(AspectsTips.Transmute)
];
```

#### 2.3 生命周期方法

**OnPlay** — 卡牌被打出时：
```csharp
protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
{
    ArgumentNullException.ThrowIfNull(cardPlay.Target);
    await DamageCmd.Attack(this.DynamicVars.Damage.BaseValue)
        .FromCard(this)
        .Targeting(cardPlay.Target!)
        .Execute(choiceContext);
}
```

**OnUpgrade** — 卡牌升级时：
```csharp
protected override void OnUpgrade()
{
    this.DynamicVars.Damage.UpgradeValueBy(4);  // 升级+伤害
    // 或者添加关键词: this.AddKeyword(CardKeyword.Innate);
}
```

**AfterCardDrawn** — 卡牌被抽到时：
```csharp
public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
{
    if (card == this)
    {
        await PowerCmd.Apply<StrengthPower>(choiceContext, this.Owner.Creature, 1, this.Owner.Creature, null);
    }
}
```

#### 2.4 使用现有自定义命令

本项目自定义了以下命令（位于 `AspectsColorlessCode/Commands/AspectsCmd.cs`）：

- `AspectsCmd.Transmute(card, replacement)` — 将一张卡牌*转化*为指定卡牌（同时处理牌组版本）
- `AspectsCmd.TransmuteToRandom(card, performer)` — 将卡牌随机*转化*

#### 2.5 使用自定义关键词

本项目自定义了 `AspectsKeywords.Cycle`（轮转关键词），如需使用：
```csharp
using AspectsColorless.AspectsColorlessCode.Enumerations;
public override IEnumerable<CardKeyword> CanonicalKeywords => [AspectsKeywords.Cycle];
```

#### 2.6 使用自定义悬浮提示

```csharp
// 引用现有的 AspectsTips 枚举值
protected override IEnumerable<IHoverTip> ExtraHoverTips => [
    AspectsHelpers.StaticHoverTip(AspectsTips.Transmute)
];
```

如需 **新增自定义悬浮提示**，参见下方「添加新枚举值 / 关键词」章节。

---

### 第 3 步：添加本地化

卡牌的本地化 Key 格式为：`ASPECTSCOLORLESS-{CARD_ID_UPPER_SNAKE}`，即 Mod ID 的大写下划线形式 + 卡牌 ID 的大写下划线形式。

例如 `HappyHour` → `ASPECTSCOLORLESS-HAPPY_HOUR`

#### 3.1 英文本地化

编辑 `AspectsColorless/localization/eng/cards.json`，添加：

```json
"ASPECTSCOLORLESS-MY_CARD.title": "My Card",
"ASPECTSCOLORLESS-MY_CARD.description": "Deal {Damage:diff()} damage."
```

**本地化动态变量语法**（参考现有卡牌）：
| 语法 | 用途 |
|------|------|
| `{Damage:diff()}` | 显示伤害值，升级时会标记颜色差异 |
| `{Block:diff()}` | 显示格挡值 |
| `{StrengthPower:diff()}` | 显示力量值 |
| `{IfUpgraded:show:A\|B}` | 升级前显示 B，升级后显示 A |
| `{Amount:plural:even\|odd}` | Power 中根据数值显示不同文本 |
| `[gold]text[/gold]` | 金色高亮文本 |
| `[blue]text[/blue]` | 蓝色高亮文本 |

#### 3.2 中文本地化

编辑 `AspectsColorless/localization/zhs/cards.json`，添加对应的中文翻译。保持 Key 完全一致，只翻译 value。

**中文翻译风格**（参考现有卡牌）：
- 使用 `[gold]关键词[/gold]` 包裹游戏术语（与英文版一致）
- 保持 `{Damage:diff()}` 等动态变量不变
- 语气贴近原文风格

---

### 第 4 步（条件）：创建自定义 Power

如果卡牌需要新的 Power（如 `LunariCharm` 需要 `LunariCharmPower`），执行以下子步骤：

#### 4.1 创建 Power C# 类

在 `AspectsColorlessCode/Powers/` 下创建 `{PowerName}.cs`，继承 `AspectsPowerModel`：

```csharp
using AspectsColorless.AspectsColorlessCode.Abstract;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace AspectsColorless.AspectsColorlessCode.Powers;

public class MyPower : AspectsPowerModel
{
    public override PowerType Type => PowerType.Buff;           // Buff / Debuff
    public override PowerStackType StackType => PowerStackType.Counter;  // Counter / Intensity / Duration
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced; // Instanced / Singleton

    // 重写需要的回调，如 AfterAttack, AtStartOfTurn, OnPowerApplied 等
}
```

常用的 Power 回调：
- `AfterAttack` — 攻击后触发
- `AtStartOfTurn` — 回合开始时触发
- `OnPowerApplied` — 有 Power 被应用时触发
- `ModifyDamage` — 修改伤害计算

#### 4.2 Power 本地化

编辑以下两个文件，Key 格式为 `ASPECTSCOLORLESS-{POWER_NAME_UPPER_SNAKE}`：

- `AspectsColorless/localization/eng/powers.json`
- `AspectsColorless/localization/zhs/powers.json`

```json
"ASPECTSCOLORLESS-MY_POWER.title": "My Power",
"ASPECTSCOLORLESS-MY_POWER.description": "Description text.",
"ASPECTSCOLORLESS-MY_POWER.smartDescription": "Compact text: [blue]{Amount}[/blue]."
```

> **注意**：`smartDescription` 是 Power 在 UI 中的精简描述（可选但推荐）。`{Amount}` 会自动渲染为 Power 的层数/数值。

#### 4.3 Power 图标

Power 图标需要 PNG 文件放置在 `AspectsColorless/images/powers/`，文件名格式为 `{powerid}.png`（小写，不含前缀）。

Agent **不需要**创建或生成图片文件，但应在完成时提醒用户：
> ⚠️ 请手动将 Power 图标放置到 `AspectsColorless/images/powers/{powerid}.png`

---

### 第 5 步（条件）：添加卡图文件

卡牌立绘需要 PNG 文件放置在 `AspectsColorless/images/card_portraits/`，文件名格式为 `{cardid}.png`（小写，不含前缀，如 `goldenlance.png`）。

`AspectsCardModel` 基类会自动查找对应图片，找不到时回退到默认 `card.png`。

Agent **不需要**创建图片，但应在完成时提醒用户：
> ⚠️ 请手动将卡牌立绘放置到 `AspectsColorless/images/card_portraits/{cardid}.png`

---

### 第 6 步：编译验证

**代码写完后必须执行 build 确保能通过编译。** 编译失败时必须根据错误信息修复，不得留下编译错误。

```bash
dotnet build
```

如果编译报错：
1. 仔细阅读错误信息，定位到具体文件和行号
2. 修正后重新 `dotnet build`，直到 0 error 为止
3. Warning 也应在合理范围内处理干净

### 第 7 步：检查清单（Agent 自查）

在提交代码前，逐项确认：

- [ ] **`dotnet build` 通过，0 error**
- [ ] **已通读项目中至少 2-3 个不同类型的现有卡牌**，新代码的范式与它们一致
- [ ] **没有引入与现有代码不同的新范式**（如有，已向用户特别确认并获批）
- [ ] 所有不明确的细节**已先向用户确认**，没有自行猜测的参数或行为
- [ ] C# 卡牌文件已创建在 `AspectsColorlessCode/Cards/`
- [ ] 正确设置了 `[Pool]` 属性（一般无色牌用 `ColorlessCardPool`，状态牌用 `StatusCardPool`）
- [ ] `CanonicalKeywords`、`CanonicalVars`、`ExtraHoverTips` 已正确配置
- [ ] `OnPlay` / `OnUpgrade` 逻辑完整
- [ ] 英文本地化已添加（`eng/cards.json`）
- [ ] 中文本地化已添加（`zhs/cards.json`）
- [ ] 如果创建了 Power：Power C# 文件、Power 本地化（eng + zhs）、提醒用户放置 Power 图标
- [ ] 提醒用户放置卡牌立绘图片
- [ ] 本地化 Key 格式正确：`ASPECTSCOLORLESS-{UPPER_SNAKE_CASE}`
- [ ] 没有引入不必要的依赖（using 语句整洁）

---

## 添加新枚举值 / 自定义关键词（进阶）

### 新增自定义悬浮提示

1. 在 `AspectsColorlessCode/Enumerations/AspectsTips.cs` 的 `AspectsTips` 枚举中添加新值
2. 在 `AspectsColorless/localization/eng/static_hover_tips.json` 中添加：
   ```json
   "NEWTIP.title": "Tip Title",
   "NEWTIP.description": "Tip description."
   ```
3. 在 `AspectsColorless/localization/zhs/static_hover_tips.json` 中添加对应中文

Key 由 `StringHelper.Slugify(tip.ToString())` 自动生成（即枚举名的大写形式）。

### 新增自定义关键词

1. 在 `AspectsColorlessCode/Enumerations/AspectsKeywords.cs` 中添加：
   ```csharp
   [CustomEnum, KeywordProperties(AutoKeywordPosition.Before)]  // 或 After
   public static CardKeyword YourKeyword;
   ```
2. 在 `eng/card_keywords.json` 和 `zhs/card_keywords.json` 中添加：
   ```json
   "ASPECTSCOLORLESS-YOUR_KEYWORD.title": "Your Keyword",
   "ASPECTSCOLORLESS-YOUR_KEYWORD.description": "What it does."
   ```
3. 如果关键词需要自定义行为（类似 Cycle），在 `AspectsColorlessCode/Patches/` 下添加对应的 Harmony Patch

---

## 代码风格约定

- 使用 **C# 12 主构造函数**语法（如现有代码：`public class Drunk() : AspectsCardModel(...)`）
- `using` 语句保持整洁，不引入未使用的命名空间
- 命名空间：`AspectsColorless.AspectsColorlessCode.Cards`（卡牌）、`AspectsColorless.AspectsColorlessCode.Powers`（Power）
- 异步方法遵循 `async Task` / `async ValueTask` 模式
- 日志使用 `MainFile.Logger`（如需要）
