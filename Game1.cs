using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Input;

namespace FactoryGame;

public sealed class Game1 : Game
{
    private enum Language
    {
        English,
        Chinese
    }
    private const int MapWidth = 256;  // 扩大地图
    private const int MapHeight = 256;
    private const int TileWidth = 64;
    private const int TileHeight = 32;

    // 无限世界：区块系统（预留）
    private readonly Dictionary<Point, Chunk> _chunks = new();
    private readonly HashSet<Point> _loadedChunks = new();
    private int _worldSeed;

    private const float ConveyorSpeed = 1.2f; // tiles per second
    private const float FastConveyorSpeed = 2.2f;
    private const float MinerInterval = 1.0f;
    private const float SmeltTime = 2.0f;
    private const float AssembleTime = 2.5f;
    private const float LabTime = 3.0f;
    private const int ToolbarHeight = 80;
    private const int ToolbarPadding = 12;
    private const int ToolbarButtonSize = 56;
    private const int TaskbarHeight = 60;
    private const float TileElevation = 8f;
    private const float MachineElevation = 12f;
    private static readonly int[] ResearchTargets = { 10, 20, 30, 40, 60, 80, 100, 150 };

    private readonly Tile[,] _tiles = new Tile[MapWidth, MapHeight];
    private readonly List<Item> _items = new();  // 保留旧的物品列表（兼容）
    private readonly Dictionary<Point, ConveyorEntity> _conveyors = new();  // 新增：传送带实体
    private readonly Dictionary<Point, MinerState> _miners = new();
    private readonly Dictionary<Point, ProcessorState> _processors = new();
    private readonly Dictionary<Point, StorageState> _storages = new();
    private readonly Dictionary<Point, int> _splitterIndex = new();
    private readonly bool[,] _oreMap = new bool[MapWidth, MapHeight];
    private readonly float[,] _congestion = new float[MapWidth, MapHeight];
    // 新增：多种矿石类型地图
    private readonly OreType[,] _oreTypeMap = new OreType[MapWidth, MapHeight];
    // 新增：地形地图
    private readonly TerrainType[,] _terrainMap = new TerrainType[MapWidth, MapHeight];
    // 新增：区域系统
    private readonly List<RegionInfo> _regions = new();
    // 新增：地下传送带状态
    private readonly Dictionary<Point, UndergroundState> _undergrounds = new();
    // 新增：燃煤发电机状态
    private readonly Dictionary<Point, CoalGeneratorState> _coalGenerators = new();
    // 新增：路由器状态
    private readonly Dictionary<Point, RouterState> _routers = new();
    
    // 粒子效果系统
    private readonly List<Particle> _particles = new();
    
    // 调试模式
    private bool _debugMode = false;
    private string _debugMessage = "";
    private float _debugMessageTimer = 0f;

    private readonly InputState _input = new();
    private readonly Camera2D _camera = new();

    private GraphicsDeviceManager _graphics = null!;
    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
    private readonly Dictionary<TileType, Texture2D> _tileTextures = new();
    private readonly Dictionary<ItemType, Texture2D> _itemTextures = new();
    private Texture2D _tileMask = null!;
    private SoundEffect _sfxPlace = null!;
    private SoundEffect _sfxRotate = null!;
    private SoundEffect _sfxError = null!;
    private SoundEffect _sfxUnlock = null!;
    private readonly Dictionary<string, SystemTextEntry> _systemTextCache = new();
    private readonly LinkedList<string> _systemTextLru = new();
    private const int SystemTextCacheLimit = 200;

    private sealed class SystemTextEntry
    {
        public required Texture2D Texture;
        public int Width;
        public int Height;
        public required LinkedListNode<string> Node;
    }

    private Tool _tool = Tool.Conveyor;
    private Direction _direction = Direction.East;
    private bool _paused;
    private bool _autoPaused;
    private Vector2 _origin;
    private float _titleTimer;
    private Rectangle[] _toolButtons = Array.Empty<Rectangle>();
    private Rectangle _directionButton;
    // 材料库存系统
    private readonly Dictionary<ItemType, int> _inventory = new()
    {
        { ItemType.Plate, 100 },      // 初始铁板
        { ItemType.Gear, 50 },        // 初始齿轮
        { ItemType.CopperPlate, 20 }, // 初始铜板
    };
    private int _totalOreStored;
    private int _totalPlatesStored;
    private int _totalGearStored;
    private int _totalScienceStored;
    private int _researchPoints;
    private float _researchToastTimer;
    private string _researchToast = string.Empty;
    private int _powerProduced;
    private int _powerUsed;
    private float _powerRatio = 1f;
    private float _elapsed;
    private readonly Queue<float> _plateDeliveries = new();
    private readonly Queue<float> _scienceDeliveries = new();
    private Tool[] _toolbarTools = Array.Empty<Tool>();
    private readonly Random _random = new();
    private bool _unlockSplitter;
    private bool _unlockMerger;
    private bool _unlockRouter = true;  // 路由器默认解锁
    private bool _unlockAssembler = true;
    private bool _unlockLab = true;
    private bool _unlockGenerator;
    private bool _unlockFastConveyor;
    // 新增解锁项
    private bool _unlockUnderground;
    private bool _unlockCoalGenerator;
    private bool _unlockAdvancedMiner;
    private bool _unlockAssemblerMk2;
    private bool _unlockChemicalPlant;
    // 新增统计
    private int _totalCopperStored;
    private int _totalCoalStored;
    private int _totalCircuitStored;
    private int _totalSteelStored;
    // 新增成就系统
    private readonly HashSet<string> _achievements = new();
    private string _achievementToast = string.Empty;
    private float _achievementToastTimer;
    private string? _hoverTooltip;
    private Vector2 _hoverTooltipPos;
    private bool _showSettings;
    private bool _showGrid = true;
    private bool _showOreHighlight = true;
    private bool _showTooltips = true;
    private bool _autoPauseOnFocusLoss = true;
    private float _sfxVolume = 0.6f;
    private bool _showTutorial = true;
    private bool _developerMode;
    private bool _developerApplied;
    private bool _showDevMenu;
    private bool _showRecipeBook;
    private int _recipeBookPage;
    private int _inventoryScrollOffset;  // 库存滚动偏移
    private int _tutorialStep;
    private bool _tutorialDone;
    private Language _language = Language.Chinese;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            SynchronizeWithVerticalRetrace = true
        };
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += (_, _) =>
        {
            UpdateOrigin();
            UpdateUiLayout();
        };
    }

    protected override void Initialize()
    {
        base.Initialize();
        UpdateOrigin();
        UpdateUiLayout();
        ClearMap();
        GenerateResourceMap();
        CreateDemoLayout();
        RebuildToolbar();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _tileMask = CreateDiamondTexture(TileWidth, TileHeight, Color.White, new Color(0, 0, 0, 0), 0.05f);

        // 加载建筑贴图（优先从文件加载，否则使用程序化生成）
        LoadTileTextures();

        // 加载物品贴图
        LoadItemTextures();

        _sfxPlace = CreateTone(520, 0.10f, 0.18f);
        _sfxRotate = CreateTone(420, 0.08f, 0.14f);
        _sfxError = CreateTone(220, 0.14f, 0.20f);
        _sfxUnlock = CreateTone(640, 0.16f, 0.20f);
    }

    private void LoadTileTextures()
    {
        // 优先从项目目录加载，其次从运行时目录加载
        string projectPath = @"C:\Users\Administrator\Desktop\新建文件夹\fna\Content\Textures\Tiles";
        string runtimePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "Textures", "Tiles");
        string texturePath = Directory.Exists(projectPath) ? projectPath : runtimePath;

        // 建筑贴图配置：类型 -> (填充色, 边框色)
        var tileConfigs = new Dictionary<TileType, (Color fill, Color border)>
        {
            { TileType.Empty, (new Color(65, 70, 75), new Color(45, 48, 52)) },
            { TileType.Conveyor, (new Color(85, 105, 125), new Color(60, 75, 95)) },
            { TileType.FastConveyor, (new Color(95, 115, 140), new Color(70, 85, 105)) },
            { TileType.Splitter, (new Color(90, 110, 135), new Color(65, 80, 100)) },
            { TileType.Merger, (new Color(85, 110, 125), new Color(60, 80, 95)) },
            { TileType.Router, (new Color(100, 115, 140), new Color(70, 85, 105)) },
            { TileType.Miner, (new Color(95, 120, 95), new Color(65, 85, 65)) },
            { TileType.Smelter, (new Color(140, 110, 85), new Color(100, 75, 60)) },
            { TileType.Assembler, (new Color(115, 100, 130), new Color(80, 65, 95)) },
            { TileType.Lab, (new Color(90, 120, 130), new Color(65, 90, 100)) },
            { TileType.Generator, (new Color(130, 130, 100), new Color(95, 95, 70)) },
            { TileType.Storage, (new Color(120, 115, 85), new Color(85, 80, 60)) },
            { TileType.UndergroundEntry, (new Color(75, 95, 115), new Color(55, 70, 85)) },
            { TileType.UndergroundExit, (new Color(75, 95, 115), new Color(55, 70, 85)) },
            { TileType.CoalGenerator, (new Color(100, 90, 75), new Color(70, 60, 50)) },
            { TileType.AdvancedMiner, (new Color(105, 130, 105), new Color(75, 95, 75)) },
            { TileType.AssemblerMk2, (new Color(125, 105, 140), new Color(90, 75, 105)) },
            { TileType.ChemicalPlant, (new Color(105, 135, 115), new Color(75, 100, 85)) },
        };

        foreach (var (tileType, colors) in tileConfigs)
        {
            string filePath = Path.Combine(texturePath, $"{tileType}.png");
            if (File.Exists(filePath))
            {
                // 从文件加载贴图
                _tileTextures[tileType] = LoadTextureFromFile(filePath);
            }
            else
            {
                // 使用程序化生成
                _tileTextures[tileType] = CreateDiamondTexture(TileWidth, TileHeight, colors.fill, colors.border, 0.06f);
            }
        }
    }

    private void LoadItemTextures()
    {
        // 优先从项目目录加载，其次从运行时目录加载
        string projectPath = @"C:\Users\Administrator\Desktop\新建文件夹\fna\Content\Textures\Items";
        string runtimePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "Textures", "Items");
        string texturePath = Directory.Exists(projectPath) ? projectPath : runtimePath;

        // 物品贴图配置：类型 -> 颜色
        var itemConfigs = new Dictionary<ItemType, Color>
        {
            { ItemType.Ore, new Color(120, 140, 160) },           // 铁矿石 - 蓝灰色
            { ItemType.Plate, new Color(180, 190, 200) },         // 铁板 - 银色
            { ItemType.Gear, new Color(200, 180, 140) },          // 齿轮 - 金属黄
            { ItemType.Science, new Color(100, 180, 220) },       // 科学包 - 蓝色
            { ItemType.CopperOre, new Color(180, 100, 60) },      // 铜矿石 - 橙棕色
            { ItemType.CopperPlate, new Color(220, 140, 80) },    // 铜板 - 铜色
            { ItemType.Coal, new Color(50, 50, 55) },             // 煤炭 - 黑色
            { ItemType.GoldOre, new Color(200, 170, 50) },        // 金矿石 - 暗金色
            { ItemType.GoldPlate, new Color(255, 215, 0) },       // 金板 - 金色
            { ItemType.TitaniumOre, new Color(160, 170, 180) },   // 钛矿石 - 银灰色
            { ItemType.TitaniumPlate, new Color(200, 210, 220) }, // 钛板 - 亮银色
            { ItemType.UraniumOre, new Color(80, 160, 80) },      // 铀矿石 - 暗绿色
            { ItemType.UraniumPlate, new Color(100, 220, 100) },  // 铀板 - 亮绿色
            { ItemType.CopperWire, new Color(200, 120, 60) },     // 铜线 - 铜色
            { ItemType.Circuit, new Color(60, 140, 60) },         // 电路板 - 绿色
            { ItemType.Steel, new Color(160, 165, 170) },         // 钢材 - 钢灰色
            { ItemType.RedScience, new Color(220, 80, 80) },      // 红色科学包
            { ItemType.GreenScience, new Color(80, 200, 80) },    // 绿色科学包
        };

        foreach (var (itemType, color) in itemConfigs)
        {
            string filePath = Path.Combine(texturePath, $"{itemType}.png");
            if (File.Exists(filePath))
            {
                // 从文件加载贴图
                _itemTextures[itemType] = LoadTextureFromFile(filePath);
            }
            else
            {
                // 使用程序化生成圆形物品贴图
                _itemTextures[itemType] = CreateItemTexture(16, color);
            }
        }
    }

    private Texture2D LoadTextureFromFile(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Texture2D.FromStream(GraphicsDevice, stream);
    }

    private Texture2D CreateItemTexture(int size, Color color)
    {
        var texture = new Texture2D(GraphicsDevice, size, size);
        Color[] data = new Color[size * size];
        int border = 1; // 边框宽度

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 方形填充，带1像素边框
                if (x >= border && x < size - border && y >= border && y < size - border)
                {
                    data[y * size + x] = color;
                }
                else
                {
                    // 边框颜色（稍暗）
                    data[y * size + x] = new Color(
                        (byte)(color.R * 0.6f),
                        (byte)(color.G * 0.6f),
                        (byte)(color.B * 0.6f),
                        (byte)255
                    );
                }
            }
        }

        texture.SetData(data);
        return texture;
    }

    /// <summary>
    /// 一键生成所有贴图文件到 Content/Textures 目录
    /// </summary>
    public void GenerateAllTextures()
    {
        // 生成到项目根目录的 Content/Textures
        string projectRoot = @"C:\Users\Administrator\Desktop\新建文件夹\fna";
        string basePath = Path.Combine(projectRoot, "Content", "Textures");
        string tilesPath = Path.Combine(basePath, "Tiles");
        string itemsPath = Path.Combine(basePath, "Items");

        // 创建目录
        Directory.CreateDirectory(tilesPath);
        Directory.CreateDirectory(itemsPath);

        // 建筑贴图配置
        var tileConfigs = new Dictionary<TileType, (Color fill, Color border)>
        {
            { TileType.Empty, (new Color(65, 70, 75), new Color(45, 48, 52)) },
            { TileType.Conveyor, (new Color(85, 105, 125), new Color(60, 75, 95)) },
            { TileType.FastConveyor, (new Color(95, 115, 140), new Color(70, 85, 105)) },
            { TileType.Splitter, (new Color(90, 110, 135), new Color(65, 80, 100)) },
            { TileType.Merger, (new Color(85, 110, 125), new Color(60, 80, 95)) },
            { TileType.Router, (new Color(100, 115, 140), new Color(70, 85, 105)) },
            { TileType.Miner, (new Color(95, 120, 95), new Color(65, 85, 65)) },
            { TileType.Smelter, (new Color(140, 110, 85), new Color(100, 75, 60)) },
            { TileType.Assembler, (new Color(115, 100, 130), new Color(80, 65, 95)) },
            { TileType.Lab, (new Color(90, 120, 130), new Color(65, 90, 100)) },
            { TileType.Generator, (new Color(130, 130, 100), new Color(95, 95, 70)) },
            { TileType.Storage, (new Color(120, 115, 85), new Color(85, 80, 60)) },
            { TileType.UndergroundEntry, (new Color(75, 95, 115), new Color(55, 70, 85)) },
            { TileType.UndergroundExit, (new Color(75, 95, 115), new Color(55, 70, 85)) },
            { TileType.CoalGenerator, (new Color(100, 90, 75), new Color(70, 60, 50)) },
            { TileType.AdvancedMiner, (new Color(105, 130, 105), new Color(75, 95, 75)) },
            { TileType.AssemblerMk2, (new Color(125, 105, 140), new Color(90, 75, 105)) },
            { TileType.ChemicalPlant, (new Color(105, 135, 115), new Color(75, 100, 85)) },
        };

        // 生成建筑贴图
        foreach (var (tileType, colors) in tileConfigs)
        {
            string filePath = Path.Combine(tilesPath, $"{tileType}.png");
            SaveDiamondTexture(filePath, TileWidth, TileHeight, colors.fill, colors.border, 0.06f);
        }

        // 物品贴图配置
        var itemConfigs = new Dictionary<ItemType, Color>
        {
            { ItemType.Ore, new Color(120, 140, 160) },           // 铁矿石 - 蓝灰色
            { ItemType.Plate, new Color(180, 190, 200) },         // 铁板 - 银色
            { ItemType.Gear, new Color(200, 180, 140) },          // 齿轮 - 金属黄
            { ItemType.Science, new Color(100, 180, 220) },       // 科学包 - 蓝色
            { ItemType.CopperOre, new Color(180, 100, 60) },      // 铜矿石 - 橙棕色
            { ItemType.CopperPlate, new Color(220, 140, 80) },    // 铜板 - 铜色
            { ItemType.Coal, new Color(50, 50, 55) },             // 煤炭 - 黑色
            { ItemType.GoldOre, new Color(200, 170, 50) },        // 金矿石 - 暗金色
            { ItemType.GoldPlate, new Color(255, 215, 0) },       // 金板 - 金色
            { ItemType.TitaniumOre, new Color(160, 170, 180) },   // 钛矿石 - 银灰色
            { ItemType.TitaniumPlate, new Color(200, 210, 220) }, // 钛板 - 亮银色
            { ItemType.UraniumOre, new Color(80, 160, 80) },      // 铀矿石 - 暗绿色
            { ItemType.UraniumPlate, new Color(100, 220, 100) },  // 铀板 - 亮绿色
            { ItemType.CopperWire, new Color(200, 120, 60) },     // 铜线 - 铜色
            { ItemType.Circuit, new Color(60, 140, 60) },         // 电路板 - 绿色
            { ItemType.Steel, new Color(160, 165, 170) },         // 钢材 - 钢灰色
            { ItemType.RedScience, new Color(220, 80, 80) },      // 红色科学包
            { ItemType.GreenScience, new Color(80, 200, 80) },    // 绿色科学包
        };

        // 生成物品贴图
        foreach (var (itemType, color) in itemConfigs)
        {
            string filePath = Path.Combine(itemsPath, $"{itemType}.png");
            SaveSquareTexture(filePath, 16, color);
        }

        System.Diagnostics.Debug.WriteLine($"贴图已生成到: {basePath}");
    }

    /// <summary>
    /// 保存菱形贴图为PNG文件
    /// </summary>
    private void SaveDiamondTexture(string filePath, int width, int height, Color fill, Color border, float borderThickness)
    {
        Color[] data = new Color[width * height];
        float halfW = width / 2f;
        float halfH = height / 2f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = MathF.Abs(x - halfW) / halfW;
                float dy = MathF.Abs(y - halfH) / halfH;
                float distance = dx + dy;
                if (distance <= 1f)
                {
                    float edge = 1f - distance;
                    if (edge < borderThickness)
                    {
                        data[y * width + x] = border;
                    }
                    else
                    {
                        data[y * width + x] = fill;
                    }
                }
                else
                {
                    data[y * width + x] = new Color(0, 0, 0, 0);
                }
            }
        }

        SaveColorArrayToPng(filePath, width, height, data);
    }

    /// <summary>
    /// 保存方形贴图为PNG文件
    /// </summary>
    private void SaveSquareTexture(string filePath, int size, Color color)
    {
        Color[] data = new Color[size * size];
        int border = 1; // 边框宽度

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 方形填充，带1像素边框
                if (x >= border && x < size - border && y >= border && y < size - border)
                {
                    data[y * size + x] = color;
                }
                else
                {
                    // 边框颜色（稍暗）
                    data[y * size + x] = new Color(
                        (byte)(color.R * 0.6f),
                        (byte)(color.G * 0.6f),
                        (byte)(color.B * 0.6f),
                        (byte)255
                    );
                }
            }
        }

        SaveColorArrayToPng(filePath, size, size, data);
    }

    /// <summary>
    /// 将颜色数组保存为PNG文件
    /// </summary>
    private void SaveColorArrayToPng(string filePath, int width, int height, Color[] data)
    {
        using var bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color c = data[y * width + x];
                bitmap.SetPixel(x, y, System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B));
            }
        }

        bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
    }

    protected override void Update(GameTime gameTime)
    {
        _input.Update();

        if (_input.KeyPressed(Keys.Escape))
        {
            _showSettings = !_showSettings;
            return;
        }

        HandleInput(gameTime);
        ApplyDeveloperMode();

        if (!_paused)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _elapsed += dt;
            TrimQueues();
            UpdatePower();
            UpdateCoalGenerators(dt);
            UpdateCongestion(dt);
            UpdateItems(dt);
            ConsumeProcessorInputs();
            UpdateProcessors(dt);
            UpdateMiners(dt);
            ConsumeStorageItems();
            ConsumeCoalGeneratorFuel();
            if (_researchToastTimer > 0f)
            {
                _researchToastTimer -= dt;
            }
            UpdateTutorialProgress();
            UpdateParticles(dt);
            
            // 更新调试消息计时器
            if (_debugMessageTimer > 0f)
            {
                _debugMessageTimer -= dt;
            }
        }

        UpdateWindowTitle((float)gameTime.ElapsedGameTime.TotalSeconds);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(28, 32, 38));  // 柔和的深蓝灰背景

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);

        _hoverTooltip = null;
        DrawTiles();
        DrawItems();
        DrawParticles();
        DrawBuildingPreview();
        DrawToolbar();
        DrawHover();
        DrawTaskbar();
        DrawTooltip();
        DrawTutorial();
        DrawRecipeBook();
        DrawSettings();
        DrawDevMenu();
        DrawDebugMessage();

        _spriteBatch.End();

        base.Draw(gameTime);
    }

    private void HandleInput(GameTime gameTime)
    {
        if (!IsActive)
        {
            return;
        }

        if (_input.KeyPressed(Keys.F1))
        {
            _showSettings = !_showSettings;
        }

        if (_showSettings)
        {
            HandleSettingsInput();
            if (_showDevMenu)
            {
                HandleDevMenuInput();
            }
            return;
        }

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        float panSpeed = 600f * dt / MathF.Max(_camera.Zoom, 0.01f);

        if (_input.KeyDown(Keys.W) || _input.KeyDown(Keys.Up))
        {
            _camera.Pan += new Vector2(0f, panSpeed);
        }
        if (_input.KeyDown(Keys.S) || _input.KeyDown(Keys.Down))
        {
            _camera.Pan += new Vector2(0f, -panSpeed);
        }
        if (_input.KeyDown(Keys.A) || _input.KeyDown(Keys.Left))
        {
            _camera.Pan += new Vector2(panSpeed, 0f);
        }
        if (_input.KeyDown(Keys.D) || _input.KeyDown(Keys.Right))
        {
            _camera.Pan += new Vector2(-panSpeed, 0f);
        }

        int scroll = _input.ScrollDelta;
        if (scroll != 0)
        {
            _camera.Zoom = MathHelper.Clamp(_camera.Zoom + scroll * 0.0015f, 0.5f, 2.5f);
        }

        if (_input.KeyPressed(Keys.OemPlus) || _input.KeyPressed(Keys.Add))
        {
            _camera.Zoom = MathHelper.Clamp(_camera.Zoom + 0.1f, 0.5f, 2.5f);
        }
        if (_input.KeyPressed(Keys.OemMinus) || _input.KeyPressed(Keys.Subtract))
        {
            _camera.Zoom = MathHelper.Clamp(_camera.Zoom - 0.1f, 0.5f, 2.5f);
        }

        if (_input.KeyPressed(Keys.Space))
        {
            _paused = !_paused;
        }

        if (_input.KeyPressed(Keys.F2))
        {
            _showTutorial = !_showTutorial;
        }

        if (_input.KeyPressed(Keys.F3))
        {
            _showRecipeBook = !_showRecipeBook;
        }

        // F5 保存游戏
        if (_input.KeyPressed(Keys.F5))
        {
            SaveGame();
        }

        // F6 切换调试模式
        if (_input.KeyPressed(Keys.F6))
        {
            _debugMode = !_debugMode;
            _debugMessage = $"调试模式已{(_debugMode ? "启用" : "禁用")}";
            _debugMessageTimer = 2f; // 显示2秒
        }

        // F9 加载游戏
        if (_input.KeyPressed(Keys.F9))
        {
            LoadGame();
        }

        if (_input.KeyPressed(Keys.D1)) SelectTool(Tool.Conveyor);
        if (_input.KeyPressed(Keys.D2)) SelectTool(Tool.Miner);
        if (_input.KeyPressed(Keys.D3)) SelectTool(Tool.Smelter);
        if (_input.KeyPressed(Keys.D4)) SelectTool(Tool.Storage);
        if (_input.KeyPressed(Keys.D5)) SelectTool(Tool.Splitter);
        if (_input.KeyPressed(Keys.D6)) SelectTool(Tool.Merger);
        if (_input.KeyPressed(Keys.D7)) SelectTool(Tool.Assembler);
        if (_input.KeyPressed(Keys.D8)) SelectTool(Tool.Lab);
        if (_input.KeyPressed(Keys.D9)) SelectTool(Tool.Generator);
        if (_input.KeyPressed(Keys.D0)) _tool = Tool.Erase;

        if (_input.KeyPressed(Keys.R))
        {
            _direction = DirectionUtil.RotateCW(_direction);
        }

        bool uiConsumed = HandleToolbarClick();

        Point? hover = GetHoveredTile();
        if (!uiConsumed && hover.HasValue)
        {
            if (_input.LeftClicked)
            {
                // 检查是否点击了锁定区域
                if (_terrainMap[hover.Value.X, hover.Value.Y] == TerrainType.Locked)
                {
                    TryUnlockRegion(hover.Value);
                }
                else
                {
                    PlaceTile(hover.Value);
                }
            }
            if (_input.RightClicked)
            {
                RotateTile(hover.Value);
            }
        }


    }

    private void ApplyDeveloperMode()
    {
        if (!_developerMode)
        {
            _developerApplied = false;
            return;
        }

        if (_developerApplied)
        {
            return;
        }

        // 解锁所有建筑
        _unlockSplitter = true;
        _unlockMerger = true;
        _unlockRouter = true;
        _unlockAssembler = true;
        _unlockLab = true;
        _unlockGenerator = true;
        _unlockFastConveyor = true;
        _unlockUnderground = true;
        _unlockCoalGenerator = true;
        _unlockAdvancedMiner = true;
        _unlockAssemblerMk2 = true;
        _unlockChemicalPlant = true;

        // 解锁所有区域
        for (int i = 0; i < _regions.Count; i++)
        {
            if (!_regions[i].IsUnlocked)
            {
                UnlockRegionTerrain(i);
            }
        }

        _developerApplied = true;
        RebuildToolbar();
    }

    private void UpdateItems(float dt)
    {
        // 使用新的传送带系统更新物品
        UpdateConveyors(dt);

        // 保留旧的物品更新逻辑（用于兼容非传送带上的物品）
        if (_items.Count == 0)
        {
            return;
        }

        var occupied = new HashSet<Point>(_items.Select(item => item.Tile));
        var reserved = new HashSet<Point>();

        foreach (var item in _items)
        {
            if (item.Dir != Point.Zero)
            {
                item.Progress += item.Speed * dt;
                if (item.Progress >= 1f)
                {
                    occupied.Remove(item.Tile);
                    item.Tile = new Point(item.Tile.X + item.Dir.X, item.Tile.Y + item.Dir.Y);
                    occupied.Add(item.Tile);
                    item.Progress = 0f;
                    item.Dir = Point.Zero;
                }
                continue;
            }

            if (!TryGetNextMove(item.Tile, occupied, reserved, out Point dir, out Point next))
            {
                _congestion[item.Tile.X, item.Tile.Y] = MathF.Min(1f, _congestion[item.Tile.X, item.Tile.Y] + 0.25f);
                continue;
            }

            item.Dir = dir;
            item.Progress = 0f;
            item.Speed = GetTileSpeed(_tiles[item.Tile.X, item.Tile.Y].Type);
            reserved.Add(next);
        }
    }

    // 传送带最小移动阈值（防止抖动）
    private const float MinMove = 1f / (short.MaxValue - 2);

    /// <summary>
    /// 更新所有传送带 - 参考 Mindustry 的 Conveyor.update()
    /// </summary>
    private void UpdateConveyors(float dt)
    {
        // 先复制键列表，避免在遍历时修改集合
        var conveyorKeys = _conveyors.Keys.ToList();

        foreach (var tilePos in conveyorKeys)
        {
            if (!_conveyors.TryGetValue(tilePos, out var entity)) continue;

            if (!InBounds(tilePos)) continue;

            Tile tile = _tiles[tilePos.X, tilePos.Y];
            if (tile.Type != TileType.Conveyor && tile.Type != TileType.FastConveyor)
            {
                continue;
            }

            // 获取传送带速度（参考 Mindustry: speed * Timers.delta()）
            float speed = tile.Type == TileType.FastConveyor ? FastConveyorSpeed * dt : ConveyorSpeed * dt;

            // 重置最小物品位置
            entity.MinItem = 1f;

            int minRemove = int.MaxValue;

            // 从后向前遍历物品（参考 Mindustry）
            for (int i = entity.Items.Count - 1; i >= 0; i--)
            {
                long packed = entity.Items[i];
                ItemType itemType = ConveyorItemPos.GetItemType(packed);
                float x = ConveyorItemPos.GetX(packed);
                float y = ConveyorItemPos.GetY(packed);
                short seed = ConveyorItemPos.GetSeed(packed);

                // 计算下一个物品的位置（用于间距控制）
                float nextPos = (i == entity.Items.Count - 1) ? 100f : ConveyorItemPos.GetY(entity.Items[i + 1]);
                float maxMove = MathF.Min(nextPos - ConveyorEntity.ItemSpace - y, speed);

                // 使用 MinMove 阈值防止微小移动导致抖动
                if (maxMove > MinMove)
                {
                    // 移动物品
                    y += maxMove;
                    // 横向偏移逐渐归零（使用 LerpDelta 风格）
                    x = LerpDelta(x, 0f, 0.06f, dt);
                }
                // 被阻挡时不改变 X 位置，保持排队状态

                y = Math.Clamp(y, 0f, 1f);

                // 检查是否到达传送带末端
                if (y >= 0.9999f)
                {
                    // 尝试输出到下一个格子
                    if (TryOffloadConveyorItem(tilePos, tile.Direction, itemType))
                    {
                        minRemove = Math.Min(i, minRemove);
                        continue;
                    }
                }

                // 更新最小位置
                if (y < entity.MinItem)
                {
                    entity.MinItem = y;
                }

                // 重新打包并更新
                entity.Items[i] = ConveyorItemPos.Pack(itemType, x, y, seed);
            }

            // 移除已输出的物品
            if (minRemove != int.MaxValue)
            {
                entity.Truncate(minRemove);
            }
        }
    }

    /// <summary>
    /// Mindustry 风格的 LerpDelta - 帧率无关的插值
    /// </summary>
    private float LerpDelta(float from, float to, float speed, float dt)
    {
        return from + (to - from) * Math.Clamp(speed * dt * 60f, 0f, 1f);
    }

    /// <summary>
    /// 尝试将物品从传送带输出到下一个格子
    /// </summary>
    private bool TryOffloadConveyorItem(Point fromTile, Direction direction, ItemType itemType)
    {
        Point dir = DirectionUtil.ToPoint(direction);
        Point nextTile = new Point(fromTile.X + dir.X, fromTile.Y + dir.Y);

        if (!InBounds(nextTile)) return false;

        Tile next = _tiles[nextTile.X, nextTile.Y];

        // 输出到传送带
        if (next.Type == TileType.Conveyor || next.Type == TileType.FastConveyor)
        {
            // 检查是否反向输入（Mindustry: (source.getRotation() + 2) % 4 == tile.getRotation()）
            // 如果目标传送带的方向正好指向来源，则不能输入
            Direction oppositeDir = GetOppositeDirection(direction);
            if (next.Direction == oppositeDir)
            {
                return false; // 不能反向输入
            }

            // 检查目标传送带是否可以接受
            if (!_conveyors.TryGetValue(nextTile, out var nextEntity))
            {
                nextEntity = new ConveyorEntity();
                _conveyors[nextTile] = nextEntity;
            }

            // 计算输入方向（是否从侧面）
            int relativeDir = GetRelativeDirection(fromTile, nextTile, next.Direction);
            bool fromSide = relativeDir != 0 && relativeDir != 2;

            if (!nextEntity.CanAccept(fromSide)) return false;

            // 计算初始位置（参考 Mindustry handleItem）
            float startY = 0f;
            float startX = 0f;

            if (fromSide)
            {
                startY = 0.5f;
                // 根据相对方向决定 X 偏移
                startX = (relativeDir == 1 || relativeDir == -3) ? -0.9f : 0.9f;
            }

            nextEntity.AddItem(itemType, startX, startY);
            return true;
        }

        // 输出到处理器
        if (IsProcessorType(next.Type))
        {
            // 查找处理器
            Point processorPos = nextTile;
            if (next.ParentTile.HasValue)
            {
                processorPos = next.ParentTile.Value;
            }

            if (_processors.TryGetValue(processorPos, out var processor))
            {
                // 初始化配方
                if (processor.Recipe == null)
                {
                    processor.Recipe = Recipes.GetRecipe(_tiles[processorPos.X, processorPos.Y].Type, itemType);
                    if (processor.Recipe?.Fuel != null)
                    {
                        processor.RequiresFuel = true;
                    }
                }

                // 检查是否可以接受
                if (processor.CanAcceptItem(itemType))
                {
                    processor.AddItem(itemType);
                    return true;
                }
                if (processor.CanAcceptFuel(itemType))
                {
                    processor.AddFuel(processor.Recipe?.FuelDuration ?? 30f);
                    return true;
                }
            }
        }

        // 输出到仓库
        if (next.Type == TileType.Storage)
        {
            if (_storages.TryGetValue(nextTile, out var storage))
            {
                storage.Count++;
                // 添加到材料库存
                AddToInventory(itemType, 1);
                // 更新统计
                UpdateStorageStats(itemType);
                return true;
            }
            return false;
        }

        // 输出到路由器 - 尝试直接转发到下一个传送带
        if (next.Type == TileType.Router)
        {
            return TryRouterForward(nextTile, itemType, direction);
        }

        // 输出到分流器
        if (next.Type == TileType.Splitter)
        {
            return TrySplitterForward(nextTile, itemType, direction);
        }

        // 输出到合并器 - 直接转发
        if (next.Type == TileType.Merger)
        {
            return TryMergerForward(nextTile, itemType, direction);
        }

        return false;
    }

    /// <summary>
    /// 路由器转发物品到下一个传送带
    /// </summary>
    private bool TryRouterForward(Point routerTile, ItemType itemType, Direction inputDir)
    {
        var outputs = GetRouterOutputs(routerTile);
        if (outputs.Length == 0) return false;

        int index = _routers.TryGetValue(routerTile, out var state) ? state.OutputIndex : 0;

        for (int i = 0; i < outputs.Length; i++)
        {
            Direction outDir = outputs[(index + i) % outputs.Length];
            Point outStep = DirectionUtil.ToPoint(outDir);
            Point candidate = new Point(routerTile.X + outStep.X, routerTile.Y + outStep.Y);

            if (!InBounds(candidate)) continue;

            Tile targetTile = _tiles[candidate.X, candidate.Y];

            // 输出到传送带
            if (targetTile.Type == TileType.Conveyor || targetTile.Type == TileType.FastConveyor)
            {
                // 检查是否反向
                if (targetTile.Direction == GetOppositeDirection(outDir)) continue;

                if (!_conveyors.TryGetValue(candidate, out var conveyorEntity))
                {
                    conveyorEntity = new ConveyorEntity();
                    _conveyors[candidate] = conveyorEntity;
                }

                if (conveyorEntity.CanAccept(false))
                {
                    conveyorEntity.AddItem(itemType, 0f, 0f);
                    if (!_routers.ContainsKey(routerTile))
                    {
                        _routers[routerTile] = new RouterState();
                    }
                    _routers[routerTile].OutputIndex = (index + i + 1) % outputs.Length;
                    return true;
                }
            }

            // 输出到处理器
            if (IsProcessorType(targetTile.Type))
            {
                Point processorPos = targetTile.ParentTile ?? candidate;
                if (_processors.TryGetValue(processorPos, out var processor))
                {
                    if (processor.Recipe == null)
                    {
                        processor.Recipe = Recipes.GetRecipe(_tiles[processorPos.X, processorPos.Y].Type, itemType);
                    }
                    if (processor.CanAcceptItem(itemType))
                    {
                        processor.AddItem(itemType);
                        if (!_routers.ContainsKey(routerTile))
                        {
                            _routers[routerTile] = new RouterState();
                        }
                        _routers[routerTile].OutputIndex = (index + i + 1) % outputs.Length;
                        return true;
                    }
                }
            }

            // 输出到仓库
            if (targetTile.Type == TileType.Storage)
            {
                if (_storages.TryGetValue(candidate, out var storage))
                {
                    storage.Count++;
                    if (!_routers.ContainsKey(routerTile))
                    {
                        _routers[routerTile] = new RouterState();
                    }
                    _routers[routerTile].OutputIndex = (index + i + 1) % outputs.Length;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 分流器转发物品
    /// </summary>
    private bool TrySplitterForward(Point splitterTile, ItemType itemType, Direction inputDir)
    {
        Tile splitter = _tiles[splitterTile.X, splitterTile.Y];
        Direction splitterDir = splitter.Direction;

        // 分流器有两个输出：左侧和右侧
        Direction leftDir = RotateDirection(splitterDir, -1);
        Direction rightDir = RotateDirection(splitterDir, 1);

        int index = _splitterIndex.TryGetValue(splitterTile, out var state) ? state : 0;
        Direction[] outputs = { leftDir, rightDir };

        for (int i = 0; i < 2; i++)
        {
            Direction outDir = outputs[(index + i) % 2];
            Point outStep = DirectionUtil.ToPoint(outDir);
            Point candidate = new Point(splitterTile.X + outStep.X, splitterTile.Y + outStep.Y);

            if (!InBounds(candidate)) continue;

            Tile targetTile = _tiles[candidate.X, candidate.Y];

            if (targetTile.Type == TileType.Conveyor || targetTile.Type == TileType.FastConveyor)
            {
                if (targetTile.Direction == GetOppositeDirection(outDir)) continue;

                if (!_conveyors.TryGetValue(candidate, out var conveyorEntity))
                {
                    conveyorEntity = new ConveyorEntity();
                    _conveyors[candidate] = conveyorEntity;
                }

                if (conveyorEntity.CanAccept(false))
                {
                    conveyorEntity.AddItem(itemType, 0f, 0f);
                    _splitterIndex[splitterTile] = (index + i + 1) % 2;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 合并器转发物品（直接向前输出）
    /// </summary>
    private bool TryMergerForward(Point mergerTile, ItemType itemType, Direction inputDir)
    {
        Tile merger = _tiles[mergerTile.X, mergerTile.Y];
        Direction outDir = merger.Direction;
        Point outStep = DirectionUtil.ToPoint(outDir);
        Point candidate = new Point(mergerTile.X + outStep.X, mergerTile.Y + outStep.Y);

        if (!InBounds(candidate)) return false;

        Tile targetTile = _tiles[candidate.X, candidate.Y];

        if (targetTile.Type == TileType.Conveyor || targetTile.Type == TileType.FastConveyor)
        {
            if (targetTile.Direction == GetOppositeDirection(outDir)) return false;

            if (!_conveyors.TryGetValue(candidate, out var conveyorEntity))
            {
                conveyorEntity = new ConveyorEntity();
                _conveyors[candidate] = conveyorEntity;
            }

            if (conveyorEntity.CanAccept(false))
            {
                conveyorEntity.AddItem(itemType, 0f, 0f);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 旋转方向（-1=逆时针, 1=顺时针）
    /// </summary>
    private Direction RotateDirection(Direction dir, int steps)
    {
        Direction[] dirs = { Direction.North, Direction.East, Direction.South, Direction.West };
        int idx = Array.IndexOf(dirs, dir);
        if (idx < 0) return dir;
        return dirs[(idx + steps + 4) % 4];
    }

    /// <summary>
    /// 获取相对方向（0=后方, 1=右侧, 2=前方, 3=左侧）
    /// </summary>
    private int GetRelativeDirection(Point from, Point to, Direction toDirection)
    {
        Point toDir = DirectionUtil.ToPoint(toDirection);
        Point fromDir = new Point(to.X - from.X, to.Y - from.Y);

        // 计算相对角度
        if (fromDir.X == toDir.X && fromDir.Y == toDir.Y) return 2; // 前方
        if (fromDir.X == -toDir.X && fromDir.Y == -toDir.Y) return 0; // 后方

        // 侧面
        return (fromDir.X * toDir.Y - fromDir.Y * toDir.X) > 0 ? 1 : 3;
    }

    /// <summary>
    /// 获取相反方向
    /// </summary>
    private Direction GetOppositeDirection(Direction dir)
    {
        return dir switch
        {
            Direction.North => Direction.South,
            Direction.South => Direction.North,
            Direction.East => Direction.West,
            Direction.West => Direction.East,
            _ => Direction.None
        };
    }

    /// <summary>
    /// 检查是否是处理器类型
    /// </summary>
    private bool IsProcessorType(TileType type)
    {
        return type == TileType.Smelter ||
               type == TileType.Assembler ||
               type == TileType.AssemblerMk2 ||
               type == TileType.Lab ||
               type == TileType.ChemicalPlant;
    }

    /// <summary>
    /// 线性插值
    /// </summary>
    private float Lerp(float a, float b, float t)
    {
        return a + (b - a) * Math.Clamp(t, 0f, 1f);
    }

    private void UpdateCongestion(float dt)
    {
        float decay = 0.6f * dt;
        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                if (_congestion[x, y] > 0f)
                {
                    _congestion[x, y] = MathF.Max(0f, _congestion[x, y] - decay);
                }
            }
        }
    }

    private void TrimQueues()
    {
        float cutoff = _elapsed - 60f;
        while (_plateDeliveries.Count > 0 && _plateDeliveries.Peek() < cutoff)
        {
            _plateDeliveries.Dequeue();
        }
        while (_scienceDeliveries.Count > 0 && _scienceDeliveries.Peek() < cutoff)
        {
            _scienceDeliveries.Dequeue();
        }
    }

    private void ConsumeProcessorInputs()
    {
        foreach (var pair in _processors)
        {
            var state = pair.Value;
            var tile = _tiles[pair.Key.X, pair.Key.Y];
            Point processorPos = pair.Key;
            Point processorSize = BuildingSize.GetSize(tile.Type);

            // 获取建筑外部的边缘位置（物品在传送带上，不在建筑内部）
            var edges = GetExternalEdges(processorPos, processorSize);

            // 遍历边缘寻找可接受的物品
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var item = _items[i];

                // 检查物品是否在边缘位置
                bool isOnEdge = false;
                Point edgePos = Point.Zero;
                foreach (var edge in edges)
                {
                    if (item.Tile == edge)
                    {
                        isOnEdge = true;
                        edgePos = edge;
                        break;
                    }
                }

                if (!isOnEdge) continue;

                // 检查物品是否正在移动向处理器，或者已经停止（被阻挡）
                // 物品必须满足以下条件之一才能被吸收：
                // 1. 物品已停止移动（Dir == Point.Zero）且传送带指向处理器
                // 2. 物品正在移动且移动方向指向处理器
                bool isMovingToProcessor = false;

                if (item.Dir == Point.Zero)
                {
                    // 物品已停止，检查所在传送带的方向是否指向处理器
                    if (InBounds(item.Tile))
                    {
                        Tile conveyorTile = _tiles[item.Tile.X, item.Tile.Y];
                        if (conveyorTile.Type == TileType.Conveyor || conveyorTile.Type == TileType.FastConveyor)
                        {
                            Point conveyorDir = DirectionUtil.ToPoint(conveyorTile.Direction);
                            Point nextPos = new Point(item.Tile.X + conveyorDir.X, item.Tile.Y + conveyorDir.Y);
                            // 检查下一个位置是否在处理器内部
                            if (IsInsideProcessor(nextPos, processorPos, processorSize))
                            {
                                isMovingToProcessor = true;
                            }
                        }
                    }
                }
                else
                {
                    // 物品正在移动，检查移动方向是否指向处理器
                    Point nextPos = new Point(item.Tile.X + item.Dir.X, item.Tile.Y + item.Dir.Y);
                    if (IsInsideProcessor(nextPos, processorPos, processorSize))
                    {
                        isMovingToProcessor = true;
                    }
                }

                if (!isMovingToProcessor) continue;

                // 初始化配方（非冶炼厂，冶炼厂在 HasEnoughInputs 中动态选择）
                if (state.Recipe == null && tile.Type != TileType.Smelter)
                {
                    state.Recipe = Recipes.GetRecipe(tile.Type, item.Type);
                    if (state.Recipe != null && state.Recipe.Fuel.HasValue)
                    {
                        state.RequiresFuel = true;
                    }
                }

                // 检查是否是燃料
                if (state.CanAcceptFuel(item.Type))
                {
                    state.AddFuel(state.Recipe?.FuelDuration ?? 30f);
                    _items.RemoveAt(i);

                    if (_debugMode)
                    {
                        Console.WriteLine($"处理器接收燃料: 位置=({processorPos.X},{processorPos.Y}), 燃料={item.Type}");
                    }
                    continue;
                }

                // 检查是否可以接受该物品
                if (state.CanAcceptItem(item.Type))
                {
                    state.AddItem(item.Type);
                    _items.RemoveAt(i);

                    if (_debugMode)
                    {
                        Console.WriteLine($"处理器接收物品: 位置=({processorPos.X},{processorPos.Y}), 物品={item.Type}, 缓冲区={state.InputBuffer.GetValueOrDefault(item.Type, 0)}");
                    }
                }
            }
        }
    }

    // 检查位置是否在处理器内部
    private bool IsInsideProcessor(Point pos, Point processorPos, Point processorSize)
    {
        // 处理器占用的格子范围：
        // X: processorPos.X 到 processorPos.X + processorSize.X - 1
        // Y: processorPos.Y - processorSize.Y + 1 到 processorPos.Y
        return pos.X >= processorPos.X && pos.X < processorPos.X + processorSize.X &&
               pos.Y <= processorPos.Y && pos.Y > processorPos.Y - processorSize.Y;
    }

    private void ConsumeStorageItems()
    {
        if (_storages.Count == 0 || _items.Count == 0)
        {
            return;
        }

        for (int i = _items.Count - 1; i >= 0; i--)
        {
            var item = _items[i];
            if (!InBounds(item.Tile))
            {
                continue;
            }

            if (_tiles[item.Tile.X, item.Tile.Y].Type != TileType.Storage)
            {
                continue;
            }

            if (_storages.TryGetValue(item.Tile, out var storage))
            {
                storage.Count += 1;
            }

            // 添加到材料库存
            AddToInventory(item.Type, 1);

            // 更新统计
            switch (item.Type)
            {
                case ItemType.Plate:
                    _totalPlatesStored += 1;
                    _plateDeliveries.Enqueue(_elapsed);
                    break;
                case ItemType.Gear:
                    _totalGearStored += 1;
                    break;
                case ItemType.Science:
                    _totalScienceStored += 1;
                    _researchPoints += 1;
                    _scienceDeliveries.Enqueue(_elapsed);
                    CheckResearchUnlocks();
                    break;
                case ItemType.Ore:
                    _totalOreStored += 1;
                    break;
                case ItemType.CopperPlate:
                    _totalCopperStored += 1;
                    break;
                case ItemType.Coal:
                    _totalCoalStored += 1;
                    break;
                case ItemType.Circuit:
                    _totalCircuitStored += 1;
                    _researchPoints += 2; // 电路板给更多研究点
                    CheckResearchUnlocks();
                    break;
                case ItemType.Steel:
                    _totalSteelStored += 1;
                    break;
                case ItemType.RedScience:
                    _researchPoints += 3;
                    CheckResearchUnlocks();
                    break;
                case ItemType.GreenScience:
                    _researchPoints += 5;
                    CheckResearchUnlocks();
                    break;
            }

            _items.RemoveAt(i);
            CheckAchievements();
        }
    }

    private void ConsumeCoalGeneratorFuel()
    {
        if (_coalGenerators.Count == 0 || _items.Count == 0)
        {
            return;
        }

        for (int i = _items.Count - 1; i >= 0; i--)
        {
            var item = _items[i];
            if (item.Type != ItemType.Coal)
            {
                continue;
            }

            if (!InBounds(item.Tile))
            {
                continue;
            }

            if (_tiles[item.Tile.X, item.Tile.Y].Type != TileType.CoalGenerator)
            {
                continue;
            }

            if (_coalGenerators.TryGetValue(item.Tile, out var state))
            {
                // 只有当燃料不足时才消耗煤炭
                if (!state.HasFuel || state.FuelTimer < 5f)
                {
                    state.HasFuel = true;
                    state.FuelTimer += CoalGeneratorState.FuelDuration;
                    _items.RemoveAt(i);
                }
            }
        }
    }

    private int GetItemValue(ItemType type, float multiplier)
    {
        int baseValue = type switch
        {
            ItemType.Ore => 1,
            ItemType.Plate => 5,
            ItemType.Gear => 3,
            ItemType.Science => 2,
            ItemType.CopperOre => 1,
            ItemType.CopperPlate => 4,
            ItemType.Coal => 2,
            ItemType.CopperWire => 3,
            ItemType.Circuit => 8,
            ItemType.Steel => 6,
            ItemType.RedScience => 10,
            ItemType.GreenScience => 15,
            _ => 1
        };
        return (int)(baseValue * multiplier);
    }

    private void UpdateProcessors(float dt)
    {
        float scaledDt = dt * _powerRatio;
        foreach (var pair in _processors)
        {
            var tile = _tiles[pair.Key.X, pair.Key.Y];
            var state = pair.Value;
            Point processorPos = pair.Key;
            Point processorSize = BuildingSize.GetSize(tile.Type);

            // 更新燃烧时间
            if (state.RequiresFuel && state.BurnTime > 0)
            {
                state.BurnTime -= scaledDt;
                if (state.BurnTime < 0) state.BurnTime = 0;
            }

            // 检查是否可以开始制作
            if (!state.IsCrafting && state.HasEnoughInputs())
            {
                // 如果需要燃料，检查燃料
                if (state.RequiresFuel && state.BurnTime <= 0)
                {
                    // 没有燃料，无法制作
                }
                else
                {
                    state.ConsumeInputs();
                    if (_debugMode)
                    {
                        Console.WriteLine($"处理器开始制作: 位置=({processorPos.X},{processorPos.Y}), 配方={state.Recipe?.Name}");
                    }
                }
            }

            // 更新制作进度
            if (state.IsCrafting)
            {
                // 如果需要燃料但没有燃料，暂停制作
                if (state.RequiresFuel && state.BurnTime <= 0)
                {
                    continue;
                }

                state.CraftTimer -= scaledDt;
                if (state.CraftTimer <= 0f)
                {
                    state.FinishCraft();

                    // 添加处理器完成粒子效果
                    Vector2 processorPos2 = new Vector2(processorPos.X + (processorSize.X - 1) * 0.5f, processorPos.Y - (processorSize.Y - 1) * 0.5f);
                    CreateParticles(processorPos2, ParticleType.Star, 6);

                    if (_debugMode)
                    {
                        Console.WriteLine($"处理器完成制作: 位置=({processorPos.X},{processorPos.Y}), 输出缓冲区={state.OutputBuffer.Count}");
                    }
                }
            }

            // 尝试输出物品（参考 Mindustry 的 tryDump 轮询机制）
            if (state.OutputBuffer.Count > 0)
            {
                TryDumpProcessorOutput(pair.Key, processorSize, state);
            }
        }
    }

    // 参考 Mindustry 的 tryDump 方法，轮询输出物品
    private void TryDumpProcessorOutput(Point processorPos, Point processorSize, ProcessorState state)
    {
        // 获取建筑外部的边缘位置（不是建筑本身占用的格子）
        var outputEdges = GetExternalEdges(processorPos, processorSize);
        var inputEdges = GetProcessorInputEdges(processorPos, processorSize);

        // 过滤出非输入边缘
        var validOutputEdges = new List<Point>();
        foreach (var edge in outputEdges)
        {
            if (!inputEdges.Contains(edge) && InBounds(edge))
            {
                validOutputEdges.Add(edge);
            }
        }

        if (validOutputEdges.Count == 0)
        {
            if (_debugMode)
            {
                Console.WriteLine($"处理器无有效输出边缘: 位置=({processorPos.X},{processorPos.Y}), 外部边缘数={outputEdges.Count}, 输入边缘数={inputEdges.Count}");
            }
            return;
        }

        // 轮询尝试输出
        int startIndex = state.DumpDirection % validOutputEdges.Count;
        for (int i = 0; i < validOutputEdges.Count; i++)
        {
            int idx = (startIndex + i) % validOutputEdges.Count;
            Point candidate = validOutputEdges[idx];

            // 检查位置是否被占用
            if (FindItemAt(candidate) != null) continue;

            // 检查目标是否可以接收物品
            Tile targetTile = _tiles[candidate.X, candidate.Y];
            if (!CanAcceptItemAt(candidate, targetTile.Type)) continue;

            // 输出物品
            var outputItem = state.TryTakeOutput();
            if (outputItem.HasValue)
            {
                // 如果目标是传送带，添加到传送带实体
                if (targetTile.Type == TileType.Conveyor || targetTile.Type == TileType.FastConveyor)
                {
                    if (!_conveyors.TryGetValue(candidate, out var conveyorEntity))
                    {
                        conveyorEntity = new ConveyorEntity();
                        _conveyors[candidate] = conveyorEntity;
                    }

                    if (conveyorEntity.CanAccept(false))
                    {
                        conveyorEntity.AddItem(outputItem.Value, 0f, 0f);
                    }
                    else
                    {
                        // 传送带满了，放回输出缓冲区
                        state.OutputBuffer.Insert(0, outputItem.Value);
                        continue;
                    }
                }
                else
                {
                    // 其他类型使用旧的物品列表
                    _items.Add(new Item { Type = outputItem.Value, Tile = candidate, Dir = Point.Zero, Progress = 0f });
                }

                state.DumpDirection = (idx + 1) % validOutputEdges.Count;

                if (_debugMode)
                {
                    Console.WriteLine($"处理器输出物品: 位置=({processorPos.X},{processorPos.Y}), 输出位置=({candidate.X},{candidate.Y}), 物品={outputItem.Value}");
                }
                return;
            }
        }
    }

    // 获取建筑外部的边缘位置（建筑周围一圈的格子）
    private List<Point> GetExternalEdges(Point pos, Point size)
    {
        var edges = new List<Point>();

        // 上边缘（建筑顶部上方）
        for (int dx = 0; dx < size.X; dx++)
        {
            edges.Add(new Point(pos.X + dx, pos.Y - size.Y));
        }

        // 下边缘（建筑底部下方）
        for (int dx = 0; dx < size.X; dx++)
        {
            edges.Add(new Point(pos.X + dx, pos.Y + 1));
        }

        // 左边缘（建筑左侧）
        for (int dy = 0; dy < size.Y; dy++)
        {
            edges.Add(new Point(pos.X - 1, pos.Y - dy));
        }

        // 右边缘（建筑右侧）
        for (int dy = 0; dy < size.Y; dy++)
        {
            edges.Add(new Point(pos.X + size.X, pos.Y - dy));
        }

        return edges;
    }

    // 检查指定位置是否可以接收物品
    private bool CanAcceptItemAt(Point pos, TileType type)
    {
        return type == TileType.Conveyor ||
               type == TileType.FastConveyor ||
               type == TileType.Splitter ||
               type == TileType.Merger ||
               type == TileType.Router ||
               type == TileType.Storage ||
               type == TileType.Smelter ||
               type == TileType.Assembler ||
               type == TileType.AssemblerMk2 ||
               type == TileType.Lab ||
               type == TileType.CoalGenerator ||
               type == TileType.ChemicalPlant;
    }

    // 检测处理器的输入边缘（有传送带/分流器等指向处理器的边缘）
    // 获取多方块建筑的所有边缘位置
    private List<Point> GetBuildingEdges(Point pos, Point size)
    {
        var edges = new List<Point>();

        // 上边缘（Y - size.Y）- 多方块建筑的顶部边缘
        for (int dx = 0; dx < size.X; dx++)
        {
            edges.Add(new Point(pos.X + dx, pos.Y - size.Y));
        }

        // 中边缘（Y）- 多方块建筑的底部边缘
        for (int dx = 0; dx < size.X; dx++)
        {
            edges.Add(new Point(pos.X + dx, pos.Y));
        }

        // 下边缘（Y + 1）- 底部边缘下方
        for (int dx = 0; dx < size.X; dx++)
        {
            edges.Add(new Point(pos.X + dx, pos.Y + 1));
        }

        // 左边缘（X - 1）- 左侧边缘
        for (int dy = 0; dy < size.Y; dy++)
        {
            edges.Add(new Point(pos.X - 1, pos.Y - dy));
        }

        // 右边缘（X + size.X）- 右侧边缘
        for (int dy = 0; dy < size.Y; dy++)
        {
            edges.Add(new Point(pos.X + size.X, pos.Y - dy));
        }

        return edges;
    }

    private HashSet<Point> GetProcessorInputEdges(Point pos, Point size)
    {
        var inputEdges = new HashSet<Point>();

        // 检查所有边缘位置
        foreach (var edge in GetBuildingEdges(pos, size))
        {
            if (IsInputToProcessor(edge, pos, size))
            {
                inputEdges.Add(edge);
            }
        }

        return inputEdges;
    }

    // 检查某个位置是否是处理器的输入
    private bool IsInputToProcessor(Point neighborPos, Point processorPos, Point processorSize)
    {
        if (!InBounds(neighborPos))
            return false;

        Tile neighbor = _tiles[neighborPos.X, neighborPos.Y];

        // 计算从邻居到处理器的方向
        Point toProcessor = Point.Zero;
        if (neighborPos.Y < processorPos.Y - (processorSize.Y - 1)) toProcessor = new Point(0, 1);  // 上方 -> 向下
        else if (neighborPos.Y > processorPos.Y) toProcessor = new Point(0, -1);  // 下方 -> 向上
        else if (neighborPos.X < processorPos.X) toProcessor = new Point(1, 0);  // 左方 -> 向右
        else if (neighborPos.X >= processorPos.X + processorSize.X) toProcessor = new Point(-1, 0);  // 右方 -> 向左

        // 检查传送带是否指向处理器
        if (neighbor.Type == TileType.Conveyor || neighbor.Type == TileType.FastConveyor)
        {
            Point dir = DirectionUtil.ToPoint(neighbor.Direction);
            if (dir == toProcessor)
                return true;
        }

        // 检查分流器输出是否指向处理器
        if (neighbor.Type == TileType.Splitter)
        {
            var outputs = GetSplitterOutputs(neighbor.Direction);
            Direction targetDir = PointToDirection(toProcessor);
            foreach (var outDir in outputs)
            {
                if (outDir == targetDir)
                    return true;
            }
        }

        // 检查路由器（路由器可以向任何方向输出）
        if (neighbor.Type == TileType.Router)
        {
            return true;
        }

        // 检查矿机输出
        if (neighbor.Type == TileType.Miner || neighbor.Type == TileType.AdvancedMiner)
        {
            return true;  // 矿机可以向任何边缘输出
        }

        return false;
    }

    private Direction PointToDirection(Point p)
    {
        if (p.Y < 0) return Direction.North;
        if (p.Y > 0) return Direction.South;
        if (p.X > 0) return Direction.East;
        if (p.X < 0) return Direction.West;
        return Direction.None;
    }

    private void UpdateMiners(float dt)
    {
        float scaledDt = dt * _powerRatio;

        foreach (var pair in _miners)
        {
            var tile = _tiles[pair.Key.X, pair.Key.Y];
            var state = pair.Value;

            // 高级矿工速度更快
            float speedMultiplier = tile.Type == TileType.AdvancedMiner ? 2f : 1f;
            state.Timer -= scaledDt * speedMultiplier;

            if (state.Timer > 0f)
            {
                continue;
            }

            // 多方块矿机：检查覆盖范围内的矿点
            Point minerPos = pair.Key;
            Point minerSize = BuildingSize.GetSize(tile.Type);
            OreType foundOre = OreType.None;

            for (int dy = 0; dy < minerSize.Y && foundOre == OreType.None; dy++)
            {
                for (int dx = 0; dx < minerSize.X && foundOre == OreType.None; dx++)
                {
                    // 多方块建筑的主格子在左下角，所以覆盖范围是minerPos.X + dx, minerPos.Y - dy
                    Point checkPos = new Point(minerPos.X + dx, minerPos.Y - dy);
                    if (InBounds(checkPos) && _oreMap[checkPos.X, checkPos.Y])
                    {
                        foundOre = _oreTypeMap[checkPos.X, checkPos.Y];
                    }
                }
            }

            if (foundOre == OreType.None)
            {
                state.Timer = 0.5f;
                continue;
            }

            // 尝试所有边缘位置输出（不再只依赖方向）
            Point? validOutput = FindMinerOutputPosition(minerPos, minerSize);

            if (validOutput.HasValue)
            {
                // 根据矿石类型产出不同物品
                ItemType itemType = foundOre switch
                {
                    OreType.Copper => ItemType.CopperOre,
                    OreType.Coal => ItemType.Coal,
                    OreType.Gold => ItemType.GoldOre,
                    OreType.Titanium => ItemType.TitaniumOre,
                    OreType.Uranium => ItemType.UraniumOre,
                    _ => ItemType.Ore
                };

                // 尝试添加到传送带实体
                Point outputPos = validOutput.Value;
                Tile outputTile = _tiles[outputPos.X, outputPos.Y];

                if (outputTile.Type == TileType.Conveyor || outputTile.Type == TileType.FastConveyor)
                {
                    // 添加到传送带实体
                    if (!_conveyors.TryGetValue(outputPos, out var conveyorEntity))
                    {
                        conveyorEntity = new ConveyorEntity();
                        _conveyors[outputPos] = conveyorEntity;
                    }

                    if (conveyorEntity.CanAccept(false))
                    {
                        conveyorEntity.AddItem(itemType, 0f, 0f);
                    }
                    else
                    {
                        // 传送带满了，稍后重试
                        state.Timer = 0.1f;
                        continue;
                    }
                }
                else
                {
                    // 其他类型使用旧的物品列表
                    _items.Add(new Item { Type = itemType, Tile = outputPos, Dir = Point.Zero, Progress = 0f });
                }

                // 添加矿机工作粒子效果
                Vector2 minerPos2 = new Vector2(minerPos.X + (minerSize.X - 1) * 0.5f, minerPos.Y - (minerSize.Y - 1) * 0.5f);
                CreateParticles(minerPos2, ParticleType.Spark, 12);

                state.Timer = MinerInterval;
            }
            else
            {
                state.Timer = 0.1f;
            }
        }
    }

    // 查找矿机的有效输出位置（尝试所有边缘）
    private Point? FindMinerOutputPosition(Point minerPos, Point minerSize)
    {
        var inputEdges = GetProcessorInputEdges(minerPos, minerSize);
        var outputCandidates = new List<Point>();

        // 获取所有边缘位置
        var allEdges = GetBuildingEdges(minerPos, minerSize);

        // 过滤出非输入边缘的位置
        foreach (var edge in allEdges)
        {
            if (!inputEdges.Contains(edge))
            {
                outputCandidates.Add(edge);
            }
        }

        // 随机打乱候选位置，确保公平尝试所有方向
        var rng = new Random();
        outputCandidates = outputCandidates.OrderBy(x => rng.Next()).ToList();

        // 查找有效的输出位置
        foreach (var candidate in outputCandidates)
        {
            if (!InBounds(candidate))
            {
                continue;
            }

            if (FindItemAt(candidate) != null)
            {
                continue;
            }

            Tile targetTile = _tiles[candidate.X, candidate.Y];

            // 检查目标是否可以接收物品
            if (targetTile.Type == TileType.Conveyor ||
                targetTile.Type == TileType.FastConveyor ||
                targetTile.Type == TileType.Splitter ||
                targetTile.Type == TileType.Merger ||
                targetTile.Type == TileType.Router ||
                targetTile.Type == TileType.Smelter ||
                targetTile.Type == TileType.Assembler ||
                targetTile.Type == TileType.Lab ||
                targetTile.Type == TileType.Storage ||
                targetTile.Type == TileType.CoalGenerator ||
                targetTile.Type == TileType.AssemblerMk2 ||
                targetTile.Type == TileType.ChemicalPlant)
            {
                return candidate;
            }
        }

        return null;
    }

    private void UpdatePower()
    {
        int generators = 0;
        int coalGenerators = 0;
        int splitterCount = 0;
        int mergerCount = 0;

        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                TileType type = _tiles[x, y].Type;
                if (type == TileType.Generator) generators++;
                if (type == TileType.CoalGenerator) coalGenerators++;
                if (type == TileType.Splitter) splitterCount++;
                if (type == TileType.Merger) mergerCount++;
            }
        }

        // 计算燃煤发电机产生的电力（只有有燃料的才产电）
        int coalPower = 0;
        foreach (var kvp in _coalGenerators)
        {
            if (kvp.Value.HasFuel)
            {
                coalPower += 20; // 每个燃煤发电机产生20电力
            }
        }

        int produced = 10 + generators * 12 + coalPower;

        _powerProduced = produced;
        _powerUsed = _miners.Count * 2 + _processors.Count * 3 + splitterCount + mergerCount;
        if (_powerUsed <= 0)
        {
            _powerRatio = 1f;
        }
        else
        {
            _powerRatio = MathHelper.Clamp(_powerProduced / (float)_powerUsed, 0f, 1f);
        }
    }

    private void UpdateCoalGenerators(float dt)
    {
        foreach (var kvp in _coalGenerators)
        {
            var state = kvp.Value;
            if (state.HasFuel)
            {
                state.FuelTimer -= dt;
                if (state.FuelTimer <= 0f)
                {
                    state.HasFuel = false;
                    state.FuelTimer = 0f;
                }
            }
        }
    }


    private Item? FindItemAt(Point tile)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i].Tile == tile)
            {
                return _items[i];
            }
        }
        return null;
    }

    private Direction GetFlowDirection(Point tile)
    {
        if (!InBounds(tile))
        {
            return Direction.None;
        }

        Tile t = _tiles[tile.X, tile.Y];
        return t.Type == TileType.Conveyor || t.Type == TileType.FastConveyor ? t.Direction : Direction.None;
    }

    private bool TryGetNextMove(Point tile, HashSet<Point> occupied, HashSet<Point> reserved, out Point dir, out Point next)
    {
        dir = Point.Zero;
        next = tile;

        Tile t = _tiles[tile.X, tile.Y];
        if (t.Type == TileType.Conveyor || t.Type == TileType.FastConveyor || t.Type == TileType.Merger)
        {
            Direction flow = t.Direction;
            dir = DirectionUtil.ToPoint(flow);
            if (dir == Point.Zero)
            {
                return false;
            }

            next = new Point(tile.X + dir.X, tile.Y + dir.Y);
            if (!InBounds(next))
            {
                return false;
            }

            if (occupied.Contains(next) || reserved.Contains(next))
            {
                return false;
            }

            if (!CanEnterTile(tile, next, dir))
            {
                return false;
            }

            return true;
        }

        if (t.Type == TileType.Splitter)
        {
            var outputs = GetSplitterOutputs(t.Direction);
            int index = _splitterIndex.TryGetValue(tile, out int value) ? value : 0;

            for (int i = 0; i < outputs.Length; i++)
            {
                Direction outDir = outputs[(index + i) % outputs.Length];
                Point outStep = DirectionUtil.ToPoint(outDir);
                if (outStep == Point.Zero)
                {
                    continue;
                }

                Point candidate = new Point(tile.X + outStep.X, tile.Y + outStep.Y);
                if (!InBounds(candidate))
                {
                    continue;
                }

                if (occupied.Contains(candidate) || reserved.Contains(candidate))
                {
                    continue;
                }

                if (!CanEnterTile(tile, candidate, outStep))
                {
                    continue;
                }

                _splitterIndex[tile] = (index + i + 1) % outputs.Length;
                dir = outStep;
                next = candidate;
                return true;
            }
        }

        // 路由器逻辑：自动检测输入方向，其他方向都是输出
        if (t.Type == TileType.Router)
        {
            var outputs = GetRouterOutputs(tile);
            if (outputs.Length == 0)
            {
                return false;
            }

            int index = _routers.TryGetValue(tile, out var state) ? state.OutputIndex : 0;

            for (int i = 0; i < outputs.Length; i++)
            {
                Direction outDir = outputs[(index + i) % outputs.Length];
                Point outStep = DirectionUtil.ToPoint(outDir);
                if (outStep == Point.Zero)
                {
                    continue;
                }

                Point candidate = new Point(tile.X + outStep.X, tile.Y + outStep.Y);
                if (!InBounds(candidate))
                {
                    continue;
                }

                if (occupied.Contains(candidate) || reserved.Contains(candidate))
                {
                    continue;
                }

                if (!CanEnterTile(tile, candidate, outStep))
                {
                    continue;
                }

                if (!_routers.ContainsKey(tile))
                {
                    _routers[tile] = new RouterState();
                }
                _routers[tile].OutputIndex = (index + i + 1) % outputs.Length;
                dir = outStep;
                next = candidate;
                return true;
            }
        }

        return false;
    }

    // 获取路由器的输出方向（排除输入方向）
    private Direction[] GetRouterOutputs(Point tile)
    {
        var allDirs = new[] { Direction.North, Direction.East, Direction.South, Direction.West };
        var outputs = new List<Direction>();

        foreach (var dir in allDirs)
        {
            Point step = DirectionUtil.ToPoint(dir);
            Point neighbor = new Point(tile.X + step.X, tile.Y + step.Y);

            // 检查这个方向是否是输入（有传送带/分流器等指向路由器）
            if (!IsInputDirection(tile, dir))
            {
                outputs.Add(dir);
            }
        }

        return outputs.ToArray();
    }

    // 检查某个方向是否是输入方向
    private bool IsInputDirection(Point routerTile, Direction dir)
    {
        Point step = DirectionUtil.ToPoint(dir);
        Point neighbor = new Point(routerTile.X + step.X, routerTile.Y + step.Y);

        if (!InBounds(neighbor))
        {
            return false;
        }

        Tile neighborTile = _tiles[neighbor.X, neighbor.Y];

        // 检查邻居是否指向路由器
        if (neighborTile.Type == TileType.Conveyor || neighborTile.Type == TileType.FastConveyor)
        {
            Point neighborDir = DirectionUtil.ToPoint(neighborTile.Direction);
            // 如果邻居的输出方向指向路由器
            if (neighborDir.X == -step.X && neighborDir.Y == -step.Y)
            {
                return true;
            }
        }

        // 分流器的输出可能指向路由器
        if (neighborTile.Type == TileType.Splitter)
        {
            var splitterOutputs = GetSplitterOutputs(neighborTile.Direction);
            Direction oppositeDir = Opposite(dir);
            foreach (var outDir in splitterOutputs)
            {
                if (outDir == oppositeDir)
                {
                    return true;
                }
            }
        }

        // 矿机、熔炉等的输出
        if (neighborTile.Type == TileType.Miner || neighborTile.Type == TileType.Smelter ||
            neighborTile.Type == TileType.Assembler || neighborTile.Type == TileType.Lab ||
            neighborTile.Type == TileType.AdvancedMiner || neighborTile.Type == TileType.AssemblerMk2 ||
            neighborTile.Type == TileType.ChemicalPlant)
        {
            Point neighborDir = DirectionUtil.ToPoint(neighborTile.Direction);
            if (neighborDir.X == -step.X && neighborDir.Y == -step.Y)
            {
                return true;
            }
        }

        return false;
    }

    private Direction[] GetSplitterOutputs(Direction direction)
    {
        return direction switch
        {
            Direction.North => new[] { Direction.North, Direction.West, Direction.East },
            Direction.East => new[] { Direction.East, Direction.North, Direction.South },
            Direction.South => new[] { Direction.South, Direction.East, Direction.West },
            Direction.West => new[] { Direction.West, Direction.South, Direction.North },
            _ => new[] { Direction.East, Direction.North, Direction.South }
        };
    }

    private bool CanEnterTile(Point from, Point to, Point dir)
    {
        if (!InBounds(to))
        {
            return false;
        }

        Tile t = _tiles[to.X, to.Y];
        if (t.Type == TileType.Splitter)
        {
            Point inputDir = DirectionUtil.ToPoint(Opposite(t.Direction));
            return dir == inputDir;
        }

        if (t.Type == TileType.Merger)
        {
            Point outputDir = DirectionUtil.ToPoint(t.Direction);
            return dir != outputDir;
        }

        // 路由器可以从任何方向接收输入
        if (t.Type == TileType.Router)
        {
            return true;
        }

        return true;
    }

    private Direction Opposite(Direction direction)
    {
        return direction switch
        {
            Direction.North => Direction.South,
            Direction.East => Direction.West,
            Direction.South => Direction.North,
            Direction.West => Direction.East,
            _ => Direction.None
        };
    }

    private float GetTileSpeed(TileType type)
    {
        return type switch
        {
            TileType.FastConveyor => FastConveyorSpeed,
            TileType.Conveyor => ConveyorSpeed,
            TileType.Splitter => ConveyorSpeed,
            TileType.Merger => ConveyorSpeed,
            _ => ConveyorSpeed
        };
    }

    private float GetElevation(TileType type)
    {
        return type switch
        {
            TileType.Conveyor => TileElevation,
            TileType.FastConveyor => TileElevation,
            TileType.Splitter => TileElevation,
            TileType.Merger => TileElevation,
            TileType.UndergroundEntry => TileElevation,
            TileType.UndergroundExit => TileElevation,
            TileType.Miner => MachineElevation,
            TileType.Smelter => MachineElevation,
            TileType.Assembler => MachineElevation,
            TileType.Lab => MachineElevation,
            TileType.Generator => MachineElevation,
            TileType.Storage => MachineElevation,
            TileType.CoalGenerator => MachineElevation,
            TileType.AdvancedMiner => MachineElevation,
            TileType.AssemblerMk2 => MachineElevation,
            TileType.ChemicalPlant => MachineElevation,
            _ => 0f
        };
    }

    private void DrawConveyorStripes(Vector2 center, TileType type, Direction direction)
    {
        if (type != TileType.Conveyor && type != TileType.FastConveyor && type != TileType.Splitter && type != TileType.Merger)
        {
            return;
        }

        Point dir = DirectionUtil.ToPoint(direction);
        if (dir == Point.Zero)
        {
            return;
        }

        Vector2 neighbor = WorldToScreen(new Vector2(dir.X, dir.Y));
        Vector2 baseDir = Vector2.Normalize(neighbor - WorldToScreen(Vector2.Zero));
        float spacing = 8f * _camera.Zoom;
        float size = 4f * _camera.Zoom;
        Color stripe = type == TileType.FastConveyor ? new Color(255, 255, 255) : new Color(220, 220, 220);

        for (int i = -1; i <= 1; i++)
        {
            Vector2 pos = center + baseDir * (i * spacing);
            _spriteBatch.Draw(_pixel, new Rectangle((int)(pos.X - size), (int)(pos.Y - size), (int)(size * 2f), (int)(size * 2f)), stripe);
        }
    }

    private void DrawTileGrid(Vector2 center, Color color)
    {
        Vector2 right = WorldToScreen(new Vector2(1, 0)) - WorldToScreen(Vector2.Zero);
        Vector2 down = WorldToScreen(new Vector2(0, 1)) - WorldToScreen(Vector2.Zero);
        Vector2 p1 = center + right * 0.5f;
        Vector2 p2 = center + down * 0.5f;
        Vector2 p3 = center - right * 0.5f;
        Vector2 p4 = center - down * 0.5f;
        DrawLine(p1, p2, color, 1f * _camera.Zoom);
        DrawLine(p2, p3, color, 1f * _camera.Zoom);
        DrawLine(p3, p4, color, 1f * _camera.Zoom);
        DrawLine(p4, p1, color, 1f * _camera.Zoom);
    }

    private void DrawTiles()
    {
        // 计算可见区域 - 需要考虑等距视角的菱形特性
        var viewport = GraphicsDevice.Viewport;

        // 获取屏幕四个角的世界坐标
        Vector2 topLeft = ScreenToWorld(new Vector2(0, 0));
        Vector2 topRight = ScreenToWorld(new Vector2(viewport.Width, 0));
        Vector2 bottomLeft = ScreenToWorld(new Vector2(0, viewport.Height));
        Vector2 bottomRight = ScreenToWorld(new Vector2(viewport.Width, viewport.Height));

        // 计算包围盒，增加额外边距确保完整渲染
        float padding = 10f / _camera.Zoom + 5f; // 根据缩放调整边距
        int minX = Math.Max(0, (int)MathF.Floor(Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X)) - padding));
        int maxX = Math.Min(MapWidth, (int)MathF.Ceiling(Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X)) + padding));
        int minY = Math.Max(0, (int)MathF.Floor(Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y)) - padding));
        int maxY = Math.Min(MapHeight, (int)MathF.Ceiling(Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y)) + padding));

        for (int layer = minX + minY; layer <= maxX + maxY; layer++)
        {
            for (int y = minY; y < maxY; y++)
            {
                for (int x = minX; x < maxX; x++)
                {
                    if (x + y != layer)
                    {
                        continue;
                    }

                    Tile tile = _tiles[x, y];
                    TerrainType terrain = _terrainMap[x, y];
                    Vector2 center = WorldToScreen(new Vector2(x, y));
                    float elevation = GetElevation(tile.Type) * _camera.Zoom;
                    Vector2 topCenter = center - new Vector2(0f, elevation);

                    // 根据地形类型绘制不同颜色的底层（柔和配色）
                    Color terrainColor = terrain switch
                    {
                        TerrainType.Grass => new Color(55, 65, 55),      // 柔和的深绿灰
                        TerrainType.Mountain => new Color(70, 65, 60),   // 柔和的棕灰
                        TerrainType.Water => new Color(45, 55, 70),      // 柔和的深蓝灰
                        TerrainType.Locked => new Color(35, 35, 40),     // 深灰
                        _ => new Color(55, 65, 55)
                    };

                    // 绘制地形底层
                    if (tile.Type == TileType.Empty)
                    {
                        _spriteBatch.Draw(_tileTextures[TileType.Empty], topCenter, null, terrainColor, 0f, new Vector2(TileWidth / 2f, TileHeight / 2f), _camera.Zoom, SpriteEffects.None, 0f);

                        // 山地和水域的额外标记
                        if (terrain == TerrainType.Mountain)
                        {
                            _spriteBatch.Draw(_tileMask, topCenter - new Vector2(0, 4 * _camera.Zoom), null, new Color(120, 100, 80, 150), 0f, new Vector2(TileWidth / 2f, TileHeight / 2f), _camera.Zoom * 0.6f, SpriteEffects.None, 0f);
                        }
                        else if (terrain == TerrainType.Water)
                        {
                            _spriteBatch.Draw(_tileMask, topCenter, null, new Color(60, 100, 150, 100), 0f, new Vector2(TileWidth / 2f, TileHeight / 2f), _camera.Zoom, SpriteEffects.None, 0f);
                        }
                        else if (terrain == TerrainType.Locked)
                        {
                            _spriteBatch.Draw(_tileMask, topCenter, null, new Color(20, 20, 25, 200), 0f, new Vector2(TileWidth / 2f, TileHeight / 2f), _camera.Zoom, SpriteEffects.None, 0f);
                        }
                    }
                    else
                    {
                        // 多方块建筑：只在主格子渲染
                        Point size = BuildingSize.GetSize(tile.Type);
                        bool isMainTile = !tile.ParentTile.HasValue;

                        if (isMainTile)
                        {
                            // 多方块建筑的主格子在左下角，需要计算建筑的实际中心位置
                            float centerOffsetX = (size.X - 1) * 0.5f;
                            float centerOffsetY = (size.Y - 1) * 0.5f;
                            Vector2 buildingCenter = WorldToScreen(new Vector2(x + centerOffsetX, y - centerOffsetY));
                            float buildingElevation = GetElevation(tile.Type) * _camera.Zoom;
                            Vector2 buildingTop = buildingCenter - new Vector2(0f, buildingElevation);

                            // 绘制阴影（更大）
                            float shadowScale = Math.Max(size.X, size.Y);
                            _spriteBatch.Draw(_tileMask, buildingCenter + new Vector2(0f, 6f * shadowScale) * _camera.Zoom, null, new Color(0, 0, 0, 80), 0f, new Vector2(TileWidth / 2f, TileHeight / 2f), _camera.Zoom * shadowScale, SpriteEffects.None, 0f);

                            // 绘制建筑纹理（按尺寸缩放）
                            Texture2D texture = _tileTextures[tile.Type];
                            float buildingScale = Math.Max(size.X, size.Y);
                            _spriteBatch.Draw(texture, buildingTop, null, Color.White, 0f, new Vector2(TileWidth / 2f, TileHeight / 2f), _camera.Zoom * buildingScale, SpriteEffects.None, 0f);

                            // 绘制方向箭头（在建筑中心）
                            if (DirectionUtil.UsesDirection(tile.Type))
                            {
                                DrawDirectionArrow(buildingTop, tile.Direction, new Color(240, 240, 240));
                            }
                        }
                        // 子格子不渲染建筑，但仍然绘制网格
                    }

                    if (_showOreHighlight && tile.Type == TileType.Empty && _oreMap[x, y] && terrain == TerrainType.Grass)
                    {
                        // 根据矿石类型显示不同颜色
                        OreType oreType = _oreTypeMap[x, y];
                        Color oreColor = oreType switch
                        {
                            OreType.Copper => new Color(180, 120, 80, 120),
                            OreType.Coal => new Color(60, 60, 70, 120),
                            OreType.Gold => new Color(255, 215, 0, 120),      // 金色
                            OreType.Titanium => new Color(180, 180, 200, 120), // 银白色
                            OreType.Uranium => new Color(50, 200, 50, 120),    // 绿色
                            _ => new Color(100, 120, 140, 120)                 // 铁矿 - 蓝灰色
                        };
                        _spriteBatch.Draw(_tileMask, topCenter, null, oreColor, 0f, new Vector2(TileWidth / 2f, TileHeight / 2f), _camera.Zoom, SpriteEffects.None, 0f);
                    }

                    // 单格建筑的方向箭头和条纹（多方块建筑在上面已经处理）
                    bool isSingleBlock = !BuildingSize.IsMultiBlock(tile.Type);
                    if (isSingleBlock && DirectionUtil.UsesDirection(tile.Type))
                    {
                        DrawDirectionArrow(topCenter, tile.Direction, new Color(240, 240, 240));
                        DrawConveyorStripes(topCenter, tile.Type, tile.Direction);
                    }

                    if (tile.Type == TileType.Splitter)
                    {
                        DrawSplitterOverlay(topCenter, tile.Direction);
                    }
                    else if (tile.Type == TileType.Merger)
                    {
                        DrawMergerOverlay(topCenter, tile.Direction);
                    }
                    else if (tile.Type == TileType.Router)
                    {
                        DrawRouterOverlay(topCenter, new Point(x, y));
                    }

                    if (tile.Type == TileType.Storage)
                    {
                        DrawStorageMarker(topCenter);
                    }

                    if (_showGrid && terrain != TerrainType.Locked)
                    {
                        DrawTileGrid(topCenter, tile.Type == TileType.Empty ? new Color(40, 40, 50, 80) : new Color(0, 0, 0, 50));
                    }
                }
            }
        }
    }

    private void DrawItems()
    {
        // 绘制传送带上的物品（新系统）
        DrawConveyorItems();

        // 绘制旧系统的物品（兼容）
        if (_items.Count == 0)
        {
            return;
        }

        foreach (var item in _items)
        {
            Vector2 world = new Vector2(item.Tile.X, item.Tile.Y) + new Vector2(item.Dir.X, item.Dir.Y) * item.Progress;
            Vector2 pos = WorldToScreen(world);
            float elevation = 0f;
            if (InBounds(item.Tile))
            {
                elevation = GetElevation(_tiles[item.Tile.X, item.Tile.Y].Type) * _camera.Zoom;
            }
            pos -= new Vector2(0f, elevation);
            float size = 12f * _camera.Zoom;

            // 使用贴图渲染物品
            if (_itemTextures.TryGetValue(item.Type, out var texture))
            {
                // 绘制阴影
                _spriteBatch.Draw(texture, pos + new Vector2(2f, 3f) * _camera.Zoom, null, new Color(0, 0, 0, 80), 0f, new Vector2(8, 8), _camera.Zoom * 0.75f, SpriteEffects.None, 0f);
                // 绘制物品
                _spriteBatch.Draw(texture, pos, null, Color.White, 0f, new Vector2(8, 8), _camera.Zoom * 0.75f, SpriteEffects.None, 0f);
            }
            else
            {
                // 后备：使用矩形
                _spriteBatch.Draw(_pixel, new Rectangle((int)(pos.X - size / 2f + 3f * _camera.Zoom), (int)(pos.Y - size / 2f + 4f * _camera.Zoom), (int)size, (int)size), new Color(0, 0, 0, 60));
                Color color = GetItemColor(item.Type);
                _spriteBatch.Draw(_pixel, new Rectangle((int)(pos.X - size / 2f), (int)(pos.Y - size / 2f), (int)size, (int)size), color);
            }
        }
    }

    /// <summary>
    /// 绘制传送带上的物品 - 参考 Mindustry 的 drawLayer
    /// </summary>
    private void DrawConveyorItems()
    {
        foreach (var kvp in _conveyors)
        {
            Point tilePos = kvp.Key;
            ConveyorEntity entity = kvp.Value;

            if (!InBounds(tilePos) || entity.Items.Count == 0) continue;

            Tile tile = _tiles[tilePos.X, tilePos.Y];
            if (tile.Type != TileType.Conveyor && tile.Type != TileType.FastConveyor) continue;

            Direction direction = tile.Direction;
            float rotation = direction switch
            {
                Direction.North => 0f,
                Direction.East => 90f,
                Direction.South => 180f,
                Direction.West => 270f,
                _ => 0f
            };

            // 计算方向向量
            Point dirPoint = DirectionUtil.ToPoint(direction);
            Vector2 dirVec = new Vector2(dirPoint.X, dirPoint.Y);

            foreach (long packed in entity.Items)
            {
                ItemType itemType = ConveyorItemPos.GetItemType(packed);
                float x = ConveyorItemPos.GetX(packed);
                float y = ConveyorItemPos.GetY(packed);

                // 计算物品在世界坐标中的位置
                // y: 0 = 传送带起点, 1 = 传送带终点
                // x: 横向偏移 [-1, 1]
                Vector2 basePos = new Vector2(tilePos.X, tilePos.Y);

                // 根据传送带方向计算偏移
                Vector2 forward = dirVec * (y - 0.5f);  // 纵向偏移
                Vector2 right = new Vector2(-dirVec.Y, dirVec.X) * x * 0.3f;  // 横向偏移

                Vector2 worldPos = basePos + forward + right;
                Vector2 screenPos = WorldToScreen(worldPos);

                float elevation = GetElevation(tile.Type) * _camera.Zoom;
                screenPos -= new Vector2(0f, elevation);

                // 使用贴图渲染物品
                if (_itemTextures.TryGetValue(itemType, out var texture))
                {
                    // 绘制阴影
                    _spriteBatch.Draw(texture, screenPos + new Vector2(2f, 3f) * _camera.Zoom, null, new Color(0, 0, 0, 80), 0f, new Vector2(8, 8), _camera.Zoom * 0.75f, SpriteEffects.None, 0f);
                    // 绘制物品
                    _spriteBatch.Draw(texture, screenPos, null, Color.White, 0f, new Vector2(8, 8), _camera.Zoom * 0.75f, SpriteEffects.None, 0f);
                }
                else
                {
                    // 后备：使用矩形
                    float size = 10f * _camera.Zoom;
                    _spriteBatch.Draw(_pixel,
                        new Rectangle((int)(screenPos.X - size / 2f + 3f * _camera.Zoom), (int)(screenPos.Y - size / 2f + 4f * _camera.Zoom), (int)size, (int)size),
                        new Color(0, 0, 0, 60));
                    Color color = GetItemColor(itemType);
                    _spriteBatch.Draw(_pixel,
                        new Rectangle((int)(screenPos.X - size / 2f), (int)(screenPos.Y - size / 2f), (int)size, (int)size),
                        color);
                }
            }
        }
    }

    /// <summary>
    /// 获取物品颜色
    /// </summary>
    private Color GetItemColor(ItemType type)
    {
        return type switch
        {
            ItemType.Ore => new Color(120, 200, 240),
            ItemType.Plate => new Color(240, 220, 120),
            ItemType.Gear => new Color(200, 160, 90),
            ItemType.Science => new Color(140, 220, 200),
            ItemType.CopperOre => new Color(200, 140, 100),
            ItemType.CopperPlate => new Color(220, 160, 120),
            ItemType.Coal => new Color(60, 60, 70),
            ItemType.GoldOre => new Color(255, 215, 0),
            ItemType.GoldPlate => new Color(255, 230, 100),
            ItemType.TitaniumOre => new Color(100, 150, 180),
            ItemType.TitaniumPlate => new Color(140, 180, 200),
            ItemType.UraniumOre => new Color(100, 200, 100),
            ItemType.UraniumPlate => new Color(150, 230, 150),
            ItemType.CopperWire => new Color(200, 120, 80),
            ItemType.Circuit => new Color(100, 180, 100),
            ItemType.Steel => new Color(180, 180, 200),
            ItemType.RedScience => new Color(220, 100, 100),
            ItemType.GreenScience => new Color(100, 200, 100),
            _ => new Color(240, 240, 240)
        };
    }

    private void DrawHover()
    {
        Point? hover = GetHoveredTile();
        if (!hover.HasValue)
        {
            return;
        }

        Vector2 center = WorldToScreen(new Vector2(hover.Value.X, hover.Value.Y));
        float elevation = GetElevation(_tiles[hover.Value.X, hover.Value.Y].Type) * _camera.Zoom;
        center -= new Vector2(0f, elevation);
        _spriteBatch.Draw(_tileMask, center, null, new Color(255, 255, 255, 60), 0f, new Vector2(TileWidth / 2f, TileHeight / 2f), _camera.Zoom, SpriteEffects.None, 0f);

        if (_showTooltips || _debugMode)
        {
            string hint = GetTileHint(hover.Value);
            
            // 调试模式：添加详细的调试信息
            if (_debugMode)
            {
                Vector2 mouseWorld = ScreenToWorld(new Vector2(_input.MouseX, _input.MouseY));
                
                // 构建详细的调试信息
                string debugInfo = $"调试信息:\n" +
                                  $"屏幕坐标: ({_input.MouseX}, {_input.MouseY})\n" +
                                  $"世界坐标: ({mouseWorld.X:F2}, {mouseWorld.Y:F2})\n" +
                                  $"瓦片坐标: ({hover.Value.X}, {hover.Value.Y})\n" +
                                  $"运行情况: 物品={_items.Count}, 处理器={_processors.Count}, 矿工={_miners.Count}\n" +
                                  $"储存信息: 板={_totalPlatesStored}, 科学={_totalScienceStored}\n" +
                                  $"电力: {_powerProduced}/{_powerUsed} ({(int)(_powerRatio * 100)}%)\n" +
                                  $"库存: 铁板={GetInventoryCount(ItemType.Plate)}, 齿轮={GetInventoryCount(ItemType.Gear)}, 铜板={GetInventoryCount(ItemType.CopperPlate)}, 电路={GetInventoryCount(ItemType.Circuit)}\n" +
                                  $"研究点: {_researchPoints}";
                
                // 如果有原有的提示信息，将调试信息添加到后面
                if (!string.IsNullOrWhiteSpace(hint))
                {
                    hint += "\n\n" + debugInfo;
                }
                else
                {
                    hint = debugInfo;
                }
            }
            
            if (!string.IsNullOrWhiteSpace(hint))
            {
                SetTooltip(hint, new Vector2(_input.MouseX + 12, _input.MouseY + 12));
            }
        }
    }

    private void DrawBuildingPreview()
    {
        if (_tool == Tool.Erase)
        {
            return;
        }

        Point? hover = GetHoveredTile();
        if (!hover.HasValue)
        {
            return;
        }

        var (type, direction) = GetPlacementSpec();
        Point size = BuildingSize.GetSize(type);
        
        // 检查是否可以放置
        bool canPlace = true;
        
        if (size.X > 1 || size.Y > 1)
        {
            // 多方块建筑：检查所有格子
            // 以鼠标悬停点为左下角基准点
            Point mainTilePos = hover.Value;
            
            for (int dy = 0; dy < size.Y; dy++)
            {
                for (int dx = 0; dx < size.X; dx++)
                {
                    Point p = new Point(mainTilePos.X + dx, mainTilePos.Y - dy);
                    if (!InBounds(p))
                    {
                        canPlace = false;
                        break;
                    }

                    // 检查地形
                    TerrainType terrain = _terrainMap[p.X, p.Y];
                    if (terrain != TerrainType.Grass)
                    {
                        canPlace = false;
                        break;
                    }

                    // 检查是否已有建筑
                    Tile existing = _tiles[p.X, p.Y];
                    if (existing.Type != TileType.Empty)
                    {
                        canPlace = false;
                        break;
                    }
                }
                if (!canPlace) break;
            }
        }
        else
        {
            // 单格建筑
            if (!InBounds(hover.Value))
            {
                canPlace = false;
            }
            else
            {
                TerrainType terrain = _terrainMap[hover.Value.X, hover.Value.Y];
                if (terrain != TerrainType.Grass)
                {
                    canPlace = false;
                }
                
                Tile existing = _tiles[hover.Value.X, hover.Value.Y];
                if (existing.Type != TileType.Empty)
                {
                    canPlace = false;
                }
            }
        }

        // 检查矿机特殊条件
        if ((type == TileType.Miner || type == TileType.AdvancedMiner) && canPlace)
        {
            int minerSize = type == TileType.AdvancedMiner ? 3 : 2;
            // 以鼠标悬停点为左下角基准点
            Point mainTilePos = hover.Value;
            if (!HasOreInAreaForPlacement(mainTilePos, minerSize))
            {
                canPlace = false;
            }
        }

        // 绘制预览
        Color previewColor = canPlace ? 
            new Color(100, 255, 100, 120) :  // 绿色 - 可以放置
            new Color(255, 100, 100, 120);   // 红色 - 不能放置

        if (size.X > 1 || size.Y > 1)
        {
            // 多方块建筑预览
            // 以鼠标悬停点为左下角基准点
            Point mainTilePos = hover.Value;
            
            // 计算建筑中心位置（用于渲染）
            float centerOffsetX = (size.X - 1) * 0.5f;
            float centerOffsetY = (size.Y - 1) * 0.5f;
            Vector2 buildingCenter = WorldToScreen(new Vector2(mainTilePos.X + centerOffsetX, mainTilePos.Y - centerOffsetY));
            
            // 绘制建筑覆盖区域
            for (int dy = 0; dy < size.Y; dy++)
            {
                for (int dx = 0; dx < size.X; dx++)
                {
                    Point p = new Point(mainTilePos.X + dx, mainTilePos.Y - dy);
                    if (InBounds(p))
                    {
                        Vector2 tileCenter = WorldToScreen(new Vector2(p.X, p.Y));
                        _spriteBatch.Draw(_tileMask, tileCenter, null, previewColor, 0f, new Vector2(TileWidth / 2f, TileHeight / 2f), _camera.Zoom, SpriteEffects.None, 0f);
                    }
                }
            }
            
            // 绘制建筑纹理预览（半透明）
            if (canPlace)
            {
                float buildingElevation = GetElevation(type) * _camera.Zoom;
                Vector2 buildingTop = buildingCenter - new Vector2(0f, buildingElevation);
                
                float buildingScale = Math.Max(size.X, size.Y);
                Texture2D texture = _tileTextures[type];
                _spriteBatch.Draw(texture, buildingTop, null, new Color(255, 255, 255, 100), 0f, new Vector2(TileWidth / 2f, TileHeight / 2f), _camera.Zoom * buildingScale, SpriteEffects.None, 0f);
                
                // 绘制方向箭头
                if (DirectionUtil.UsesDirection(type))
                {
                    DrawDirectionArrow(buildingTop, direction, new Color(240, 240, 240, 150));
                }
            }
        }
        else
        {
            // 单格建筑预览
            Vector2 tileCenter = WorldToScreen(new Vector2(hover.Value.X, hover.Value.Y));
            _spriteBatch.Draw(_tileMask, tileCenter, null, previewColor, 0f, new Vector2(TileWidth / 2f, TileHeight / 2f), _camera.Zoom, SpriteEffects.None, 0f);
            
            // 绘制建筑纹理预览（半透明）
            if (canPlace)
            {
                float elevation = GetElevation(type) * _camera.Zoom;
                Vector2 top = tileCenter - new Vector2(0f, elevation);
                
                if (_tileTextures.TryGetValue(type, out var texture))
                {
                    _spriteBatch.Draw(texture, top, null, new Color(255, 255, 255, 100), 0f, new Vector2(TileWidth / 2f, TileHeight / 2f), _camera.Zoom, SpriteEffects.None, 0f);
                }
                
                // 绘制方向箭头
                if (DirectionUtil.UsesDirection(type))
                {
                    DrawDirectionArrow(top, direction, new Color(240, 240, 240, 150));
                }
            }
        }
    }

    private void DrawToolbar()
    {
        var viewport = GraphicsDevice.Viewport;
        var bar = new Rectangle(0, 0, viewport.Width, ToolbarHeight);
        _spriteBatch.Draw(_pixel, bar, new Color(25, 30, 40, 220));

        if (_toolbarTools.Length == 0)
        {
            return;
        }

        for (int i = 0; i < _toolButtons.Length; i++)
        {
            Rectangle rect = _toolButtons[i];
            Tool tool = _toolbarTools[i];
            Color fill = GetToolColor(tool);
            _spriteBatch.Draw(_pixel, rect, fill);

            if (tool == _tool)
            {
                DrawRectOutline(rect, new Color(255, 255, 255), 3);
            }
            else
            {
                DrawRectOutline(rect, new Color(10, 10, 10), 2);
            }

            DrawToolIcon(tool, rect);

            if (_showTooltips && rect.Contains(_input.MouseX, _input.MouseY))
            {
                SetTooltip(GetToolTooltip(tool), new Vector2(_input.MouseX + 12, _input.MouseY + 12));
            }
        }

        _spriteBatch.Draw(_pixel, _directionButton, new Color(35, 40, 55));
        DrawRectOutline(_directionButton, new Color(255, 255, 255), 2);
        DrawDirectionArrow(new Vector2(_directionButton.Center.X, _directionButton.Center.Y), _direction, new Color(240, 240, 240));

        if (_showTooltips && _directionButton.Contains(_input.MouseX, _input.MouseY))
        {
            SetTooltip(T("TIP_ROTATE"), new Vector2(_input.MouseX + 12, _input.MouseY + 12));
        }
    }

    private void DrawTaskbar()
    {
        var viewport = GraphicsDevice.Viewport;
        var bar = new Rectangle(0, viewport.Height - TaskbarHeight, viewport.Width, TaskbarHeight);
        _spriteBatch.Draw(_pixel, bar, new Color(22, 26, 34, 230));

        int y = bar.Y + 10;
        int x = 18;

        // 定义所有库存物品
        var inventoryItems = new (ItemType type, Color color, Color outline, string enName, string cnName)[]
        {
            (ItemType.Plate, new Color(180, 180, 200), new Color(100, 100, 120), "Iron Plate", "铁板"),
            (ItemType.Gear, new Color(160, 160, 180), new Color(90, 90, 110), "Gear", "齿轮"),
            (ItemType.CopperPlate, new Color(200, 140, 100), new Color(140, 90, 60), "Copper Plate", "铜板"),
            (ItemType.Circuit, new Color(80, 180, 80), new Color(50, 120, 50), "Circuit", "电路板"),
            (ItemType.CopperWire, new Color(200, 120, 80), new Color(140, 70, 40), "Copper Wire", "铜线"),
            (ItemType.Steel, new Color(140, 140, 160), new Color(80, 80, 100), "Steel", "钢材"),
            (ItemType.GoldPlate, new Color(220, 200, 80), new Color(160, 140, 40), "Gold Plate", "金板"),
            (ItemType.TitaniumPlate, new Color(180, 180, 220), new Color(120, 120, 160), "Titanium Plate", "钛板"),
            (ItemType.UraniumPlate, new Color(100, 220, 100), new Color(60, 160, 60), "Uranium Plate", "铀板"),
            (ItemType.Science, new Color(140, 200, 220), new Color(80, 140, 160), "Science", "科研包"),
            (ItemType.RedScience, new Color(220, 100, 100), new Color(160, 60, 60), "Red Science", "红色科研"),
            (ItemType.GreenScience, new Color(100, 220, 100), new Color(60, 160, 60), "Green Science", "绿色科研"),
        };

        // 计算可显示的物品数量和滚动
        int itemWidth = 70;
        int inventoryAreaWidth = viewport.Width - 400; // 留出右侧空间给电力、研究等
        int maxVisibleItems = Math.Max(1, inventoryAreaWidth / itemWidth);
        int totalItems = inventoryItems.Length;
        int maxScroll = Math.Max(0, totalItems - maxVisibleItems);

        // 限制滚动范围
        _inventoryScrollOffset = Math.Clamp(_inventoryScrollOffset, 0, maxScroll);

        // 绘制库存区域背景
        var inventoryBg = new Rectangle(x - 4, y + 2, maxVisibleItems * itemWidth + 8, 36);
        _spriteBatch.Draw(_pixel, inventoryBg, new Color(30, 34, 44, 200));
        DrawRectOutline(inventoryBg, new Color(50, 54, 64), 1);

        // 绘制滚动指示器
        if (_inventoryScrollOffset > 0)
        {
            // 左箭头
            DrawText("<", new Vector2(x - 2, y + 4), 2, new Color(180, 180, 200));
        }
        if (_inventoryScrollOffset < maxScroll)
        {
            // 右箭头
            DrawText(">", new Vector2(x + maxVisibleItems * itemWidth - 8, y + 4), 2, new Color(180, 180, 200));
        }

        // 绘制可见的库存物品
        for (int i = 0; i < maxVisibleItems && i + _inventoryScrollOffset < totalItems; i++)
        {
            var item = inventoryItems[i + _inventoryScrollOffset];
            int itemX = x + i * itemWidth;
            int count = GetInventoryCount(item.type);

            // 只显示数量大于0的物品或前4个基础物品
            bool isBasicItem = i + _inventoryScrollOffset < 4;
            if (count > 0 || isBasicItem)
            {
                var itemRect = new Rectangle(itemX, y + 6, 18, 18);
                _spriteBatch.Draw(_pixel, itemRect, item.color);
                DrawRectOutline(itemRect, item.outline, 2);
                DrawNumber(new Vector2(itemX + 24, y), count, count > 0 ? new Color(230, 230, 230) : new Color(100, 100, 100));

                if (_showTooltips && itemRect.Contains(_input.MouseX, _input.MouseY))
                {
                    SetTooltip(_language == Language.English ? item.enName : item.cnName, new Vector2(_input.MouseX + 12, _input.MouseY + 12));
                }
            }
        }

        // 处理库存区域的滚动
        if (inventoryBg.Contains(_input.MouseX, _input.MouseY))
        {
            int scroll = _input.ScrollDelta;
            if (scroll != 0)
            {
                _inventoryScrollOffset = Math.Clamp(_inventoryScrollOffset - scroll, 0, maxScroll);
            }
        }

        // Power icon + ratio (右侧固定位置)
        int rightSectionX = viewport.Width - 380;
        int powerX = rightSectionX;
        var powerRect = new Rectangle(powerX, y + 6, 18, 18);
        _spriteBatch.Draw(_pixel, powerRect, new Color(220, 170, 90));
        DrawRectOutline(powerRect, new Color(120, 80, 30), 2);
        int powerPercent = _powerUsed == 0 ? 100 : (int)MathF.Round(_powerRatio * 100f);
        DrawNumber(new Vector2(powerX + 28, y), powerPercent, new Color(230, 220, 200));
        if (_showTooltips && powerRect.Contains(_input.MouseX, _input.MouseY))
        {
            SetTooltip(T("TIP_POWER"), new Vector2(_input.MouseX + 12, _input.MouseY + 12));
        }

        // Research icon + points
        int researchX = powerX + 100;
        var researchRect = new Rectangle(researchX, y + 6, 18, 18);
        _spriteBatch.Draw(_pixel, researchRect, new Color(140, 220, 200));
        DrawRectOutline(researchRect, new Color(60, 120, 110), 2);
        DrawNumber(new Vector2(researchX + 28, y), _researchPoints, new Color(220, 240, 240));
        if (_showTooltips && researchRect.Contains(_input.MouseX, _input.MouseY))
        {
            SetTooltip(T("TIP_RESEARCH"), new Vector2(_input.MouseX + 12, _input.MouseY + 12));
        }

        if (_researchToastTimer > 0f && !string.IsNullOrWhiteSpace(_researchToast))
        {
            int toastScale = 2;
            Point size = MeasureTextSize(_researchToast, toastScale);
            int toastX = (viewport.Width - size.X) / 2;
            int toastY = bar.Y - size.Y - 12;
            var toastRect = new Rectangle(toastX - 10, toastY - 6, size.X + 20, size.Y + 12);
            _spriteBatch.Draw(_pixel, toastRect, new Color(18, 20, 26, 230));
            DrawRectOutline(toastRect, new Color(90, 120, 120), 2);
            DrawText(_researchToast, new Vector2(toastX, toastY), toastScale, new Color(200, 240, 240));
        }
    }

    private void DrawTutorial()
    {
        if (!_showTutorial || _tutorialDone)
        {
            return;
        }

        int width = 520;
        int height = 160;
        int x = 16;
        int y = ToolbarHeight + 16;
        var rect = new Rectangle(x, y, width, height);
        _spriteBatch.Draw(_pixel, rect, new Color(18, 20, 26, 220));
        DrawRectOutline(rect, new Color(80, 80, 90), 2);

        DrawText(T("TUTORIAL_TITLE"), new Vector2(x + 16, y + 12), 2, new Color(230, 230, 230));
        string step = GetTutorialText();
        DrawText(step, new Vector2(x + 16, y + 42), 2, new Color(200, 200, 200));
        DrawText(T("TUTORIAL_HINT"), new Vector2(x + 16, y + 120), 2, new Color(150, 150, 160));
    }

    private void DrawRecipeBook()
    {
        if (!_showRecipeBook)
        {
            return;
        }

        var viewport = GraphicsDevice.Viewport;
        int width = 600;
        int height = 480;
        int x = (viewport.Width - width) / 2;
        int y = (viewport.Height - height) / 2;
        var rect = new Rectangle(x, y, width, height);
        _spriteBatch.Draw(_pixel, rect, new Color(16, 20, 28, 245));
        DrawRectOutline(rect, new Color(100, 120, 140), 2);

        DrawText(T("RECIPE_TITLE"), new Vector2(x + 16, y + 12), 2, new Color(230, 230, 230));
        DrawText(T("RECIPE_HINT"), new Vector2(x + 16, y + height - 28), 2, new Color(150, 150, 160));

        // 页面导航
        int totalPages = 4;
        string pageText = $"{_recipeBookPage + 1}/{totalPages}";
        DrawText(pageText, new Vector2(x + width - 80, y + 12), 2, new Color(200, 200, 200));

        // 左右箭头按钮
        var leftBtn = new Rectangle(x + width - 140, y + 8, 24, 24);
        var rightBtn = new Rectangle(x + width - 50, y + 8, 24, 24);
        _spriteBatch.Draw(_pixel, leftBtn, new Color(40, 50, 60));
        _spriteBatch.Draw(_pixel, rightBtn, new Color(40, 50, 60));
        DrawText("<", new Vector2(leftBtn.X + 8, leftBtn.Y + 4), 2, new Color(220, 220, 220));
        DrawText(">", new Vector2(rightBtn.X + 8, rightBtn.Y + 4), 2, new Color(220, 220, 220));

        // 处理点击
        if (_input.LeftClicked)
        {
            Point mouse = new Point(_input.MouseX, _input.MouseY);
            if (leftBtn.Contains(mouse))
            {
                _recipeBookPage = (_recipeBookPage - 1 + totalPages) % totalPages;
            }
            else if (rightBtn.Contains(mouse))
            {
                _recipeBookPage = (_recipeBookPage + 1) % totalPages;
            }
        }

        int contentY = y + 50;
        int lineHeight = 26;

        switch (_recipeBookPage)
        {
            case 0: // 基础建筑
                DrawRecipeSection(x + 16, contentY, T("RECIPE_BASIC"), new[]
                {
                    (T("RECIPE_CONVEYOR"), T("RECIPE_CONVEYOR_DESC")),
                    (T("RECIPE_FAST_CONV"), T("RECIPE_FAST_CONV_DESC")),
                    (T("RECIPE_MINER"), T("RECIPE_MINER_DESC")),
                    (T("RECIPE_SMELTER"), T("RECIPE_SMELTER_DESC")),
                    (T("RECIPE_STORAGE"), T("RECIPE_STORAGE_DESC")),
                });
                break;
            case 1: // 物流建筑
                DrawRecipeSection(x + 16, contentY, T("RECIPE_LOGISTICS"), new[]
                {
                    (T("RECIPE_SPLITTER"), T("RECIPE_SPLITTER_DESC")),
                    (T("RECIPE_MERGER"), T("RECIPE_MERGER_DESC")),
                    (T("RECIPE_ROUTER"), T("RECIPE_ROUTER_DESC")),
                    (T("RECIPE_UNDERGROUND"), T("RECIPE_UNDERGROUND_DESC")),
                });
                break;
            case 2: // 生产建筑
                DrawRecipeSection(x + 16, contentY, T("RECIPE_PRODUCTION"), new[]
                {
                    (T("RECIPE_ASSEMBLER"), T("RECIPE_ASSEMBLER_DESC")),
                    (T("RECIPE_ASM_MK2"), T("RECIPE_ASM_MK2_DESC")),
                    (T("RECIPE_LAB"), T("RECIPE_LAB_DESC")),
                    (T("RECIPE_CHEM"), T("RECIPE_CHEM_DESC")),
                    (T("RECIPE_ADV_MINER"), T("RECIPE_ADV_MINER_DESC")),
                });
                break;
            case 3: // 电力系统
                DrawRecipeSection(x + 16, contentY, T("RECIPE_POWER"), new[]
                {
                    (T("RECIPE_GENERATOR"), T("RECIPE_GENERATOR_DESC")),
                    (T("RECIPE_COAL_GEN"), T("RECIPE_COAL_GEN_DESC")),
                    (T("RECIPE_POWER_INFO"), T("RECIPE_POWER_INFO_DESC")),
                    (T("RECIPE_SELL_POWER"), T("RECIPE_SELL_POWER_DESC")),
                });
                break;
        }
    }

    private void DrawRecipeSection(int x, int y, string title, (string name, string desc)[] recipes)
    {
        DrawText(title, new Vector2(x, y), 2, new Color(180, 200, 220));
        y += 32;

        foreach (var (name, desc) in recipes)
        {
            // 名称
            DrawText(name, new Vector2(x, y), 2, new Color(220, 200, 140));
            y += 22;
            // 描述
            DrawText(desc, new Vector2(x + 16, y), 2, new Color(180, 180, 190));
            y += 28;
        }
    }

    private void DrawDevMenu()
    {
        if (!_showDevMenu)
        {
            return;
        }

        var viewport = GraphicsDevice.Viewport;
        var lineTexts = new[]
        {
            T("DEV_TITLE"),
            $"{T("DEV_INVENTORY")}: {GetInventoryCount(ItemType.Plate)}/{GetInventoryCount(ItemType.Gear)}/{GetInventoryCount(ItemType.CopperPlate)}",
            $"{T("DEV_RESEARCH")}: {_researchPoints}",
            $"{T("DEV_PLATES")}: {_totalPlatesStored}",
            $"{T("DEV_SCIENCE")}: {_totalScienceStored}",
            T("DEV_ADD_RESEARCH"),
            T("DEV_ADD_MATERIALS"),
            T("DEV_ADD_PLATES"),
            T("DEV_ADD_SCIENCE"),
            T("DEV_HINTS")
        };

        int width = GetPanelWidth(lineTexts, 2, 32, 420);
        int height = 300;  // 增加高度以容纳新按钮
        int x = (viewport.Width - width) / 2;
        int y = (viewport.Height - height) / 2;
        var rect = new Rectangle(x, y, width, height);
        _spriteBatch.Draw(_pixel, rect, new Color(14, 18, 24, 245));
        DrawRectOutline(rect, new Color(90, 110, 120), 2);

        DrawText(T("DEV_TITLE"), new Vector2(x + 16, y + 16), 2, new Color(220, 230, 240));
        DrawText($"铁板/齿轮/铜板: {GetInventoryCount(ItemType.Plate)}/{GetInventoryCount(ItemType.Gear)}/{GetInventoryCount(ItemType.CopperPlate)}", new Vector2(x + 16, y + 44), 2, new Color(180, 190, 200));
        int colX = x + (width - 32) / 2;
        DrawText($"{T("DEV_PLATES")}: {_totalPlatesStored}", new Vector2(x + 16, y + 66), 2, new Color(180, 190, 200));
        DrawText($"{T("DEV_SCIENCE")}: {_totalScienceStored}", new Vector2(colX, y + 66), 2, new Color(180, 190, 200));
        DrawText($"{T("DEV_RESEARCH")}: {_researchPoints}", new Vector2(colX, y + 44), 2, new Color(180, 190, 200));

        // 添加调试模式状态显示
        DrawText($"调试模式: {(_debugMode ? "已启用" : "已禁用")}", new Vector2(x + 16, y + 88), 2, new Color(180, 190, 200));

        Rectangle[] buttons = GetDevButtons(rect);
        DrawDevButton(buttons[0], T("DEV_ADD_RESEARCH"));
        DrawDevButton(buttons[1], T("DEV_ADD_CREDITS"));
        DrawDevButton(buttons[2], T("DEV_ADD_PLATES"));
        DrawDevButton(buttons[3], T("DEV_ADD_SCIENCE"));
        DrawDevButton(buttons[4], "生成所有贴图 [T]");

        DrawText(T("DEV_HINTS"), new Vector2(x + 16, rect.Bottom - 28), 2, new Color(150, 160, 170));
    }

    private void DrawDebugMessage()
    {
        if (_debugMessageTimer > 0f && !string.IsNullOrEmpty(_debugMessage))
        {
            var viewport = GraphicsDevice.Viewport;
            float scale = 2f;
            
            // 估算文本宽度
            int textWidth = _debugMessage.Length * 12 * (int)scale;
            Vector2 textPos = new Vector2(
                (viewport.Width - textWidth) / 2f,
                50f
            );

            // 绘制半透明背景
            Rectangle background = new Rectangle(
                (int)(textPos.X - 10),
                (int)(textPos.Y - 5),
                textWidth + 20,
                30
            );
            _spriteBatch.Draw(_pixel, background, new Color(0, 0, 0, 150));

            // 绘制文本
            DrawText(_debugMessage, textPos, (int)scale, Color.White);
        }
    }

    private Rectangle[] GetDevButtons(Rectangle panel)
    {
        int buttonWidth = panel.Width - 32;
        int buttonHeight = 26;
        int startX = panel.X + 16;
        int startY = panel.Y + 98;
        int gap = 10;

        return new[]
        {
            new Rectangle(startX, startY, buttonWidth, buttonHeight),
            new Rectangle(startX, startY + (buttonHeight + gap), buttonWidth, buttonHeight),
            new Rectangle(startX, startY + (buttonHeight + gap) * 2, buttonWidth, buttonHeight),
            new Rectangle(startX, startY + (buttonHeight + gap) * 3, buttonWidth, buttonHeight),
            new Rectangle(startX, startY + (buttonHeight + gap) * 4, buttonWidth, buttonHeight)
        };
    }

    private void DrawDevButton(Rectangle rect, string label)
    {
        _spriteBatch.Draw(_pixel, rect, new Color(28, 32, 42, 240));
        DrawRectOutline(rect, new Color(70, 90, 110), 2);
        DrawText(label, new Vector2(rect.X + 10, rect.Y + 6), 2, new Color(210, 220, 230));
    }

    private void HandleDevMenuInput()
    {
        if (!_showDevMenu)
        {
            return;
        }

        if (_input.KeyPressed(Keys.F3))
        {
            _showDevMenu = false;
            return;
        }

        if (_input.KeyPressed(Keys.Q)) DevAddResearch(5);
        if (_input.KeyPressed(Keys.W)) DevAddMaterials(50);
        if (_input.KeyPressed(Keys.E)) DevAddPlates(10);
        if (_input.KeyPressed(Keys.R)) DevAddScience(10);
        if (_input.KeyPressed(Keys.T))
        {
            GenerateAllTextures();
            _debugMessage = "贴图已生成到 Content/Textures 目录";
            _debugMessageTimer = 3f;
        }

        if (_input.LeftClicked)
        {
            var viewport = GraphicsDevice.Viewport;
            int width = 420;
            int height = 260;
            int x = (viewport.Width - width) / 2;
            int y = (viewport.Height - height) / 2;
            var panel = new Rectangle(x, y, width, height);
            Rectangle[] buttons = GetDevButtons(panel);
            Point mouse = new Point(_input.MouseX, _input.MouseY);

            if (buttons[0].Contains(mouse)) DevAddResearch(5);
            else if (buttons[1].Contains(mouse)) DevAddMaterials(50);
            else if (buttons[2].Contains(mouse)) DevAddPlates(10);
            else if (buttons[3].Contains(mouse)) DevAddScience(10);
        }
    }

    private void DevAddResearch(int amount)
    {
        _researchPoints += amount;
        _totalScienceStored += amount;
        CheckResearchUnlocks();
        _sfxUnlock?.Play(GetSfxVolume(0.8f), 0f, 0f);
    }

    private void DevAddMaterials(int amount)
    {
        AddToInventory(ItemType.Plate, amount);
        AddToInventory(ItemType.Gear, amount / 2);
        AddToInventory(ItemType.CopperPlate, amount / 2);
        _sfxPlace?.Play(GetSfxVolume(0.6f), 0f, 0f);
    }

    private void DevAddPlates(int amount)
    {
        _totalPlatesStored += amount;
        _sfxPlace?.Play(GetSfxVolume(0.6f), 0f, 0f);
    }

    private void DevAddScience(int amount)
    {
        _totalScienceStored += amount;
        _researchPoints += amount;
        CheckResearchUnlocks();
        _sfxUnlock?.Play(GetSfxVolume(0.8f), 0f, 0f);
    }

    private int GetPanelWidth(string[] lines, int scale, int padding, int minWidth)
    {
        int maxWidth = minWidth;
        for (int i = 0; i < lines.Length; i++)
        {
            Point size = MeasureTextSize(lines[i], scale);
            maxWidth = Math.Max(maxWidth, size.X + padding * 2);
        }
        return maxWidth;
    }

    private string GetTutorialText()
    {
        return _tutorialStep switch
        {
            0 => T("TUTORIAL_1"),
            1 => T("TUTORIAL_2"),
            2 => T("TUTORIAL_3"),
            3 => T("TUTORIAL_4"),
            4 => T("TUTORIAL_5"),
            _ => T("TUTORIAL_DONE")
        };
    }

    private void DrawRectOutline(Rectangle rect, Color color, int thickness)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }

    private void DrawToolIcon(Tool tool, Rectangle rect)
    {
        Vector2 center = new Vector2(rect.Center.X, rect.Center.Y);
        float size = rect.Width * 0.35f;
        switch (tool)
        {
            case Tool.Conveyor:
                DrawLine(center - new Vector2(size, 0), center + new Vector2(size, 0), new Color(240, 240, 240), 3f);
                DrawLine(center + new Vector2(size, 0), center + new Vector2(size - 8, -6), new Color(240, 240, 240), 3f);
                DrawLine(center + new Vector2(size, 0), center + new Vector2(size - 8, 6), new Color(240, 240, 240), 3f);
                break;
            case Tool.FastConveyor:
                DrawLine(center - new Vector2(size, 0), center + new Vector2(size, 0), new Color(240, 240, 240), 4f);
                DrawLine(center + new Vector2(size, 0), center + new Vector2(size - 10, -8), new Color(240, 240, 240), 4f);
                DrawLine(center + new Vector2(size, 0), center + new Vector2(size - 10, 8), new Color(240, 240, 240), 4f);
                break;
            case Tool.Splitter:
                DrawLine(center + new Vector2(-size * 0.9f, 0), center, new Color(240, 240, 240), 3f);
                DrawLine(center, center + new Vector2(size * 0.9f, -size * 0.6f), new Color(240, 240, 240), 3f);
                DrawLine(center, center + new Vector2(size * 0.9f, size * 0.6f), new Color(240, 240, 240), 3f);
                break;
            case Tool.Merger:
                DrawLine(center + new Vector2(-size * 0.9f, -size * 0.6f), center, new Color(240, 240, 240), 3f);
                DrawLine(center + new Vector2(-size * 0.9f, size * 0.6f), center, new Color(240, 240, 240), 3f);
                DrawLine(center, center + new Vector2(size * 0.9f, 0), new Color(240, 240, 240), 3f);
                break;
            case Tool.Router:
                // 路由器图标：中心点向四个方向发散
                DrawLine(center, center + new Vector2(size * 0.8f, 0), new Color(240, 240, 240), 3f);
                DrawLine(center, center + new Vector2(-size * 0.8f, 0), new Color(240, 240, 240), 3f);
                DrawLine(center, center + new Vector2(0, size * 0.8f), new Color(240, 240, 240), 3f);
                DrawLine(center, center + new Vector2(0, -size * 0.8f), new Color(240, 240, 240), 3f);
                _spriteBatch.Draw(_pixel, new Rectangle((int)(center.X - 4), (int)(center.Y - 4), 8, 8), new Color(240, 240, 240));
                break;
            case Tool.Miner:
                _spriteBatch.Draw(_pixel, new Rectangle((int)(center.X - size * 0.6f), (int)(center.Y - size * 0.6f), (int)(size * 1.2f), (int)(size * 1.2f)), new Color(15, 30, 15));
                DrawLine(center + new Vector2(-size, -size), center + new Vector2(size, size), new Color(240, 240, 240), 3f);
                DrawLine(center + new Vector2(-size, size), center + new Vector2(size, -size), new Color(240, 240, 240), 3f);
                break;
            case Tool.Smelter:
                _spriteBatch.Draw(_pixel, new Rectangle((int)(center.X - size * 0.7f), (int)(center.Y - size * 0.5f), (int)(size * 1.4f), (int)(size * 1.0f)), new Color(60, 25, 10));
                DrawLine(center + new Vector2(-size * 0.5f, size * 0.2f), center + new Vector2(0, -size * 0.5f), new Color(240, 240, 240), 3f);
                DrawLine(center + new Vector2(0, -size * 0.5f), center + new Vector2(size * 0.5f, size * 0.2f), new Color(240, 240, 240), 3f);
                break;
            case Tool.Assembler:
                _spriteBatch.Draw(_pixel, new Rectangle((int)(center.X - size * 0.6f), (int)(center.Y - size * 0.6f), (int)(size * 1.2f), (int)(size * 1.2f)), new Color(40, 20, 60));
                DrawLine(center + new Vector2(-size, 0), center + new Vector2(size, 0), new Color(240, 240, 240), 3f);
                DrawLine(center + new Vector2(0, -size), center + new Vector2(0, size), new Color(240, 240, 240), 3f);
                break;
            case Tool.Lab:
                _spriteBatch.Draw(_pixel, new Rectangle((int)(center.X - size * 0.7f), (int)(center.Y - size * 0.5f), (int)(size * 1.4f), (int)(size * 1.0f)), new Color(20, 60, 70));
                DrawLine(center + new Vector2(-size * 0.4f, -size * 0.2f), center + new Vector2(size * 0.4f, -size * 0.2f), new Color(240, 240, 240), 3f);
                DrawLine(center + new Vector2(0, -size * 0.2f), center + new Vector2(0, size * 0.5f), new Color(240, 240, 240), 3f);
                break;
            case Tool.Generator:
                _spriteBatch.Draw(_pixel, new Rectangle((int)(center.X - size * 0.6f), (int)(center.Y - size * 0.6f), (int)(size * 1.2f), (int)(size * 1.2f)), new Color(80, 60, 20));
                DrawLine(center + new Vector2(-size * 0.3f, -size * 0.4f), center + new Vector2(size * 0.3f, size * 0.4f), new Color(240, 240, 240), 3f);
                DrawLine(center + new Vector2(size * 0.3f, -size * 0.4f), center + new Vector2(-size * 0.3f, size * 0.4f), new Color(240, 240, 240), 3f);
                break;
            case Tool.Storage:
                DrawRectOutline(new Rectangle((int)(center.X - size * 0.7f), (int)(center.Y - size * 0.5f), (int)(size * 1.4f), (int)(size * 1.0f)), new Color(240, 240, 240), 3);
                break;
            case Tool.Erase:
                DrawLine(center + new Vector2(-size, -size), center + new Vector2(size, size), new Color(240, 80, 80), 4f);
                DrawLine(center + new Vector2(-size, size), center + new Vector2(size, -size), new Color(240, 80, 80), 4f);
                break;
        }
    }

    private Color GetToolColor(Tool tool)
    {
        return tool switch
        {
            Tool.Conveyor => new Color(70, 110, 160),
            Tool.FastConveyor => new Color(90, 150, 210),
            Tool.Splitter => new Color(90, 120, 170),
            Tool.Merger => new Color(80, 120, 150),
            Tool.Router => new Color(110, 140, 180),
            Tool.Miner => new Color(80, 130, 80),
            Tool.Smelter => new Color(170, 110, 70),
            Tool.Assembler => new Color(120, 90, 150),
            Tool.Lab => new Color(80, 140, 150),
            Tool.Generator => new Color(160, 160, 90),
            Tool.Storage => new Color(130, 130, 70),
            Tool.Erase => new Color(120, 60, 60),
            // 新增工具颜色
            Tool.UndergroundBelt => new Color(60, 90, 130),
            Tool.CoalGenerator => new Color(100, 80, 60),
            Tool.AdvancedMiner => new Color(100, 160, 100),
            Tool.AssemblerMk2 => new Color(150, 110, 180),
            Tool.ChemicalPlant => new Color(100, 170, 130),
            _ => new Color(80, 80, 80)
        };
    }

    private bool HandleToolbarClick()
    {
        if (!_input.LeftClicked)
        {
            return false;
        }

        Point mouse = new Point(_input.MouseX, _input.MouseY);
        if (mouse.Y > ToolbarHeight)
        {
            return false;
        }

        if (_toolbarTools.Length == 0)
        {
            return true;
        }

        for (int i = 0; i < _toolButtons.Length; i++)
        {
            if (_toolButtons[i].Contains(mouse))
            {
                _tool = _toolbarTools[i];
                return true;
            }
        }

        if (_directionButton.Contains(mouse))
        {
            _direction = DirectionUtil.RotateCW(_direction);
            _sfxRotate?.Play(GetSfxVolume(1f), 0f, 0f);
            return true;
        }

        return true;
    }

    private void DrawDirectionArrow(Vector2 center, Direction direction, Color color)
    {
        float length = TileWidth * 0.22f * _camera.Zoom;
        DrawArrowAt(center, direction, length, color, 2f * _camera.Zoom);
    }

    private void DrawSplitterOverlay(Vector2 center, Direction direction)
    {
        float length = TileWidth * 0.18f * _camera.Zoom;
        float thickness = 3f * _camera.Zoom;
        Color outline = new Color(10, 10, 12, 220);
        Color bright = new Color(250, 250, 250, 230);
        Color inputColor = new Color(80, 210, 255, 230);

        foreach (Direction outDir in GetSplitterOutputs(direction))
        {
            DrawArrowAt(center, outDir, length, outline, thickness + 2f);
            DrawArrowAt(center, outDir, length, bright, thickness);
        }

        Direction input = Opposite(direction);
        Vector2 dir = DirectionToScreenVector(input);
        if (dir != Vector2.Zero)
        {
            Vector2 start = center - dir * (length * 0.9f);
            DrawArrowAt(start, input, length * 0.9f, outline, thickness + 2f);
            DrawArrowAt(start, input, length * 0.9f, inputColor, thickness);
        }
    }

    private void DrawMergerOverlay(Vector2 center, Direction direction)
    {
        float length = TileWidth * 0.18f * _camera.Zoom;
        float thickness = 3f * _camera.Zoom;
        Color outline = new Color(10, 10, 12, 220);
        Color inputColor = new Color(120, 230, 160, 230);
        Color outputColor = new Color(255, 245, 140, 230);

        foreach (Direction inDir in GetMergerInputs(direction))
        {
            Vector2 dir = DirectionToScreenVector(inDir);
            if (dir == Vector2.Zero)
            {
                continue;
            }
            Vector2 start = center - dir * (length * 0.9f);
            DrawArrowAt(start, inDir, length * 0.9f, outline, thickness + 2f);
            DrawArrowAt(start, inDir, length * 0.9f, inputColor, thickness);
        }

        DrawArrowAt(center, direction, length * 1.2f, outline, thickness + 2f);
        DrawArrowAt(center, direction, length * 1.2f, outputColor, thickness);
    }

    private void DrawRouterOverlay(Vector2 center, Point tile)
    {
        float length = TileWidth * 0.16f * _camera.Zoom;
        float thickness = 3f * _camera.Zoom;
        Color outline = new Color(10, 10, 12, 220);
        Color inputColor = new Color(120, 230, 160, 230);
        Color outputColor = new Color(255, 245, 140, 230);

        var allDirs = new[] { Direction.North, Direction.East, Direction.South, Direction.West };

        foreach (var dir in allDirs)
        {
            Vector2 dirVec = DirectionToScreenVector(dir);
            if (dirVec == Vector2.Zero)
            {
                continue;
            }

            bool isInput = IsInputDirection(tile, dir);
            Color arrowColor = isInput ? inputColor : outputColor;

            if (isInput)
            {
                // 输入箭头：从外向内
                Vector2 start = center - dirVec * (length * 1.2f);
                DrawArrowAt(start, dir, length * 0.9f, outline, thickness + 2f);
                DrawArrowAt(start, dir, length * 0.9f, arrowColor, thickness);
            }
            else
            {
                // 输出箭头：从中心向外
                DrawArrowAt(center, dir, length * 1.0f, outline, thickness + 2f);
                DrawArrowAt(center, dir, length * 1.0f, arrowColor, thickness);
            }
        }
    }

    private void DrawArrowAt(Vector2 start, Direction direction, float length, Color color, float thickness)
    {
        Vector2 baseDir = DirectionToScreenVector(direction);
        if (baseDir == Vector2.Zero)
        {
            return;
        }

        Vector2 end = start + baseDir * length;
        DrawLine(start, end, color, thickness);

        Vector2 left = Rotate(baseDir, 0.7f);
        Vector2 right = Rotate(baseDir, -0.7f);
        DrawLine(end, end - left * (length * 0.35f), color, thickness);
        DrawLine(end, end - right * (length * 0.35f), color, thickness);
    }

    private Vector2 DirectionToScreenVector(Direction direction)
    {
        if (direction == Direction.None)
        {
            return Vector2.Zero;
        }

        Point dir = DirectionUtil.ToPoint(direction);
        Vector2 neighbor = WorldToScreen(new Vector2(dir.X, dir.Y));
        Vector2 baseDir = neighbor - WorldToScreen(Vector2.Zero);
        if (baseDir.LengthSquared() < 0.001f)
        {
            return Vector2.Zero;
        }
        baseDir.Normalize();
        return baseDir;
    }

    private Direction RotateCCW(Direction direction)
    {
        return direction switch
        {
            Direction.North => Direction.West,
            Direction.West => Direction.South,
            Direction.South => Direction.East,
            Direction.East => Direction.North,
            _ => Direction.West
        };
    }

    private Direction[] GetMergerInputs(Direction direction)
    {
        return new[] { Opposite(direction), DirectionUtil.RotateCW(direction), RotateCCW(direction) };
    }

    private void DrawStorageMarker(Vector2 center)
    {
        float size = 8f * _camera.Zoom;
        _spriteBatch.Draw(_pixel, new Rectangle((int)(center.X - size / 2f), (int)(center.Y - size / 2f), (int)size, (int)size), new Color(250, 240, 200));
    }

    private void DrawLine(Vector2 start, Vector2 end, Color color, float thickness)
    {
        Vector2 edge = end - start;
        float length = edge.Length();
        if (length < 0.01f)
        {
            return;
        }

        float rotation = MathF.Atan2(edge.Y, edge.X);
        _spriteBatch.Draw(_pixel, start, null, color, rotation, new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
    }

    private void SetTooltip(string text, Vector2 position)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _hoverTooltip = _language == Language.English ? text.ToUpperInvariant() : text;
        _hoverTooltipPos = position;
    }

    private void DrawTooltip()
    {
        if (string.IsNullOrWhiteSpace(_hoverTooltip))
        {
            return;
        }

        int scale = 2;
        int padding = 6;
        Point textSize = MeasureTextSize(_hoverTooltip, scale);
        int textWidth = textSize.X;
        int textHeight = textSize.Y;
        int boxWidth = textWidth + padding * 2;
        int boxHeight = textHeight + padding * 2;

        int x = (int)_hoverTooltipPos.X;
        int y = (int)_hoverTooltipPos.Y;
        if (x + boxWidth > GraphicsDevice.Viewport.Width)
        {
            x = GraphicsDevice.Viewport.Width - boxWidth - 4;
        }
        if (y + boxHeight > GraphicsDevice.Viewport.Height)
        {
            y = GraphicsDevice.Viewport.Height - boxHeight - 4;
        }

        var rect = new Rectangle(x, y, boxWidth, boxHeight);
        _spriteBatch.Draw(_pixel, rect, new Color(18, 20, 26, 230));
        DrawRectOutline(rect, new Color(80, 80, 90), 2);
        DrawText(_hoverTooltip, new Vector2(x + padding, y + padding), scale, new Color(230, 230, 230));
    }

    private void DrawSettings()
    {
        if (!_showSettings)
        {
            return;
        }

        int width = 420;
        int height = 304;
        int x = (GraphicsDevice.Viewport.Width - width) / 2;
        int y = (GraphicsDevice.Viewport.Height - height) / 2;
        var rect = new Rectangle(x, y, width, height);
        _spriteBatch.Draw(_pixel, rect, new Color(16, 18, 24, 240));
        DrawRectOutline(rect, new Color(80, 80, 90), 2);

        DrawText(T("SETTINGS_TITLE"), new Vector2(x + 16, y + 16), 2, new Color(230, 230, 230));

        int lineY = y + 52;
        DrawText($"1 {T("SET_GRID")}: {OnOff(_showGrid)}", new Vector2(x + 16, lineY), 2, new Color(200, 200, 200));
        lineY += 22;
        DrawText($"2 {T("SET_ORE")}: {OnOff(_showOreHighlight)}", new Vector2(x + 16, lineY), 2, new Color(200, 200, 200));
        lineY += 22;
        DrawText($"3 {T("SET_TIPS")}: {OnOff(_showTooltips)}", new Vector2(x + 16, lineY), 2, new Color(200, 200, 200));
        lineY += 22;
        DrawText($"4 {T("SET_AUTOPAUSE")}: {OnOff(_autoPauseOnFocusLoss)}", new Vector2(x + 16, lineY), 2, new Color(200, 200, 200));
        lineY += 22;
        DrawText($"5 {T("SET_VOLUME")}: {(int)System.MathF.Round(_sfxVolume * 100f)}%", new Vector2(x + 16, lineY), 2, new Color(200, 200, 200));
        lineY += 22;
        DrawText($"6 {T("SET_TUTORIAL")}: {OnOff(_showTutorial)}", new Vector2(x + 16, lineY), 2, new Color(200, 200, 200));
        lineY += 22;
        DrawText($"7 {T("SET_LANG")}: {(_language == Language.Chinese ? "中文" : "EN")}", new Vector2(x + 16, lineY), 2, new Color(200, 200, 200));
        lineY += 22;
        DrawText($"8 {T("SET_DEV")}: {OnOff(_developerMode)}", new Vector2(x + 16, lineY), 2, new Color(200, 200, 200));
        lineY += 22;
        DrawText($"9 {T("SET_DEVMENU")}: {OnOff(_showDevMenu)}", new Vector2(x + 16, lineY), 2, new Color(200, 200, 200));
        lineY += 26;
        DrawText(T("SET_HINTS"), new Vector2(x + 16, lineY), 2, new Color(160, 160, 170));
    }

    private string OnOff(bool value) => _language == Language.Chinese ? (value ? "开" : "关") : (value ? "ON" : "OFF");

    private void HandleSettingsInput()
    {
        if (_input.KeyPressed(Keys.D1)) _showGrid = !_showGrid;
        if (_input.KeyPressed(Keys.D2)) _showOreHighlight = !_showOreHighlight;
        if (_input.KeyPressed(Keys.D3)) _showTooltips = !_showTooltips;
        if (_input.KeyPressed(Keys.D4)) _autoPauseOnFocusLoss = !_autoPauseOnFocusLoss;
        if (_input.KeyPressed(Keys.D7)) _language = _language == Language.Chinese ? Language.English : Language.Chinese;
        if (_input.KeyPressed(Keys.D6)) _showTutorial = !_showTutorial;
        if (_input.KeyPressed(Keys.D8))
        {
            _developerMode = !_developerMode;
            if (!_developerMode)
            {
                _showDevMenu = false;
            }
        }
        if (_input.KeyPressed(Keys.D9))
        {
            if (_developerMode)
            {
                _showDevMenu = !_showDevMenu;
            }
            else
            {
                _sfxError?.Play(GetSfxVolume(0.6f), 0f, 0f);
            }
        }

        if (_input.KeyPressed(Keys.OemPlus) || _input.KeyPressed(Keys.Add))
        {
            _sfxVolume = MathHelper.Clamp(_sfxVolume + 0.1f, 0f, 1f);
        }
        if (_input.KeyPressed(Keys.OemMinus) || _input.KeyPressed(Keys.Subtract))
        {
            _sfxVolume = MathHelper.Clamp(_sfxVolume - 0.1f, 0f, 1f);
        }
    }

    private Point MeasureTextSize(string text, int scale)
    {
        if (RequiresSystemFont(text))
        {
            SystemTextEntry entry = GetSystemText(text, scale);
            return new Point(entry.Width, entry.Height);
        }

        int width = 0;
        foreach (char c in text)
        {
            width += (GetCharWidth(c) + 1) * scale;
        }
        width = Math.Max(0, width - scale);
        return new Point(width, 7 * scale);
    }

    private int GetCharWidth(char c)
    {
        return c == ' ' ? 3 : 5;
    }

    private void DrawText(string text, Vector2 position, int scale, Color color)
    {
        if (RequiresSystemFont(text))
        {
            DrawTextSystem(text, position, scale, color);
            return;
        }

        float x = position.X;
        foreach (char c in text)
        {
            DrawChar(c, new Vector2(x, position.Y), scale, color);
            x += (GetCharWidth(c) + 1) * scale;
        }
    }

    private bool RequiresSystemFont(string text)
    {
        foreach (char c in text)
        {
            if (c > 127)
            {
                return true;
            }
        }
        return false;
    }

    private void DrawTextSystem(string text, Vector2 position, int scale, Color color)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        SystemTextEntry entry = GetSystemText(text, scale);
        _spriteBatch.Draw(entry.Texture, position, color);
    }

    private SystemTextEntry GetSystemText(string text, int scale)
    {
        string key = $"{scale}|{text}";
        if (_systemTextCache.TryGetValue(key, out SystemTextEntry? entry))
        {
            _systemTextLru.Remove(entry.Node);
            _systemTextLru.AddFirst(entry.Node);
            return entry;
        }

        if (_systemTextCache.Count >= SystemTextCacheLimit)
        {
            string evictKey = _systemTextLru.Last!.Value;
            SystemTextEntry evictEntry = _systemTextCache[evictKey];
            evictEntry.Texture.Dispose();
            _systemTextCache.Remove(evictKey);
            _systemTextLru.RemoveLast();
        }

        SystemTextEntry created = CreateSystemText(text, scale);
        _systemTextCache[key] = created;
        _systemTextLru.AddFirst(created.Node);
        return created;
    }

    private SystemTextEntry CreateSystemText(string text, int scale)
    {
        float fontSize = 8f * scale;
        using var font = new System.Drawing.Font("Microsoft YaHei UI", fontSize, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
        System.Drawing.Size size;
        using (var measureBmp = new System.Drawing.Bitmap(1, 1, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
        using (var g = System.Drawing.Graphics.FromImage(measureBmp))
        {
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            System.Drawing.SizeF sizeF = g.MeasureString(text, font, int.MaxValue, System.Drawing.StringFormat.GenericTypographic);
            size = new System.Drawing.Size((int)Math.Ceiling(sizeF.Width), (int)Math.Ceiling(sizeF.Height));
        }

        int width = Math.Max(1, size.Width);
        int height = Math.Max(1, size.Height);
        using var bmp = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.Clear(System.Drawing.Color.Transparent);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.DrawString(text, font, System.Drawing.Brushes.White, new System.Drawing.PointF(0f, 0f), System.Drawing.StringFormat.GenericTypographic);
        }

        System.Drawing.Imaging.BitmapData data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, width, height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        int stride = data.Stride;
        int bytes = Math.Abs(stride) * height;
        byte[] pixelBytes = new byte[bytes];
        Marshal.Copy(data.Scan0, pixelBytes, 0, bytes);
        bmp.UnlockBits(data);

        Color[] colors = new Color[width * height];
        if (stride < 0)
        {
            stride = -stride;
        }

        for (int y = 0; y < height; y++)
        {
            int row = y * stride;
            for (int x = 0; x < width; x++)
            {
                int i = row + x * 4;
                byte b = pixelBytes[i + 0];
                byte g = pixelBytes[i + 1];
                byte r = pixelBytes[i + 2];
                byte a = pixelBytes[i + 3];
                colors[y * width + x] = new Color(r, g, b, a);
            }
        }

        var texture = new Texture2D(GraphicsDevice, width, height, false, SurfaceFormat.Color);
        texture.SetData(colors);

        var node = new LinkedListNode<string>($"{scale}|{text}");
        return new SystemTextEntry
        {
            Texture = texture,
            Width = width,
            Height = height,
            Node = node
        };
    }

    private void DrawChar(char c, Vector2 pos, int scale, Color color)
    {
        if (c == ' ')
        {
            return;
        }

        byte[] rows = GetCharPattern(c);
        for (int y = 0; y < rows.Length; y++)
        {
            byte row = rows[y];
            for (int x = 0; x < 5; x++)
            {
                if ((row & (1 << (4 - x))) != 0)
                {
                    _spriteBatch.Draw(_pixel, new Rectangle((int)pos.X + x * scale, (int)pos.Y + y * scale, scale, scale), color);
                }
            }
        }
    }

    private byte[] GetCharPattern(char c)
    {
        return c switch
        {
            'A' => new byte[] { 0b01110, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001 },
            'B' => new byte[] { 0b11110, 0b10001, 0b10001, 0b11110, 0b10001, 0b10001, 0b11110 },
            'C' => new byte[] { 0b01111, 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b01111 },
            'D' => new byte[] { 0b11110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b11110 },
            'E' => new byte[] { 0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b11111 },
            'F' => new byte[] { 0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b10000 },
            'G' => new byte[] { 0b01111, 0b10000, 0b10000, 0b10111, 0b10001, 0b10001, 0b01110 },
            'H' => new byte[] { 0b10001, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001 },
            'I' => new byte[] { 0b01110, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110 },
            'J' => new byte[] { 0b00111, 0b00010, 0b00010, 0b00010, 0b00010, 0b10010, 0b01100 },
            'K' => new byte[] { 0b10001, 0b10010, 0b10100, 0b11000, 0b10100, 0b10010, 0b10001 },
            'L' => new byte[] { 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b11111 },
            'M' => new byte[] { 0b10001, 0b11011, 0b10101, 0b10101, 0b10001, 0b10001, 0b10001 },
            'N' => new byte[] { 0b10001, 0b11001, 0b10101, 0b10011, 0b10001, 0b10001, 0b10001 },
            'O' => new byte[] { 0b01110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110 },
            'P' => new byte[] { 0b11110, 0b10001, 0b10001, 0b11110, 0b10000, 0b10000, 0b10000 },
            'Q' => new byte[] { 0b01110, 0b10001, 0b10001, 0b10001, 0b10101, 0b10010, 0b01101 },
            'R' => new byte[] { 0b11110, 0b10001, 0b10001, 0b11110, 0b10100, 0b10010, 0b10001 },
            'S' => new byte[] { 0b01111, 0b10000, 0b10000, 0b01110, 0b00001, 0b00001, 0b11110 },
            'T' => new byte[] { 0b11111, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100 },
            'U' => new byte[] { 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110 },
            'V' => new byte[] { 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01010, 0b00100 },
            'W' => new byte[] { 0b10001, 0b10001, 0b10001, 0b10101, 0b10101, 0b10101, 0b01010 },
            'X' => new byte[] { 0b10001, 0b10001, 0b01010, 0b00100, 0b01010, 0b10001, 0b10001 },
            'Y' => new byte[] { 0b10001, 0b10001, 0b01010, 0b00100, 0b00100, 0b00100, 0b00100 },
            'Z' => new byte[] { 0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b10000, 0b11111 },
            '0' => new byte[] { 0b01110, 0b10001, 0b10011, 0b10101, 0b11001, 0b10001, 0b01110 },
            '1' => new byte[] { 0b00100, 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110 },
            '2' => new byte[] { 0b01110, 0b10001, 0b00001, 0b00010, 0b00100, 0b01000, 0b11111 },
            '3' => new byte[] { 0b11110, 0b00001, 0b00001, 0b01110, 0b00001, 0b00001, 0b11110 },
            '4' => new byte[] { 0b00010, 0b00110, 0b01010, 0b10010, 0b11111, 0b00010, 0b00010 },
            '5' => new byte[] { 0b11111, 0b10000, 0b10000, 0b11110, 0b00001, 0b00001, 0b11110 },
            '6' => new byte[] { 0b01110, 0b10000, 0b10000, 0b11110, 0b10001, 0b10001, 0b01110 },
            '7' => new byte[] { 0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b01000, 0b01000 },
            '8' => new byte[] { 0b01110, 0b10001, 0b10001, 0b01110, 0b10001, 0b10001, 0b01110 },
            '9' => new byte[] { 0b01110, 0b10001, 0b10001, 0b01111, 0b00001, 0b00001, 0b01110 },
            '.' => new byte[] { 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b01100, 0b01100 },
            ':' => new byte[] { 0b00000, 0b01100, 0b01100, 0b00000, 0b01100, 0b01100, 0b00000 },
            '-' => new byte[] { 0b00000, 0b00000, 0b00000, 0b11111, 0b00000, 0b00000, 0b00000 },
            '/' => new byte[] { 0b00001, 0b00010, 0b00100, 0b01000, 0b10000, 0b00000, 0b00000 },
            '%' => new byte[] { 0b11001, 0b11010, 0b00100, 0b01000, 0b10110, 0b00110, 0b00000 },
            _ => new byte[] { 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b00000 }
        };
    }

    private void DrawNumber(Vector2 position, int value, Color color)
    {
        if (value < 0)
        {
            value = 0;
        }

        string text = value.ToString();
        int size = 12;
        int spacing = 4;
        for (int i = 0; i < text.Length; i++)
        {
            int digit = text[i] - '0';
            DrawDigit(position + new Vector2(i * (size + spacing), 0), size, digit, color);
        }
    }

    private void DrawDigit(Vector2 pos, int size, int digit, Color color)
    {
        int w = size;
        int h = size * 2;
        int t = Math.Max(2, size / 5);

        bool a = digit switch { 0 or 2 or 3 or 5 or 6 or 7 or 8 or 9 => true, _ => false };
        bool b = digit switch { 0 or 1 or 2 or 3 or 4 or 7 or 8 or 9 => true, _ => false };
        bool c = digit switch { 0 or 1 or 3 or 4 or 5 or 6 or 7 or 8 or 9 => true, _ => false };
        bool d = digit switch { 0 or 2 or 3 or 5 or 6 or 8 or 9 => true, _ => false };
        bool e = digit switch { 0 or 2 or 6 or 8 => true, _ => false };
        bool f = digit switch { 0 or 4 or 5 or 6 or 8 or 9 => true, _ => false };
        bool g = digit switch { 2 or 3 or 4 or 5 or 6 or 8 or 9 => true, _ => false };

        if (a) _spriteBatch.Draw(_pixel, new Rectangle((int)pos.X, (int)pos.Y, w, t), color);
        if (b) _spriteBatch.Draw(_pixel, new Rectangle((int)(pos.X + w - t), (int)pos.Y, t, h / 2), color);
        if (c) _spriteBatch.Draw(_pixel, new Rectangle((int)(pos.X + w - t), (int)(pos.Y + h / 2), t, h / 2), color);
        if (d) _spriteBatch.Draw(_pixel, new Rectangle((int)pos.X, (int)(pos.Y + h - t), w, t), color);
        if (e) _spriteBatch.Draw(_pixel, new Rectangle((int)pos.X, (int)(pos.Y + h / 2), t, h / 2), color);
        if (f) _spriteBatch.Draw(_pixel, new Rectangle((int)pos.X, (int)pos.Y, t, h / 2), color);
        if (g) _spriteBatch.Draw(_pixel, new Rectangle((int)pos.X, (int)(pos.Y + h / 2 - t / 2), w, t), color);
    }

    private Vector2 Rotate(Vector2 v, float radians)
    {
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

    private Vector2 WorldToScreen(Vector2 world)
    {
        float x = (world.X - world.Y) * (TileWidth / 2f);
        float y = (world.X + world.Y) * (TileHeight / 2f);
        Vector2 iso = new Vector2(x, y);
        return (iso + _camera.Pan) * _camera.Zoom + _origin;
    }

    private Vector2 ScreenToWorld(Vector2 screen)
    {
        Vector2 local = (screen - _origin) / _camera.Zoom - _camera.Pan;
        float wx = (local.Y / (TileHeight / 2f) + local.X / (TileWidth / 2f)) * 0.5f;
        float wy = (local.Y / (TileHeight / 2f) - local.X / (TileWidth / 2f)) * 0.5f;
        return new Vector2(wx, wy);
    }

    private Point? GetHoveredTile()
    {
        if (_input.MouseY <= ToolbarHeight)
        {
            return null;
        }
        if (_input.MouseY >= GraphicsDevice.Viewport.Height - TaskbarHeight)
        {
            return null;
        }

        Vector2 mouse = new Vector2(_input.MouseX, _input.MouseY);
        Vector2 world = ScreenToWorld(mouse);
        int tx = (int)MathF.Floor(world.X + 0.5f);
        int ty = (int)MathF.Floor(world.Y + 0.5f);
        if (tx < 0 || ty < 0 || tx >= MapWidth || ty >= MapHeight)
        {
            return null;
        }
        return new Point(tx, ty);
    }

    private void PlaceTile(Point tile)
    {
        if (!InBounds(tile))
        {
            return;
        }

        // 检查地形是否可建造
        TerrainType terrain = _terrainMap[tile.X, tile.Y];
        if (terrain == TerrainType.Locked)
        {
            _sfxError.Play(GetSfxVolume(1f), 0f, 0f);
            return; // 未解锁区域
        }
        if (terrain == TerrainType.Mountain || terrain == TerrainType.Water)
        {
            _sfxError.Play(GetSfxVolume(1f), 0f, 0f);
            return; // 不可建造地形
        }

        if (_tool != Tool.Erase)
        {
            if (!IsToolUnlocked(_tool))
            {
                _sfxError.Play(GetSfxVolume(1f), 0f, 0f);
                return;
            }

            (TileType type, Direction direction) = GetPlacementSpec();
            Tile current = _tiles[tile.X, tile.Y];
            if (current.Type == type && current.Direction == direction)
            {
                return;
            }

            // 矿机需要检查是否有矿点（多方块矿机检查覆盖范围内是否有矿点）
            if (_tool == Tool.Miner || _tool == Tool.AdvancedMiner)
            {
                int size = _tool == Tool.AdvancedMiner ? 3 : 2;
                // 计算主格子位置（最左边）
                Point mainTilePos = new Point(tile.X - (size - 1), tile.Y);
                if (!HasOreInAreaForPlacement(mainTilePos, size))
                {
                    _sfxError.Play(GetSfxVolume(1f), 0f, 0f);
                    return;
                }
            }

            // 检查多方块建筑是否可以放置
            Point buildingSize = BuildingSize.GetSize(type);
            if (buildingSize.X > 1 || buildingSize.Y > 1)
            {
                // 以点击位置为左下角基准点
                Point mainTilePos = tile;
                if (!CanPlaceMultiBlock(mainTilePos, buildingSize))
                {
                    _sfxError.Play(GetSfxVolume(1f), 0f, 0f);
                    return;
                }
            }

            // 检查材料是否足够
            if (!CanAffordTool(_tool))
            {
                _sfxError.Play();
                return;
            }

            // 消耗材料
            ConsumeMaterials(_tool);
        }

        switch (_tool)
        {
            case Tool.Erase:
                SetTile(tile, TileType.Empty, Direction.None);
                break;
            case Tool.Conveyor:
                SetTile(tile, TileType.Conveyor, _direction);
                break;
            case Tool.FastConveyor:
                SetTile(tile, TileType.FastConveyor, _direction);
                break;
            case Tool.Splitter:
                SetTile(tile, TileType.Splitter, _direction);
                break;
            case Tool.Merger:
                SetTile(tile, TileType.Merger, _direction);
                break;
            case Tool.Miner:
                SetTile(tile, TileType.Miner, _direction);
                break;
            case Tool.Smelter:
                SetTile(tile, TileType.Smelter, _direction);
                break;
            case Tool.Assembler:
                SetTile(tile, TileType.Assembler, _direction);
                break;
            case Tool.Lab:
                SetTile(tile, TileType.Lab, _direction);
                break;
            case Tool.Generator:
                SetTile(tile, TileType.Generator, Direction.None);
                break;
            case Tool.Storage:
                SetTile(tile, TileType.Storage, Direction.None);
                break;
            case Tool.Router:
                SetTile(tile, TileType.Router, Direction.None);
                break;
            // 新增工具
            case Tool.UndergroundBelt:
                // 交替放置入口和出口
                var existingUnderground = _undergrounds.Values.LastOrDefault(u => u.LinkedExit == null);
                if (existingUnderground != null)
                {
                    SetTile(tile, TileType.UndergroundExit, _direction);
                }
                else
                {
                    SetTile(tile, TileType.UndergroundEntry, _direction);
                }
                break;
            case Tool.CoalGenerator:
                SetTile(tile, TileType.CoalGenerator, Direction.None);
                break;
            case Tool.AdvancedMiner:
                SetTile(tile, TileType.AdvancedMiner, _direction);
                break;
            case Tool.AssemblerMk2:
                SetTile(tile, TileType.AssemblerMk2, _direction);
                break;
            case Tool.ChemicalPlant:
                SetTile(tile, TileType.ChemicalPlant, _direction);
                break;
        }

        _sfxPlace.Play(GetSfxVolume(1f), 0f, 0f);
        
        // 添加放置粒子效果
        Vector2 worldPos = new Vector2(tile.X, tile.Y);
        CreateParticles(worldPos, ParticleType.Spark, 8);
    }

    private void RotateTile(Point tile)
    {
        Tile current = _tiles[tile.X, tile.Y];
        if (!DirectionUtil.UsesDirection(current.Type))
        {
            return;
        }

        current.Direction = DirectionUtil.RotateCW(current.Direction);
        _tiles[tile.X, tile.Y] = current;
        _sfxRotate.Play(GetSfxVolume(1f), 0f, 0f);
    }

    private void SetTile(Point tile, TileType type, Direction direction)
    {
        if (!InBounds(tile))
        {
            return;
        }

        Point size = BuildingSize.GetSize(type);

        // 多方块建筑
        if (size.X > 1 || size.Y > 1)
        {
            // 以点击位置为左下角基准点
            Point mainTilePos = tile;

            // 先清除所有占用的格子
            for (int dy = 0; dy < size.Y; dy++)
            {
                for (int dx = 0; dx < size.X; dx++)
                {
                    Point p = new Point(mainTilePos.X + dx, mainTilePos.Y - dy);
                    if (InBounds(p))
                    {
                        ClearTileState(p);
                    }
                }
            }

            // 设置主格子（在左下角）
            _tiles[mainTilePos.X, mainTilePos.Y] = new Tile { Type = type, Direction = direction, ParentTile = null };

            // 设置子格子（指向主格子）
            for (int dy = 0; dy < size.Y; dy++)
            {
                for (int dx = 0; dx < size.X; dx++)
                {
                    if (dx == 0 && dy == 0) continue; // 跳过主格子
                    Point p = new Point(mainTilePos.X + dx, mainTilePos.Y - dy);
                    if (InBounds(p))
                    {
                        _tiles[p.X, p.Y] = new Tile { Type = type, Direction = direction, ParentTile = mainTilePos };
                    }
                }
            }

            // 添加建筑状态（只在主格子）
            AddBuildingState(mainTilePos, type, direction);
        }
        else
        {
            // 单格建筑：原有逻辑
            Tile previous = _tiles[tile.X, tile.Y];
            if (previous.Type == type && previous.Direction == direction)
            {
                return;
            }

            ClearTileState(tile);
            _tiles[tile.X, tile.Y] = new Tile { Type = type, Direction = direction, ParentTile = null };
            AddBuildingState(tile, type, direction);
        }
    }

    private bool HasOreInArea(Point origin, int size)
    {
        for (int dy = 0; dy < size; dy++)
        {
            for (int dx = 0; dx < size; dx++)
            {
                Point p = new Point(origin.X + dx, origin.Y + dy);
                if (InBounds(p) && _oreMap[p.X, p.Y])
                {
                    return true;
                }
            }
        }
        return false;
    }

    private bool HasOreInAreaForPlacement(Point origin, int size)
    {
        for (int dy = 0; dy < size; dy++)
        {
            for (int dx = 0; dx < size; dx++)
            {
                Point p = new Point(origin.X + dx, origin.Y - dy);
                if (InBounds(p) && _oreMap[p.X, p.Y])
                {
                    return true;
                }
            }
        }
        return false;
    }

    private bool CanPlaceMultiBlock(Point origin, Point size)
    {
        for (int dy = 0; dy < size.Y; dy++)
        {
            for (int dx = 0; dx < size.X; dx++)
            {
                Point p = new Point(origin.X + dx, origin.Y - dy);
                if (!InBounds(p))
                {
                    return false;
                }

                // 检查地形
                TerrainType terrain = _terrainMap[p.X, p.Y];
                if (terrain != TerrainType.Grass)
                {
                    return false;
                }

                // 检查是否已有建筑（除非是空的）
                Tile existing = _tiles[p.X, p.Y];
                if (existing.Type != TileType.Empty)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private void ClearTileState(Point tile)
    {
        if (!InBounds(tile)) return;

        Tile previous = _tiles[tile.X, tile.Y];

        // 如果是子格子，找到主格子并清除整个建筑
        if (previous.ParentTile.HasValue)
        {
            ClearMultiBlockBuilding(previous.ParentTile.Value);
            return;
        }

        // 如果是主格子且是多方块建筑，清除所有子格子
        Point size = BuildingSize.GetSize(previous.Type);
        if (size.X > 1 || size.Y > 1)
        {
            ClearMultiBlockBuilding(tile);
            return;
        }

        // 单格建筑：清除状态
        if (previous.Type == TileType.Miner || previous.Type == TileType.AdvancedMiner)
        {
            _miners.Remove(tile);
        }
        if (previous.Type == TileType.Smelter || previous.Type == TileType.Assembler ||
            previous.Type == TileType.Lab || previous.Type == TileType.AssemblerMk2 ||
            previous.Type == TileType.ChemicalPlant)
        {
            _processors.Remove(tile);
        }
        if (previous.Type == TileType.Storage)
        {
            _storages.Remove(tile);
        }
        if (previous.Type == TileType.Splitter)
        {
            _splitterIndex.Remove(tile);
        }
        if (previous.Type == TileType.UndergroundEntry || previous.Type == TileType.UndergroundExit)
        {
            _undergrounds.Remove(tile);
        }
        if (previous.Type == TileType.CoalGenerator)
        {
            _coalGenerators.Remove(tile);
        }
        if (previous.Type == TileType.Router)
        {
            _routers.Remove(tile);
        }
        // 清除传送带实体数据
        if (previous.Type == TileType.Conveyor || previous.Type == TileType.FastConveyor)
        {
            _conveyors.Remove(tile);
        }

        _tiles[tile.X, tile.Y] = new Tile { Type = TileType.Empty, Direction = Direction.None, ParentTile = null };
    }

    private void ClearMultiBlockBuilding(Point mainTile)
    {
        if (!InBounds(mainTile)) return;

        Tile main = _tiles[mainTile.X, mainTile.Y];
        Point size = BuildingSize.GetSize(main.Type);

        // 清除建筑状态
        if (main.Type == TileType.Miner || main.Type == TileType.AdvancedMiner)
        {
            _miners.Remove(mainTile);
        }
        if (main.Type == TileType.Smelter || main.Type == TileType.Assembler ||
            main.Type == TileType.Lab || main.Type == TileType.AssemblerMk2 ||
            main.Type == TileType.ChemicalPlant)
        {
            _processors.Remove(mainTile);
        }
        if (main.Type == TileType.Generator || main.Type == TileType.CoalGenerator)
        {
            _coalGenerators.Remove(mainTile);
        }

        // 清除所有格子和区域内的物品
        for (int dy = 0; dy < size.Y; dy++)
        {
            for (int dx = 0; dx < size.X; dx++)
            {
                Point p = new Point(mainTile.X + dx, mainTile.Y - dy);
                if (InBounds(p))
                {
                    // 清除格子
                    _tiles[p.X, p.Y] = new Tile { Type = TileType.Empty, Direction = Direction.None, ParentTile = null };
                    
                    // 清除该位置的物品
                    for (int i = _items.Count - 1; i >= 0; i--)
                    {
                        if (_items[i].Tile == p)
                        {
                            _items.RemoveAt(i);
                        }
                    }
                }
            }
        }
        
        // 同时清除建筑输出位置的物品（防止物品卡在输出位置）
        // 检查上边缘
        for (int dx = 0; dx < size.X; dx++)
        {
            Point edge = new Point(mainTile.X + dx, mainTile.Y - 1);
            if (InBounds(edge))
            {
                for (int i = _items.Count - 1; i >= 0; i--)
                {
                    if (_items[i].Tile == edge)
                    {
                        _items.RemoveAt(i);
                    }
                }
            }
        }
        // 检查下边缘
        for (int dx = 0; dx < size.X; dx++)
        {
            Point edge = new Point(mainTile.X + dx, mainTile.Y + size.Y);
            if (InBounds(edge))
            {
                for (int i = _items.Count - 1; i >= 0; i--)
                {
                    if (_items[i].Tile == edge)
                    {
                        _items.RemoveAt(i);
                    }
                }
            }
        }
        // 检查左边缘
        for (int dy = 0; dy < size.Y; dy++)
        {
            Point edge = new Point(mainTile.X - 1, mainTile.Y - dy);
            if (InBounds(edge))
            {
                for (int i = _items.Count - 1; i >= 0; i--)
                {
                    if (_items[i].Tile == edge)
                    {
                        _items.RemoveAt(i);
                    }
                }
            }
        }
        // 检查右边缘
        for (int dy = 0; dy < size.Y; dy++)
        {
            Point edge = new Point(mainTile.X + size.X, mainTile.Y - dy);
            if (InBounds(edge))
            {
                for (int i = _items.Count - 1; i >= 0; i--)
                {
                    if (_items[i].Tile == edge)
                    {
                        _items.RemoveAt(i);
                    }
                }
            }
        }
    }

    private void AddBuildingState(Point tile, TileType type, Direction direction)
    {
        if (type == TileType.Miner || type == TileType.AdvancedMiner)
        {
            _miners[tile] = new MinerState { Timer = MinerInterval };
        }
        if (type == TileType.Smelter)
        {
            _processors[tile] = new ProcessorState
            {
                BuildingType = TileType.Smelter,
                Recipe = null,  // 不设置默认配方，根据输入动态选择
                RequiresFuel = false,
                InputCapacity = 10,
                OutputCapacity = 10
            };
        }
        if (type == TileType.Assembler)
        {
            _processors[tile] = new ProcessorState
            {
                BuildingType = TileType.Assembler,
                Recipe = Recipes.AssembleGear,  // 默认配方
                RequiresFuel = false,
                InputCapacity = 10,
                OutputCapacity = 10
            };
        }
        if (type == TileType.Lab)
        {
            _processors[tile] = new ProcessorState
            {
                BuildingType = TileType.Lab,
                Recipe = Recipes.ResearchScience,
                RequiresFuel = false,
                InputCapacity = 10,
                OutputCapacity = 10
            };
        }
        if (type == TileType.AssemblerMk2)
        {
            _processors[tile] = new ProcessorState
            {
                BuildingType = TileType.AssemblerMk2,
                Recipe = Recipes.AssembleCircuit,
                RequiresFuel = false,
                InputCapacity = 10,
                OutputCapacity = 10
            };
        }
        if (type == TileType.ChemicalPlant)
        {
            _processors[tile] = new ProcessorState
            {
                BuildingType = TileType.ChemicalPlant,
                Recipe = Recipes.AssembleWire,  // 临时配方
                RequiresFuel = false,
                InputCapacity = 10,
                OutputCapacity = 10
            };
        }
        if (type == TileType.Storage)
        {
            _storages[tile] = new StorageState();
        }
        if (type == TileType.Splitter)
        {
            _splitterIndex[tile] = 0;
        }
        if (type == TileType.UndergroundEntry || type == TileType.UndergroundExit)
        {
            _undergrounds[tile] = new UndergroundState { Direction = direction };
            TryLinkUnderground(tile, type, direction);
        }
        if (type == TileType.CoalGenerator)
        {
            _coalGenerators[tile] = new CoalGeneratorState();
        }
        if (type == TileType.Router)
        {
            _routers[tile] = new RouterState();
        }
    }

    private void TryLinkUnderground(Point tile, TileType type, Direction direction)
    {
        // 尝试链接地下传送带入口和出口
        Point dir = DirectionUtil.ToPoint(direction);
        int maxDistance = 5;

        for (int i = 1; i <= maxDistance; i++)
        {
            Point check = new Point(tile.X + dir.X * i, tile.Y + dir.Y * i);
            if (!InBounds(check))
            {
                break;
            }

            Tile checkTile = _tiles[check.X, check.Y];
            bool isEntry = type == TileType.UndergroundEntry;
            TileType targetType = isEntry ? TileType.UndergroundExit : TileType.UndergroundEntry;

            if (checkTile.Type == targetType && checkTile.Direction == direction)
            {
                if (_undergrounds.TryGetValue(tile, out var state))
                {
                    state.LinkedExit = check;
                }
                if (_undergrounds.TryGetValue(check, out var otherState))
                {
                    otherState.LinkedExit = tile;
                }
                break;
            }
        }
    }

    private void ClearMap()
    {
        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                _tiles[x, y] = new Tile { Type = TileType.Empty, Direction = Direction.None };
                _oreMap[x, y] = false;
                _oreTypeMap[x, y] = OreType.None;
                _terrainMap[x, y] = TerrainType.Locked; // 默认锁定
                _congestion[x, y] = 0f;
            }
        }
        _items.Clear();
        _miners.Clear();
        _processors.Clear();
        _storages.Clear();
        _splitterIndex.Clear();
        _undergrounds.Clear();
        _coalGenerators.Clear();
        _routers.Clear();
        _regions.Clear();

        // 初始化区域系统
        InitializeRegions();

        // 将相机移动到地图中心（等距视角需要调整）
        float centerX = MapWidth / 2f;
        float centerY = MapHeight / 2f;
        _camera.Pan = new Vector2(-centerX, -centerY);
    }

    private void InitializeRegions()
    {
        // 创建区域网格，每个区域32x32
        int regionSize = 32;
        int regionsX = MapWidth / regionSize;
        int regionsY = MapHeight / regionSize;
        int id = 0;

        for (int ry = 0; ry < regionsY; ry++)
        {
            for (int rx = 0; rx < regionsX; rx++)
            {
                var region = new RegionInfo
                {
                    Id = id,
                    Name = $"区域 {rx + 1}-{ry + 1}",
                    Bounds = new Rectangle(rx * regionSize, ry * regionSize, regionSize, regionSize),
                    // 中心4个区域默认解锁
                    IsUnlocked = (rx >= regionsX/2 - 1 && rx <= regionsX/2 && ry >= regionsY/2 - 1 && ry <= regionsY/2),
                    UnlockCost = Math.Abs(rx - regionsX/2) * 50 + Math.Abs(ry - regionsY/2) * 50 + 100
                };
                _regions.Add(region);
                id++;
            }
        }

        // 解锁初始区域的地形
        foreach (var region in _regions)
        {
            if (region.IsUnlocked)
            {
                UnlockRegionTerrain(region.Id);
            }
        }
    }

    private void UnlockRegionTerrain(int regionId)
    {
        if (regionId < 0 || regionId >= _regions.Count)
            return;

        var region = _regions[regionId];
        region.IsUnlocked = true;

        // 使用噪声生成该区域的地形
        GenerateTerrainForRegion(region.Bounds);
        // 在该区域生成矿物
        GenerateOresForRegion(region.Bounds, regionId);
    }

    private float PerlinNoise(float x, float y, int seed)
    {
        // 简化的Perlin噪声实现
        int xi = (int)MathF.Floor(x) & 255;
        int yi = (int)MathF.Floor(y) & 255;
        float xf = x - MathF.Floor(x);
        float yf = y - MathF.Floor(y);

        float u = xf * xf * (3 - 2 * xf);
        float v = yf * yf * (3 - 2 * yf);

        int aa = Hash(xi, yi, seed);
        int ab = Hash(xi, yi + 1, seed);
        int ba = Hash(xi + 1, yi, seed);
        int bb = Hash(xi + 1, yi + 1, seed);

        float x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
        float x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);

        return (Lerp(x1, x2, v) + 1) / 2; // 归一化到0-1
    }

    private int Hash(int x, int y, int seed)
    {
        int h = seed + x * 374761393 + y * 668265263;
        h = (h ^ (h >> 13)) * 1274126177;
        return h ^ (h >> 16);
    }

    private float Grad(int hash, float x, float y)
    {
        int h = hash & 3;
        float u = h < 2 ? x : y;
        float v = h < 2 ? y : x;
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }

    private void GenerateTerrainForRegion(Rectangle bounds)
    {
        int seed = _random.Next();
        float scale = 0.08f; // 降低scale使地形更连续

        for (int y = bounds.Y; y < bounds.Y + bounds.Height; y++)
        {
            for (int x = bounds.X; x < bounds.X + bounds.Width; x++)
            {
                if (!InBounds(new Point(x, y)))
                    continue;

                // 多层噪声叠加，增加地形变化
                float noise = PerlinNoise(x * scale, y * scale, seed);
                noise += PerlinNoise(x * scale * 2, y * scale * 2, seed + 1) * 0.5f;
                noise += PerlinNoise(x * scale * 4, y * scale * 4, seed + 2) * 0.25f;
                noise /= 1.75f;

                // 根据噪声值决定地形 - 调整阈值增加山地比例
                if (noise < 0.20f)
                {
                    _terrainMap[x, y] = TerrainType.Water;
                }
                else if (noise > 0.55f) // 从0.75降低到0.55，大幅增加山地
                {
                    _terrainMap[x, y] = TerrainType.Mountain;
                }
                else
                {
                    _terrainMap[x, y] = TerrainType.Grass;
                }
            }
        }
    }

    private void GenerateOresForRegion(Rectangle bounds, int regionId)
    {
        // 每个区域生成不同类型的矿物
        int ironCount = 3 + regionId % 3;
        int copperCount = 2 + (regionId / 2) % 3;
        int coalCount = 2 + (regionId / 3) % 2;
        // 稀有矿物在更高级区域生成
        int goldCount = regionId >= 2 ? 1 + regionId % 2 : 0;
        int titaniumCount = regionId >= 3 ? 1 + regionId % 2 : 0;
        int uraniumCount = regionId >= 4 ? 1 : 0;

        // 铁矿
        for (int i = 0; i < ironCount; i++)
        {
            int cx = bounds.X + _random.Next(3, bounds.Width - 3);
            int cy = bounds.Y + _random.Next(3, bounds.Height - 3);
            int radius = _random.Next(2, 5);
            GenerateOreClusterInRegion(cx, cy, radius, OreType.Iron, bounds);
        }

        // 铜矿
        for (int i = 0; i < copperCount; i++)
        {
            int cx = bounds.X + _random.Next(3, bounds.Width - 3);
            int cy = bounds.Y + _random.Next(3, bounds.Height - 3);
            int radius = _random.Next(2, 4);
            GenerateOreClusterInRegion(cx, cy, radius, OreType.Copper, bounds);
        }

        // 煤炭
        for (int i = 0; i < coalCount; i++)
        {
            int cx = bounds.X + _random.Next(3, bounds.Width - 3);
            int cy = bounds.Y + _random.Next(3, bounds.Height - 3);
            int radius = _random.Next(2, 4);
            GenerateOreClusterInRegion(cx, cy, radius, OreType.Coal, bounds);
        }

        // 金矿（区域2+）
        for (int i = 0; i < goldCount; i++)
        {
            int cx = bounds.X + _random.Next(3, bounds.Width - 3);
            int cy = bounds.Y + _random.Next(3, bounds.Height - 3);
            int radius = _random.Next(1, 3);
            GenerateOreClusterInRegion(cx, cy, radius, OreType.Gold, bounds);
        }

        // 钛矿（区域3+）
        for (int i = 0; i < titaniumCount; i++)
        {
            int cx = bounds.X + _random.Next(3, bounds.Width - 3);
            int cy = bounds.Y + _random.Next(3, bounds.Height - 3);
            int radius = _random.Next(1, 3);
            GenerateOreClusterInRegion(cx, cy, radius, OreType.Titanium, bounds);
        }

        // 铀矿（区域4+）
        for (int i = 0; i < uraniumCount; i++)
        {
            int cx = bounds.X + _random.Next(3, bounds.Width - 3);
            int cy = bounds.Y + _random.Next(3, bounds.Height - 3);
            int radius = _random.Next(1, 2);
            GenerateOreClusterInRegion(cx, cy, radius, OreType.Uranium, bounds);
        }
    }

    private void GenerateOreClusterInRegion(int cx, int cy, int radius, OreType oreType, Rectangle bounds)
    {
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                int nx = cx + x;
                int ny = cy + y;

                if (nx < bounds.X || nx >= bounds.X + bounds.Width ||
                    ny < bounds.Y || ny >= bounds.Y + bounds.Height)
                    continue;

                if (!InBounds(new Point(nx, ny)))
                    continue;

                // 只在草地上生成矿物
                if (_terrainMap[nx, ny] != TerrainType.Grass)
                    continue;

                if (MathF.Sqrt(x * x + y * y) <= radius + _random.NextDouble() * 0.5)
                {
                    _oreMap[nx, ny] = true;
                    _oreTypeMap[nx, ny] = oreType;
                }
            }
        }
    }

    private void GenerateResourceMap()
    {
        // 现在由区域系统处理矿物生成
        // 这个函数保留用于兼容性
    }

    private void GenerateOreCluster(int cx, int cy, int radius, OreType oreType)
    {
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                int nx = cx + x;
                int ny = cy + y;
                if (!InBounds(new Point(nx, ny)))
                {
                    continue;
                }

                // 只在草地上生成矿物
                if (_terrainMap[nx, ny] != TerrainType.Grass)
                    continue;

                if (MathF.Sqrt(x * x + y * y) <= radius + _random.NextDouble() * 0.5)
                {
                    _oreMap[nx, ny] = true;
                    _oreTypeMap[nx, ny] = oreType;
                }
            }
        }
    }

    private void CreateDemoLayout()
    {
        // 计算地图中心坐标
        int centerX = MapWidth / 2;
        int centerY = MapHeight / 2;
        
        // 确保初始建筑区域的地形都是草地
        EnsureGrassArea(centerX - 12, centerY - 12, 26, 26);
        
        // 第一个生产线 - 在地图中心附近
        Point miner1Pos = new Point(centerX - 10, centerY - 10);
        _oreMap[miner1Pos.X, miner1Pos.Y] = true;
        _oreTypeMap[miner1Pos.X, miner1Pos.Y] = OreType.Iron;
        SetTile(miner1Pos, TileType.Miner, Direction.East);
        
        // 矿机到熔炉的传送带
        for (int x = miner1Pos.X + 2; x <= miner1Pos.X + 7; x++)
        {
            SetTile(new Point(x, miner1Pos.Y), TileType.Conveyor, Direction.East);
        }
        
        // 熔炉
        Point smelter1Pos = new Point(miner1Pos.X + 8, miner1Pos.Y);
        SetTile(smelter1Pos, TileType.Smelter, Direction.South);
        
        // 熔炉到仓库的传送带
        for (int y = smelter1Pos.Y + 2; y <= smelter1Pos.Y + 5; y++)
        {
            SetTile(new Point(smelter1Pos.X, y), TileType.Conveyor, Direction.South);
        }
        
        // 仓库
        Point storage1Pos = new Point(smelter1Pos.X, smelter1Pos.Y + 6);
        SetTile(storage1Pos, TileType.Storage, Direction.None);

        // 第二个生产线 - 在另一个方向
        Point miner2Pos = new Point(centerX - 10, centerY + 8);
        _oreMap[miner2Pos.X, miner2Pos.Y] = true;
        _oreTypeMap[miner2Pos.X, miner2Pos.Y] = OreType.Copper;
        SetTile(miner2Pos, TileType.Miner, Direction.South);
        
        // 矿机到熔炉的传送带
        for (int y = miner2Pos.Y + 2; y <= miner2Pos.Y + 6; y++)
        {
            SetTile(new Point(miner2Pos.X, y), TileType.Conveyor, Direction.South);
        }
        
        // 熔炉
        Point smelter2Pos = new Point(miner2Pos.X, miner2Pos.Y + 7);
        SetTile(smelter2Pos, TileType.Smelter, Direction.East);
        
        // 熔炉到仓库的传送带
        for (int x = smelter2Pos.X + 2; x <= smelter2Pos.X + 5; x++)
        {
            SetTile(new Point(x, smelter2Pos.Y), TileType.Conveyor, Direction.East);
        }
        
        // 仓库
        Point storage2Pos = new Point(smelter2Pos.X + 6, smelter2Pos.Y);
        SetTile(storage2Pos, TileType.Storage, Direction.None);
    }

    private void EnsureGrassArea(int startX, int startY, int width, int height)
    {
        for (int y = startY; y < startY + height && y < MapHeight; y++)
        {
            for (int x = startX; x < startX + width && x < MapWidth; x++)
            {
                if (x >= 0 && y >= 0)
                {
                    _terrainMap[x, y] = TerrainType.Grass;
                }
            }
        }
    }

    private bool InBounds(Point tile) => tile.X >= 0 && tile.Y >= 0 && tile.X < MapWidth && tile.Y < MapHeight;

    private (TileType Type, Direction Direction) GetPlacementSpec()
    {
        return _tool switch
        {
            Tool.Conveyor => (TileType.Conveyor, _direction),
            Tool.FastConveyor => (TileType.FastConveyor, _direction),
            Tool.Splitter => (TileType.Splitter, _direction),
            Tool.Merger => (TileType.Merger, _direction),
            Tool.Router => (TileType.Router, Direction.None),
            Tool.Miner => (TileType.Miner, _direction),
            Tool.Smelter => (TileType.Smelter, _direction),
            Tool.Assembler => (TileType.Assembler, _direction),
            Tool.Lab => (TileType.Lab, _direction),
            Tool.Generator => (TileType.Generator, Direction.None),
            Tool.Storage => (TileType.Storage, Direction.None),
            // 新增工具
            Tool.UndergroundBelt => (TileType.UndergroundEntry, _direction),
            Tool.CoalGenerator => (TileType.CoalGenerator, Direction.None),
            Tool.AdvancedMiner => (TileType.AdvancedMiner, _direction),
            Tool.AssemblerMk2 => (TileType.AssemblerMk2, _direction),
            Tool.ChemicalPlant => (TileType.ChemicalPlant, _direction),
            _ => (TileType.Empty, Direction.None)
        };
    }

    private int GetToolCost(Tool tool)
    {
        // 保留旧方法用于显示（已弃用，使用 GetToolMaterialCost）
        return 0;
    }

    /// <summary>
    /// 获取工具的材料消耗
    /// </summary>
    private Dictionary<ItemType, int> GetToolMaterialCost(Tool tool)
    {
        return tool switch
        {
            Tool.Conveyor => new Dictionary<ItemType, int> { { ItemType.Plate, 1 } },
            Tool.FastConveyor => new Dictionary<ItemType, int> { { ItemType.Plate, 2 }, { ItemType.Gear, 1 } },
            Tool.Splitter => new Dictionary<ItemType, int> { { ItemType.Plate, 3 }, { ItemType.Gear, 2 } },
            Tool.Merger => new Dictionary<ItemType, int> { { ItemType.Plate, 3 }, { ItemType.Gear, 2 } },
            Tool.Router => new Dictionary<ItemType, int> { { ItemType.Plate, 2 }, { ItemType.Gear, 1 } },
            Tool.Miner => new Dictionary<ItemType, int> { { ItemType.Plate, 5 }, { ItemType.Gear, 3 } },
            Tool.Smelter => new Dictionary<ItemType, int> { { ItemType.Plate, 8 }, { ItemType.Gear, 4 } },
            Tool.Assembler => new Dictionary<ItemType, int> { { ItemType.Plate, 10 }, { ItemType.Gear, 5 } },
            Tool.Lab => new Dictionary<ItemType, int> { { ItemType.Plate, 8 }, { ItemType.Gear, 6 }, { ItemType.Circuit, 2 } },
            Tool.Generator => new Dictionary<ItemType, int> { { ItemType.Plate, 12 }, { ItemType.Gear, 8 } },
            Tool.Storage => new Dictionary<ItemType, int> { { ItemType.Plate, 4 } },
            Tool.UndergroundBelt => new Dictionary<ItemType, int> { { ItemType.Plate, 4 }, { ItemType.Gear, 2 } },
            Tool.CoalGenerator => new Dictionary<ItemType, int> { { ItemType.Plate, 15 }, { ItemType.Gear, 10 }, { ItemType.CopperPlate, 5 } },
            Tool.AdvancedMiner => new Dictionary<ItemType, int> { { ItemType.Plate, 20 }, { ItemType.Gear, 12 }, { ItemType.Circuit, 4 } },
            Tool.AssemblerMk2 => new Dictionary<ItemType, int> { { ItemType.Plate, 15 }, { ItemType.Gear, 8 }, { ItemType.Circuit, 6 } },
            Tool.ChemicalPlant => new Dictionary<ItemType, int> { { ItemType.Plate, 18 }, { ItemType.Gear, 10 }, { ItemType.CopperPlate, 8 } },
            Tool.Erase => new Dictionary<ItemType, int>(), // 删除不消耗材料
            _ => new Dictionary<ItemType, int>()
        };
    }

    /// <summary>
    /// 检查是否有足够的材料
    /// </summary>
    private bool CanAffordTool(Tool tool)
    {
        var cost = GetToolMaterialCost(tool);
        foreach (var item in cost)
        {
            int have = _inventory.GetValueOrDefault(item.Key, 0);
            if (have < item.Value) return false;
        }
        return true;
    }

    /// <summary>
    /// 消耗建造材料
    /// </summary>
    private void ConsumeMaterials(Tool tool)
    {
        var cost = GetToolMaterialCost(tool);
        foreach (var item in cost)
        {
            if (_inventory.ContainsKey(item.Key))
            {
                _inventory[item.Key] -= item.Value;
            }
        }
    }

    /// <summary>
    /// 获取库存中某物品的数量
    /// </summary>
    private int GetInventoryCount(ItemType item)
    {
        return _inventory.GetValueOrDefault(item, 0);
    }

    /// <summary>
    /// 添加物品到库存
    /// </summary>
    private void AddToInventory(ItemType item, int count = 1)
    {
        if (!_inventory.ContainsKey(item))
            _inventory[item] = 0;
        _inventory[item] += count;
    }

    /// <summary>
    /// 更新仓库存储统计
    /// </summary>
    private void UpdateStorageStats(ItemType itemType)
    {
        switch (itemType)
        {
            case ItemType.Plate:
                _totalPlatesStored += 1;
                _plateDeliveries.Enqueue(_elapsed);
                break;
            case ItemType.Gear:
                _totalGearStored += 1;
                break;
            case ItemType.Science:
                _totalScienceStored += 1;
                _researchPoints += 1;
                _scienceDeliveries.Enqueue(_elapsed);
                CheckResearchUnlocks();
                break;
            case ItemType.Ore:
                _totalOreStored += 1;
                break;
            case ItemType.CopperPlate:
                _totalCopperStored += 1;
                break;
            case ItemType.Coal:
                _totalCoalStored += 1;
                break;
            case ItemType.Circuit:
                _totalCircuitStored += 1;
                _researchPoints += 2;
                CheckResearchUnlocks();
                break;
            case ItemType.Steel:
                _totalSteelStored += 1;
                break;
            case ItemType.RedScience:
                _researchPoints += 3;
                CheckResearchUnlocks();
                break;
            case ItemType.GreenScience:
                _researchPoints += 5;
                CheckResearchUnlocks();
                break;
        }
        CheckAchievements();
    }

    private string GetToolTooltip(Tool tool)
    {
        return tool switch
        {
            Tool.Conveyor => T("TOOL_CONV"),
            Tool.FastConveyor => T("TOOL_FAST"),
            Tool.Splitter => T("TOOL_SPLIT"),
            Tool.Merger => T("TOOL_MERGE"),
            Tool.Router => T("TOOL_ROUTER"),
            Tool.Miner => T("TOOL_MINER"),
            Tool.Smelter => T("TOOL_SMELT"),
            Tool.Assembler => T("TOOL_ASM"),
            Tool.Lab => T("TOOL_LAB"),
            Tool.Generator => T("TOOL_GEN"),
            Tool.Storage => T("TOOL_STORE"),
            Tool.Erase => T("TOOL_ERASE"),
            // 新增工具提示
            Tool.UndergroundBelt => T("TOOL_UNDERGROUND"),
            Tool.CoalGenerator => T("TOOL_COAL_GEN"),
            Tool.AdvancedMiner => T("TOOL_ADV_MINER"),
            Tool.AssemblerMk2 => T("TOOL_ASM_MK2"),
            Tool.ChemicalPlant => T("TOOL_CHEM"),
            _ => T("TOOL")
        };
    }

    private string T(string key)
    {
        if (_language == Language.English)
        {
            return key switch
            {
                "SETTINGS_TITLE" => "SETTINGS",
                "SET_GRID" => "GRID",
                "SET_ORE" => "ORE HIGHLIGHT",
                "SET_TIPS" => "TOOLTIPS",
                "SET_AUTOPAUSE" => "AUTO-PAUSE",
                "SET_VOLUME" => "SFX VOLUME",
                "SET_TUTORIAL" => "TUTORIAL",
                "SET_LANG" => "LANGUAGE",
                "SET_DEV" => "DEV MODE",
                "SET_DEVMENU" => "DEV MENU",
                "SET_HINTS" => "ESC CLOSE  +/- VOLUME  7 LANGUAGE  8 DEV  9 MENU",
                "DEV_TITLE" => "DEVELOPER MENU",
                "DEV_HINTS" => "Q +RESEARCH  W +MATERIALS  E +PLATES  R +SCIENCE",
                "DEV_ADD_RESEARCH" => "+5 RESEARCH",
                "DEV_ADD_MATERIALS" => "+50 MATERIALS",
                "DEV_ADD_PLATES" => "+10 PLATES",
                "DEV_ADD_SCIENCE" => "+10 SCIENCE",
                "DEV_RESEARCH" => "RESEARCH",
                "DEV_MATERIALS" => "MATERIALS",

                "DEV_PLATES" => "PLATES",
                "DEV_SCIENCE" => "SCIENCE",
                "TUTORIAL_TITLE" => "TUTORIAL",
                "TUTORIAL_HINT" => "PRESS F2 TO HIDE",
                "TUTORIAL_1" => "1 PLACE MINER ON ORE (TOOL 2)",
                "TUTORIAL_2" => "2 PLACE CONVEYOR AND SMELTER (1,3)",
                "TUTORIAL_3" => "3 DELIVER 5 PLATES TO STORAGE (4)",
                "TUTORIAL_4" => "4 PLACE ASSEMBLER AND LAB (7,8)",
                "TUTORIAL_5" => "5 GENERATE SCIENCE AND UNLOCK TOOLS",
                "TUTORIAL_DONE" => "TUTORIAL COMPLETE",
                // 配方手册翻译
                "RECIPE_TITLE" => "RECIPE BOOK (F3)",
                "RECIPE_HINT" => "F3 CLOSE  LEFT/RIGHT ARROWS TO NAVIGATE",
                "RECIPE_BASIC" => "== BASIC BUILDINGS ==",
                "RECIPE_LOGISTICS" => "== LOGISTICS ==",
                "RECIPE_PRODUCTION" => "== PRODUCTION ==",
                "RECIPE_POWER" => "== POWER SYSTEM ==",
                "RECIPE_CONVEYOR" => "CONVEYOR [Plate x1]",
                "RECIPE_CONVEYOR_DESC" => "MOVES ITEMS AT 1.2 TILES/SEC",
                "RECIPE_FAST_CONV" => "FAST CONVEYOR [Plate x2 Gear x1]",
                "RECIPE_FAST_CONV_DESC" => "MOVES ITEMS AT 2.2 TILES/SEC",
                "RECIPE_MINER" => "MINER [Plate x5 Gear x3]",
                "RECIPE_MINER_DESC" => "EXTRACTS ORE FROM DEPOSITS (1/SEC)",
                "RECIPE_SMELTER" => "SMELTER [Plate x8 Gear x4]",
                "RECIPE_SMELTER_DESC" => "ORE -> PLATE (2 SEC)",
                "RECIPE_STORAGE" => "STORAGE [Plate x4]",
                "RECIPE_STORAGE_DESC" => "COLLECTS ITEMS TO INVENTORY",
                "RECIPE_SPLITTER" => "SPLITTER [Plate x3 Gear x2]",
                "RECIPE_SPLITTER_DESC" => "1 INPUT -> 3 OUTPUTS (ROUND ROBIN)",
                "RECIPE_MERGER" => "MERGER [Plate x3 Gear x2]",
                "RECIPE_MERGER_DESC" => "3 INPUTS -> 1 OUTPUT",
                "RECIPE_ROUTER" => "ROUTER [Plate x2 Gear x1]",
                "RECIPE_ROUTER_DESC" => "AUTO-DETECT INPUT, OTHER SIDES OUTPUT",
                "RECIPE_UNDERGROUND" => "UNDERGROUND [Plate x4 Gear x2]",
                "RECIPE_UNDERGROUND_DESC" => "BYPASSES UP TO 5 TILES",
                "RECIPE_ASSEMBLER" => "ASSEMBLER [Plate x10 Gear x5]",
                "RECIPE_ASSEMBLER_DESC" => "PLATE -> GEAR (2.5 SEC)",
                "RECIPE_ASM_MK2" => "ASSEMBLER MK2 [Plate x15 Gear x8 Circuit x6]",
                "RECIPE_ASM_MK2_DESC" => "DUAL INPUT, FASTER PROCESSING",
                "RECIPE_LAB" => "LAB [Plate x8 Gear x6 Circuit x2]",
                "RECIPE_LAB_DESC" => "GEAR -> SCIENCE (3 SEC)",
                "RECIPE_CHEM" => "CHEM PLANT [Plate x18 Gear x10 CuPlate x8]",
                "RECIPE_CHEM_DESC" => "COPPER PLATE -> COPPER WIRE",
                "RECIPE_ADV_MINER" => "ADV MINER [Plate x20 Gear x12 Circuit x4]",
                "RECIPE_ADV_MINER_DESC" => "2X MINING SPEED",
                "RECIPE_GENERATOR" => "GENERATOR [Plate x12 Gear x8]",
                "RECIPE_GENERATOR_DESC" => "PROVIDES 10 POWER UNITS",
                "RECIPE_COAL_GEN" => "COAL GEN [Plate x15 Gear x10 CuPlate x5]",
                "RECIPE_COAL_GEN_DESC" => "BURNS COAL FOR 20 POWER (30 SEC/COAL)",
                "RECIPE_POWER_INFO" => "POWER SYSTEM",
                "RECIPE_POWER_INFO_DESC" => "MACHINES SLOW DOWN IF POWER < 100%",
                "TIP_ROTATE" => "ROTATE PLACEMENT DIRECTION (R)",
                "TIP_INVENTORY" => "INVENTORY",
                "TIP_PLATES" => "PLATES DELIVERED",
                "TIP_SCIENCE" => "SCIENCE DELIVERED",
                "TIP_POWER" => "POWER RATIO",
                "TIP_RESEARCH" => "RESEARCH TARGET",
                "UNLOCK_SPLITTER" => "SPLITTER",
                "UNLOCK_MERGER" => "MERGER",
                "UNLOCK_ROUTER" => "ROUTER",
                "UNLOCK_ASM" => "ASSEMBLER",
                "UNLOCK_GEN" => "GENERATOR",
                "UNLOCK_FAST" => "FAST CONVEYOR",
                "HINT_ORE" => "ORE DEPOSIT - PLACE MINER",
                "HINT_CONV" => "CONVEYOR - MOVES ITEMS",
                "HINT_FAST" => "FAST CONVEYOR - FASTER ITEMS",
                "HINT_SPLIT" => "SPLITTER - 3 OUTPUTS",
                "HINT_MERGE" => "MERGER - 1 OUTPUT",
                "HINT_ROUTER" => "ROUTER - AUTO INPUT MULTI OUTPUT",
                "HINT_MINER" => "MINER - ORE OUTPUT",
                "HINT_SMELT" => "SMELTER - ORE TO PLATE",
                "HINT_ASM" => "ASSEMBLER - PLATE TO GEAR",
                "HINT_LAB" => "LAB - GEAR TO SCIENCE",
                "HINT_GEN" => "GENERATOR - ADDS POWER",
                "HINT_STORE" => "STORAGE - COLLECTS ITEMS",
                "TOOL_CONV" => "CONVEYOR [Plate x1] SPEED 1.2",
                "TOOL_FAST" => "FAST CONVEYOR [Plate x2 Gear x1] SPEED 2.2",
                "TOOL_SPLIT" => "SPLITTER [Plate x3 Gear x2] 3 OUTPUTS",
                "TOOL_MERGE" => "MERGER [Plate x3 Gear x2] 1 OUTPUT",
                "TOOL_ROUTER" => "ROUTER [Plate x2 Gear x1] MULTI-OUTPUT",
                "TOOL_MINER" => "MINER [Plate x5 Gear x3] MINES ORE",
                "TOOL_SMELT" => "SMELTER [Plate x8 Gear x4] ORE TO PLATE",
                "TOOL_ASM" => "ASSEMBLER [Plate x10 Gear x5] PLATE TO GEAR",
                "TOOL_LAB" => "LAB [Plate x8 Gear x6 Circuit x2] GEAR TO SCIENCE",
                "TOOL_GEN" => "GENERATOR [Plate x12 Gear x8] ADDS POWER",
                "TOOL_STORE" => "STORAGE [Plate x4] ACCEPTS ITEMS",
                "TOOL_ERASE" => "ERASE REMOVE TILE",
                // 新增翻译
                "TOOL_UNDERGROUND" => "UNDERGROUND [Plate x4 Gear x2] BYPASSES",
                "TOOL_COAL_GEN" => "COAL GEN [Plate x15 Gear x10 CuPlate x5] BURNS COAL",
                "TOOL_ADV_MINER" => "ADV MINER [Plate x20 Gear x12 Circuit x4] 2X SPEED",
                "TOOL_ASM_MK2" => "ASM MK2 [Plate x15 Gear x8 Circuit x6] DUAL INPUT",
                "TOOL_CHEM" => "CHEM PLANT [Plate x18 Gear x10 CuPlate x8] CU TO WIRE",
                "UNLOCK_UNDERGROUND" => "UNDERGROUND BELT",
                "UNLOCK_COAL_GEN" => "COAL GENERATOR",
                "UNLOCK_ADV_MINER" => "ADVANCED MINER",
                "UNLOCK_ASM_MK2" => "ASSEMBLER MK2",
                "UNLOCK_CHEM" => "CHEMICAL PLANT",
                "ACH_FIRST_PLATE" => "FIRST PLATE",
                "ACH_PLATE_MASTER" => "PLATE MASTER (100 PLATES)",
                "ACH_SCIENTIST" => "SCIENTIST (50 RESEARCH)",
                "ACH_INDUSTRIAL" => "INDUSTRIAL (10 MINERS)",
                "ACH_POWER_TYCOON" => "POWER TYCOON (5 GENERATORS)",
                "ACH_DIVERSIFIED" => "DIVERSIFIED (ALL ITEM TYPES)",
                "HINT_COPPER" => "COPPER DEPOSIT - PLACE MINER",
                "HINT_COAL" => "COAL DEPOSIT - PLACE MINER",
                "HINT_GOLD" => "GOLD DEPOSIT - PLACE MINER",
                "HINT_TITANIUM" => "TITANIUM DEPOSIT - PLACE MINER",
                "HINT_URANIUM" => "URANIUM DEPOSIT - PLACE MINER",
                "HINT_UNDERGROUND" => "UNDERGROUND BELT - BYPASSES OBSTACLES",
                "HINT_COAL_GEN" => "COAL GENERATOR - BURNS COAL",
                "HINT_ADV_MINER" => "ADVANCED MINER - 2X SPEED",
                "HINT_ASM_MK2" => "ASSEMBLER MK2 - DUAL INPUT",
                "HINT_CHEM" => "CHEMICAL PLANT - COPPER TO WIRE",
                "HINT_LOCKED" => "LOCKED REGION - CLICK TO UNLOCK",
                "HINT_MOUNTAIN" => "MOUNTAIN - CANNOT BUILD",
                "HINT_WATER" => "WATER - CANNOT BUILD",
                _ => key
            };
        }

        return key switch
        {
            "SETTINGS_TITLE" => "设置",
            "SET_GRID" => "网格",
            "SET_ORE" => "矿点高亮",
            "SET_TIPS" => "提示",
            "SET_AUTOPAUSE" => "自动暂停",
            "SET_VOLUME" => "音效音量",
            "SET_TUTORIAL" => "新手引导",
            "SET_LANG" => "语言",
            "SET_DEV" => "开发者模式",
            "SET_DEVMENU" => "开发菜单",
            "SET_HINTS" => "ESC 关闭  +/- 音量  7 语言  8 开发者  9 菜单",
            "DEV_TITLE" => "开发者菜单",
            "DEV_HINTS" => "Q +科研  W +材料  E +板  R +科研",
            "DEV_ADD_RESEARCH" => "+5 科研",
            "DEV_ADD_MATERIALS" => "+50 材料",
            "DEV_ADD_PLATES" => "+10 板",
            "DEV_ADD_SCIENCE" => "+10 科研",
            "DEV_RESEARCH" => "科研",
            "DEV_MATERIALS" => "材料",
            "DEV_PLATES" => "板",
            "DEV_SCIENCE" => "科研",
            "TUTORIAL_TITLE" => "新手引导",
            "TUTORIAL_HINT" => "按 F2 隐藏",
            "TUTORIAL_1" => "1 在矿点放置矿机（工具2）",
            "TUTORIAL_2" => "2 放置传送带与熔炉（1,3）",
            "TUTORIAL_3" => "3 向仓库交付 5 片金属板（4）",
            "TUTORIAL_4" => "4 放置组装机与实验室（7,8）",
            "TUTORIAL_5" => "5 生产科研并解锁新工具",
            "TUTORIAL_DONE" => "引导完成",
            // 配方手册翻译
            "RECIPE_TITLE" => "配方手册 (F3)",
            "RECIPE_HINT" => "F3 关闭  左右箭头翻页",
            "RECIPE_BASIC" => "== 基础建筑 ==",
            "RECIPE_LOGISTICS" => "== 物流系统 ==",
            "RECIPE_PRODUCTION" => "== 生产建筑 ==",
            "RECIPE_POWER" => "== 电力系统 ==",
            "RECIPE_CONVEYOR" => "传送带 [铁板x1]",
            "RECIPE_CONVEYOR_DESC" => "运输物品，速度 1.2 格/秒",
            "RECIPE_FAST_CONV" => "快速传送带 [铁板x2 齿轮x1]",
            "RECIPE_FAST_CONV_DESC" => "运输物品，速度 2.2 格/秒",
            "RECIPE_MINER" => "矿机 [铁板x5 齿轮x3]",
            "RECIPE_MINER_DESC" => "从矿点提取矿石（1个/秒）",
            "RECIPE_SMELTER" => "熔炉 [铁板x8 齿轮x4]",
            "RECIPE_SMELTER_DESC" => "矿石 -> 金属板（2秒）",
            "RECIPE_STORAGE" => "仓库 [铁板x4]",
            "RECIPE_STORAGE_DESC" => "收集物品到库存",
            "RECIPE_SPLITTER" => "分流器 [铁板x3 齿轮x2]",
            "RECIPE_SPLITTER_DESC" => "1输入 -> 3输出（轮流分配）",
            "RECIPE_MERGER" => "合流器 [铁板x3 齿轮x2]",
            "RECIPE_MERGER_DESC" => "3输入 -> 1输出",
            "RECIPE_ROUTER" => "路由器 [铁板x2 齿轮x1]",
            "RECIPE_ROUTER_DESC" => "自动检测输入，其他方向输出",
            "RECIPE_UNDERGROUND" => "地下传送带 [铁板x4 齿轮x2]",
            "RECIPE_UNDERGROUND_DESC" => "可穿越最多5格障碍",
            "RECIPE_ASSEMBLER" => "组装机 [铁板x10 齿轮x5]",
            "RECIPE_ASSEMBLER_DESC" => "金属板 -> 齿轮（2.5秒）",
            "RECIPE_ASM_MK2" => "装配机Mk2 [铁板x15 齿轮x8 电路x6]",
            "RECIPE_ASM_MK2_DESC" => "双输入，加工更快",
            "RECIPE_LAB" => "实验室 [铁板x8 齿轮x6 电路x2]",
            "RECIPE_LAB_DESC" => "齿轮 -> 科研包（3秒）",
            "RECIPE_CHEM" => "化工厂 [铁板x18 齿轮x10 铜板x8]",
            "RECIPE_CHEM_DESC" => "铜板 -> 铜线",
            "RECIPE_ADV_MINER" => "高级矿机 [铁板x20 齿轮x12 电路x4]",
            "RECIPE_ADV_MINER_DESC" => "2倍采矿速度",
            "RECIPE_GENERATOR" => "发电机 [铁板x12 齿轮x8]",
            "RECIPE_GENERATOR_DESC" => "提供 10 单位电力",
            "RECIPE_COAL_GEN" => "燃煤发电机 [铁板x15 齿轮x10 铜板x5]",
            "RECIPE_COAL_GEN_DESC" => "燃烧煤炭产生 20 电力（30秒/煤）",
            "RECIPE_POWER_INFO" => "电力系统",
            "RECIPE_POWER_INFO_DESC" => "电力不足时机器减速",
            "TIP_ROTATE" => "旋转放置方向（R）",
            "TIP_INVENTORY" => "库存",
            "TIP_PLATES" => "已交付金属板",
            "TIP_SCIENCE" => "已交付科研",
            "TIP_POWER" => "电力比例",
            "TIP_RESEARCH" => "研究目标",
            "UNLOCK_SPLITTER" => "分流器",
            "UNLOCK_MERGER" => "合流器",
            "UNLOCK_ROUTER" => "路由器",
            "UNLOCK_ASM" => "组装机",
            "UNLOCK_GEN" => "发电机",
            "UNLOCK_FAST" => "快速带",
            "HINT_ORE" => "矿点 - 可放矿机",
            "HINT_CONV" => "传送带 - 运输物品",
            "HINT_FAST" => "快速带 - 更快运输",
            "HINT_SPLIT" => "分流器 - 三方向输出",
            "HINT_MERGE" => "合流器 - 合并输出",
            "HINT_ROUTER" => "路由器 - 自动输入多方向输出",
            "HINT_MINER" => "矿机 - 产出矿石",
            "HINT_SMELT" => "熔炉 - 矿石变板",
            "HINT_ASM" => "组装机 - 板变齿轮",
            "HINT_LAB" => "实验室 - 齿轮变科研",
            "HINT_GEN" => "发电机 - 提供电力",
            "HINT_STORE" => "仓库 - 收集物品",
            "TOOL_CONV" => "传送带 [铁板x1] 速度1.2",
            "TOOL_FAST" => "快速带 [铁板x2 齿轮x1] 速度2.2",
            "TOOL_SPLIT" => "分流器 [铁板x3 齿轮x2] 三路输出",
            "TOOL_MERGE" => "合流器 [铁板x3 齿轮x2] 合并输出",
            "TOOL_ROUTER" => "路由器 [铁板x2 齿轮x1] 自动多方向输出",
            "TOOL_MINER" => "矿机 [铁板x5 齿轮x3] 需矿点",
            "TOOL_SMELT" => "熔炉 [铁板x8 齿轮x4] 矿石->板",
            "TOOL_ASM" => "组装机 [铁板x10 齿轮x5] 板->齿轮",
            "TOOL_LAB" => "实验室 [铁板x8 齿轮x6 电路x2] 齿轮->科研",
            "TOOL_GEN" => "发电机 [铁板x12 齿轮x8] 提供电力",
            "TOOL_STORE" => "仓库 [铁板x4] 收集物品",
            "TOOL_ERASE" => "清除 方块",
            // 新增翻译
            "TOOL_UNDERGROUND" => "地下带 [铁板x4 齿轮x2] 穿越障碍",
            "TOOL_COAL_GEN" => "燃煤发电机 [铁板x15 齿轮x10 铜板x5] 燃烧煤炭",
            "TOOL_ADV_MINER" => "高级矿机 [铁板x20 齿轮x12 电路x4] 2倍速度",
            "TOOL_ASM_MK2" => "装配机Mk2 [铁板x15 齿轮x8 电路x6] 双输入",
            "TOOL_CHEM" => "化工厂 [铁板x18 齿轮x10 铜板x8] 铜板->铜线",
            "UNLOCK_UNDERGROUND" => "地下传送带",
            "UNLOCK_COAL_GEN" => "燃煤发电机",
            "UNLOCK_ADV_MINER" => "高级矿机",
            "UNLOCK_ASM_MK2" => "装配机Mk2",
            "UNLOCK_CHEM" => "化工厂",
            "ACH_FIRST_PLATE" => "第一块板材",
            "ACH_PLATE_MASTER" => "生产大师（100板材）",
            "ACH_SCIENTIST" => "科学家（50研究点）",
            "ACH_INDUSTRIAL" => "工业巨头（10矿机）",
            "ACH_POWER_TYCOON" => "电力大亨（5发电机）",
            "ACH_DIVERSIFIED" => "多元化（所有物品类型）",
            "HINT_COPPER" => "铜矿点 - 可放矿机",
            "HINT_COAL" => "煤矿点 - 可放矿机",
            "HINT_GOLD" => "金矿点 - 可放矿机",
            "HINT_TITANIUM" => "钛矿点 - 可放矿机",
            "HINT_URANIUM" => "铀矿点 - 可放矿机",
            "HINT_UNDERGROUND" => "地下带 - 穿越障碍",
            "HINT_COAL_GEN" => "燃煤发电机 - 燃烧煤炭",
            "HINT_ADV_MINER" => "高级矿机 - 2倍速度",
            "HINT_ASM_MK2" => "装配机Mk2 - 双输入",
            "HINT_CHEM" => "化工厂 - 铜板变铜线",
            "HINT_LOCKED" => "未解锁区域 - 点击解锁",
            "HINT_MOUNTAIN" => "山地 - 无法建造",
            "HINT_WATER" => "水域 - 无法建造",
            _ => key
        };
    }

    private string GetTileHint(Point tile)
    {
        if (!InBounds(tile))
        {
            return string.Empty;
        }

        // 检查地形类型
        TerrainType terrain = _terrainMap[tile.X, tile.Y];
        if (terrain == TerrainType.Locked)
        {
            // 查找该区域的解锁费用
            foreach (var region in _regions)
            {
                if (region.Bounds.Contains(tile) && !region.IsUnlocked)
                {
                    return _language == Language.English
                        ? $"LOCKED REGION - COST {region.UnlockCost} TO UNLOCK"
                        : $"未解锁区域 - 需要 {region.UnlockCost} 解锁";
                }
            }
            return T("HINT_LOCKED");
        }
        if (terrain == TerrainType.Mountain)
        {
            return T("HINT_MOUNTAIN");
        }
        if (terrain == TerrainType.Water)
        {
            return T("HINT_WATER");
        }

        Tile t = _tiles[tile.X, tile.Y];
        if (t.Type == TileType.Empty && _oreMap[tile.X, tile.Y])
        {
            // 根据矿石类型显示不同提示
            OreType oreType = _oreTypeMap[tile.X, tile.Y];
            return oreType switch
            {
                OreType.Copper => T("HINT_COPPER"),
                OreType.Coal => T("HINT_COAL"),
                OreType.Gold => T("HINT_GOLD"),
                OreType.Titanium => T("HINT_TITANIUM"),
                OreType.Uranium => T("HINT_URANIUM"),
                _ => T("HINT_ORE")
            };
        }

        // 获取处理器信息（如果是处理器类型建筑）
        string processorInfo = GetProcessorInfo(tile, t.Type);
        if (!string.IsNullOrEmpty(processorInfo))
        {
            return processorInfo;
        }

        return t.Type switch
        {
            TileType.Conveyor => T("HINT_CONV"),
            TileType.FastConveyor => T("HINT_FAST"),
            TileType.Splitter => T("HINT_SPLIT"),
            TileType.Merger => T("HINT_MERGE"),
            TileType.Router => T("HINT_ROUTER"),
            TileType.Miner => T("HINT_MINER"),
            TileType.Generator => T("HINT_GEN"),
            TileType.Storage => GetStorageInfo(tile),
            // 新增建筑提示
            TileType.UndergroundEntry => T("HINT_UNDERGROUND"),
            TileType.UndergroundExit => T("HINT_UNDERGROUND"),
            TileType.CoalGenerator => GetCoalGeneratorInfo(tile),
            TileType.AdvancedMiner => T("HINT_ADV_MINER"),
            _ => string.Empty
        };
    }

    // 获取处理器信息（缓冲区、配方、进度）
    private string GetProcessorInfo(Point tile, TileType type)
    {
        // 查找处理器状态（可能需要找到主格子）
        Point mainTile = tile;
        if (_tiles[tile.X, tile.Y].ParentTile.HasValue)
        {
            mainTile = _tiles[tile.X, tile.Y].ParentTile.Value;
        }

        if (!_processors.TryGetValue(mainTile, out var state))
        {
            // 尝试直接用当前格子
            if (!_processors.TryGetValue(tile, out state))
            {
                return string.Empty;
            }
        }

        var sb = new System.Text.StringBuilder();

        // 建筑名称
        string buildingName = type switch
        {
            TileType.Smelter => _language == Language.English ? "SMELTER" : "冶炼厂",
            TileType.Assembler => _language == Language.English ? "ASSEMBLER" : "装配机",
            TileType.AssemblerMk2 => _language == Language.English ? "ASSEMBLER MK2" : "装配机Mk2",
            TileType.Lab => _language == Language.English ? "LAB" : "实验室",
            TileType.ChemicalPlant => _language == Language.English ? "CHEMICAL PLANT" : "化工厂",
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(buildingName))
        {
            return string.Empty;
        }

        sb.AppendLine(buildingName);

        // 配方信息
        if (state.Recipe != null)
        {
            sb.AppendLine(_language == Language.English ? $"Recipe: {state.Recipe.Name}" : $"配方: {state.Recipe.Name}");

            // 输入需求
            sb.Append(_language == Language.English ? "Input: " : "输入: ");
            bool first = true;
            foreach (var input in state.Recipe.Inputs)
            {
                if (!first) sb.Append(", ");
                string itemName = GetItemName(input.Key);
                int have = state.InputBuffer.GetValueOrDefault(input.Key, 0);
                sb.Append($"{itemName} {have}/{input.Value}");
                first = false;
            }
            sb.AppendLine();

            // 输出
            sb.Append(_language == Language.English ? "Output: " : "输出: ");
            first = true;
            foreach (var output in state.Recipe.Outputs)
            {
                if (!first) sb.Append(", ");
                string itemName = GetItemName(output.Key);
                sb.Append($"{itemName} x{output.Value}");
                first = false;
            }
            sb.AppendLine();

            // 燃料需求
            if (state.RequiresFuel && state.Recipe.Fuel.HasValue)
            {
                string fuelName = GetItemName(state.Recipe.Fuel.Value);
                sb.AppendLine(_language == Language.English
                    ? $"Fuel: {fuelName} ({state.BurnTime:F1}s)"
                    : $"燃料: {fuelName} ({state.BurnTime:F1}秒)");
            }
        }
        else
        {
            sb.AppendLine(_language == Language.English ? "No recipe set" : "未设置配方");
        }

        // 制作进度
        if (state.IsCrafting)
        {
            float progress = state.GetProgress() * 100f;
            sb.AppendLine(_language == Language.English
                ? $"Crafting: {progress:F0}%"
                : $"制作中: {progress:F0}%");
        }

        // 输出缓冲区
        if (state.OutputBuffer.Count > 0)
        {
            sb.AppendLine(_language == Language.English
                ? $"Output buffer: {state.OutputBuffer.Count}"
                : $"输出缓冲: {state.OutputBuffer.Count}");
        }

        return sb.ToString().TrimEnd();
    }

    // 获取物品名称
    private string GetItemName(ItemType type)
    {
        if (_language == Language.English)
        {
            return type switch
            {
                ItemType.Ore => "Iron Ore",
                ItemType.Plate => "Iron Plate",
                ItemType.Gear => "Gear",
                ItemType.Science => "Science",
                ItemType.CopperOre => "Copper Ore",
                ItemType.CopperPlate => "Copper Plate",
                ItemType.Coal => "Coal",
                ItemType.GoldOre => "Gold Ore",
                ItemType.GoldPlate => "Gold Plate",
                ItemType.TitaniumOre => "Titanium Ore",
                ItemType.TitaniumPlate => "Titanium Plate",
                ItemType.UraniumOre => "Uranium Ore",
                ItemType.UraniumPlate => "Uranium Plate",
                ItemType.CopperWire => "Copper Wire",
                ItemType.Circuit => "Circuit",
                ItemType.Steel => "Steel",
                ItemType.RedScience => "Red Science",
                ItemType.GreenScience => "Green Science",
                _ => type.ToString()
            };
        }
        else
        {
            return type switch
            {
                ItemType.Ore => "铁矿",
                ItemType.Plate => "铁板",
                ItemType.Gear => "齿轮",
                ItemType.Science => "科学包",
                ItemType.CopperOre => "铜矿",
                ItemType.CopperPlate => "铜板",
                ItemType.Coal => "煤炭",
                ItemType.GoldOre => "金矿",
                ItemType.GoldPlate => "金板",
                ItemType.TitaniumOre => "钛矿",
                ItemType.TitaniumPlate => "钛板",
                ItemType.UraniumOre => "铀矿",
                ItemType.UraniumPlate => "铀板",
                ItemType.CopperWire => "铜线",
                ItemType.Circuit => "电路板",
                ItemType.Steel => "钢材",
                ItemType.RedScience => "红色科学包",
                ItemType.GreenScience => "绿色科学包",
                _ => type.ToString()
            };
        }
    }

    // 获取仓库信息
    private string GetStorageInfo(Point tile)
    {
        if (_storages.TryGetValue(tile, out var storage))
        {
            return _language == Language.English
                ? $"STORAGE - Items: {storage.Count}"
                : $"仓库 - 物品: {storage.Count}";
        }
        return T("HINT_STORE");
    }

    // 获取燃煤发电机信息
    private string GetCoalGeneratorInfo(Point tile)
    {
        // 查找主格子
        Point mainTile = tile;
        if (_tiles[tile.X, tile.Y].ParentTile.HasValue)
        {
            mainTile = _tiles[tile.X, tile.Y].ParentTile.Value;
        }

        if (_coalGenerators.TryGetValue(mainTile, out var state) || _coalGenerators.TryGetValue(tile, out state))
        {
            string status = state.HasFuel
                ? (_language == Language.English ? "Running" : "运行中")
                : (_language == Language.English ? "No Fuel" : "无燃料");
            return _language == Language.English
                ? $"COAL GENERATOR - {status}\nFuel: {state.FuelTimer:F1}s"
                : $"燃煤发电机 - {status}\n燃料: {state.FuelTimer:F1}秒";
        }
        return T("HINT_COAL_GEN");
    }

    // 解锁区域
    private void TryUnlockRegion(Point tile)
    {
        foreach (var region in _regions)
        {
            if (region.Bounds.Contains(tile) && !region.IsUnlocked)
            {
                // 使用铁板解锁区域
                int plateCost = region.UnlockCost;
                if (GetInventoryCount(ItemType.Plate) >= plateCost)
                {
                    _inventory[ItemType.Plate] -= plateCost;
                    UnlockRegionTerrain(region.Id);
                    _sfxUnlock?.Play(GetSfxVolume(1f), 0f, 0f);
                }
                else
                {
                    _sfxError?.Play(GetSfxVolume(1f), 0f, 0f);
                }
                break;
            }
        }
    }

    private void UpdateOrigin()
    {
        // 将视角设置在地图中心
        // 计算地图中心的世界坐标
        Vector2 mapCenterWorld = new Vector2(MapWidth / 2f, MapHeight / 2f);
        Vector2 mapCenterScreen = WorldToScreen(mapCenterWorld);
        
        // 调整相机平移，使地图中心在屏幕中心
        _camera.Pan = new Vector2(
            GraphicsDevice.Viewport.Width / 2f - mapCenterScreen.X,
            GraphicsDevice.Viewport.Height / 2f - mapCenterScreen.Y
        );
        
        _origin = new Vector2(GraphicsDevice.Viewport.Width / 2f, GraphicsDevice.Viewport.Height / 2f);
    }

    private void UpdateUiLayout()
    {
        int startX = ToolbarPadding;
        int y = (ToolbarHeight - ToolbarButtonSize) / 2;
        _toolButtons = new Rectangle[_toolbarTools.Length == 0 ? 5 : _toolbarTools.Length];
        for (int i = 0; i < _toolButtons.Length; i++)
        {
            _toolButtons[i] = new Rectangle(startX + i * (ToolbarButtonSize + ToolbarPadding), y, ToolbarButtonSize, ToolbarButtonSize);
        }

        int dirX = startX + _toolButtons.Length * (ToolbarButtonSize + ToolbarPadding) + ToolbarPadding;
        _directionButton = new Rectangle(dirX, y, ToolbarButtonSize, ToolbarButtonSize);
    }

    private void RebuildToolbar()
    {
        var tools = new List<Tool>
        {
            Tool.Conveyor
        };

        if (_unlockFastConveyor)
        {
            tools.Add(Tool.FastConveyor);
        }

        if (_unlockUnderground)
        {
            tools.Add(Tool.UndergroundBelt);
        }

        tools.Add(Tool.Miner);

        if (_unlockAdvancedMiner)
        {
            tools.Add(Tool.AdvancedMiner);
        }

        tools.Add(Tool.Smelter);

        if (_unlockAssembler)
        {
            tools.Add(Tool.Assembler);
        }

        if (_unlockAssemblerMk2)
        {
            tools.Add(Tool.AssemblerMk2);
        }

        if (_unlockChemicalPlant)
        {
            tools.Add(Tool.ChemicalPlant);
        }

        if (_unlockLab)
        {
            tools.Add(Tool.Lab);
        }

        if (_unlockSplitter)
        {
            tools.Add(Tool.Splitter);
        }

        if (_unlockMerger)
        {
            tools.Add(Tool.Merger);
        }

        if (_unlockRouter)
        {
            tools.Add(Tool.Router);
        }

        if (_unlockGenerator)
        {
            tools.Add(Tool.Generator);
        }

        if (_unlockCoalGenerator)
        {
            tools.Add(Tool.CoalGenerator);
        }

        tools.Add(Tool.Storage);
        tools.Add(Tool.Erase);

        _toolbarTools = tools.ToArray();
        UpdateUiLayout();
    }

    private bool IsToolUnlocked(Tool tool)
    {
        return tool switch
        {
            Tool.FastConveyor => _unlockFastConveyor,
            Tool.Splitter => _unlockSplitter,
            Tool.Merger => _unlockMerger,
            Tool.Router => _unlockRouter,
            Tool.Assembler => _unlockAssembler,
            Tool.Lab => _unlockLab,
            Tool.Generator => _unlockGenerator,
            // 新增工具解锁
            Tool.UndergroundBelt => _unlockUnderground,
            Tool.CoalGenerator => _unlockCoalGenerator,
            Tool.AdvancedMiner => _unlockAdvancedMiner,
            Tool.AssemblerMk2 => _unlockAssemblerMk2,
            Tool.ChemicalPlant => _unlockChemicalPlant,
            _ => true
        };
    }

    private void SelectTool(Tool tool)
    {
        if (IsToolUnlocked(tool))
        {
            _tool = tool;
        }
        else
        {
            _sfxError?.Play(GetSfxVolume(1f), 0f, 0f);
        }
    }

    private Texture2D CreateDiamondTexture(int width, int height, Color fill, Color border, float borderThickness)
    {
        var texture = new Texture2D(GraphicsDevice, width, height);
        Color[] data = new Color[width * height];
        float halfW = width / 2f;
        float halfH = height / 2f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = MathF.Abs(x - halfW) / halfW;
                float dy = MathF.Abs(y - halfH) / halfH;
                float distance = dx + dy;
                if (distance <= 1f)
                {
                    float edge = 1f - distance;
                    if (edge < borderThickness)
                    {
                        data[y * width + x] = border;
                    }
                    else
                    {
                        data[y * width + x] = fill;
                    }
                }
                else
                {
                    data[y * width + x] = new Color(0, 0, 0, 0);
                }
            }
        }

        texture.SetData(data);
        return texture;
    }

    private void UpdateTutorialProgress()
    {
        if (_tutorialDone)
        {
            return;
        }

        if (_tutorialStep == 0 && _miners.Count > 0)
        {
            _tutorialStep = 1;
        }
        else if (_tutorialStep == 1 && HasTile(TileType.Smelter))
        {
            _tutorialStep = 2;
        }
        else if (_tutorialStep == 2 && _totalPlatesStored >= 5)
        {
            _tutorialStep = 3;
        }
        else if (_tutorialStep == 3 && HasTile(TileType.Assembler) && HasTile(TileType.Lab))
        {
            _tutorialStep = 4;
        }
        else if (_tutorialStep == 4 && _researchPoints >= 5)
        {
            _tutorialDone = true;
        }
    }

    private bool HasTile(TileType type)
    {
        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                if (_tiles[x, y].Type == type)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void CheckResearchUnlocks()
    {
        bool changed = false;
        var unlocked = new List<string>();

        // 10研究点：分流器
        if (!_unlockSplitter && _researchPoints >= ResearchTargets[0])
        {
            _unlockSplitter = true;
            changed = true;
            unlocked.Add(T("UNLOCK_SPLITTER"));
        }
        // 20研究点：合并器
        if (!_unlockMerger && _researchPoints >= ResearchTargets[1])
        {
            _unlockMerger = true;
            changed = true;
            unlocked.Add(T("UNLOCK_MERGER"));
        }
        // 25研究点：路由器
        if (!_unlockRouter && _researchPoints >= 25)
        {
            _unlockRouter = true;
            changed = true;
            unlocked.Add(T("UNLOCK_ROUTER"));
        }
        // 30研究点：装配机（已默认解锁，这里解锁地下传送带）
        if (!_unlockUnderground && _researchPoints >= ResearchTargets[2])
        {
            _unlockUnderground = true;
            changed = true;
            unlocked.Add(T("UNLOCK_UNDERGROUND"));
        }
        // 40研究点：发电机
        if (!_unlockGenerator && _researchPoints >= ResearchTargets[3])
        {
            _unlockGenerator = true;
            changed = true;
            unlocked.Add(T("UNLOCK_GEN"));
        }
        // 60研究点：快速传送带
        if (!_unlockFastConveyor && _researchPoints >= ResearchTargets[4])
        {
            _unlockFastConveyor = true;
            changed = true;
            unlocked.Add(T("UNLOCK_FAST"));
        }
        // 80研究点：燃煤发电机
        if (!_unlockCoalGenerator && _researchPoints >= ResearchTargets[5])
        {
            _unlockCoalGenerator = true;
            changed = true;
            unlocked.Add(T("UNLOCK_COAL_GEN"));
        }
        // 100研究点：高级矿工
        if (!_unlockAdvancedMiner && _researchPoints >= ResearchTargets[6])
        {
            _unlockAdvancedMiner = true;
            changed = true;
            unlocked.Add(T("UNLOCK_ADV_MINER"));
        }
        // 150研究点：装配机Mk2和化工厂
        if (!_unlockAssemblerMk2 && _researchPoints >= ResearchTargets[7])
        {
            _unlockAssemblerMk2 = true;
            _unlockChemicalPlant = true;
            changed = true;
            unlocked.Add(T("UNLOCK_ASM_MK2"));
            unlocked.Add(T("UNLOCK_CHEM"));
        }

        if (changed)
        {
            RebuildToolbar();
            ShowResearchToast(unlocked);
        }
    }

    private void ShowResearchToast(List<string> unlocked)
    {
        if (unlocked.Count == 0)
        {
            return;
        }

        string joined = string.Join(" / ", unlocked);
        _researchToast = _language == Language.English ? $"RESEARCH UNLOCKED: {joined}" : $"科研已解锁：{joined}";
        _researchToastTimer = 3f;
        _sfxUnlock?.Play(GetSfxVolume(0.9f), 0f, 0f);
    }

    private void CheckAchievements()
    {
        var newAchievements = new List<string>();

        // 第一块板材
        if (!_achievements.Contains("FIRST_PLATE") && _totalPlatesStored >= 1)
        {
            _achievements.Add("FIRST_PLATE");
            newAchievements.Add(T("ACH_FIRST_PLATE"));
        }

        // 生产大师：100块板材
        if (!_achievements.Contains("PLATE_MASTER") && _totalPlatesStored >= 100)
        {
            _achievements.Add("PLATE_MASTER");
            newAchievements.Add(T("ACH_PLATE_MASTER"));
            AddToInventory(ItemType.Plate, 50);
            AddToInventory(ItemType.Gear, 25);
        }

        // 科学家：50研究点
        if (!_achievements.Contains("SCIENTIST") && _researchPoints >= 50)
        {
            _achievements.Add("SCIENTIST");
            newAchievements.Add(T("ACH_SCIENTIST"));
            AddToInventory(ItemType.Plate, 30);
            AddToInventory(ItemType.Gear, 15);
        }

        // 工业巨头：10个矿工
        if (!_achievements.Contains("INDUSTRIAL") && _miners.Count >= 10)
        {
            _achievements.Add("INDUSTRIAL");
            newAchievements.Add(T("ACH_INDUSTRIAL"));
            AddToInventory(ItemType.Plate, 40);
            AddToInventory(ItemType.Gear, 20);
        }

        // 电力大亨：5个发电机
        int generatorCount = 0;
        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                if (_tiles[x, y].Type == TileType.Generator || _tiles[x, y].Type == TileType.CoalGenerator)
                {
                    generatorCount++;
                }
            }
        }
        if (!_achievements.Contains("POWER_TYCOON") && generatorCount >= 5)
        {
            _achievements.Add("POWER_TYCOON");
            newAchievements.Add(T("ACH_POWER_TYCOON"));
            AddToInventory(ItemType.Plate, 50);
            AddToInventory(ItemType.Gear, 30);
        }

        // 多元化：生产所有类型的物品
        if (!_achievements.Contains("DIVERSIFIED") &&
            _totalPlatesStored > 0 && _totalGearStored > 0 &&
            _totalScienceStored > 0 && _totalCopperStored > 0)
        {
            _achievements.Add("DIVERSIFIED");
            newAchievements.Add(T("ACH_DIVERSIFIED"));
            AddToInventory(ItemType.Plate, 80);
            AddToInventory(ItemType.Gear, 40);
            AddToInventory(ItemType.CopperPlate, 30);
        }

        // 显示成就提示
        if (newAchievements.Count > 0)
        {
            string joined = string.Join(" / ", newAchievements);
            _achievementToast = _language == Language.English ? $"ACHIEVEMENT: {joined}" : $"成就达成：{joined}";
            _achievementToastTimer = 4f;
            _sfxUnlock?.Play(GetSfxVolume(1f), 0f, 0f);
        }
    }

    private SoundEffect CreateTone(int frequencyHz, float durationSeconds, float volume)
    {
        const int sampleRate = 44100;
        int sampleCount = (int)(sampleRate * durationSeconds);
        byte[] buffer = new byte[sampleCount * 2];
        float increment = MathF.Tau * frequencyHz / sampleRate;
        float phase = 0f;
        short amp = (short)(short.MaxValue * MathHelper.Clamp(volume, 0f, 1f));
        int attack = Math.Max(1, (int)(sampleCount * 0.08f));
        int release = Math.Max(1, (int)(sampleCount * 0.18f));

        for (int i = 0; i < sampleCount; i++)
        {
            float env;
            if (i < attack)
            {
                env = i / (float)attack;
            }
            else if (i > sampleCount - release)
            {
                env = (sampleCount - i) / (float)release;
            }
            else
            {
                env = 1f;
            }

            short sample = (short)(MathF.Sin(phase) * amp * env);
            buffer[i * 2] = (byte)(sample & 0xFF);
            buffer[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            phase += increment;
        }

        return new SoundEffect(buffer, sampleRate, AudioChannels.Mono);
    }

    private float GetSfxVolume(float baseVolume)
    {
        return MathHelper.Clamp(baseVolume * _sfxVolume, 0f, 1f);
    }

    private void UpdateWindowTitle(float dt)
    {
        _titleTimer += dt;
        if (_titleTimer < 1f)
        {
            return;
        }

        _titleTimer = 0f;
        int stored = _storages.Values.Sum(s => s.Count);
        string research = _language == Language.English ? $"Research: {_researchPoints}" : $"研究: {_researchPoints}";
        string power = _powerUsed == 0 ? (_language == Language.English ? "Power: 0/0" : "电力: 0/0") : (_language == Language.English ? $"Power: {_powerProduced}/{_powerUsed}" : $"电力: {_powerProduced}/{_powerUsed}");
        string inventory = _language == Language.English ? "Inv" : "库存";
        string plates = _language == Language.English ? "Plates" : "板";
        string science = _language == Language.English ? "Science" : "科研";
        string storedText = _language == Language.English ? "Stored" : "已存";
        string toolText = _language == Language.English ? "Tool" : "工具";
        string dirText = _language == Language.English ? "Dir" : "方向";
        string pausedText = _paused ? (_language == Language.English ? "[Paused]" : "[暂停]") : "";
        Window.Title = $"FNA Factory - {inventory}: {GetInventoryCount(ItemType.Plate)}/{GetInventoryCount(ItemType.Gear)}/{GetInventoryCount(ItemType.CopperPlate)} - {plates}: {_totalPlatesStored} - {science}: {_totalScienceStored} - {power} - {research} - {storedText}: {stored} - {toolText}: {_tool} - {dirText}: {_direction} {pausedText}";
    }

    protected override void OnDeactivated(object? sender, EventArgs args)
    {
        if (_autoPauseOnFocusLoss && !_paused)
        {
            _autoPaused = true;
            _paused = true;
        }
        base.OnDeactivated(sender, args);
    }

    protected override void OnActivated(object? sender, EventArgs args)
    {
        if (_autoPaused)
        {
            _paused = false;
            _autoPaused = false;
        }
        base.OnActivated(sender, args);
    }

    private string GetSavePath()
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FactoryGame");
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }
        return Path.Combine(folder, "save.json");
    }

    private void SaveGame()
    {
        try
        {
            var saveData = new SaveData
            {
                Inventory = new Dictionary<ItemType, int>(_inventory),
                ResearchPoints = _researchPoints,
                TotalPlatesStored = _totalPlatesStored,
                TotalScienceStored = _totalScienceStored,
                TotalGearStored = _totalGearStored,
                CameraX = _camera.Pan.X,
                CameraY = _camera.Pan.Y,
                CameraZoom = _camera.Zoom
            };

            // 保存所有非空格子
            for (int y = 0; y < MapHeight; y++)
            {
                for (int x = 0; x < MapWidth; x++)
                {
                    var tile = _tiles[x, y];
                    if (tile.Type != TileType.Empty)
                    {
                        saveData.Tiles.Add(new SavedTile
                        {
                            X = x,
                            Y = y,
                            Type = (int)tile.Type,
                            Direction = (int)tile.Direction,
                            ParentX = tile.ParentTile?.X,
                            ParentY = tile.ParentTile?.Y
                        });
                    }
                }
            }

            // 保存区域解锁状态
            foreach (var region in _regions)
            {
                saveData.Regions.Add(new SavedRegion
                {
                    Id = region.Id,
                    IsUnlocked = region.IsUnlocked
                });
            }

            // 保存解锁的工具
            if (_unlockSplitter) saveData.UnlockedTools.Add((int)Tool.Splitter);
            if (_unlockMerger) saveData.UnlockedTools.Add((int)Tool.Merger);
            if (_unlockRouter) saveData.UnlockedTools.Add((int)Tool.Router);
            if (_unlockGenerator) saveData.UnlockedTools.Add((int)Tool.Generator);
            if (_unlockFastConveyor) saveData.UnlockedTools.Add((int)Tool.FastConveyor);
            if (_unlockUnderground) saveData.UnlockedTools.Add((int)Tool.UndergroundBelt);
            if (_unlockCoalGenerator) saveData.UnlockedTools.Add((int)Tool.CoalGenerator);
            if (_unlockAdvancedMiner) saveData.UnlockedTools.Add((int)Tool.AdvancedMiner);
            if (_unlockAssemblerMk2) saveData.UnlockedTools.Add((int)Tool.AssemblerMk2);
            if (_unlockChemicalPlant) saveData.UnlockedTools.Add((int)Tool.ChemicalPlant);

            string json = JsonSerializer.Serialize(saveData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(GetSavePath(), json);

            _researchToast = _language == Language.English ? "GAME SAVED" : "游戏已保存";
            _researchToastTimer = 2f;
            _sfxUnlock?.Play(GetSfxVolume(0.5f), 0f, 0f);
        }
        catch (Exception ex)
        {
            _researchToast = _language == Language.English ? $"SAVE FAILED: {ex.Message}" : $"保存失败: {ex.Message}";
            _researchToastTimer = 3f;
        }
    }

    private void LoadGame()
    {
        try
        {
            string path = GetSavePath();
            if (!File.Exists(path))
            {
                _researchToast = _language == Language.English ? "NO SAVE FILE" : "没有存档";
                _researchToastTimer = 2f;
                return;
            }

            string json = File.ReadAllText(path);
            var saveData = JsonSerializer.Deserialize<SaveData>(json);
            if (saveData == null)
            {
                _researchToast = _language == Language.English ? "INVALID SAVE" : "存档无效";
                _researchToastTimer = 2f;
                return;
            }

            // 清除当前状态
            for (int y = 0; y < MapHeight; y++)
            {
                for (int x = 0; x < MapWidth; x++)
                {
                    _tiles[x, y] = new Tile { Type = TileType.Empty, Direction = Direction.None };
                }
            }
            _items.Clear();
            _miners.Clear();
            _processors.Clear();
            _storages.Clear();
            _splitterIndex.Clear();
            _undergrounds.Clear();
            _coalGenerators.Clear();
            _routers.Clear();

            // 恢复数据
            _inventory.Clear();
            if (saveData.Inventory != null)
            {
                foreach (var kvp in saveData.Inventory)
                {
                    _inventory[kvp.Key] = kvp.Value;
                }
            }
            _researchPoints = saveData.ResearchPoints;
            _totalPlatesStored = saveData.TotalPlatesStored;
            _totalScienceStored = saveData.TotalScienceStored;
            _totalGearStored = saveData.TotalGearStored;
            _camera.Pan = new Vector2(saveData.CameraX, saveData.CameraY);
            _camera.Zoom = saveData.CameraZoom;

            // 恢复格子
            foreach (var savedTile in saveData.Tiles)
            {
                var tile = new Tile
                {
                    Type = (TileType)savedTile.Type,
                    Direction = (Direction)savedTile.Direction,
                    ParentTile = savedTile.ParentX.HasValue && savedTile.ParentY.HasValue
                        ? new Point(savedTile.ParentX.Value, savedTile.ParentY.Value)
                        : null
                };
                _tiles[savedTile.X, savedTile.Y] = tile;

                // 重建建筑状态（只对主格子）
                if (!tile.ParentTile.HasValue)
                {
                    AddBuildingState(new Point(savedTile.X, savedTile.Y), tile.Type, tile.Direction);
                }
            }

            // 恢复区域解锁状态
            foreach (var savedRegion in saveData.Regions)
            {
                var region = _regions.FirstOrDefault(r => r.Id == savedRegion.Id);
                if (region != null)
                {
                    region.IsUnlocked = savedRegion.IsUnlocked;
                }
            }

            // 恢复解锁的工具
            _unlockSplitter = saveData.UnlockedTools.Contains((int)Tool.Splitter);
            _unlockMerger = saveData.UnlockedTools.Contains((int)Tool.Merger);
            _unlockRouter = saveData.UnlockedTools.Contains((int)Tool.Router) || true; // 路由器默认解锁
            _unlockGenerator = saveData.UnlockedTools.Contains((int)Tool.Generator);
            _unlockFastConveyor = saveData.UnlockedTools.Contains((int)Tool.FastConveyor);
            _unlockUnderground = saveData.UnlockedTools.Contains((int)Tool.UndergroundBelt);
            _unlockCoalGenerator = saveData.UnlockedTools.Contains((int)Tool.CoalGenerator);
            _unlockAdvancedMiner = saveData.UnlockedTools.Contains((int)Tool.AdvancedMiner);
            _unlockAssemblerMk2 = saveData.UnlockedTools.Contains((int)Tool.AssemblerMk2);
            _unlockChemicalPlant = saveData.UnlockedTools.Contains((int)Tool.ChemicalPlant);

            RebuildToolbar();

            _researchToast = _language == Language.English ? "GAME LOADED" : "游戏已加载";
            _researchToastTimer = 2f;
            _sfxUnlock?.Play(GetSfxVolume(0.5f), 0f, 0f);
        }
        catch (Exception ex)
        {
            _researchToast = _language == Language.English ? $"LOAD FAILED: {ex.Message}" : $"加载失败: {ex.Message}";
            _researchToastTimer = 3f;
        }
    }

    private void UpdateParticles(float dt)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var particle = _particles[i];
            particle.Life -= dt;
            
            if (particle.Life <= 0f)
            {
                _particles.RemoveAt(i);
                continue;
            }
            
            particle.Position += particle.Velocity * dt;
            
            if (particle.Type == ParticleType.Smoke)
            {
                particle.Velocity.Y += 20f * dt;
            }
            else if (particle.Type == ParticleType.Spark)
            {
                particle.Velocity.Y += 50f * dt;
            }
        }
    }

    private void DrawParticles()
    {
        foreach (var particle in _particles)
        {
            float alpha = particle.Life / particle.MaxLife;
            Color color = new Color(particle.Color.R, particle.Color.G, particle.Color.B, (byte)(alpha * 255));
            
            float size = particle.Size * _camera.Zoom;
            Vector2 screenPos = WorldToScreen(particle.Position);
            
            switch (particle.Type)
            {
                case ParticleType.Spark:
                    _spriteBatch.Draw(_pixel, new Rectangle((int)(screenPos.X - size/2), (int)(screenPos.Y - size/2), (int)size, (int)size), color);
                    break;
                    
                case ParticleType.Star:
                    DrawStar(screenPos, size, color);
                    break;
            }
        }
    }

    private void DrawStar(Vector2 center, float size, Color color)
    {
        for (int i = 0; i < 5; i++)
        {
            float angle = (i * 72f - 90f) * MathF.PI / 180f;
            float outerRadius = size;
            float innerRadius = size * 0.5f;
            
            Vector2 outer1 = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * outerRadius;
            float angle2 = ((i * 72 + 36) % 360 - 90f) * MathF.PI / 180f;
            Vector2 inner = center + new Vector2(MathF.Cos(angle2), MathF.Sin(angle2)) * innerRadius;
            float angle3 = (((i + 1) * 72) % 360 - 90f) * MathF.PI / 180f;
            Vector2 outer2 = center + new Vector2(MathF.Cos(angle3), MathF.Sin(angle3)) * outerRadius;
            
            DrawLine(outer1, inner, color, 2f * _camera.Zoom);
            DrawLine(inner, outer2, color, 2f * _camera.Zoom);
        }
    }

    private void CreateParticles(Vector2 worldPos, ParticleType type, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var particle = new Particle
            {
                Position = worldPos,
                Type = type,
                MaxLife = 1f + _random.NextSingle() * 0.5f,
                Life = 1f + _random.NextSingle() * 0.5f
            };
            
            switch (type)
            {
                case ParticleType.Spark:
                    particle.Color = new Color(255, 200 + _random.Next(56), 100, 255);
                    particle.Size = 2f + _random.NextSingle() * 2f;
                    particle.Velocity = new Vector2((_random.NextSingle() - 0.5f) * 100f, -_random.NextSingle() * 50f);
                    break;
                    
                case ParticleType.Star:
                    particle.Color = new Color(255, 255, 150, 200);
                    particle.Size = 4f + _random.NextSingle() * 2f;
                    particle.Velocity = new Vector2((_random.NextSingle() - 0.5f) * 50f, -_random.NextSingle() * 80f);
                    break;
            }
            
            _particles.Add(particle);
        }
    }
}
