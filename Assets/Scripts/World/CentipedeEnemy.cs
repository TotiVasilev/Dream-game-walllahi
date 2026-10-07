using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class CentipedeEnemy : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MineGrid mineGrid;
    [SerializeField] private Tilemap backgroundTilemap;

    [Header("Main Sprites")]
    [Tooltip("Original head sprite. Your artwork faces LEFT.")]
    [SerializeField] private Sprite headSprite;

    [Tooltip(
        "Original straight body sprite. " +
        "The DARK / HEAD end is on the LEFT."
    )]
    [SerializeField] private Sprite straightBodySprite;

    [Tooltip("Original tail sprite.")]
    [SerializeField] private Sprite tailSprite;

    [Header("Corner Sprites - Movement Direction")]
    [Tooltip("Moving RIGHT before the corner, then DOWN.")]
    [SerializeField] private Sprite rightToDown;

    [Tooltip("Moving RIGHT before the corner, then UP.")]
    [SerializeField] private Sprite rightToUp;

    [Tooltip("Moving LEFT before the corner, then DOWN.")]
    [SerializeField] private Sprite leftToDown;

    [Tooltip("Moving LEFT before the corner, then UP.")]
    [SerializeField] private Sprite leftToUp;

    [Tooltip("Moving DOWN before the corner, then RIGHT.")]
    [SerializeField] private Sprite downToRight;

    [Tooltip("Moving DOWN before the corner, then LEFT.")]
    [SerializeField] private Sprite downToLeft;

    [Tooltip("Moving UP before the corner, then RIGHT.")]
    [SerializeField] private Sprite upToRight;

    [Tooltip("Moving UP before the corner, then LEFT.")]
    [SerializeField] private Sprite upToLeft;

    [Header("Tail")]
    [Tooltip("Direction the original tail artwork faces.")]
    [SerializeField] private Direction tailDefaultDirection =
        Direction.Right;

    [Header("Centipede")]
    [Min(3)]
    [SerializeField] private int segmentCount = 5;

    [SerializeField] private Vector2Int startingHeadPosition =
        new Vector2Int(5, 8);

    [SerializeField] private Direction startingDirection =
        Direction.Right;

    [Header("Spawn")]
    [Min(0.01f)]
    [SerializeField] private float spawnCheckInterval = 0.1f;

    [Header("Movement")]
    [Min(0.01f)]
    [SerializeField] private float moveDuration = 0.18f;

    [Min(0f)]
    [SerializeField] private float movePause = 0.05f;

    [Header("Procedural Movement")]
    [Min(0f)]
    [SerializeField] private float forwardWeight = 0.6f;

    [Min(0f)]
    [SerializeField] private float leftTurnWeight = 0.2f;

    [Min(0f)]
    [SerializeField] private float rightTurnWeight = 0.2f;

    [Header("Rendering")]
    [SerializeField] private string sortingLayerName =
        "Default";

    [SerializeField] private int orderInLayer = 15;

    private readonly List<Segment> segments =
        new List<Segment>();

    private Direction currentDirection;
    private bool initialized;

    public enum Direction
    {
        Up,
        Right,
        Down,
        Left
    }

    private class Segment
    {
        public GameObject gameObject;
        public Transform transform;
        public SpriteRenderer renderer;

        public Vector2Int gridPosition;

        public Vector3 previousWorldPosition;
        public Vector3 targetWorldPosition;
    }

    private struct DirectionChoice
    {
        public Direction direction;
        public float weight;
    }

    private void Start()
    {
        StartCoroutine(
            WaitForMineAndInitialize()
        );
    }

    private IEnumerator WaitForMineAndInitialize()
    {
        if (mineGrid == null)
        {
            Debug.LogError(
                "CentipedeEnemy needs a MineGrid reference.",
                this
            );

            yield break;
        }

        if (backgroundTilemap == null)
        {
            Debug.LogError(
                "CentipedeEnemy needs the MineBackgroundTilemap reference.",
                this
            );

            yield break;
        }

        if (headSprite == null ||
            straightBodySprite == null ||
            tailSprite == null)
        {
            Debug.LogError(
                "CentipedeEnemy is missing its head, straight body, or tail sprite.",
                this
            );

            yield break;
        }

        if (!DoesStartingBodyFitInsideGrid())
        {
            Debug.LogError(
                "The centipede starting body is outside the mine. " +
                "Change Starting Head Position, Starting Direction, or Segment Count.",
                this
            );

            yield break;
        }

        while (!AreStartingCellsGenerated())
        {
            yield return new WaitForSeconds(
                spawnCheckInterval
            );
        }

        InitializeCentipede();

        if (initialized)
        {
            StartCoroutine(
                MovementLoop()
            );
        }
    }

    private bool DoesStartingBodyFitInsideGrid()
    {
        Vector2Int direction =
            DirectionToGridVector(
                startingDirection
            );

        for (int i = 0;
             i < segmentCount;
             i++)
        {
            Vector2Int position =
                startingHeadPosition -
                direction * i;

            if (!mineGrid.IsValidPosition(
                    position))
            {
                return false;
            }
        }

        return true;
    }

    private bool AreStartingCellsGenerated()
    {
        Vector2Int direction =
            DirectionToGridVector(
                startingDirection
            );

        for (int i = 0;
             i < segmentCount;
             i++)
        {
            Vector2Int position =
                startingHeadPosition -
                direction * i;

            Vector3Int cell =
                mineGrid.GridToTilemapCell(
                    position
                );

            if (!backgroundTilemap.HasTile(
                    cell))
            {
                return false;
            }
        }

        return true;
    }

    private void InitializeCentipede()
    {
        ClearSegments();

        currentDirection =
            startingDirection;

        Vector2Int direction =
            DirectionToGridVector(
                currentDirection
            );

        for (int i = 0;
             i < segmentCount;
             i++)
        {
            Vector2Int position =
                startingHeadPosition -
                direction * i;

            CreateSegment(
                position,
                i
            );
        }

        UpdateAllSegmentSprites();

        initialized = true;
    }

    private void CreateSegment(
        Vector2Int position,
        int index)
    {
        GameObject segmentObject =
            new GameObject(
                "Centipede Segment " +
                index
            );

        segmentObject.transform.SetParent(
            transform
        );

        Vector3 worldPosition =
            mineGrid.GridToWorld(
                position
            );

        segmentObject.transform.position =
            worldPosition;

        SpriteRenderer spriteRenderer =
            segmentObject.AddComponent<SpriteRenderer>();

        spriteRenderer.sortingLayerName =
            sortingLayerName;

        spriteRenderer.sortingOrder =
            orderInLayer;

        Segment segment =
            new Segment
            {
                gameObject =
                    segmentObject,

                transform =
                    segmentObject.transform,

                renderer =
                    spriteRenderer,

                gridPosition =
                    position,

                previousWorldPosition =
                    worldPosition,

                targetWorldPosition =
                    worldPosition
            };

        segments.Add(
            segment
        );
    }

    private IEnumerator MovementLoop()
    {
        while (initialized)
        {
            Direction? nextDirection =
                ChooseNextDirection();

            if (!nextDirection.HasValue)
            {
                yield return new WaitForSeconds(
                    0.1f
                );

                continue;
            }

            currentDirection =
                nextDirection.Value;

            yield return StartCoroutine(
                MoveOneCell(
                    currentDirection
                )
            );

            if (movePause > 0f)
            {
                yield return new WaitForSeconds(
                    movePause
                );
            }
        }
    }

    private Direction? ChooseNextDirection()
    {
        Direction forward =
            currentDirection;

        Direction left =
            TurnLeft(
                currentDirection
            );

        Direction right =
            TurnRight(
                currentDirection
            );

        List<DirectionChoice> choices =
            new List<DirectionChoice>();

        TryAddDirectionChoice(
            choices,
            forward,
            forwardWeight
        );

        TryAddDirectionChoice(
            choices,
            left,
            leftTurnWeight
        );

        TryAddDirectionChoice(
            choices,
            right,
            rightTurnWeight
        );

        if (choices.Count == 0)
        {
            Direction reverse =
                ReverseDirection(
                    currentDirection
                );

            Vector2Int reversePosition =
                GetNextHeadPosition(
                    reverse
                );

            if (CanMoveHeadTo(
                    reversePosition))
            {
                return reverse;
            }

            return null;
        }

        float totalWeight = 0f;

        for (int i = 0;
             i < choices.Count;
             i++)
        {
            totalWeight +=
                choices[i].weight;
        }

        if (totalWeight <= 0f)
        {
            int randomIndex =
                Random.Range(
                    0,
                    choices.Count
                );

            return choices[
                randomIndex
            ].direction;
        }

        float randomValue =
            Random.Range(
                0f,
                totalWeight
            );

        float accumulatedWeight = 0f;

        for (int i = 0;
             i < choices.Count;
             i++)
        {
            accumulatedWeight +=
                choices[i].weight;

            if (randomValue <=
                accumulatedWeight)
            {
                return choices[
                    i
                ].direction;
            }
        }

        return choices[
            choices.Count - 1
        ].direction;
    }

    private void TryAddDirectionChoice(
        List<DirectionChoice> choices,
        Direction direction,
        float weight)
    {
        Vector2Int targetPosition =
            GetNextHeadPosition(
                direction
            );

        if (!CanMoveHeadTo(
                targetPosition))
        {
            return;
        }

        DirectionChoice choice =
            new DirectionChoice
            {
                direction =
                    direction,

                weight =
                    Mathf.Max(
                        0f,
                        weight
                    )
            };

        choices.Add(
            choice
        );
    }

    private Vector2Int GetNextHeadPosition(
        Direction direction)
    {
        return segments[0].gridPosition +
               DirectionToGridVector(
                   direction
               );
    }

    private bool CanMoveHeadTo(
        Vector2Int position)
    {
        if (!IsValidGeneratedPosition(
                position))
        {
            return false;
        }

        for (int i = 0;
             i < segments.Count;
             i++)
        {
            if (segments[i].gridPosition ==
                position)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsValidGeneratedPosition(
        Vector2Int position)
    {
        if (!mineGrid.IsValidPosition(
                position))
        {
            return false;
        }

        Vector3Int cell =
            mineGrid.GridToTilemapCell(
                position
            );

        return backgroundTilemap.HasTile(
            cell
        );
    }

    private IEnumerator MoveOneCell(
        Direction direction)
    {
        Vector2Int newHeadPosition =
            segments[0].gridPosition +
            DirectionToGridVector(
                direction
            );

        List<Vector2Int> oldPositions =
            new List<Vector2Int>();

        for (int i = 0;
             i < segments.Count;
             i++)
        {
            oldPositions.Add(
                segments[i].gridPosition
            );

            segments[i].previousWorldPosition =
                segments[i].transform.position;
        }

        segments[0].gridPosition =
            newHeadPosition;

        for (int i = 1;
             i < segments.Count;
             i++)
        {
            segments[i].gridPosition =
                oldPositions[i - 1];
        }

        for (int i = 0;
             i < segments.Count;
             i++)
        {
            segments[i].targetWorldPosition =
                mineGrid.GridToWorld(
                    segments[i].gridPosition
                );
        }

        UpdateAllSegmentSprites();

        float elapsed = 0f;

        float duration =
            Mathf.Max(
                0.01f,
                moveDuration
            );

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / duration
                );

            float smoothT =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

            for (int i = 0;
                 i < segments.Count;
                 i++)
            {
                segments[i].transform.position =
                    Vector3.Lerp(
                        segments[i].previousWorldPosition,
                        segments[i].targetWorldPosition,
                        smoothT
                    );
            }

            yield return null;
        }

        for (int i = 0;
             i < segments.Count;
             i++)
        {
            segments[i].transform.position =
                segments[i].targetWorldPosition;
        }
    }

    private void UpdateAllSegmentSprites()
    {
        if (segments.Count < 2)
            return;

        UpdateHeadSprite();

        for (int i = 1;
             i < segments.Count - 1;
             i++)
        {
            UpdateBodySprite(
                i
            );
        }

        UpdateTailSprite();
    }

    private void UpdateHeadSprite()
    {
        Segment head =
            segments[0];

        Segment body =
            segments[1];

        Vector2Int directionToBody =
            body.gridPosition -
            head.gridPosition;

        Vector2Int headFacing =
            -directionToBody;

        head.renderer.sprite =
            headSprite;

        head.transform.rotation =
            Quaternion.Euler(
                0f,
                0f,
                GetRotationFromDirection(
                    Vector2Int.left,
                    headFacing
                )
            );
    }

    private void UpdateTailSprite()
    {
        int tailIndex =
            segments.Count - 1;

        Segment tail =
            segments[tailIndex];

        Segment body =
            segments[tailIndex - 1];

        Vector2Int directionToBody =
            body.gridPosition -
            tail.gridPosition;

        tail.renderer.sprite =
            tailSprite;

        tail.transform.rotation =
            Quaternion.Euler(
                0f,
                0f,
                GetRotationFromDirection(
                    DirectionToGridVector(
                        tailDefaultDirection
                    ),
                    directionToBody
                )
            );
    }

    private void UpdateBodySprite(
        int index)
    {
        Segment current =
            segments[index];

        Segment headNeighbor =
            segments[index - 1];

        Segment tailNeighbor =
            segments[index + 1];

        /*
         * The centipede body list is:
         *
         * HEAD -> ... -> TAIL
         *
         * But to determine the actual movement
         * through this body piece, we trace:
         *
         * TAIL -> CURRENT -> HEAD
         */

        Vector2Int tailSide =
            tailNeighbor.gridPosition -
            current.gridPosition;

        Vector2Int headSide =
            headNeighbor.gridPosition -
            current.gridPosition;

        bool straight =
            headSide == -tailSide;

        if (straight)
        {
            UpdateStraightBodySprite(
                current,
                headSide
            );

            return;
        }

        /*
         * IMPORTANT:
         *
         * tailSide tells us where the tail is,
         * but we want the direction the
         * centipede is MOVING as it enters
         * the current cell.
         *
         * Example:
         *
         * Tail is RIGHT of the corner.
         *
         * The centipede therefore moves LEFT
         * INTO the corner.
         *
         * So incomingMovement = -tailSide.
         *
         * headSide already tells us the
         * direction we leave the corner.
         */
        Vector2Int incomingMovement =
            -tailSide;

        Vector2Int outgoingMovement =
            headSide;

        UpdateCornerBodySprite(
            current,
            incomingMovement,
            outgoingMovement
        );
    }

    private void UpdateStraightBodySprite(
        Segment segment,
        Vector2Int headSide)
    {
        segment.renderer.sprite =
            straightBodySprite;

        /*
         * CONFIRMED FROM YOUR ART:
         *
         * DARK = HEAD.
         *
         * On the original straight sprite,
         * DARK / HEAD is LEFT.
         */
        segment.transform.rotation =
            Quaternion.Euler(
                0f,
                0f,
                GetRotationFromDirection(
                    Vector2Int.left,
                    headSide
                )
            );
    }

    private void UpdateCornerBodySprite(
        Segment segment,
        Vector2Int incomingMovement,
        Vector2Int outgoingMovement)
    {
        Sprite selectedSprite =
            GetCornerSpriteByMovement(
                incomingMovement,
                outgoingMovement
            );

        if (selectedSprite == null)
        {
            Debug.LogWarning(
                "No corner sprite for movement " +
                GridVectorToDirection(
                    incomingMovement
                ) +
                " -> " +
                GridVectorToDirection(
                    outgoingMovement
                ),
                this
            );

            segment.renderer.sprite =
                straightBodySprite;

            segment.transform.rotation =
                Quaternion.identity;

            return;
        }

        segment.renderer.sprite =
            selectedSprite;

        /*
         * All 8 directional corners are
         * already drawn correctly.
         *
         * NEVER rotate corner artwork.
         */
        segment.transform.rotation =
            Quaternion.identity;
    }

    private Sprite GetCornerSpriteByMovement(
        Vector2Int incomingMovement,
        Vector2Int outgoingMovement)
    {
        Direction incoming =
            GridVectorToDirection(
                incomingMovement
            );

        Direction outgoing =
            GridVectorToDirection(
                outgoingMovement
            );

        if (incoming == Direction.Right &&
            outgoing == Direction.Down)
        {
            return rightToDown;
        }

        if (incoming == Direction.Right &&
            outgoing == Direction.Up)
        {
            return rightToUp;
        }

        if (incoming == Direction.Left &&
            outgoing == Direction.Down)
        {
            return leftToDown;
        }

        if (incoming == Direction.Left &&
            outgoing == Direction.Up)
        {
            return leftToUp;
        }

        if (incoming == Direction.Down &&
            outgoing == Direction.Right)
        {
            return downToRight;
        }

        if (incoming == Direction.Down &&
            outgoing == Direction.Left)
        {
            return downToLeft;
        }

        if (incoming == Direction.Up &&
            outgoing == Direction.Right)
        {
            return upToRight;
        }

        if (incoming == Direction.Up &&
            outgoing == Direction.Left)
        {
            return upToLeft;
        }

        return null;
    }

    private float GetRotationFromDirection(
        Vector2Int originalDirection,
        Vector2Int targetDirection)
    {
        Vector2 originalWorldDirection =
            GridVectorToWorldVector(
                originalDirection
            );

        Vector2 targetWorldDirection =
            GridVectorToWorldVector(
                targetDirection
            );

        return Vector2.SignedAngle(
            originalWorldDirection,
            targetWorldDirection
        );
    }

    private Vector2 GridVectorToWorldVector(
        Vector2Int gridDirection)
    {
        return new Vector2(
            gridDirection.x,
            -gridDirection.y
        );
    }

    private Vector2Int DirectionToGridVector(
        Direction direction)
    {
        switch (direction)
        {
            case Direction.Up:
                return new Vector2Int(
                    0,
                    -1
                );

            case Direction.Right:
                return new Vector2Int(
                    1,
                    0
                );

            case Direction.Down:
                return new Vector2Int(
                    0,
                    1
                );

            case Direction.Left:
                return new Vector2Int(
                    -1,
                    0
                );
        }

        return Vector2Int.zero;
    }

    private Direction GridVectorToDirection(
        Vector2Int direction)
    {
        if (direction == Vector2Int.right)
        {
            return Direction.Right;
        }

        if (direction == Vector2Int.left)
        {
            return Direction.Left;
        }

        if (direction.y > 0)
        {
            return Direction.Down;
        }

        return Direction.Up;
    }

    private Direction TurnLeft(
        Direction direction)
    {
        switch (direction)
        {
            case Direction.Up:
                return Direction.Left;

            case Direction.Left:
                return Direction.Down;

            case Direction.Down:
                return Direction.Right;

            case Direction.Right:
                return Direction.Up;
        }

        return direction;
    }

    private Direction TurnRight(
        Direction direction)
    {
        switch (direction)
        {
            case Direction.Up:
                return Direction.Right;

            case Direction.Right:
                return Direction.Down;

            case Direction.Down:
                return Direction.Left;

            case Direction.Left:
                return Direction.Up;
        }

        return direction;
    }

    private Direction ReverseDirection(
        Direction direction)
    {
        switch (direction)
        {
            case Direction.Up:
                return Direction.Down;

            case Direction.Down:
                return Direction.Up;

            case Direction.Left:
                return Direction.Right;

            case Direction.Right:
                return Direction.Left;
        }

        return direction;
    }

    private void ClearSegments()
    {
        initialized = false;

        for (int i = segments.Count - 1;
             i >= 0;
             i--)
        {
            if (segments[i].gameObject != null)
            {
                Destroy(
                    segments[i].gameObject
                );
            }
        }

        segments.Clear();
    }
}