using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MineGenerator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MineGrid mineGrid;

    [SerializeField] private Tilemap dirtTilemap;
    [SerializeField] private TileBase dirtTile;
    [SerializeField] private TileBase brokenDirtTile;

    [SerializeField] private Tilemap backgroundTilemap;
    [SerializeField] private TileBase backgroundTile;

    [SerializeField] private PlayerGridMovement player;
    [SerializeField] private Camera mineCamera;

    [SerializeField] private MineTNTManager tntManager;

    [Header("Starting Generation")]
    [Min(1)]
    [SerializeField] private int startingRows = 30;

    [Header("Empty Tiles")]
    [Min(1)]
    [SerializeField] private int rowsPerEmptyGroup = 5;

    [Min(0)]
    [SerializeField] private int emptyTilesPerGroup = 4;

    [Header("TNT Generation")]
    [Min(1)]
    [SerializeField] private int rowsPerTNTGroup = 10;

    [Min(0)]
    [SerializeField] private int tntPerGroup = 1;

    [Header("Infinite Generation")]
    [Min(1)]
    [SerializeField] private int generateAheadDistance = 10;

    [Min(1)]
    [SerializeField] private int rowsPerGeneration = 10;

    [Header("Cleanup")]
    [Min(0)]
    [SerializeField] private int rowsToKeepAboveCamera = 3;

    private int deepestGeneratedDepth;
    private int shallowestGeneratedDepth;

    private bool runActive;

    private void Start()
    {
        PrepareSurfaceMine();
    }

    private void Update()
    {
        if (!runActive)
            return;

        GenerateAheadIfNeeded();
        RemoveOldRows();
    }

    public void PrepareSurfaceMine()
    {
        runActive = false;

        ClearMine();

        // Surface preview stays completely solid.
        // No TNT is generated here.
        for (int depth = 0;
             depth < startingRows;
             depth++)
        {
            for (int column = 0;
                 column < mineGrid.PlayableColumns;
                 column++)
            {
                Vector2Int position =
                    new Vector2Int(
                        column,
                        depth
                    );

                PlaceBackground(
                    position
                );

                PlaceSolidDirt(
                    position
                );
            }
        }

        shallowestGeneratedDepth = 0;

        deepestGeneratedDepth =
            startingRows - 1;
    }

    public void StartNewRun()
    {
        runActive = false;

        ClearMine();

        GenerateMineSection(
            0,
            startingRows - 1,
            true
        );

        shallowestGeneratedDepth = 0;

        deepestGeneratedDepth =
            startingRows - 1;

        runActive = true;
    }

    private void GenerateAheadIfNeeded()
    {
        if (player == null)
            return;

        int playerDepth =
            player.GridPosition.y;

        int distanceToBottom =
            deepestGeneratedDepth -
            playerDepth;

        if (distanceToBottom >
            generateAheadDistance)
        {
            return;
        }

        int firstNewDepth =
            deepestGeneratedDepth + 1;

        int lastNewDepth =
            deepestGeneratedDepth +
            rowsPerGeneration;

        GenerateMineSection(
            firstNewDepth,
            lastNewDepth,
            false
        );

        deepestGeneratedDepth =
            lastNewDepth;
    }

    private void GenerateMineSection(
        int firstDepth,
        int lastDepth,
        bool createStartingOpening)
    {
        // First create all dirt/background.
        for (int depth = firstDepth;
             depth <= lastDepth;
             depth++)
        {
            for (int column = 0;
                 column < mineGrid.PlayableColumns;
                 column++)
            {
                Vector2Int position =
                    new Vector2Int(
                        column,
                        depth
                    );

                PlaceBackground(
                    position
                );

                PlaceSolidDirt(
                    position
                );
            }
        }

        GenerateEmptyTiles(
            firstDepth,
            lastDepth,
            createStartingOpening
        );

        GenerateHiddenTNT(
            firstDepth,
            lastDepth,
            createStartingOpening
        );

        if (createStartingOpening &&
            firstDepth <= 0 &&
            lastDepth >= 0)
        {
            Vector2Int startingPosition =
                new Vector2Int(
                    mineGrid.StartingColumn,
                    0
                );

            PlaceOpenTile(
                startingPosition
            );
        }
    }

    private void GenerateEmptyTiles(
        int firstDepth,
        int lastDepth,
        bool createStartingOpening)
    {
        int groupStartDepth =
            firstDepth;

        while (groupStartDepth <=
               lastDepth)
        {
            int groupEndDepth =
                Mathf.Min(
                    groupStartDepth +
                    rowsPerEmptyGroup - 1,
                    lastDepth
                );

            List<Vector2Int> candidates =
                new List<Vector2Int>();

            for (int depth =
                     groupStartDepth;
                 depth <= groupEndDepth;
                 depth++)
            {
                for (int column = 0;
                     column <
                     mineGrid.PlayableColumns;
                     column++)
                {
                    Vector2Int position =
                        new Vector2Int(
                            column,
                            depth
                        );

                    bool isPlayerStart =
                        createStartingOpening &&
                        depth == 0 &&
                        column ==
                        mineGrid.StartingColumn;

                    if (!isPlayerStart)
                    {
                        candidates.Add(
                            position
                        );
                    }
                }
            }

            Shuffle(
                candidates
            );

            int amount =
                Mathf.Min(
                    emptyTilesPerGroup,
                    candidates.Count
                );

            for (int i = 0;
                 i < amount;
                 i++)
            {
                PlaceOpenTile(
                    candidates[i]
                );
            }

            groupStartDepth =
                groupEndDepth + 1;
        }
    }

    private void GenerateHiddenTNT(
        int firstDepth,
        int lastDepth,
        bool createStartingOpening)
    {
        if (tntManager == null)
            return;

        int groupStartDepth =
            firstDepth;

        while (groupStartDepth <=
               lastDepth)
        {
            int groupEndDepth =
                Mathf.Min(
                    groupStartDepth +
                    rowsPerTNTGroup - 1,
                    lastDepth
                );

            List<Vector2Int> candidates =
                new List<Vector2Int>();

            for (int depth =
                     groupStartDepth;
                 depth <= groupEndDepth;
                 depth++)
            {
                for (int column = 0;
                     column <
                     mineGrid.PlayableColumns;
                     column++)
                {
                    Vector2Int position =
                        new Vector2Int(
                            column,
                            depth
                        );

                    bool isPlayerStart =
                        createStartingOpening &&
                        depth == 0 &&
                        column ==
                        mineGrid.StartingColumn;

                    if (isPlayerStart)
                        continue;

                    Vector3Int cell =
                        mineGrid.GridToTilemapCell(
                            position
                        );

                    TileBase tile =
                        dirtTilemap.GetTile(
                            cell
                        );

                    // TNT can ONLY hide inside
                    // an actual solid dirt cell.
                    if (tile == dirtTile)
                    {
                        candidates.Add(
                            position
                        );
                    }
                }
            }

            Shuffle(
                candidates
            );

            int amount =
                Mathf.Min(
                    tntPerGroup,
                    candidates.Count
                );

            for (int i = 0;
                 i < amount;
                 i++)
            {
                tntManager.RegisterHiddenTNT(
                    candidates[i]
                );
            }

            groupStartDepth =
                groupEndDepth + 1;
        }
    }

    private void Shuffle(
        List<Vector2Int> positions)
    {
        for (int i = 0;
             i < positions.Count;
             i++)
        {
            int randomIndex =
                Random.Range(
                    i,
                    positions.Count
                );

            Vector2Int temp =
                positions[i];

            positions[i] =
                positions[randomIndex];

            positions[randomIndex] =
                temp;
        }
    }

    private void PlaceBackground(
        Vector2Int position)
    {
        if (backgroundTilemap == null ||
            backgroundTile == null)
        {
            return;
        }

        Vector3Int cell =
            mineGrid.GridToTilemapCell(
                position
            );

        backgroundTilemap.SetTile(
            cell,
            backgroundTile
        );
    }

    private void PlaceSolidDirt(
        Vector2Int position)
    {
        if (dirtTilemap == null ||
            dirtTile == null)
        {
            return;
        }

        Vector3Int cell =
            mineGrid.GridToTilemapCell(
                position
            );

        dirtTilemap.SetTile(
            cell,
            dirtTile
        );
    }

    private void PlaceOpenTile(
        Vector2Int position)
    {
        if (dirtTilemap == null)
            return;

        Vector3Int cell =
            mineGrid.GridToTilemapCell(
                position
            );

        if (brokenDirtTile != null)
        {
            dirtTilemap.SetTile(
                cell,
                brokenDirtTile
            );
        }
        else
        {
            dirtTilemap.SetTile(
                cell,
                null
            );
        }

        if (tntManager != null)
        {
            tntManager.RemoveTNTAt(
                position
            );
        }
    }

    private void ClearMine()
    {
        if (dirtTilemap != null)
        {
            dirtTilemap.ClearAllTiles();
        }

        if (backgroundTilemap != null)
        {
            backgroundTilemap.ClearAllTiles();
        }

        if (tntManager != null)
        {
            tntManager.ClearAllTNT();
        }
    }

    private void RemoveOldRows()
    {
        if (mineCamera == null)
            return;

        float cameraTop =
            mineCamera.transform.position.y +
            mineCamera.orthographicSize;

        float depthAtCameraTop =
            (mineGrid.transform.position.y -
             cameraTop) /
            mineGrid.TileSize;

        int topVisibleDepth =
            Mathf.FloorToInt(
                depthAtCameraTop
            );

        int firstDepthToKeep =
            topVisibleDepth -
            rowsToKeepAboveCamera;

        if (firstDepthToKeep <=
            shallowestGeneratedDepth)
        {
            return;
        }

        int lastDepthToRemove =
            Mathf.Min(
                firstDepthToKeep - 1,
                deepestGeneratedDepth
            );

        for (int depth =
                 shallowestGeneratedDepth;
             depth <= lastDepthToRemove;
             depth++)
        {
            RemoveRow(
                depth
            );
        }

        shallowestGeneratedDepth =
            lastDepthToRemove + 1;
    }

    private void RemoveRow(
        int depth)
    {
        for (int column = 0;
             column <
             mineGrid.PlayableColumns;
             column++)
        {
            Vector2Int position =
                new Vector2Int(
                    column,
                    depth
                );

            Vector3Int cell =
                mineGrid.GridToTilemapCell(
                    position
                );

            if (dirtTilemap != null)
            {
                dirtTilemap.SetTile(
                    cell,
                    null
                );
            }

            if (backgroundTilemap != null)
            {
                backgroundTilemap.SetTile(
                    cell,
                    null
                );
            }

            if (tntManager != null)
            {
                tntManager.RemoveTNTAt(
                    position
                );
            }
        }
    }
}