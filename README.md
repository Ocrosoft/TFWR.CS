# 简介

本项目是一个面向《编程农场》（The Farmer Was Replaced）游戏的 C# 到游戏使用的类 Python 代码自动转换工具。

## 功能特性

- 支持以 C# 编写《编程农场》游戏脚本
- 运行时自动将 C# 代码转译为游戏支持的类 Python 代码

## 依赖说明

在编写游戏脚本时，请务必引用 `TFWR.CS.REF` 包。该包内包含了游戏内置方法的声明，便于获得代码补全与类型提示体验。

## 快速开始

待完善。

## 注意事项

- 本项目仍处于开发阶段，仅供学习与娱乐。
- 由于《编程农场》脚本语法有限，部分 C# 语法和特性无法被完整支持。
- 暂不支持调试 C# 代码。若需单步运行或调试，请在游戏内对生成代码进行调试。

## 免责声明

本项目为个人开发，与《编程农场》官方无任何关联。请勿将本项目用于商业用途。

## 语法与特性支持说明（正在补充中）

### 不支持或有限支持的用法

- **单文件多类/成员重复**：建议每个文件只包含一个类，避免成员冲突。
- **继承**：类继承关系会被忽略。
- **结构体**：结构体将被视为普通类处理。
- **构造方法**：构造方法会被当作普通方法调用，不会自动生成默认构造函数。
- **Lambda 表达式**：Lambda 会被注释包裹，无法运行，转译时会有警告。
- **LINQ**：不支持 LINQ 相关方法（如 `Where`, `Select`, `OrderBy` 等），使用时会有警告。
- **三元表达式与 null 合并**：不支持 `condition ? a : b` 和 `??`，请用 if-else 替代。

**❌ 不支持：**
```csharp
var result = condition ? trueValue : falseValue;
var value = nullableValue ?? defaultValue;
```

**✅ 推荐写法：**
```csharp
// 三元表达式改用 if-else
var result;
if (condition)
{
    result = trueValue;
}
else
{
    result = falseValue;
}

// null coalescing 改用 if-else
var value;
if (nullableValue != null)
{
    value = nullableValue;
}
else
{
    value = defaultValue;
}
```

### List 支持方法

**✅ 支持：**
```csharp
Add(item)           // → list.append(item)
Remove(item)        // → list.remove(item)
Contains(item)      // → item in list
Insert(index, item) // → list.insert(index, item)
Clear()             // → list = []
First()             // → list[0]
Last()              // → list[-1]
Any()               // → len(list) > 0
ElementAt(index)    // → list[index]
IndexOf(item)       // → list.index(item)
RemoveAt(index)     // → del list[index]
```

**⚠️ 不支持（会警告）：**
- `FirstOrDefault()`, `LastOrDefault()`, `ElementAtOrDefault()` - 依赖三元表达式，请用 if-else
- `Sort()`, `Reverse()` - 排序和反转
- `Where()`, `Select()`, `OrderBy()` 等 LINQ
- `Find()`, `FindAll()`, `Exists()` 等谓词
- `AddRange()`, `RemoveRange()` 等批量操作
- `ForEach()` - 请用 for 循环

### Dictionary 支持方法

**✅ 支持：**
```csharp
ContainsKey(key)      // → key in dict
ContainsValue(value)  // → value in dict.values()
Add(key, value)       // → dict[key] = value
Remove(key)           // → del dict[key]
Clear()               // → dict = {}
Keys()                // → list(dict.keys())
Values()              // → list(dict.values())
```

**⚠️ 不支持（会警告）：**
- `TryGetValue(key, out value)` - 用 `dict.get(key, None)` 替代
- `GetValueOrDefault(key)` - 用 `dict.get(key, None)` 替代
- `TryAdd()`, `EnsureCapacity()` 等其他方法

### 对象创建与注意事项

**✅ 推荐写法：**
```csharp
List<string> list = new();                // → list = []
Dictionary<int, string> dict = new();     // → dict = {}
List<KeyValuePair<int, int>> pairs = [new(1, 2)];  // → pairs = [(1, 2)]
```

**注意：**
- `KeyValuePair<K, V>` 会被转为元组 `(key, value)`
- 避免使用 List 的带参构造函数
- 不要忽略列表读取方法的返回值，否则游戏会报错

**❌ 避免写法：**
```csharp
list.First();  // 忽略返回值，游戏不允许
list[0];       // 忽略返回值，游戏不允许
```