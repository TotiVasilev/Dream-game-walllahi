using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Central, per-level mine art and movement/drilling settings.
/// Assign this component to both MineGenerator and PlayerGridMovement.
/// Sprite arrays are converted into reusable, in-memory Tiles at runtime.
/// </summary>
public class LevelHolder : MonoBehaviour
{
    [Serializable]
    public class CrackStage
    {
        [Tooltip("Optional label to identify this damage stage.")]
        public string stageName = "Crack";

        [Tooltip("When this crack appears, as a fraction of the full drill duration (0 = immediately, 1 = at the end).")]
        [Range(0f, 1f)]
        public float normalizedTime = 0.3f;

        [Tooltip("Randomly chosen transparent crack overlay sprites for this stage. These are drawn OVER the existing dirt tile.")]
        public Sprite[] sprites;
    }

    [Serializable]
    public class LevelTiles
    {
        [Tooltip("Name shown in the Inspector.")]
        public string levelName = "New Level";

        [Header("Mine Generation")]
        [Tooltip("Variations for intact dirt blocks.")]
        public Sprite[] dirtSprites;

        [Tooltip("Variations for the background behind the dirt.")]
        public Sprite[] backgroundSprites;

        [Header("Movement and Drilling")]
        [Min(0f)]
        [Tooltip("Time in seconds to fall one grid cell.")]
        public float fallMoveDuration = 0.08f;

        [Min(0f)]
        [Tooltip("Time in seconds to drill one block. Zero means instant drilling.")]
        public float drillDuration = 1f;

        [Tooltip("Any number of crack stages. Give every stage its own normalized time (0-1) and Sprite array. Stages are chosen by time, so they need not be listed in chronological order.")]
        public CrackStage[] crackStages =
        {
            new CrackStage { stageName = "Crack 1", normalizedTime = 0.3f },
            new CrackStage { stageName = "Crack 2", normalizedTime = 0.6f }
        };

        [Tooltip("Sprites left in mined-out cells. Empty array means the tile is cleared.")]
        public Sprite[] brokenSprites;

        // Keep old serialized data while transitioning from the two fixed crack arrays.
        // These fields are hidden from the Inspector and copied into the new stages.
        [SerializeField, HideInInspector] private Sprite[] crack1Sprites;
        [SerializeField, HideInInspector] private Sprite[] crack2Sprites;

        internal void MigrateOldCrackSprites()
        {
            if (crackStages == null)
                crackStages = new CrackStage[0];

            if (crack1Sprites != null && crack1Sprites.Length > 0)
            {
                EnsureStage(0, "Crack 1", 0.3f);
                if (crackStages[0].sprites == null || crackStages[0].sprites.Length == 0)
                    crackStages[0].sprites = crack1Sprites;
                crack1Sprites = null;
            }

            if (crack2Sprites != null && crack2Sprites.Length > 0)
            {
                EnsureStage(1, "Crack 2", 0.6f);
                if (crackStages[1].sprites == null || crackStages[1].sprites.Length == 0)
                    crackStages[1].sprites = crack2Sprites;
                crack2Sprites = null;
            }
        }

        private void EnsureStage(int index, string defaultName, float defaultTime)
        {
            if (crackStages.Length <= index)
                Array.Resize(ref crackStages, index + 1);

            if (crackStages[index] == null)
                crackStages[index] = new CrackStage
                {
                    stageName = defaultName,
                    normalizedTime = defaultTime
                };
        }
    }

    [Header("Level Library")]
    [SerializeField] private LevelTiles[] levels = new LevelTiles[0];

    [Header("Active Level")]
    [Min(0)]
    [SerializeField] private int activeLevelIndex;

    private class RuntimeLevel
    {
        public Tile[] dirt;
        public Tile[] background;
        public Tile[][] crackStages;
        public Tile[] broken;
    }

    private RuntimeLevel[] runtimeLevels;
    private readonly HashSet<TileBase> naturalDirtTiles = new HashSet<TileBase>();
    private readonly HashSet<TileBase> mineableTiles = new HashSet<TileBase>();
    private readonly HashSet<TileBase> brokenTiles = new HashSet<TileBase>();

    public int ActiveLevelIndex => activeLevelIndex;
    public int LevelCount => levels != null ? levels.Length : 0;

    public string ActiveLevelName => GetActiveLevelData()?.levelName ?? string.Empty;

    public float ActiveFallMoveDuration => Mathf.Max(0f, GetActiveLevelData()?.fallMoveDuration ?? 0.08f);
    public float ActiveDrillDuration => Mathf.Max(0f, GetActiveLevelData()?.drillDuration ?? 1f);

    public int ActiveCrackStageCount
    {
        get
        {
            CrackStage[] stages = GetActiveLevelData()?.crackStages;
            return stages != null ? stages.Length : 0;
        }
    }

    private void Awake()
    {
        MigrateOldData();
        InitializeTiles();
    }

    private void OnValidate()
    {
        activeLevelIndex = Mathf.Clamp(activeLevelIndex, 0, Mathf.Max(0, LevelCount - 1));
        MigrateOldData();
    }

    private void MigrateOldData()
    {
        if (levels == null)
            return;

        foreach (LevelTiles level in levels)
            level?.MigrateOldCrackSprites();
    }

    /// <summary>Changes the selected level. Call StartNewRun afterwards to regenerate.</summary>
    public bool SetActiveLevel(int index)
    {
        if (index < 0 || index >= LevelCount || levels[index] == null)
        {
            Debug.LogWarning($"LevelHolder: Level index {index} does not exist.", this);
            return false;
        }

        activeLevelIndex = index;
        return true;
    }

    public float GetCrackStageNormalizedTime(int index)
    {
        CrackStage[] stages = GetActiveLevelData()?.crackStages;
        if (stages == null || index < 0 || index >= stages.Length || stages[index] == null)
            return 1f;

        return Mathf.Clamp01(stages[index].normalizedTime);
    }

    public TileBase GetRandomDirtTile() => PickRandomTile(GetActiveRuntimeLevel()?.dirt);
    public TileBase GetRandomBackgroundTile() => PickRandomTile(GetActiveRuntimeLevel()?.background);
    public TileBase GetRandomBrokenTile() => PickRandomTile(GetActiveRuntimeLevel()?.broken);

    public TileBase GetRandomCrackStageTile(int stageIndex)
    {
        Tile[][] stages = GetActiveRuntimeLevel()?.crackStages;
        if (stages == null || stageIndex < 0 || stageIndex >= stages.Length)
            return null;

        return PickRandomTile(stages[stageIndex]);
    }

    /// <summary>True for unbroken, generated dirt (including different sprite variants).</summary>
    public bool IsNaturalDirtTile(TileBase tile) => tile != null && naturalDirtTiles.Contains(tile);

    /// <summary>True for solid dirt that can be drilled. Crack overlays are deliberately excluded.</summary>
    public bool IsMineableTile(TileBase tile) => tile != null && mineableTiles.Contains(tile);

    /// <summary>True for broken dirt tiles, which are passable and have no collision.</summary>
    public bool IsBrokenTile(TileBase tile) => tile != null && brokenTiles.Contains(tile);

    private LevelTiles GetActiveLevelData()
    {
        if (levels == null || activeLevelIndex < 0 || activeLevelIndex >= levels.Length)
            return null;

        return levels[activeLevelIndex];
    }

    private RuntimeLevel GetActiveRuntimeLevel()
    {
        InitializeTiles();
        if (runtimeLevels == null || activeLevelIndex < 0 || activeLevelIndex >= runtimeLevels.Length)
            return null;

        return runtimeLevels[activeLevelIndex];
    }

    private void InitializeTiles()
    {
        if (runtimeLevels != null)
            return;

        MigrateOldData();
        int count = LevelCount;
        runtimeLevels = new RuntimeLevel[count];
        activeLevelIndex = Mathf.Clamp(activeLevelIndex, 0, Mathf.Max(0, count - 1));

        for (int i = 0; i < count; i++)
        {
            LevelTiles source = levels[i];
            if (source == null)
                continue;

            CrackStage[] sourceStages = source.crackStages;
            Tile[][] runtimeCracks = new Tile[sourceStages != null ? sourceStages.Length : 0][];

            for (int stage = 0; stage < runtimeCracks.Length; stage++)
                runtimeCracks[stage] = CreateTiles(sourceStages[stage]?.sprites, Tile.ColliderType.None);

            RuntimeLevel runtime = new RuntimeLevel
            {
                dirt = CreateTiles(source.dirtSprites, Tile.ColliderType.Grid),
                background = CreateTiles(source.backgroundSprites, Tile.ColliderType.None),
                crackStages = runtimeCracks,
                broken = CreateTiles(source.brokenSprites, Tile.ColliderType.None)
            };

            runtimeLevels[i] = runtime;
            AddTiles(naturalDirtTiles, runtime.dirt);
            AddTiles(mineableTiles, runtime.dirt);
            AddTiles(brokenTiles, runtime.broken);
        }

        if (count == 0)
            Debug.LogWarning("LevelHolder: Add at least one level to the Levels array.", this);
    }

    private static Tile[] CreateTiles(Sprite[] sprites, Tile.ColliderType colliderType)
    {
        if (sprites == null || sprites.Length == 0)
            return new Tile[0];

        List<Tile> result = new List<Tile>(sprites.Length);
        foreach (Sprite sprite in sprites)
        {
            if (sprite == null)
                continue;

            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.colliderType = colliderType;
            tile.flags = TileFlags.None;
            result.Add(tile);
        }
        return result.ToArray();
    }

    private static void AddTiles(HashSet<TileBase> destination, Tile[] tiles)
    {
        if (tiles == null)
            return;

        foreach (Tile tile in tiles)
            destination.Add(tile);
    }

    private static TileBase PickRandomTile(Tile[] tiles)
    {
        return tiles == null || tiles.Length == 0
            ? null
            : tiles[UnityEngine.Random.Range(0, tiles.Length)];
    }

    private void OnDestroy()
    {
        if (runtimeLevels == null)
            return;

        foreach (RuntimeLevel level in runtimeLevels)
        {
            if (level == null)
                continue;

            DestroyTiles(level.dirt);
            DestroyTiles(level.background);
            foreach (Tile[] stage in level.crackStages)
                DestroyTiles(stage);
            DestroyTiles(level.broken);
        }

        runtimeLevels = null;
        naturalDirtTiles.Clear();
        mineableTiles.Clear();
        brokenTiles.Clear();
    }

    private static void DestroyTiles(Tile[] tiles)
    {
        if (tiles == null)
            return;

        foreach (Tile tile in tiles)
        {
            if (tile != null)
                Destroy(tile);
        }
    }
}
