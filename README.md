# [中文版](README.zh.md)

# Introduction

This project is an automatic transpiler that converts C# code to Python-like code for use in the game "The Farmer Was Replaced" (TFWR).

## Features

- Write TFWR game scripts in C#
- Automatically transpile C# code to Python-like code supported by the game at runtime

## Dependencies

When writing game scripts, make sure to reference the `TFWR.CS.REF` package. This package contains declarations for in-game built-in methods, providing code completion and type hints.

## Quick Start

To be completed.

## Notes

- This project is under development and for learning/entertainment only.
- Some C# syntax and features are not fully supported due to TFWR script limitations.
- Debugging C# code is not supported. For step-by-step debugging, debug the generated code inside the game.

## Disclaimer

This project is developed independently and is not affiliated with the official TFWR game. Do not use this project for commercial purposes.

## Syntax and Feature Support (WIP)

### Unsupported or Limited Features

- **Multiple classes/members per file:** Only one class per file is recommended to avoid conflicts.
- **Inheritance:** Class inheritance is ignored.
- **Structs:** Structs are treated as regular classes.
- **Constructors:** Constructors are treated as normal methods and default constructors are not auto-generated.
- **Lambda expressions:** Lambdas are commented out and not executable; warnings will be issued during transpilation.
- **LINQ:** LINQ methods (e.g., `Where`, `Select`, `OrderBy`) are not supported and will trigger warnings.
- **Ternary and null-coalescing:** `condition ? a : b` and `??` are not supported; use if-else instead.

**❌ Not supported:**
```csharp
var result = condition ? trueValue : falseValue;
var value = nullableValue ?? defaultValue;
```

**✅ Recommended:**
```csharp
// Replace ternary with if-else
var result;
if (condition)
{
    result = trueValue;
}
else
{
    result = falseValue;
}

// Replace null-coalescing with if-else
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

### Supported List Methods

**✅ Supported:**
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

**⚠️ Not supported (will warn):**
- `FirstOrDefault()`, `LastOrDefault()`, `ElementAtOrDefault()` - Use if-else instead
- `Sort()`, `Reverse()` - Sorting and reversing
- `Where()`, `Select()`, `OrderBy()` and other LINQ
- `Find()`, `FindAll()`, `Exists()` and other predicates
- `AddRange()`, `RemoveRange()` and other batch operations
- `ForEach()` - Use a for loop

### Supported Dictionary Methods

**✅ Supported:**
```csharp
ContainsKey(key)      // → key in dict
ContainsValue(value)  // → value in dict.values()
Add(key, value)       // → dict[key] = value
Remove(key)           // → del dict[key]
Clear()               // → dict = {}
Keys()                // → list(dict.keys())
Values()              // → list(dict.values())
```

**⚠️ Not supported (will warn):**
- `TryGetValue(key, out value)` - Use `dict.get(key, None)` instead
- `GetValueOrDefault(key)` - Use `dict.get(key, None)` instead
- `TryAdd()`, `EnsureCapacity()` and other methods

### Object Creation and Notes

**✅ Recommended:**
```csharp
List<string> list = new();                // → list = []
Dictionary<int, string> dict = new();     // → dict = {}
List<KeyValuePair<int, int>> pairs = [new(1, 2)];  // → pairs = [(1, 2)]
```

**Notes:**
- `KeyValuePair<K, V>` is converted to tuple `(key, value)`
- Avoid using List constructors with parameters
- Do not ignore the return value of list access methods, or the game will throw errors

**❌ Avoid:**
```csharp
list.First();  // Ignoring return value is not allowed in the game
list[0];       // Ignoring return value is not allowed in the game
```
