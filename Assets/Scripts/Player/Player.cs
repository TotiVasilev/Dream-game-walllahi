using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

public class PlayerGridMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MineGrid grid;
    [SerializeField] private Tilemap dirtTilemap;
    [SerializeField] private Camera mineCamera;
    [SerializeField] private MineTNTManager tntManager;
    [SerializeField] private CoinCollectEffect coinCollectEffect;

    [Header("Dirt Tiles")]
    [SerializeField] private TileBase dirtNormal;
    [SerializeField] private TileBase dirtCrack1;
    [SerializeField] private TileBase dirtCrack2;
    [SerializeField] private TileBase dirtBroken;

    [Header("Swipe")]
    [Min(1f)]
    [SerializeField] private float minimumSwipeDistance = 50f;

    [Header("Movement")]
    [Min(0f)]
    [SerializeField] private float moveDuration = 0.1f;

    [Header("Gravity")]
    [Min(0f)]
    [SerializeField] private float fallMoveDuration = 0.08f;

    [Header("Drilling")]
    [Min(0f)]
    [SerializeField] private float drillDuration = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float crack1Time = 0.3f;

    [Range(0f, 1f)]
    [SerializeField] private float crack2Time = 0.6f;

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
        ResetTileTransform(
            tileCell
        );

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
                    if (dirtCrack2 != null)
                    {
                        dirtTilemap.SetTile(
                            tileCell,
                            dirtCrack2
                        );
                    }

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
        return tile != null &&
               tile != dirtBroken;
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
                tileBelow == dirtBroken;

            if (!isOpenBelow)
            {
                yield break;
            }

            yield return StartCoroutine(
                MovePlayer(
                    positionBelow,
                    fallMoveDuration
                )
            );
        }
    }

    private IEnumerator DrillTileAnimation(
        Vector3Int tileCell)
    {
        if (drillDuration <= 0f)
        {
            yield break;
        }

        if (dirtNormal != null)
        {
            dirtTilemap.SetTile(
                tileCell,
                dirtNormal
            );
        }

        float elapsed = 0f;

        bool showedCrack1 = false;
        bool showedCrack2 = false;

        while (elapsed <
               drillDuration)
        {
            elapsed +=
                Time.deltaTime;

            float progress =
                Mathf.Clamp01(
                    elapsed /
                    drillDuration
                );

            if (!showedCrack1 &&
                progress >= crack1Time)
            {
                showedCrack1 = true;

                if (dirtCrack1 != null)
                {
                    dirtTilemap.SetTile(
                        tileCell,
                        dirtCrack1
                    );
                }
            }

            if (!showedCrack2 &&
                progress >= crack2Time)
            {
                showedCrack2 = true;

                if (dirtCrack2 != null)
                {
                    dirtTilemap.SetTile(
                        tileCell,
                        dirtCrack2
                    );
                }
            }

            ApplyTileShake(
                tileCell,
                elapsed
            );

            yield return null;
        }

        ResetTileTransform(
            tileCell
        );
    }

    private void BreakTileImmediately(
        Vector3Int tileCell)
    {
        ResetTileTransform(
            tileCell
        );

        if (dirtBroken != null)
        {
            dirtTilemap.SetTile(
                tileCell,
                dirtBroken
            );
        }

        ResetTileTransform(
            tileCell
        );

        if (coinCollectEffect != null)
        {
            Vector2Int brokenPosition =
                TilemapCellToGridPosition(
                    tileCell
                );

            coinCollectEffect.SpawnCoin(
                brokenPosition
            );
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
        float elapsed)
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

        dirtTilemap.SetTransformMatrix(
            tileCell,
            shakeMatrix
        );
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