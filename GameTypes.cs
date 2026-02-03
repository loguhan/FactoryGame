using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace FactoryGame;

internal enum TileType
{
    Empty,
    Conveyor,
    FastConveyor,
    Splitter,
    Merger,
    Router,         // 路由器 - 自动检测输入方向
    Miner,
    Smelter,
    Assembler,
    Lab,
    Generator,
    Storage,
    // 新增建筑
    UndergroundEntry,   // 地下传送带入口
    UndergroundExit,    // 地下传送带出口
    CoalGenerator,      // 燃煤发电机
    AdvancedMiner,      // 高级矿工
    AssemblerMk2,       // 装配机Mk2（双输入）
    ChemicalPlant       // 化工厂
}

internal enum ItemType
{
    Ore,            // 铁矿石
    Plate,          // 铁板
    Gear,           // 齿轮
    Science,        // 科学包
    // 基础矿物
    CopperOre,      // 铜矿石
    CopperPlate,    // 铜板
    Coal,           // 煤炭
    // 新增矿物
    GoldOre,        // 金矿石
    GoldPlate,      // 金板
    TitaniumOre,    // 钛矿石
    TitaniumPlate,  // 钛板
    UraniumOre,     // 铀矿石
    UraniumPlate,   // 铀板
    // 中间产物
    CopperWire,     // 铜线
    Circuit,        // 电路板
    Steel,          // 钢材
    // 科学包
    RedScience,     // 红色科学包
    GreenScience    // 绿色科学包
}

internal enum Direction
{
    None,
    North,
    East,
    South,
    West
}

internal enum Tool
{
    Conveyor,
    FastConveyor,
    Splitter,
    Merger,
    Router,             // 路由器
    Miner,
    Smelter,
    Assembler,
    Lab,
    Generator,
    Storage,
    Erase,
    // 新增工具
    UndergroundBelt,    // 地下传送带
    CoalGenerator,      // 燃煤发电机
    AdvancedMiner,      // 高级矿工
    AssemblerMk2,       // 装配机Mk2
    ChemicalPlant       // 化工厂
}

internal struct Tile
{
    public TileType Type;
    public Direction Direction;
    public Point? ParentTile;  // 多方块建筑的主格子位置（如果是子格子）
}

// 建筑尺寸信息
internal static class BuildingSize
{
    public static Point GetSize(TileType type)
    {
        return type switch
        {
            // 1x1 建筑
            TileType.Conveyor => new Point(1, 1),
            TileType.FastConveyor => new Point(1, 1),
            TileType.Splitter => new Point(1, 1),
            TileType.Merger => new Point(1, 1),
            TileType.Router => new Point(1, 1),
            TileType.UndergroundEntry => new Point(1, 1),
            TileType.UndergroundExit => new Point(1, 1),
            TileType.Storage => new Point(1, 1),

            // 2x2 建筑
            TileType.Miner => new Point(2, 2),
            TileType.Smelter => new Point(2, 2),
            TileType.Generator => new Point(2, 2),
            TileType.CoalGenerator => new Point(2, 2),
            TileType.Lab => new Point(2, 2),

            // 3x3 建筑
            TileType.Assembler => new Point(3, 3),
            TileType.AdvancedMiner => new Point(3, 3),
            TileType.AssemblerMk2 => new Point(3, 3),
            TileType.ChemicalPlant => new Point(3, 3),

            _ => new Point(1, 1)
        };
    }

    public static bool IsMultiBlock(TileType type)
    {
        var size = GetSize(type);
        return size.X > 1 || size.Y > 1;
    }
}

/// <summary>
/// 旧的物品类（保留兼容性）
/// </summary>
internal sealed class Item
{
    public ItemType Type;
    public Point Tile;
    public Point Dir;
    public float Progress;
    public float Speed;
}

/// <summary>
/// 传送带物品位置 - 参考 Mindustry 的 ItemPos
/// 使用 long 打包存储: [itemId:16][x:16][y:16][seed:16]
/// </summary>
internal static class ConveyorItemPos
{
    /// <summary>
    /// 打包物品数据为 long
    /// </summary>
    /// <param name="itemType">物品类型</param>
    /// <param name="x">横向偏移 [-1, 1]</param>
    /// <param name="y">纵向位置 [0, 1]</param>
    /// <param name="seed">随机种子</param>
    public static long Pack(ItemType itemType, float x, float y, short seed)
    {
        short itemId = (short)itemType;
        short xShort = (short)(x * short.MaxValue);
        short yShort = (short)((y - 1f) * short.MaxValue);

        return ((long)itemId << 48) | ((long)(ushort)xShort << 32) | ((long)(ushort)yShort << 16) | (ushort)seed;
    }

    /// <summary>
    /// 解包物品类型
    /// </summary>
    public static ItemType GetItemType(long packed)
    {
        return (ItemType)(short)(packed >> 48);
    }

    /// <summary>
    /// 解包横向偏移
    /// </summary>
    public static float GetX(long packed)
    {
        short xShort = (short)((packed >> 32) & 0xFFFF);
        return xShort / (float)short.MaxValue;
    }

    /// <summary>
    /// 解包纵向位置
    /// </summary>
    public static float GetY(long packed)
    {
        short yShort = (short)((packed >> 16) & 0xFFFF);
        return yShort / (float)short.MaxValue + 1f;
    }

    /// <summary>
    /// 解包随机种子
    /// </summary>
    public static short GetSeed(long packed)
    {
        return (short)(packed & 0xFFFF);
    }

    /// <summary>
    /// 更新 Y 位置并重新打包
    /// </summary>
    public static long SetY(long packed, float newY)
    {
        ItemType itemType = GetItemType(packed);
        float x = GetX(packed);
        short seed = GetSeed(packed);
        return Pack(itemType, x, newY, seed);
    }

    /// <summary>
    /// 更新 X 位置并重新打包
    /// </summary>
    public static long SetX(long packed, float newX)
    {
        ItemType itemType = GetItemType(packed);
        float y = GetY(packed);
        short seed = GetSeed(packed);
        return Pack(itemType, newX, y, seed);
    }

    /// <summary>
    /// 更新 X 和 Y 位置并重新打包
    /// </summary>
    public static long SetXY(long packed, float newX, float newY)
    {
        ItemType itemType = GetItemType(packed);
        short seed = GetSeed(packed);
        return Pack(itemType, newX, newY, seed);
    }
}

/// <summary>
/// 传送带实体状态 - 参考 Mindustry 的 ConveyorEntity
/// 每个传送带格子存储一个物品列表
/// </summary>
internal sealed class ConveyorEntity
{
    /// <summary>
    /// 物品列表，使用 long 打包存储，按 Y 位置排序
    /// </summary>
    public List<long> Items = new();

    /// <summary>
    /// 最小物品位置（用于判断是否可以接受新物品）
    /// </summary>
    public float MinItem = 1f;

    /// <summary>
    /// 物品间距
    /// </summary>
    public const float ItemSpace = 0.135f;

    /// <summary>
    /// 检查是否可以接受物品
    /// </summary>
    /// <param name="fromSide">是否从侧面输入</param>
    public bool CanAccept(bool fromSide)
    {
        if (fromSide)
        {
            return MinItem > 0.52f;
        }
        return MinItem > ItemSpace;
    }

    /// <summary>
    /// 添加物品
    /// </summary>
    /// <param name="itemType">物品类型</param>
    /// <param name="x">横向偏移</param>
    /// <param name="y">纵向位置</param>
    public void AddItem(ItemType itemType, float x, float y)
    {
        short seed = (short)Random.Shared.Next(short.MinValue, short.MaxValue);
        long packed = ConveyorItemPos.Pack(itemType, x, y, seed);

        // 按 Y 位置插入排序
        int insertIndex = Items.Count;
        for (int i = 0; i < Items.Count; i++)
        {
            if (ConveyorItemPos.GetY(Items[i]) > y)
            {
                insertIndex = i;
                break;
            }
        }
        Items.Insert(insertIndex, packed);

        // 更新最小位置
        if (y < MinItem)
        {
            MinItem = y;
        }
    }

    /// <summary>
    /// 移除已输出的物品（从末尾截断）
    /// </summary>
    public void Truncate(int index)
    {
        if (index < Items.Count)
        {
            Items.RemoveRange(index, Items.Count - index);
        }
    }
}

internal sealed class MinerState
{
    public float Timer;
}

/// <summary>
/// 处理器状态 - 参考 Mindustry 的设计
/// 支持输入/输出缓冲区、多输入配方、燃料系统
/// </summary>
internal sealed class ProcessorState
{
    // 建筑类型（用于多配方支持）
    public TileType BuildingType;

    // 配方定义（可空，会根据输入自动设置）
    public ProcessorRecipe? Recipe;

    // 输入缓冲区 - 每种物品类型的数量
    public Dictionary<ItemType, int> InputBuffer = new();

    // 输出缓冲区
    public List<ItemType> OutputBuffer = new();

    // 制作状态
    public float CraftTimer;        // 当前制作进度计时器
    public bool IsCrafting;         // 是否正在制作

    // 燃料系统（可选）
    public float BurnTime;          // 剩余燃烧时间
    public bool RequiresFuel;       // 是否需要燃料

    // 容量限制
    public int InputCapacity = 10;  // 每种输入物品的最大数量
    public int OutputCapacity = 10; // 输出缓冲区最大数量

    // 输出方向轮询索引
    public int DumpDirection;

    /// <summary>
    /// 检查是否可以接受指定物品
    /// </summary>
    public bool CanAcceptItem(ItemType item)
    {
        // 检查缓冲区是否已满
        int current = InputBuffer.GetValueOrDefault(item, 0);
        if (current >= InputCapacity) return false;

        // 冶炼厂特殊处理：接受所有矿物类型（支持多配方）
        if (BuildingType == TileType.Smelter)
        {
            return Recipes.IsOreType(item);
        }

        // 如果有配方，检查是否是配方需要的输入
        if (Recipe != null)
        {
            return Recipe.Inputs.ContainsKey(item);
        }

        // 没有配方时，接受矿物类型
        return Recipes.IsOreType(item);
    }

    /// <summary>
    /// 检查是否可以接受燃料
    /// </summary>
    public bool CanAcceptFuel(ItemType item)
    {
        if (!RequiresFuel) return false;
        if (Recipe?.Fuel != item) return false;
        return BurnTime < 60f; // 燃料不超过60秒
    }

    /// <summary>
    /// 添加物品到输入缓冲区
    /// </summary>
    public void AddItem(ItemType item)
    {
        if (!InputBuffer.ContainsKey(item))
            InputBuffer[item] = 0;
        InputBuffer[item]++;
    }

    /// <summary>
    /// 添加燃料
    /// </summary>
    public void AddFuel(float duration)
    {
        BurnTime += duration;
    }

    /// <summary>
    /// 检查是否有足够的输入材料开始制作
    /// 对于冶炼厂，会动态选择匹配的配方
    /// </summary>
    public bool HasEnoughInputs()
    {
        // 冶炼厂特殊处理：动态选择配方
        if (BuildingType == TileType.Smelter)
        {
            return TrySelectSmelterRecipe();
        }

        if (Recipe == null) return false;

        foreach (var input in Recipe.Inputs)
        {
            int have = InputBuffer.GetValueOrDefault(input.Key, 0);
            if (have < input.Value) return false;
        }
        return true;
    }

    /// <summary>
    /// 尝试为冶炼厂选择合适的配方
    /// </summary>
    private bool TrySelectSmelterRecipe()
    {
        // 检查铁矿（不需要煤炭）
        if (InputBuffer.GetValueOrDefault(ItemType.Ore, 0) >= 1)
        {
            Recipe = Recipes.SmeltIron;
            return true;
        }

        // 检查其他矿物（需要煤炭 1:1）
        int coal = InputBuffer.GetValueOrDefault(ItemType.Coal, 0);
        if (coal >= 1)
        {
            if (InputBuffer.GetValueOrDefault(ItemType.CopperOre, 0) >= 1)
            {
                Recipe = Recipes.SmeltCopper;
                return true;
            }
            if (InputBuffer.GetValueOrDefault(ItemType.GoldOre, 0) >= 1)
            {
                Recipe = Recipes.SmeltGold;
                return true;
            }
            if (InputBuffer.GetValueOrDefault(ItemType.TitaniumOre, 0) >= 1)
            {
                Recipe = Recipes.SmeltTitanium;
                return true;
            }
            if (InputBuffer.GetValueOrDefault(ItemType.UraniumOre, 0) >= 1)
            {
                Recipe = Recipes.SmeltUranium;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 消耗输入材料，开始制作
    /// </summary>
    public void ConsumeInputs()
    {
        if (Recipe == null) return;

        foreach (var input in Recipe.Inputs)
        {
            InputBuffer[input.Key] -= input.Value;
        }
        IsCrafting = true;
        CraftTimer = Recipe.CraftTime;
    }

    /// <summary>
    /// 完成制作，产出物品
    /// </summary>
    public void FinishCraft()
    {
        if (Recipe == null) return;

        foreach (var output in Recipe.Outputs)
        {
            for (int i = 0; i < output.Value; i++)
            {
                OutputBuffer.Add(output.Key);
            }
        }
        IsCrafting = false;
    }

    /// <summary>
    /// 尝试取出一个输出物品
    /// </summary>
    public ItemType? TryTakeOutput()
    {
        if (OutputBuffer.Count == 0) return null;
        var item = OutputBuffer[0];
        OutputBuffer.RemoveAt(0);
        return item;
    }

    /// <summary>
    /// 获取制作进度 (0-1)
    /// </summary>
    public float GetProgress()
    {
        if (Recipe == null || !IsCrafting) return 0f;
        return 1f - (CraftTimer / Recipe.CraftTime);
    }
}

/// <summary>
/// 处理器配方定义
/// </summary>
internal sealed class ProcessorRecipe
{
    public string Name = "";

    // 输入物品 - 物品类型 -> 数量
    public Dictionary<ItemType, int> Inputs = new();

    // 输出物品 - 物品类型 -> 数量
    public Dictionary<ItemType, int> Outputs = new();

    // 制作时间（秒）
    public float CraftTime = 2f;

    // 燃料类型（可选）
    public ItemType? Fuel;
    public float FuelDuration = 30f; // 每个燃料燃烧时间

    // 快捷创建方法
    public static ProcessorRecipe Create(string name, ItemType input, ItemType output, float time)
    {
        return new ProcessorRecipe
        {
            Name = name,
            Inputs = new Dictionary<ItemType, int> { { input, 1 } },
            Outputs = new Dictionary<ItemType, int> { { output, 1 } },
            CraftTime = time
        };
    }

    public static ProcessorRecipe CreateWithFuel(string name, ItemType input, ItemType output, float time, ItemType fuel)
    {
        return new ProcessorRecipe
        {
            Name = name,
            Inputs = new Dictionary<ItemType, int> { { input, 1 } },
            Outputs = new Dictionary<ItemType, int> { { output, 1 } },
            CraftTime = time,
            Fuel = fuel
        };
    }

    public static ProcessorRecipe CreateMultiInput(string name, Dictionary<ItemType, int> inputs, ItemType output, float time)
    {
        return new ProcessorRecipe
        {
            Name = name,
            Inputs = inputs,
            Outputs = new Dictionary<ItemType, int> { { output, 1 } },
            CraftTime = time
        };
    }
}

/// <summary>
/// 预定义的处理器配方
/// </summary>
internal static class Recipes
{
    // 冶炼厂配方 - 铁矿不需要燃料
    public static readonly ProcessorRecipe SmeltIron = ProcessorRecipe.Create(
        "冶炼铁矿", ItemType.Ore, ItemType.Plate, 2.0f);

    // 冶炼厂配方 - 其他矿物需要煤炭 1:1
    public static readonly ProcessorRecipe SmeltCopper = ProcessorRecipe.CreateMultiInput(
        "冶炼铜矿",
        new Dictionary<ItemType, int> { { ItemType.CopperOre, 1 }, { ItemType.Coal, 1 } },
        ItemType.CopperPlate, 2.0f);

    public static readonly ProcessorRecipe SmeltGold = ProcessorRecipe.CreateMultiInput(
        "冶炼金矿",
        new Dictionary<ItemType, int> { { ItemType.GoldOre, 1 }, { ItemType.Coal, 1 } },
        ItemType.GoldPlate, 3.0f);

    public static readonly ProcessorRecipe SmeltTitanium = ProcessorRecipe.CreateMultiInput(
        "冶炼钛矿",
        new Dictionary<ItemType, int> { { ItemType.TitaniumOre, 1 }, { ItemType.Coal, 1 } },
        ItemType.TitaniumPlate, 4.0f);

    public static readonly ProcessorRecipe SmeltUranium = ProcessorRecipe.CreateMultiInput(
        "冶炼铀矿",
        new Dictionary<ItemType, int> { { ItemType.UraniumOre, 1 }, { ItemType.Coal, 1 } },
        ItemType.UraniumPlate, 5.0f);

    // 装配机配方
    public static readonly ProcessorRecipe AssembleGear = ProcessorRecipe.Create(
        "制造齿轮", ItemType.Plate, ItemType.Gear, 2.5f);

    public static readonly ProcessorRecipe AssembleWire = ProcessorRecipe.Create(
        "制造铜线", ItemType.CopperPlate, ItemType.CopperWire, 1.5f);

    public static readonly ProcessorRecipe AssembleCircuit = ProcessorRecipe.CreateMultiInput(
        "制造电路板",
        new Dictionary<ItemType, int> { { ItemType.CopperWire, 1 }, { ItemType.Plate, 1 } },
        ItemType.Circuit, 3.0f);

    // 实验室配方
    public static readonly ProcessorRecipe ResearchScience = ProcessorRecipe.Create(
        "研究科学", ItemType.Gear, ItemType.Science, 3.0f);

    public static readonly ProcessorRecipe ResearchRedScience = ProcessorRecipe.CreateMultiInput(
        "红色科学",
        new Dictionary<ItemType, int> { { ItemType.Gear, 1 }, { ItemType.CopperPlate, 1 } },
        ItemType.RedScience, 4.0f);

    // 根据建筑类型和输入物品获取配方
    public static ProcessorRecipe? GetRecipe(TileType buildingType, ItemType? primaryInput = null)
    {
        return buildingType switch
        {
            TileType.Smelter => primaryInput switch
            {
                ItemType.Ore => SmeltIron,
                ItemType.CopperOre => SmeltCopper,
                ItemType.Coal => SmeltCopper, // 煤炭输入时默认铜矿配方
                ItemType.GoldOre => SmeltGold,
                ItemType.TitaniumOre => SmeltTitanium,
                ItemType.UraniumOre => SmeltUranium,
                _ => null // 不自动设置默认配方，等待正确输入
            },
            TileType.Assembler => primaryInput switch
            {
                ItemType.Plate => AssembleGear,
                ItemType.CopperPlate => AssembleWire,
                _ => AssembleGear
            },
            TileType.AssemblerMk2 => AssembleCircuit,
            TileType.Lab => primaryInput switch
            {
                ItemType.Gear => ResearchScience,
                _ => ResearchScience
            },
            _ => null
        };
    }

    // 获取建筑的所有可用配方
    public static List<ProcessorRecipe> GetAvailableRecipes(TileType buildingType)
    {
        return buildingType switch
        {
            TileType.Smelter => new List<ProcessorRecipe> { SmeltIron, SmeltCopper, SmeltGold, SmeltTitanium, SmeltUranium },
            TileType.Assembler => new List<ProcessorRecipe> { AssembleGear, AssembleWire },
            TileType.AssemblerMk2 => new List<ProcessorRecipe> { AssembleCircuit },
            TileType.Lab => new List<ProcessorRecipe> { ResearchScience, ResearchRedScience },
            _ => new List<ProcessorRecipe>()
        };
    }

    // 根据输入物品判断是否是矿石类型
    public static bool IsOreType(ItemType item)
    {
        return item == ItemType.Ore || item == ItemType.CopperOre ||
               item == ItemType.GoldOre || item == ItemType.TitaniumOre ||
               item == ItemType.UraniumOre || item == ItemType.Coal;
    }
}

internal sealed class StorageState
{
    public int Count;
}

// 新增：地下传送带状态
internal sealed class UndergroundState
{
    public Point? LinkedExit;   // 链接的出口位置
    public Direction Direction; // 方向
}

// 新增：燃煤发电机状态
internal sealed class CoalGeneratorState
{
    public float FuelTimer;     // 燃料剩余时间
    public bool HasFuel;        // 是否有燃料
    public const float FuelDuration = 30f; // 每个煤炭燃烧30秒
}

// 新增：路由器状态
internal sealed class RouterState
{
    public int OutputIndex;     // 当前输出索引（轮流分配）
}

// 新增：矿石类型映射
internal enum OreType
{
    None,
    Iron,       // 铁矿
    Copper,     // 铜矿
    Coal,       // 煤矿
    Gold,       // 金矿
    Titanium,   // 钛矿
    Uranium     // 铀矿
}

// 新增：地形类型
internal enum TerrainType
{
    Grass,      // 草地 - 可建造
    Mountain,   // 山地 - 不可建造
    Water,      // 水域 - 不可建造
    Locked      // 未解锁区域
}

// 新增：区域信息
internal sealed class RegionInfo
{
    public int Id;
    public string Name = "";
    public int UnlockCost;
    public bool IsUnlocked;
    public Rectangle Bounds;
}

// 无限世界：区块数据
internal sealed class Chunk
{
    public const int Size = 16; // 每个区块16x16格子

    public Point ChunkPos;  // 区块坐标
    public Tile[,] Tiles = new Tile[Size, Size];
    public bool[,] OreMap = new bool[Size, Size];
    public OreType[,] OreTypeMap = new OreType[Size, Size];
    public TerrainType[,] TerrainMap = new TerrainType[Size, Size];
    public bool IsGenerated;
    public bool IsDirty; // 是否需要保存

    public Point WorldToLocal(Point world)
    {
        return new Point(
            ((world.X % Size) + Size) % Size,
            ((world.Y % Size) + Size) % Size
        );
    }

    public static Point WorldToChunk(Point world)
    {
        return new Point(
            world.X >= 0 ? world.X / Size : (world.X - Size + 1) / Size,
            world.Y >= 0 ? world.Y / Size : (world.Y - Size + 1) / Size
        );
    }

    public static Point ChunkToWorld(Point chunk)
    {
        return new Point(chunk.X * Size, chunk.Y * Size);
    }
}

// 存档数据结构
internal sealed class SaveData
{
    public int Version { get; set; } = 2;  // 版本升级
    public Dictionary<ItemType, int> Inventory { get; set; } = new();  // 材料库存
    public int ResearchPoints { get; set; }
    public int TotalPlatesStored { get; set; }
    public int TotalScienceStored { get; set; }
    public int TotalGearStored { get; set; }
    public float CameraX { get; set; }
    public float CameraY { get; set; }
    public float CameraZoom { get; set; }
    public List<SavedTile> Tiles { get; set; } = new();
    public List<SavedRegion> Regions { get; set; } = new();
    public List<int> UnlockedTools { get; set; } = new();
}

internal sealed class SavedTile
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Type { get; set; }
    public int Direction { get; set; }
    public int? ParentX { get; set; }
    public int? ParentY { get; set; }
}

internal sealed class SavedRegion
{
    public int Id { get; set; }
    public bool IsUnlocked { get; set; }
}

// 粒子效果结构
internal sealed class Particle
{
    public Vector2 Position;
    public Vector2 Velocity;
    public Color Color;
    public float Size;
    public float Life;
    public float MaxLife;
    public ParticleType Type;
}

internal enum ParticleType
{
    Spark,      // 火花
    Smoke,       // 烟雾
    Star,        // 星星
    Achievement   // 成就特效
}

internal static class DirectionUtil
{
    public static Point ToPoint(Direction direction)
    {
        return direction switch
        {
            Direction.North => new Point(0, -1),
            Direction.East => new Point(1, 0),
            Direction.South => new Point(0, 1),
            Direction.West => new Point(-1, 0),
            _ => Point.Zero
        };
    }

    public static Direction RotateCW(Direction direction)
    {
        return direction switch
        {
            Direction.North => Direction.East,
            Direction.East => Direction.South,
            Direction.South => Direction.West,
            Direction.West => Direction.North,
            _ => Direction.East
        };
    }

    public static bool UsesDirection(TileType type)
    {
        return type == TileType.Conveyor
            || type == TileType.FastConveyor
            || type == TileType.Splitter
            || type == TileType.Merger
            || type == TileType.UndergroundEntry
            || type == TileType.UndergroundExit;
    }
}
