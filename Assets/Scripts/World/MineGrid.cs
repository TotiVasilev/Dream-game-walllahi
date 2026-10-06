using UnityEngine;

public class MineGrid : MonoBehaviour
{
    [Header("Grid")]
    [Min(1)]
    [SerializeField] private int playableColumns = 7;

    [Min(0.01f)]
    [SerializeField] private float tileSize = 1f;

    [Header("Camera / Visibility")]
    [Min(1)]
    [SerializeField] private int visibleRowsBelowPlayer = 3;

    [Header("Unity Tilemap Grid")]
    [SerializeField] private Grid tilemapGrid;

    public int PlayableColumns => playableColumns;
    public float TileSize => tileSize;
    public int VisibleRowsBelowPlayer => visibleRowsBelowPlayer;
    public int StartingColumn => playableColumns / 2;

    public Vector3 GridToWorld(Vector2Int gridPosition)
    {
        float gridWidth = (playableColumns - 1) * tileSize;

        float x = (gridPosition.x * tileSize) - (gridWidth / 2f);
        float y = -(gridPosition.y * tileSize);

        return transform.position + new Vector3(x, y, 0f);
    }

    public bool IsValidPosition(Vector2Int gridPosition)
    {
        bool insideColumns =
            gridPosition.x >= 0 &&
            gridPosition.x < playableColumns;

        bool underground = gridPosition.y >= 0;

        return insideColumns && underground;
    }

    private void OnValidate()
    {
        AlignTilemapGrid();
    }

    private void AlignTilemapGrid()
    {
        if (tilemapGrid == null)
            return;

        tilemapGrid.cellSize = new Vector3(tileSize, tileSize, 0f);

        float leftEdge =
            transform.position.x -
            (playableColumns * tileSize / 2f);

        float topEdge =
            transform.position.y +
            (tileSize / 2f);

        tilemapGrid.transform.position =
            new Vector3(leftEdge, topEdge, 0f);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;

        int rowsToDraw = visibleRowsBelowPlayer + 1;

        for (int x = 0; x < playableColumns; x++)
        {
            for (int y = 0; y < rowsToDraw; y++)
            {
                Vector3 position =
                    GridToWorld(new Vector2Int(x, y));

                Gizmos.DrawWireCube(
                    position,
                    new Vector3(tileSize, tileSize, 0f)
                );
            }
        }
    }
    public Vector3Int GridToTilemapCell(Vector2Int gridPosition)
{
    return new Vector3Int(
        gridPosition.x,
        -gridPosition.y - 1,
        0
    );
}
}