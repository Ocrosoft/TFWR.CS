namespace TFWR.CS.Transpiler;

/// <summary>
/// C# PascalCase 方法名 → TFWR 游戏内置函数名映射。
/// </summary>
public static class NameMappings
{
    // ── TFWR 内置方法映射 ──────────────────────────────────────────────────
    private static readonly Dictionary<string, string> BuiltinMethods = new(StringComparer.Ordinal)
    {
        // TFWR class methods  →  game builtins
        ["Harvest"] = "harvest",
        ["CanHarvest"] = "can_harvest",
        ["Plant"] = "plant",
        ["Move"] = "move",
        ["CanMove"] = "can_move",
        ["Swap"] = "swap",
        ["Till"] = "till",
        ["GetPosX"] = "get_pos_x",
        ["GetPosY"] = "get_pos_y",
        ["GetWorldSize"] = "get_world_size",
        ["GetEntityType"] = "get_entity_type",
        ["GetGroundType"] = "get_ground_type",
        ["GetTime"] = "get_time",
        ["GetTickCount"] = "get_tick_count",
        ["UseItem"] = "use_item",
        ["GetWater"] = "get_water",
        ["DoAFlip"] = "do_a_flip",
        ["PetThePiggy"] = "pet_the_piggy",
        ["Print"] = "print",
        ["SetExecutionSpeed"] = "set_execution_speed",
        ["SetWorldSize"] = "set_world_size",
        ["NumItems"] = "num_items",
        ["GetCost"] = "get_cost",
        ["Clear"] = "clear",
        ["GetCompanion"] = "get_companion",
        ["Unlock"] = "unlock",
        ["NumUnlocked"] = "num_unlocked",
        ["Measure"] = "measure",
        ["LeaderboardRun"] = "leaderboard_run",
        ["Simulate"] = "simulate",
        ["QuickPrint"] = "quick_print",
        ["ChangeHat"] = "change_hat",
        ["SpawnDrone"] = "spawn_drone",
        ["WaitFor"] = "wait_for",
        ["HasFinished"] = "has_finished",
        ["MaxDrones"] = "max_drones",
        ["NumDrones"] = "num_drones",
    };

    // ── 枚举容器类 → 游戏 Entities / Items 等 ──────────────────────────────
    // 游戏中直接用 Entities.Bush, Items.Hay 等，C# 中写 Entity.Bush, Item.Hay
    private static readonly Dictionary<string, string> EnumTypeToPrefix = new(StringComparer.Ordinal)
    {
        ["Entity"] = "Entities",
        ["Item"] = "Items",
        ["Ground"] = "Grounds",
        ["Hat"] = "Hats",
        ["Leaderboard"] = "Leaderboards",
        ["Unlock"] = "Unlocks",
    };

    // ── Direction 枚举值是全局常量 ─────────────────────────────────────────
    private static readonly HashSet<string> DirectionValues = new(StringComparer.Ordinal)
    {
        "North", "East", "South", "West", "Up", "Right", "Down", "Left"
    };

    // ── Direction 别名 → 基础值（游戏中只有 North/East/South/West）────────
    private static readonly Dictionary<string, string> DirectionAliases = new(StringComparer.Ordinal)
    {
        ["Up"] = "North",
        ["Right"] = "East",
        ["Down"] = "South",
        ["Left"] = "West"
    };

    // ── .NET → 游戏内置全局函数 ────────────────────────────────────────────
    private static readonly Dictionary<string, string> DotNetToBuiltin = new(StringComparer.Ordinal)
    {
        ["Math.Min"] = "min",
        ["Math.Max"] = "max",
        ["Math.Abs"] = "abs",
    };

    /// <summary>尝试获取 TFWR 内置方法名。</summary>
    public static bool TryGetBuiltinMethod(string csharpName, out string gameFunc)
        => BuiltinMethods.TryGetValue(csharpName, out gameFunc!);

    /// <summary>尝试获取枚举前缀 (Entity→Entities 等)。</summary>
    public static bool TryGetEnumPrefix(string enumTypeName, out string prefix)
        => EnumTypeToPrefix.TryGetValue(enumTypeName, out prefix!);

    /// <summary>是否为 Direction 值，游戏中作为全局常量使用。</summary>
    public static bool IsDirectionValue(string valueName)
        => DirectionValues.Contains(valueName);

    /// <summary>获取 Direction 值在游戏中的实际名称（Up→North 等）。</summary>
    public static string NormalizeDirection(string valueName)
        => DirectionAliases.TryGetValue(valueName, out var normalized) ? normalized : valueName;

    /// <summary>尝试获取 .NET 静态方法对应的内置函数。</summary>
    public static bool TryGetDotNetBuiltin(string dotnetCall, out string gameFunc)
        => DotNetToBuiltin.TryGetValue(dotnetCall, out gameFunc!);
}
