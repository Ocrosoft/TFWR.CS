namespace TFWR.CS.REF;

// ============================================================================
// Enums & Constants
// ============================================================================

/// <summary>表示方向</summary>
public enum Direction
{
    /// <summary>The direction north, i.e. up.</summary>
    North,
    /// <summary>The direction east, i.e. right.</summary>
    East,
    /// <summary>The direction south, i.e. down.</summary>
    South,
    /// <summary>The direction west, i.e. left.</summary>
    West,
    Up = North,
    Right = East,
    Down = South,
    Left = West
}

/// <summary>实体类型</summary>
public enum Entity
{
    /// <summary>Dinosaurs love them apparently.</summary>
    Apple,
    /// <summary>A small bush that drops Wood. Average seconds to grow: 4. Grows on: grassland or soil.</summary>
    Bush,
    /// <summary>Cacti come in 10 different sizes (0-9). Harvest adjacent sorted cacti recursively. Average seconds to grow: 1. Grows on: soil.</summary>
    Cactus,
    /// <summary>Carrots! Average seconds to grow: 6. Grows on: soil.</summary>
    Carrot,
    /// <summary>One in five pumpkins dies when it grows up. can_harvest() always returns False on dead pumpkins.</summary>
    Dead_Pumpkin,
    /// <summary>A piece of the tail of the dinosaur hat. Average seconds to grow: 0.2. Grows on: grassland or soil.</summary>
    Dinosaur,
    /// <summary>Grows automatically on grassland. Harvest to obtain Hay. Average seconds to grow: 0.5. Grows on: grassland or soil.</summary>
    Grass,
    /// <summary>Part of the maze.</summary>
    Hedge,
    /// <summary>Pumpkins grow together when next to other fully grown pumpkins. Average seconds to grow: 2. Grows on: soil.</summary>
    Pumpkin,
    /// <summary>Sunflowers collect power from the sun. Average seconds to grow: 5. Grows on: soil.</summary>
    Sunflower,
    /// <summary>A treasure that contains gold equal to the side length of the maze.</summary>
    Treasure,
    /// <summary>Trees drop more wood than bushes. Average seconds to grow: 7. Grows on: grassland or soil.</summary>
    Tree
}

/// <summary>物品类型</summary>
public enum Item
{
    /// <summary>The bones of an ancient creature.</summary>
    Bone,
    /// <summary>Obtained by harvesting sorted cacti.</summary>
    Cactus,
    /// <summary>Obtained by harvesting carrots.</summary>
    Carrot,
    /// <summary>Call use_item(Items.Fertilizer) to instantly remove 2s from the plants remaining grow time.</summary>
    Fertilizer,
    /// <summary>Found in treasure chests in mazes.</summary>
    Gold,
    /// <summary>Obtained by cutting grass.</summary>
    Hay,
    /// <summary>This item has been removed from the game but remains as a nostalgia trophy.</summary>
    Piggy,
    /// <summary>Obtained by harvesting sunflowers. The drone automatically uses this to move twice as fast.</summary>
    Power,
    /// <summary>Obtained by harvesting pumpkins.</summary>
    Pumpkin,
    /// <summary>Used to water the ground by calling use_item(Items.Water).</summary>
    Water,
    /// <summary>Call use_item(Items.Weird_Substance) on a bush to grow a maze, or on other plants to toggle their infection status.</summary>
    Weird_Substance,
    /// <summary>Obtained from bushes and trees.</summary>
    Wood
}

/// <summary>地面类型</summary>
public enum Ground
{
    /// <summary>The default ground. Grass will automatically grow on it.</summary>
    Grassland,
    /// <summary>Calling till() turns the ground into this. Calling till() again changes it back to grassland.</summary>
    Soil
}

/// <summary>帽子类型</summary>
public enum Hat
{
    /// <summary>A brown hat.</summary>
    Brown_Hat,

    /// <summary>A hat shaped like a cactus.</summary>
    Cactus_Hat,

    /// <summary>A hat shaped like a carrot.</summary>
    Carrot_Hat,

    /// <summary>Equip it to start the dinosaur game.</summary>
    Dinosaur_Hat,

    /// <summary>A golden hat.</summary>
    Gold_Hat,

    /// <summary>A golden trophy hat.</summary>
    Gold_Trophy_Hat,

    /// <summary>A golden hat shaped like a cactus.</summary>
    Golden_Cactus_Hat,

    /// <summary>A golden hat shaped like a carrot.</summary>
    Golden_Carrot_Hat,

    /// <summary>A golden version of the gold hat.</summary>
    Golden_Gold_Hat,

    /// <summary>A golden hat shaped like a pumpkin.</summary>
    Golden_Pumpkin_Hat,

    /// <summary>A golden hat shaped like a sunflower.</summary>
    Golden_Sunflower_Hat,

    /// <summary>A golden hat shaped like a tree.</summary>
    Golden_Tree_Hat,

    /// <summary>A gray hat.</summary>
    Gray_Hat,

    /// <summary>A green hat.</summary>
    Green_Hat,

    /// <summary>A hat shaped like a pumpkin.</summary>
    Pumpkin_Hat,

    /// <summary>A purple hat.</summary>
    Purple_Hat,

    /// <summary>A silver trophy hat.</summary>
    Silver_Trophy_Hat,

    /// <summary>The default hat.</summary>
    Straw_Hat,

    /// <summary>A hat shaped like a sunflower.</summary>
    Sunflower_Hat,

    /// <summary>The remains of the farmer.</summary>
    The_Farmers_Remains,

    /// <summary>A fancy top hat.</summary>
    Top_Hat,

    /// <summary>A traffic cone hat.</summary>
    Traffic_Cone,

    /// <summary>A stack of traffic cones as a hat.</summary>
    Traffic_Cone_Stack,

    /// <summary>A hat shaped like a tree.</summary>
    Tree_Hat,

    /// <summary>A magical wizard hat.</summary>
    Wizard_Hat,

    /// <summary>A wooden trophy hat.</summary>
    Wood_Trophy_Hat
}

/// <summary>排行榜类型</summary>
public enum Leaderboard
{
    /// <summary>Farm33554432 cacti with multiple drones.</summary>
    Cactus,

    /// <summary>Farm131072 cacti with a single drone on an8x8 farm.</summary>
    Cactus_Single,

    /// <summary>Farm2000000000 carrots with multiple drones.</summary>
    Carrots,

    /// <summary>Farm100000000 carrots with a single drone on an8x8 farm.</summary>
    Carrots_Single,

    /// <summary>Farm33488928 bones with multiple drones.</summary>
    Dinosaur,

    /// <summary>The most prestigious category. Completely automate the game from a single farm plot to unlocking the leaderboards again.</summary>
    Fastest_Reset,

    /// <summary>Farm2000000 hay with multiple drones.</summary>
    Hay,

    /// <summary>Farm10000000 hay with a single drone on an8x8 farm.</summary>
    Hay_Single,

    /// <summary>Farm9863168 gold with multiple drones.</summary>
    Maze,

    /// <summary>Farm616448 gold with a single drone on an8x8 farm.</summary>
    Maze_Single,

    /// <summary>Farm2000000 pumpkins with multiple drones.</summary>
    Pumpkins,

    /// <summary>Farm1000000 pumpkins with a single drone on an8x8 farm.</summary>
    Pumpkins_Single,

    /// <summary>Farm10000 power with multiple drones.</summary>
    Sunflowers,

    /// <summary>Farm10000 power with a single drone on an8x8 farm.</summary>
    Sunflowers_Single,

    /// <summary>Farm10000000000 wood with multiple drones.</summary>
    Wood,

    /// <summary>Farm500000000 wood with a single drone on an8x8 farm.</summary>
    Wood_Single
}

/// <summary>解锁/升级类型</summary>
public enum Unlock
{
    /// <summary>Automatically unlock things.</summary>
    Auto_Unlock,

    /// <summary>
    /// Unlock: Cactus!
    /// Upgrade: Increases the yield and cost of cactus.
    /// </summary>
    Cactus,

    /// <summary>
    /// Unlock: Till the soil and plant carrots.
    /// Upgrade: Increases the yield and cost of carrots.
    /// </summary>
    Carrots,

    /// <summary>Allows access to the cost of things.</summary>
    Costs,

    /// <summary>Tools to help with debugging programs.</summary>
    Debug,

    /// <summary>Functions to temporarily slow down the execution and make the grid smaller.</summary>
    Debug_2,

    /// <summary>Get access to dictionaries and sets.</summary>
    Dictionaries,

    /// <summary>
    /// Unlock: Majestic ancient creatures.
    /// Upgrade: Increases the yield and cost of dinosaurs.
    /// </summary>
    Dinosaurs,

    /// <summary>
    /// Unlock: Expands the farm land and unlocks movement.
    /// Upgrade: Expands the farm. This also clears the farm.
    /// </summary>
    Expand,

    /// <summary>Reduces the remaining growing time of the plant under the drone by2 seconds.</summary>
    Fertilizer,

    /// <summary>Define your own functions.</summary>
    Functions,

    /// <summary>Increases the yield of grass.</summary>
    Grass,

    /// <summary>Unlocks new hat colors for your drone.</summary>
    Hats,

    /// <summary>Import code from other files.</summary>
    Import,

    /// <summary>Join the leaderboard for the fastest reset time.</summary>
    Leaderboard,

    /// <summary>Use lists to store lots of values.</summary>
    Lists,

    /// <summary>Unlocks a simple while loop.</summary>
    Loops,

    /// <summary>
    /// Unlock: A maze with a treasure in the middle.
    /// Upgrade: Increases the gold in treasure chests.
    /// </summary>
    Mazes,

    /// <summary>Unlocks multiple drones and drone management functions.</summary>
    Megafarm,

    /// <summary>Arithmetic, comparison and logic operators.</summary>
    Operators,

    /// <summary>Unlocks planting.</summary>
    Plant,

    /// <summary>Use companion planting to increase the yield.</summary>
    Polyculture,

    /// <summary>
    /// Unlock: Pumpkins!
    /// Upgrade: Increases the yield and cost of pumpkins.
    /// </summary>
    Pumpkins,

    /// <summary>The drone can see what's under it and where it is.</summary>
    Senses,

    /// <summary>Unlocks simulation functions for testing and optimization.</summary>
    Simulation,

    /// <summary>Increases the speed of the drone.</summary>
    Speed,

    /// <summary>
    /// Unlock: Sunflowers and Power.
    /// Upgrade: Increases the power gained from sunflowers.
    /// </summary>
    Sunflowers,

    /// <summary>Unlocks the special hat 'The Farmers Remains'.</summary>
    The_Farmers_Remains,

    /// <summary>Functions to help measure performance.</summary>
    Timing,

    /// <summary>Unlocks the fancy Top Hat.</summary>
    Top_Hat,

    /// <summary>
    /// Unlock: Unlocks trees.
    /// Upgrade: Increases the yield of bushes and trees.
    /// </summary>
    Trees,

    /// <summary>Unlocks the `min()`, `max()` and `abs()` functions.</summary>
    Utilities,

    /// <summary>Assign values to variables.</summary>
    Variables,

    /// <summary>Water the plants to make them grow faster.</summary>
    Watering
}

/// <summary>无人机句柄（spawn_drone 的返回值）</summary>
public class DroneHandle { }

// ============================================================================
// 游戏内置函数（Builtins）
// ============================================================================

/// <summary>
/// The Farmer Was Replaced 游戏中所有内置方法的 C# 定义。
/// 所有方法均为 <c>static</c>，可通过 <c>using static TFWR;</c> 直接调用。
/// </summary>
public static class TFWR
{
    /// <summary>
    /// 收割无人机下方的实体。如果收割的实体不能被收割，它会被摧毁。
    /// </summary>
    /// <returns>如果移除了实体返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    public static bool Harvest() => throw new NotImplementedException();

    /// <summary>
    /// 判断无人机下方的植物是否已经完全生长，可以收割。
    /// </summary>
    /// <returns>如果可以收割返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    public static bool CanHarvest() => throw new NotImplementedException();

    /// <summary>
    /// 花费指定实体的种植成本，在无人机下方种植该实体。
    /// </summary>
    /// <param name="entity">要种植的实体类型。</param>
    /// <returns>如果种植成功返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    public static bool Plant(Entity entity) => throw new NotImplementedException();

    /// <summary>
    /// 将无人机向指定方向移动一格。如果超出农场边界则环绕到另一侧。
    /// </summary>
    /// <param name="direction">移动方向（North/South/East/West）。</param>
    /// <returns>如果无人机成功移动返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    public static bool Move(Direction direction) => throw new NotImplementedException();

    /// <summary>
    /// 检查无人机是否可以向指定方向移动。
    /// </summary>
    /// <param name="direction">要检查的方向。</param>
    /// <returns>如果可以移动返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    public static bool CanMove(Direction direction) => throw new NotImplementedException();

    /// <summary>
    /// 将无人机下方的实体与指定方向的相邻实体交换。
    /// </summary>
    /// <param name="direction">交换方向。</param>
    /// <returns>如果交换成功返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    public static bool Swap(Direction direction) => throw new NotImplementedException();

    /// <summary>
    /// 翻耕无人机下方的地面，将草地变为土壤，土壤变回草地。
    /// </summary>
    public static void Till() => throw new NotImplementedException();

    /// <summary>获取无人机当前的 X 坐标。X 从西侧 0 开始，向东递增。</summary>
    public static int GetPosX() => throw new NotImplementedException();

    /// <summary>获取无人机当前的 Y 坐标。Y 从南侧 0 开始，向北递增。</summary>
    public static int GetPosY() => throw new NotImplementedException();

    /// <summary>获取农场的边长。</summary>
    public static int GetWorldSize() => throw new NotImplementedException();

    /// <summary>
    /// 获取无人机下方的实体类型。
    /// </summary>
    /// <returns>如果格子为空返回 <c>null</c>，否则返回实体类型。</returns>
    public static Entity? GetEntityType() => throw new NotImplementedException();

    /// <summary>获取无人机下方的地面类型。</summary>
    public static Ground GetGroundType() => throw new NotImplementedException();

    /// <summary>
    /// 获取自游戏开始以来的时间（秒）。
    /// </summary>
    public static float GetTime() => throw new NotImplementedException();

    /// <summary>
    /// 获取自执行开始以来执行的 tick 数。
    /// </summary>
    public static int GetTickCount() => throw new NotImplementedException();

    /// <summary>
    /// 使用指定物品 n 次。仅支持部分物品（Water、Fertilizer、Weird_Substance）。
    /// </summary>
    /// <param name="item">要使用的物品。</param>
    /// <param name="n">使用次数，默认为 1。</param>
    /// <returns>如果成功使用返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    public static bool UseItem(Item item, int n = 1) => throw new NotImplementedException();

    /// <summary>
    /// 获取无人机下方的水位。
    /// </summary>
    /// <returns>0 到 1 之间的浮点数，表示水位。</returns>
    public static float GetWater() => throw new NotImplementedException();

    /// <summary>让无人机做一个翻转！耗时 1 秒，不受速度升级影响。</summary>
    public static void DoAFlip() => throw new NotImplementedException();

    /// <summary>抚摸小猪！耗时 1 秒，不受速度升级影响。</summary>
    public static void PetThePiggy() => throw new NotImplementedException();

    /// <summary>
    /// 在无人机上方用烟雾打印信息。耗时 1 秒，不受速度升级影响。
    /// </summary>
    public static void Print(params object[] something) => throw new NotImplementedException();

    /// <summary>
    /// 限制程序执行速度以便观察。speed=1 为无升级速度，speed=10 为 10 倍速。
    /// </summary>
    /// <param name="speed">执行速度倍率。0 或负数恢复最大速度。</param>
    public static void SetExecutionSpeed(float speed) => throw new NotImplementedException();

    /// <summary>
    /// 限制农场大小以便观察。最小为 3。小于 3 恢复完整大小。同时清空农场并重置无人机位置。
    /// </summary>
    /// <param name="size">农场边长。</param>
    public static void SetWorldSize(float size) => throw new NotImplementedException();

    /// <summary>
    /// 查询指定物品的当前数量。
    /// </summary>
    /// <param name="item">要查询的物品。</param>
    /// <returns>物品数量。</returns>
    public static float NumItems(Item item) => throw new NotImplementedException();

    /// <summary>
    /// 获取种植某实体的成本。
    /// </summary>
    /// <returns>物品到数量的字典，或 null。</returns>
    public static Dictionary<Item, float>? GetCost(Entity thing) => throw new NotImplementedException();

    /// <summary>
    /// 清除农场上的所有内容，将无人机移回 (0,0)，帽子恢复默认。
    /// </summary>
    public static void Clear() => throw new NotImplementedException();

    /// <summary>
    /// 获取无人机下方植物的伙伴偏好信息。
    /// </summary>
    /// <returns>返回 (companion_type, (x, y)) 的元组，如果没有伙伴则返回 null。</returns>
    public static (Entity companionType, (int x, int y) position)? GetCompanion() => throw new NotImplementedException();

    /// <summary>
    /// 解锁/升级指定项，等同于在研究树中点击对应按钮。
    /// </summary>
    /// <param name="unlock">要解锁的项。</param>
    /// <returns>如果解锁成功返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    public static bool Unlock(Unlock unlock) => throw new NotImplementedException();

    /// <summary>
    /// 检查某个解锁/实体/地面/物品是否已解锁及升级次数。
    /// </summary>
    /// <returns>如果可升级返回 1 + 升级次数；否则已解锁返回 1，未解锁返回 0。</returns>
    public static int NumUnlocked(Unlock thing) => throw new NotImplementedException();

    /// <summary>
    /// 测量无人机下方实体的值。效果取决于实体类型：
    /// Sunflower → 花瓣数；Maze → 宝藏位置；Cactus → 大小；Dinosaur → 类型编号；其他 → null。
    /// </summary>
    /// <returns>测量结果（float、(int,int) 或 null）。</returns>
    public static object? Measure() => throw new NotImplementedException();

    /// <summary>
    /// 测量指定方向的相邻实体。
    /// </summary>
    /// <param name="direction">方向。</param>
    /// <returns>测量结果。</returns>
    public static object? Measure(Direction direction) => throw new NotImplementedException();

    /// <summary>
    /// 开始排行榜计时运行。
    /// </summary>
    /// <param name="leaderboard">排行榜类型。</param>
    /// <param name="fileName">起始脚本文件名。</param>
    /// <param name="speedup">起始加速倍率。</param>
    public static void LeaderboardRun(Leaderboard leaderboard, string fileName, float speedup) => throw new NotImplementedException();

    /// <summary>
    /// 开始模拟运行。
    /// </summary>
    /// <param name="filename">起始脚本文件名。</param>
    /// <param name="simUnlocks">模拟开始时的解锁项集合。</param>
    /// <param name="simItems">模拟开始时的物品数量字典。</param>
    /// <param name="simGlobals">模拟开始时的全局变量字典。</param>
    /// <param name="seed">随机种子（正整数）。</param>
    /// <param name="speedup">起始加速倍率。</param>
    /// <returns>模拟运行耗时（秒）。</returns>
    public static float Simulate(string filename, IEnumerable<Unlock> simUnlocks, Dictionary<Item, float> simItems, Dictionary<string, object> simGlobals, float seed, float speedup) => throw new NotImplementedException();

    /// <summary>
    /// 快速打印，不暂停，仅在输出页面可见。耗时 0 tick。
    /// </summary>
    public static void QuickPrint(params object[] something) => throw new NotImplementedException();

    /// <inheritdoc cref="NumUnlocked(Unlock)"/>
    public static int NumUnlocked(Entity thing) => throw new NotImplementedException();

    /// <inheritdoc cref="NumUnlocked(Unlock)"/>
    public static int NumUnlocked(Ground thing) => throw new NotImplementedException();

    /// <inheritdoc cref="NumUnlocked(Unlock)"/>
    public static int NumUnlocked(Item thing) => throw new NotImplementedException();

    /// <summary>
    /// 获取某解锁项指定等级的成本。
    /// </summary>
    /// <param name="thing">解锁项。</param>
    /// <param name="level">可选的升级等级。</param>
    /// <returns>物品到数量的字典，或 null（已解锁且未指定等级时）。</returns>
    public static Dictionary<Item, float>? GetCost(Unlock thing, int? level = null) => throw new NotImplementedException();

    /// <summary>获取某物品的成本。</summary>
    public static Dictionary<Item, float>? GetCost(Item thing) => throw new NotImplementedException();

    /// <summary>
    /// 更换无人机的帽子。
    /// </summary>
    /// <param name="hat">目标帽子。</param>
    public static void ChangeHat(Hat hat) => throw new NotImplementedException();

    /// <summary>
    /// 在当前无人机的位置生成一个新无人机，执行指定函数。完成后自动消失。
    /// </summary>
    /// <param name="function">要执行的函数名。</param>
    /// <returns>新无人机的句柄，如果已达上限则返回 null。</returns>
    public static DroneHandle? SpawnDrone(string function) => throw new NotImplementedException();

    /// <summary>
    /// 等待指定无人机执行完毕。
    /// </summary>
    /// <param name="drone">无人机句柄。</param>
    /// <returns>无人机执行函数的返回值。</returns>
    public static object? WaitFor(DroneHandle drone) => throw new NotImplementedException();

    /// <summary>
    /// 检查指定无人机是否已完成执行。
    /// </summary>
    /// <param name="drone">无人机句柄。</param>
    /// <returns>如果已完成返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    public static bool HasFinished(DroneHandle drone) => throw new NotImplementedException();

    /// <summary>获取农场中允许的最大无人机数量。</summary>
    public static int MaxDrones() => throw new NotImplementedException();

    /// <summary>获取农场中当前的无人机数量。</summary>
    public static int NumDrones() => throw new NotImplementedException();
}
