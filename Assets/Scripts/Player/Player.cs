using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

public class PlayerGridMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MineGrid grid;
    [SerializeField] private LevelHolder levelHolder;
    [SerializeField] private Tilemap dirtTilemap;
    [Tooltip("Separate Tilemap for transparent cracks, aligned with Dirt and sorted above it.")]
    [SerializeField] private Tilemap crackOverlayTilemap;
    [SerializeField] private Camera mineCamera;
    [SerializeField] private MineTNTManager tntManager;
    [SerializeField] private CoinCollectEffect coinCollectEffect;
    [Header("Swipe")]
    [Min(1f)]
    [SerializeField] private float minimumSwipeDistance = 50f;
    [Header("Movement")]
    [Min(0f)]
    [SerializeField] private float moveDuration = 0.1f;
    // Fall duration, drill duration, and any number of crack stages
    // are now configured per level in LevelHolder.
    [Header("Drilling Shake")]
    [Min(0f)]
    [SerializeField] private float shakeAmount = 0.04f;
    [Min(0f)]
    [SerializeField] private float shakeSpeed = 30f;
    [Header("Power Drill")]
    [Min(1)]
    [SerializeField] private int drillsNeededToCharge = 6;
    [Min(1)]
    [SerializeField] private int powerDrillLength = 3;
    [Tooltip("Total time for the complete power-drill dash.")]
    [Min(0.01f)]
    [SerializeField] private float powerDashDuration = 0.22f;
    [Tooltip(
        "How far through each tile the player gets before that tile breaks."
    )]
    [Range(0.05f, 0.95f)]
    [SerializeField] private float powerBreakPoint = 0.45f;
    [Header("Power Drill Impact")]
    [Min(0f)]
    [SerializeField] private float powerShakeAmount = 0.045f;
    [Min(0f)]
    [SerializeField] private float powerShakeSpeed = 80f;
    private Vector2Int gridPosition;
    private Vector2 swipeStartPosition;
    private bool trackingSwipe;
    private bool isBusy;
    private bool inputEnabled;
    private int normalDrillsCompleted;
    private bool powerDrillArmed;
    private bool gravityCheckRequested;
    public Vector2Int GridPosition =>
        gridPosition;
    public int NormalDrillsCompleted =>
        normalDrillsCompleted;
    public int DrillsNeededToCharge =>
        drillsNeededToCharge;
    public bool IsPowerDrillReady =>
        normalDrillsCompleted >=
        drillsNeededToCharge;
    public bool IsPowerDrillArmed =>
        powerDrillArmed;
    private void Start()
    {
        if (levelHolder == null)
            Debug.LogError("PlayerGridMovement: Assign LevelHolder in the Inspector.", this);
        if (crackOverlayTilemap == null || crackOverlayTilemap == dirtTilemap)
            Debug.LogWarning("PlayerGridMovement: Assign a separate Crack Overlay Tilemap, above Dirt, to display damage cracks.", this);

        gridPosition =
            new Vector2Int(
                grid.StartingColumn,
                0
            );
        inputEnabled = false;
        ResetPowerDrill();
    }

    private void Update()
    {
        if (gravityCheckRequested &&
            !isBusy &&
            inputEnabled)
        {
            gravityCheckRequested = false;
            StartCoroutine(
                HandleExternalGravity()
            );
            return;
        }
        if (!inputEnabled)
            return;
        if (isBusy)
            return;
        ReadTouchInput();
        ReadMouseInput();
    }

    public void SetInputEnabled(
        bool enabled)
    {
        inputEnabled = enabled;
        if (!enabled)
        {
            trackingSwipe = false;
        }
    }

    public void StartMiningRun()
    {
        StopAllCoroutines();
        isBusy = false;
        trackingSwipe = false;
        gravityCheckRequested = false;
        gridPosition =
            new Vector2Int(
                grid.StartingColumn,
                0
            );
        transform.position =
            grid.GridToWorld(
                gridPosition
            );
        ResetPowerDrill();
    }

    public void ActivatePowerDrill()
    {
        if (!inputEnabled)
            return;
        if (isBusy)
            return;
        if (!IsPowerDrillReady)
            return;
        if (powerDrillArmed)
            return;
        powerDrillArmed = true;
    }

    public void CheckGravityAfterWorldChange()
    {
        gravityCheckRequested = true;
    }

    private IEnumerator HandleExternalGravity()
    {
        isBusy = true;
        yield return StartCoroutine(
            ApplyGravity()
        );
        isBusy = false;
    }

    private void ResetPowerDrill()
    {
        normalDrillsCompleted = 0;
        powerDrillArmed = false;
    }

    private void ReadTouchInput()
    {
        if (Touchscreen.current == null)
            return;
        var touch =
            Touchscreen.current.primaryTouch;
        if (touch.press.wasPressedThisFrame)
        {
            swipeStartPosition =
                touch.position.ReadValue();
            trackingSwipe = true;
        }
        if (touch.press.wasReleasedThisFrame &&
            trackingSwipe)
        {
            Vector2 swipeEndPosition =
                touch.position.ReadValue();
            ProcessSwipe(
                swipeEndPosition -
                swipeStartPosition
            );
            trackingSwipe = false;
        }
    }

    private void ReadMouseInput()
    {
        if (Mouse.current == null)
            return;
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            swipeStartPosition =
                Mouse.current.position.ReadValue();
            trackingSwipe = true;
        }
        if (Mouse.current.leftButton.wasReleasedThisFrame &&
            trackingSwipe)
        {
            Vector2 swipeEndPosition =
                Mouse.current.position.ReadValue();
            ProcessSwipe(
                swipeEndPosition -
                swipeStartPosition
            );
            trackingSwipe = false;
        }
    }

    private void ProcessSwipe(
        Vector2 swipe)
    {
        if (swipe.magnitude <
            minimumSwipeDistance)
        {
            return;
        }
        Vector2Int direction;
        if (Mathf.Abs(swipe.x) >
            Mathf.Abs(swipe.y))
        {
            direction =
                swipe.x > 0
                ? Vector2Int.right
                : Vector2Int.left;
        }
        else
        {
            direction =
                swipe.y > 0
                ? Vector2Int.up
                : Vector2Int.down;
        }
        TryAction(
            direction
        );
    }

    private void TryAction(
        Vector2Int direction)
    {
        if (powerDrillArmed)
        {
            TryPowerDrill(
                direction
            );
            return;
        }
        if (direction ==
            Vector2Int.up)
        {
            TryMineAbove();
            return;
        }
        TryNormalMove(
            direction
        );
    }

    private void TryNormalMove(
        Vector2Int direction)
    {
        Vector2Int targetPosition =
            GetGridPositionInDirection(
                gridPosition,
                direction
            );
        if (!grid.IsValidPosition(
                targetPosition))
        {
            return;
        }
        if (!IsInsideCameraTopBoundary(
                targetPosition))
        {
            return;
        }
        StartCoroutine(
            HandleNormalMove(
                targetPosition
            )
        );
    }

    private void TryMineAbove()
    {
        Vector2Int targetPosition =
            GetGridPositionInDirection(
                gridPosition,
                Vector2Int.up
            );
        if (!grid.IsValidPosition(
                targetPosition))
        {
            return;
        }
        if (!IsInsideCameraTopBoundary(
                targetPosition))
        {
            return;
        }
        Vector3Int tileCell =
            grid.GridToTilemapCell(
                targetPosition
            );
        TileBase targetTile =
            dirtTilemap.GetTile(
                tileCell
            );
        if (!IsSolidDirt(
                targetTile))
        {
            return;
        }
        StartCoroutine(
            MineAbove(
                targetPosition,
                tileCell
            )
        );
    }

    private void TryPowerDrill(
        Vector2Int direction)
    {
        Vector2Int firstPosition =
            GetGridPositionInDirection(
                gridPosition,
                direction
            );
        if (!grid.IsValidPosition(
                firstPosition))
        {
            return;
        }
        if (direction ==
                Vector2Int.up &&
            !IsInsideCameraTopBoundary(
                firstPosition))
        {
            return;
        }
        StartCoroutine(
            HandlePowerDrill(
                direction
            )
        );
    }

    private IEnumerator HandleNormalMove(
        Vector2Int targetPosition)
    {
        isBusy = true;
        Vector3Int tileCell =
            grid.GridToTilemapCell(
                targetPosition
            );
        TileBase targetTile =
            dirtTilemap.GetTile(
                tileCell
            );
        if (IsSolidDirt(
                targetTile))
        {
            bool containsTNT =
                HasHiddenTNT(
                    targetPosition
                );
            yield return StartCoroutine(
                DrillTileAnimation(
                    tileCell
                )
            );
            if (containsTNT)
            {
                RevealTNT(
                    targetPosition,
                    tileCell
                );
                isBusy = false;
                yield break;
            }
            BreakTileImmediately(
                tileCell
            );
            AddNormalDrillCharge();
        }
        yield return StartCoroutine(
            MovePlayer(
                targetPosition,
                moveDuration
            )
        );
        yield return StartCoroutine(
            ApplyGravity()
        );
        isBusy = false;
    }

    private IEnumerator MineAbove(
        Vector2Int targetPosition,
        Vector3Int tileCell)
    {
        isBusy = true;
        bool containsTNT =
            HasHiddenTNT(
                targetPosition
            );
        yield return StartCoroutine(
            DrillTileAnimation(
                tileCell
            )
        );
        if (containsTNT)
        {
            RevealTNT(
                targetPosition,
                tileCell
            );
        }
        else
        {
            BreakTileImmediately(
                tileCell
            );
            AddNormalDrillCharge();
        }
        yield return StartCoroutine(
            ApplyGravity()
        );
        isBusy = false;
    }

    private bool HasHiddenTNT(
        Vector2Int position)
    {
        if (tntManager == null)
            return false;
        return tntManager.HasHiddenTNT(
            position
        );
    }

    private void RevealTNT(
        Vector2Int position,
        Vector3Int tileCell)
    {
        ClearCrackOverlay(tileCell);
        if (tntManager != null)
        {
            tntManager.RevealTNT(
                position
            );
        }
    }

    private IEnumerator HandlePowerDrill(
        Vector2Int direction)
    {
        isBusy = true;
        ResetPowerDrill();
        yield return StartCoroutine(
            PowerDash(
                direction
            )
        );
        yield return StartCoroutine(
            ApplyGravity()
        );
        isBusy = false;
    }

    private IEnumerator PowerDash(
        Vector2Int direction)
    {
        Vector2Int startGridPosition =
            gridPosition;
        int validDistance = 0;
        for (int distance = 1;
             distance <= powerDrillLength;
             distance++)
        {
            Vector2Int testPosition =
                GetGridPositionInDirection(
                    startGridPosition,
                    direction,
                    distance
                );
            if (!grid.IsValidPosition(
                    testPosition))
            {
                break;
            }
            if (direction ==
                    Vector2Int.up &&
                !IsInsideCameraTopBoundary(
                    testPosition))
            {
                break;
            }
            validDistance =
                distance;
        }
        if (validDistance <= 0)
        {
            yield break;
        }
        Vector2Int finalGridPosition =
            GetGridPositionInDirection(
                startGridPosition,
                direction,
                validDistance
            );
        Vector3 startWorldPosition =
            grid.GridToWorld(
                startGridPosition
            );
        Vector3 finalWorldPosition =
            grid.GridToWorld(
                finalGridPosition
            );
        bool playerShouldMove =
            direction !=
            Vector2Int.up;
        int nextTileToBreak = 1;
        float duration =
            Mathf.Max(
                0.01f,
                powerDashDuration
            );
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed +=
                Time.deltaTime;
            float progress =
                Mathf.Clamp01(
                    elapsed /
                    duration
                );
            if (playerShouldMove)
            {
                transform.position =
                    Vector3.Lerp(
                        startWorldPosition,
                        finalWorldPosition,
                        progress
                    );
            }
            float travelledTiles =
                progress *
                validDistance;
            while (nextTileToBreak <=
                   validDistance)
            {
                float breakDistance =
                    (nextTileToBreak - 1) +
                    powerBreakPoint;
                if (travelledTiles <
                    breakDistance)
                {
                    break;
                }
                Vector2Int tilePosition =
                    GetGridPositionInDirection(
                        startGridPosition,
                        direction,
                        nextTileToBreak
                    );
                Vector3Int tileCell =
                    grid.GridToTilemapCell(
                        tilePosition
                    );
                TileBase targetTile =
                    dirtTilemap.GetTile(
                        tileCell
                    );
                if (IsSolidDirt(
                        targetTile))
                {
                    // Power drilling breaks immediately. Leave the underlying
                    // dirt untouched until BreakTileImmediately(), which also
                    // clears any transparent crack overlay.
                    ApplyPowerTileShake(
                        tileCell,
                        elapsed
                    );
                    BreakTileImmediately(
                        tileCell
                    );
                }
                nextTileToBreak++;
            }
            yield return null;
        }
        while (nextTileToBreak <=
               validDistance)
        {
            Vector2Int tilePosition =
                GetGridPositionInDirection(
                    startGridPosition,
                    direction,
                    nextTileToBreak
                );
            Vector3Int tileCell =
                grid.GridToTilemapCell(
                    tilePosition
                );
            TileBase targetTile =
                dirtTilemap.GetTile(
                    tileCell
                );
            if (IsSolidDirt(
                    targetTile))
            {
                BreakTileImmediately(
                    tileCell
                );
            }
            nextTileToBreak++;
        }
        if (playerShouldMove)
        {
            transform.position =
                finalWorldPosition;
            gridPosition =
                finalGridPosition;
        }
    }

    private void AddNormalDrillCharge()
    {
        if (IsPowerDrillReady)
            return;
        normalDrillsCompleted++;
        normalDrillsCompleted =
            Mathf.Min(
                normalDrillsCompleted,
                drillsNeededToCharge
            );
    }

    private bool IsSolidDirt(
        TileBase tile)
    {
        // Preserve the original behaviour: anything on the dirt Tilemap
        // is solid unless it is one of this level library's broken tiles.
        return tile != null &&
               (levelHolder == null || !levelHolder.IsBrokenTile(tile));
    }

    private Vector2Int GetGridPositionInDirection(
        Vector2Int startPosition,
        Vector2Int direction,
        int distance = 1)
    {
        return new Vector2Int(
            startPosition.x +
            direction.x *
            distance,
            startPosition.y -
            direction.y *
            distance
        );
    }

    private bool IsInsideCameraTopBoundary(
        Vector2Int targetPosition)
    {
        if (mineCamera == null)
            return true;
        Vector3 targetWorldPosition =
            grid.GridToWorld(
                targetPosition
            );
        float cameraTop =
            mineCamera.transform.position.y +
            mineCamera.orthographicSize;
        float targetTileTop =
            targetWorldPosition.y +
            grid.TileSize * 0.5f;
        return targetTileTop <=
               cameraTop;
    }

    private IEnumerator ApplyGravity()
    {
        while (true)
        {
            Vector2Int positionBelow =
                new Vector2Int(
                    gridPosition.x,
                    gridPosition.y + 1
                );
            if (!grid.IsValidPosition(
                    positionBelow))
            {
                yield break;
            }
            Vector3Int cellBelow =
                grid.GridToTilemapCell(
                    positionBelow
                );
            TileBase tileBelow =
                dirtTilemap.GetTile(
                    cellBelow
                );
            bool isOpenBelow =
                tileBelow == null ||
                (levelHolder != null && levelHolder.IsBrokenTile(tileBelow));
            if (!isOpenBelow)
            {
                yield break;
            }
            yield return StartCoroutine(
                MovePlayer(
                    positionBelow,
                    levelHolder != null ? levelHolder.ActiveFallMoveDuration : 0.08f
                )
            );
        }
    }

    private IEnumerator DrillTileAnimation(Vector3Int tileCell)
    {
        float duration = levelHolder != null ? levelHolder.ActiveDrillDuration : 1f;
        ClearCrackOverlay(tileCell);

        if (duration <= 0f)
            yield break;

        // The original randomly chosen/rotated/flipped dirt sprite remains
        // on the Dirt Tilemap throughout drilling.
        Matrix4x4 originalTileTransform = dirtTilemap.GetTransformMatrix(tileCell);
        float elapsed = 0f;
        int visibleStage = -1;
        int stageCount = levelHolder != null ? levelHolder.ActiveCrackStageCount : 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);

            // Pick the LATEST applicable stage by threshold (not by array order).
            // This also behaves correctly if more than one stage is crossed in
            // the same frame, or designers enter stages out of order.
            int nextStage = -1;
            float latestTime = -1f;
            for (int i = 0; i < stageCount; i++)
            {
                float time = levelHolder.GetCrackStageNormalizedTime(i);
                if (progress >= time && (time > latestTime || (time == latestTime && i > nextStage)))
                {
                    nextStage = i;
                    latestTime = time;
                }
            }

            if (nextStage != visibleStage)
            {
                visibleStage = nextStage;
                TileBase crack = nextStage >= 0
                    ? levelHolder.GetRandomCrackStageTile(nextStage)
                    : null;

                SetCrackOverlay(tileCell, crack, originalTileTransform);
            }

            ApplyTileShake(tileCell, elapsed, originalTileTransform);
            yield return null;
        }

        // Preserve the dirt's original random rotation/flip if the drilling
        // revealed TNT rather than destroying the block.
        dirtTilemap.SetTransformMatrix(tileCell, originalTileTransform);
        if (IsCrackOverlayAvailable() && crackOverlayTilemap.HasTile(tileCell))
            crackOverlayTilemap.SetTransformMatrix(tileCell, originalTileTransform);
    }

    private bool IsCrackOverlayAvailable()
    {
        return crackOverlayTilemap != null && crackOverlayTilemap != dirtTilemap;
    }

    private void SetCrackOverlay(Vector3Int tileCell, TileBase crackTile, Matrix4x4 dirtTransform)
    {
        if (!IsCrackOverlayAvailable())
            return;

        crackOverlayTilemap.SetTile(tileCell, crackTile);
        if (crackTile != null)
        {
            crackOverlayTilemap.SetTileFlags(tileCell, TileFlags.None);
            // Match the precise transform of the dirt sprite underneath.
            crackOverlayTilemap.SetTransformMatrix(tileCell, dirtTransform);
        }
    }

    private void ClearCrackOverlay(Vector3Int tileCell)
    {
        if (IsCrackOverlayAvailable())
            crackOverlayTilemap.SetTile(tileCell, null);
    }

    private void BreakTileImmediately(Vector3Int tileCell)
    {
        ClearCrackOverlay(tileCell);

        TileBase brokenTile = levelHolder != null
            ? levelHolder.GetRandomBrokenTile()
            : null;

        // The original dirt is replaced only AFTER drilling finishes.
        // Broken tiles are passable (runtime collider: None).
        dirtTilemap.SetTile(tileCell, brokenTile);
        if (brokenTile != null)
            dirtTilemap.SetTileFlags(tileCell, TileFlags.None);
        ResetTileTransform(tileCell);

        if (coinCollectEffect != null)
        {
            Vector2Int brokenPosition = TilemapCellToGridPosition(tileCell);
            coinCollectEffect.SpawnCoin(brokenPosition);
        }
    }

    private Vector2Int TilemapCellToGridPosition(
        Vector3Int tileCell)
    {
        return new Vector2Int(
            tileCell.x,
            -tileCell.y - 1
        );
    }

    private void ApplyTileShake(
        Vector3Int tileCell,
        float elapsed,
        Matrix4x4 originalTileTransform)
    {
        if (shakeAmount <= 0f ||
            shakeSpeed <= 0f)
        {
            return;
        }
        float xOffset =
            Mathf.Sin(
                elapsed *
                shakeSpeed
            ) * shakeAmount;
        float yOffset =
            Mathf.Sin(
                elapsed *
                shakeSpeed *
                1.37f
            ) * shakeAmount;
        Matrix4x4 shakeMatrix =
            Matrix4x4.TRS(
                new Vector3(
                    xOffset,
                    yOffset,
                    0f
                ),
                Quaternion.identity,
                Vector3.one
            );
        Matrix4x4 animatedTransform = shakeMatrix * originalTileTransform;
        dirtTilemap.SetTransformMatrix(tileCell, animatedTransform);
        // Shake BOTH layers together so transparent cracks never slide off dirt.
        if (IsCrackOverlayAvailable() && crackOverlayTilemap.HasTile(tileCell))
            crackOverlayTilemap.SetTransformMatrix(tileCell, animatedTransform);
    }

    private void ApplyPowerTileShake(
        Vector3Int tileCell,
        float elapsed)
    {
        if (powerShakeAmount <= 0f ||
            powerShakeSpeed <= 0f)
        {
            return;
        }
        float xOffset =
            Mathf.Sin(
                elapsed *
                powerShakeSpeed
            ) * powerShakeAmount;
        float yOffset =
            Mathf.Sin(
                elapsed *
                powerShakeSpeed *
                1.41f
            ) * powerShakeAmount;
        Matrix4x4 shakeMatrix =
            Matrix4x4.TRS(
                new Vector3(
                    xOffset,
                    yOffset,
                    0f
                ),
                Quaternion.identity,
                Vector3.one
            );
        dirtTilemap.SetTransformMatrix(
            tileCell,
            shakeMatrix
        );
    }

    private void ResetTileTransform(
        Vector3Int tileCell)
    {
        if (dirtTilemap == null)
            return;
        dirtTilemap.SetTransformMatrix(
            tileCell,
            Matrix4x4.identity
        );
    }

    private IEnumerator MovePlayer(
        Vector2Int targetPosition,
        float duration)
    {
        Vector3 startPosition =
            transform.position;
        Vector3 targetWorldPosition =
            grid.GridToWorld(
                targetPosition
            );
        if (duration <= 0f)
        {
            transform.position =
                targetWorldPosition;
            gridPosition =
                targetPosition;
            yield break;
        }
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed +=
                Time.deltaTime;
            float t =
                Mathf.Clamp01(
                    elapsed /
                    duration
                );
            transform.position =
                Vector3.Lerp(
                    startPosition,
                    targetWorldPosition,
                    t
                );
            yield return null;
        }
        transform.position =
            targetWorldPosition;
        gridPosition =
            targetPosition;
    }
}
