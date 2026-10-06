using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MineTNTManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MineGrid mineGrid;
    [SerializeField] private Tilemap dirtTilemap;
    [SerializeField] private PlayerGridMovement player;
    [SerializeField] private CoinCollectEffect coinCollectEffect;

    [SerializeField] private TileBase dirtNormal;
    [SerializeField] private TileBase dirtBroken;

    [Header("TNT Tiles")]
    [SerializeField] private TileBase tntTile;

    [Tooltip(
        "A bright/white tile shown briefly while the TNT flashes."
    )]
    [SerializeField] private TileBase tntFlashTile;

    [Header("TNT Fuse")]
    [Min(0f)]
    [SerializeField] private float fuseDuration = 1.5f;

    [Tooltip(
        "Flash speed when TNT is first revealed."
    )]
    [Min(0.01f)]
    [SerializeField] private float startingFlashInterval = 0.18f;

    [Tooltip(
        "Flash speed immediately before the explosion."
    )]
    [Min(0.01f)]
    [SerializeField] private float endingFlashInterval = 0.05f;

    [Header("Explosion")]
    [Min(0)]
    [SerializeField] private int straightExplosionDistance = 2;

    [Min(0)]
    [SerializeField] private int diagonalExplosionDistance = 1;

    [Header("Debug")]
    [SerializeField] private bool showHiddenTNT = true;

    [SerializeField] private Color hiddenTNTColor =
        new Color(
            1f,
            0f,
            0f,
            0.65f
        );

    [Range(0.1f, 1f)]
    [SerializeField] private float debugMarkerSize = 0.65f;

    private readonly HashSet<Vector2Int> hiddenTNT =
        new HashSet<Vector2Int>();

    private readonly HashSet<Vector2Int> activeTNT =
        new HashSet<Vector2Int>();

    public bool HasHiddenTNT(
        Vector2Int position)
    {
        return hiddenTNT.Contains(
            position
        );
    }

    public bool IsActiveTNT(
        Vector2Int position)
    {
        return activeTNT.Contains(
            position
        );
    }

    public void ClearAllTNT()
    {
        StopAllCoroutines();

        hiddenTNT.Clear();
        activeTNT.Clear();
    }

    public void RegisterHiddenTNT(
        Vector2Int position)
    {
        if (mineGrid == null)
            return;

        if (!mineGrid.IsValidPosition(
                position))
        {
            return;
        }

        hiddenTNT.Add(
            position
        );
    }

    public void RemoveTNTAt(
        Vector2Int position)
    {
        hiddenTNT.Remove(
            position
        );

        activeTNT.Remove(
            position
        );
    }

    public void RevealTNT(
        Vector2Int position)
    {
        if (!hiddenTNT.Contains(
                position))
        {
            return;
        }

        hiddenTNT.Remove(
            position
        );

        activeTNT.Add(
            position
        );

        Vector3Int cell =
            mineGrid.GridToTilemapCell(
                position
            );

        dirtTilemap.SetTransformMatrix(
            cell,
            Matrix4x4.identity
        );

        dirtTilemap.SetColor(
            cell,
            Color.white
        );

        if (tntTile != null)
        {
            dirtTilemap.SetTile(
                cell,
                tntTile
            );
        }

        StartCoroutine(
            TNTFuse(
                position
            )
        );
    }

    private IEnumerator TNTFuse(
        Vector2Int position)
    {
        Vector3Int cell =
            mineGrid.GridToTilemapCell(
                position
            );

        float elapsed = 0f;
        bool showingFlash = false;

        while (elapsed <
               fuseDuration)
        {
            float progress =
                fuseDuration > 0f
                    ? Mathf.Clamp01(
                        elapsed /
                        fuseDuration
                    )
                    : 1f;

            float interval =
                Mathf.Lerp(
                    startingFlashInterval,
                    endingFlashInterval,
                    progress
                );

            showingFlash =
                !showingFlash;

            if (showingFlash &&
                tntFlashTile != null)
            {
                dirtTilemap.SetTile(
                    cell,
                    tntFlashTile
                );
            }
            else if (tntTile != null)
            {
                dirtTilemap.SetTile(
                    cell,
                    tntTile
                );
            }

            float remainingTime =
                fuseDuration -
                elapsed;

            float waitTime =
                Mathf.Min(
                    interval,
                    remainingTime
                );

            if (waitTime <= 0f)
                break;

            yield return new WaitForSeconds(
                waitTime
            );

            elapsed +=
                waitTime;
        }

        if (tntTile != null)
        {
            dirtTilemap.SetTile(
                cell,
                tntTile
            );
        }

        Explode(
            position
        );
    }

    private void Explode(
        Vector2Int center)
    {
        activeTNT.Remove(
            center
        );

        BreakExplosionCell(
            center
        );

        BreakLine(
            center,
            Vector2Int.up,
            straightExplosionDistance
        );

        BreakLine(
            center,
            Vector2Int.down,
            straightExplosionDistance
        );

        BreakLine(
            center,
            Vector2Int.left,
            straightExplosionDistance
        );

        BreakLine(
            center,
            Vector2Int.right,
            straightExplosionDistance
        );

        BreakLine(
            center,
            new Vector2Int(
                1,
                1
            ),
            diagonalExplosionDistance
        );

        BreakLine(
            center,
            new Vector2Int(
                -1,
                1
            ),
            diagonalExplosionDistance
        );

        BreakLine(
            center,
            new Vector2Int(
                1,
                -1
            ),
            diagonalExplosionDistance
        );

        BreakLine(
            center,
            new Vector2Int(
                -1,
                -1
            ),
            diagonalExplosionDistance
        );

        if (player != null)
        {
            player.CheckGravityAfterWorldChange();
        }
    }

    private void BreakLine(
        Vector2Int center,
        Vector2Int direction,
        int distance)
    {
        for (int i = 1;
             i <= distance;
             i++)
        {
            Vector2Int position =
                center +
                direction * i;

            if (!mineGrid.IsValidPosition(
                    position))
            {
                break;
            }

            BreakExplosionCell(
                position
            );
        }
    }

    private void BreakExplosionCell(
        Vector2Int position)
    {
        Vector3Int cell =
            mineGrid.GridToTilemapCell(
                position
            );

        TileBase currentTile =
            dirtTilemap.GetTile(
                cell
            );

        /*
         * Already empty/broken cells don't
         * generate another coin.
         */
        if (currentTile == null ||
            currentTile == dirtBroken)
        {
            return;
        }

        hiddenTNT.Remove(
            position
        );

        activeTNT.Remove(
            position
        );

        dirtTilemap.SetTransformMatrix(
            cell,
            Matrix4x4.identity
        );

        dirtTilemap.SetColor(
            cell,
            Color.white
        );

        /*
         * Spawn the visual collectible from the
         * exact cell destroyed by the explosion.
         */
        if (coinCollectEffect != null)
        {
            coinCollectEffect.SpawnCoin(
                position
            );
        }

        if (dirtBroken != null)
        {
            dirtTilemap.SetTile(
                cell,
                dirtBroken
            );
        }
    }

    private void OnDrawGizmos()
    {
        if (!showHiddenTNT)
            return;

        if (mineGrid == null)
            return;

        Gizmos.color =
            hiddenTNTColor;

        foreach (Vector2Int position
                 in hiddenTNT)
        {
            Vector3 worldPosition =
                mineGrid.GridToWorld(
                    position
                );

            float size =
                mineGrid.TileSize *
                debugMarkerSize;

            Gizmos.DrawCube(
                worldPosition,
                new Vector3(
                    size,
                    size,
                    0.05f
                )
            );
        }
    }
}