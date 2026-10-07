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
    [SerializeField] private Sprite rightToDown;
    [SerializeField] private Sprite rightToUp;
    [SerializeField] private Sprite leftToDown;
    [SerializeField] private Sprite leftToUp;
    [SerializeField] private Sprite downToRight;
    [SerializeField] private Sprite downToLeft;
    [SerializeField] private Sprite upToRight;
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
    [Tooltip("How many grid tiles the centipede travels per second.")]
    [Min(0.1f)]
    [SerializeField] private float tilesPerSecond = 3f;

    [Header("Sprite Change Timing")]
    [Tooltip(
        "How far through a tile movement a body segment travels " +
        "before changing to the sprite for its destination cell. " +
        "0.5 means exactly halfway."
    )]
    [Range(0f, 1f)]
    [SerializeField] private float bodySpriteSwitchPoint = 0.5f;

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

    /*
     * currentPath represents the body at the
     * current completed grid step.
     *
     * Index 0 = head.
     * Last index = tail.
     */
    private readonly List<Vector2Int> currentPath =
        new List<Vector2Int>();

    /*
     * nextPath represents where every segment
     * will be after the current movement finishes.
     */
    private readonly List<Vector2Int> nextPath =
        new List<Vector2Int>();

    private Direction currentDirection;

    private bool initialized;
    private bool moving;

    private float movementProgress;

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

        public Vector3 startWorldPosition;
        public Vector3 targetWorldPosition;

        public bool destinationSpriteApplied;
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
                "CentipedeEnemy is missing its main sprites.",
                this
            );

            yield break;
        }

        if (!DoesStartingBodyFitInsideGrid())
        {
            Debug.LogError(
                "The centipede starting body is outside the mine.",
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

        currentPath.Clear();
        nextPath.Clear();

        currentDirection =
            startingDirection;

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

            currentPath.Add(
                position
            );

            CreateSegment(
                position,
                i
            );
        }

        movementProgress = 0f;
        moving = false;

        UpdateAllSpritesFromPath(
            currentPath
        );

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

                startWorldPosition =
                    worldPosition,

                targetWorldPosition =
                    worldPosition,

                destinationSpriteApplied =
                    true
            };

        segments.Add(
            segment
        );
    }

    private IEnumerator MovementLoop()
    {
        while (initialized)
        {
            if (!moving)
            {
                if (!PrepareNextStep())
                {
                    yield return null;
                    continue;
                }
            }

            float duration =
                1f /
                Mathf.Max(
                    0.1f,
                    tilesPerSecond
                );

            movementProgress +=
                Time.deltaTime /
                duration;

            float t =
                Mathf.Clamp01(
                    movementProgress
                );

            /*
             * HEAD:
             *
             * Its new facing direction was already
             * applied before movement started.
             *
             * BODY:
             *
             * Each segment switches to the sprite
             * belonging to its destination cell
             * when it reaches the configured point
             * through the movement.
             */
            for (int i = 0;
                 i < segments.Count;
                 i++)
            {
                Segment segment =
                    segments[i];

                segment.transform.position =
                    Vector3.Lerp(
                        segment.startWorldPosition,
                        segment.targetWorldPosition,
                        t
                    );

                if (i > 0 &&
                    !segment.destinationSpriteApplied &&
                    t >= bodySpriteSwitchPoint)
                {
                    ApplyDestinationSprite(
                        i
                    );

                    segment.destinationSpriteApplied =
                        true;
                }
            }

            if (movementProgress >= 1f)
            {
                FinishCurrentStep();
            }

            yield return null;
        }
    }

    private bool PrepareNextStep()
    {
        Direction? nextDirection =
            ChooseNextDirection();

        if (!nextDirection.HasValue)
        {
            return false;
        }

        currentDirection =
            nextDirection.Value;

        Vector2Int newHeadPosition =
            currentPath[0] +
            DirectionToGridVector(
                currentDirection
            );

        nextPath.Clear();

        /*
         * New head cell.
         */
        nextPath.Add(
            newHeadPosition
        );

        /*
         * Every other segment moves into the
         * previous segment's current cell.
         */
        for (int i = 1;
             i < segmentCount;
             i++)
        {
            nextPath.Add(
                currentPath[i - 1]
            );
        }

        for (int i = 0;
             i < segments.Count;
             i++)
        {
            Segment segment =
                segments[i];

            segment.startWorldPosition =
                segment.transform.position;

            segment.targetWorldPosition =
                mineGrid.GridToWorld(
                    nextPath[i]
                );

            segment.destinationSpriteApplied =
                false;
        }

        /*
         * HEAD MUST FACE ITS NEW MOVEMENT
         * DIRECTION BEFORE IT STARTS MOVING.
         */
        ApplyHeadMovementSprite();

        segments[0].destinationSpriteApplied =
            true;

        movementProgress = 0f;
        moving = true;

        return true;
    }

    private void ApplyHeadMovementSprite()
    {
        Segment head =
            segments[0];

        Vector2Int facingDirection =
            DirectionToGridVector(
                currentDirection
            );

        head.renderer.sprite =
            headSprite;

        head.transform.rotation =
            Quaternion.Euler(
                0f,
                0f,
                GetRotationFromDirection(
                    Vector2Int.left,
                    facingDirection
                )
            );
    }

    private void ApplyDestinationSprite(
        int index)
    {
        if (index <= 0 ||
            index >= segments.Count)
        {
            return;
        }

        /*
         * TAIL
         */
        if (index ==
            segments.Count - 1)
        {
            ApplyTailSpriteFromPath(
                nextPath
            );

            return;
        }

        /*
         * BODY
         */
        ApplyBodySpriteFromPath(
            index,
            nextPath
        );
    }

    private void FinishCurrentStep()
    {
        /*
         * Make absolutely sure every segment
         * finishes exactly at its destination.
         */
        for (int i = 0;
             i < segments.Count;
             i++)
        {
            Segment segment =
                segments[i];

            segment.transform.position =
                segment.targetWorldPosition;

            if (i > 0 &&
                !segment.destinationSpriteApplied)
            {
                ApplyDestinationSprite(
                    i
                );

                segment.destinationSpriteApplied =
                    true;
            }
        }

        /*
         * nextPath now becomes the real current
         * body configuration.
         */
        currentPath.Clear();

        for (int i = 0;
             i < nextPath.Count;
             i++)
        {
            currentPath.Add(
                nextPath[i]
            );
        }

        /*
         * Reapply everything from the completed
         * path to guarantee exact final state.
         */
        UpdateAllSpritesFromPath(
            currentPath
        );

        movementProgress = 0f;
        moving = false;
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
                currentPath[0] +
                DirectionToGridVector(
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
        Vector2Int target =
            currentPath[0] +
            DirectionToGridVector(
                direction
            );

        if (!CanMoveHeadTo(
                target))
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

    private bool CanMoveHeadTo(
        Vector2Int position)
    {
        if (!IsValidGeneratedPosition(
                position))
        {
            return false;
        }

        for (int i = 0;
             i < currentPath.Count;
             i++)
        {
            if (currentPath[i] ==
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

    private void UpdateAllSpritesFromPath(
        List<Vector2Int> bodyPath)
    {
        if (bodyPath.Count <
            segments.Count)
        {
            return;
        }

        ApplyHeadSpriteFromPath(
            bodyPath
        );

        for (int i = 1;
             i < segments.Count - 1;
             i++)
        {
            ApplyBodySpriteFromPath(
                i,
                bodyPath
            );
        }

        ApplyTailSpriteFromPath(
            bodyPath
        );
    }

    private void ApplyHeadSpriteFromPath(
        List<Vector2Int> bodyPath)
    {
        Segment head =
            segments[0];

        Vector2Int headPosition =
            bodyPath[0];

        Vector2Int bodyPosition =
            bodyPath[1];

        Vector2Int directionToBody =
            bodyPosition -
            headPosition;

        Vector2Int facingDirection =
            -directionToBody;

        head.renderer.sprite =
            headSprite;

        head.transform.rotation =
            Quaternion.Euler(
                0f,
                0f,
                GetRotationFromDirection(
                    Vector2Int.left,
                    facingDirection
                )
            );
    }

    private void ApplyBodySpriteFromPath(
        int index,
        List<Vector2Int> bodyPath)
    {
        Segment segment =
            segments[index];

        Vector2Int currentPosition =
            bodyPath[index];

        Vector2Int headNeighbor =
            bodyPath[index - 1];

        Vector2Int tailNeighbor =
            bodyPath[index + 1];

        Vector2Int headSide =
            headNeighbor -
            currentPosition;

        Vector2Int tailSide =
            tailNeighbor -
            currentPosition;

        bool straight =
            headSide == -tailSide;

        if (straight)
        {
            ApplyStraightBodySprite(
                segment,
                headSide
            );

            return;
        }

        Vector2Int incomingMovement =
            -tailSide;

        Vector2Int outgoingMovement =
            headSide;

        ApplyCornerBodySprite(
            segment,
            incomingMovement,
            outgoingMovement
        );
    }

    private void ApplyStraightBodySprite(
        Segment segment,
        Vector2Int headSide)
    {
        segment.renderer.sprite =
            straightBodySprite;

        /*
         * DARK = HEAD.
         *
         * Original dark/head side points LEFT.
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

    private void ApplyCornerBodySprite(
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
                "Missing corner sprite for " +
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
         * Your directional corner sprites are
         * already drawn in final orientation.
         */
        segment.transform.rotation =
            Quaternion.identity;
    }

    private void ApplyTailSpriteFromPath(
        List<Vector2Int> bodyPath)
    {
        int tailIndex =
            segments.Count - 1;

        Segment tail =
            segments[tailIndex];

        Vector2Int tailPosition =
            bodyPath[tailIndex];

        Vector2Int bodyPosition =
            bodyPath[tailIndex - 1];

        Vector2Int directionToBody =
            bodyPosition -
            tailPosition;

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
        Vector2 originalWorld =
            GridVectorToWorldVector(
                originalDirection
            );

        Vector2 targetWorld =
            GridVectorToWorldVector(
                targetDirection
            );

        return Vector2.SignedAngle(
            originalWorld,
            targetWorld
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
            return Direction.Right;

        if (direction == Vector2Int.left)
            return Direction.Left;

        if (direction.y > 0)
            return Direction.Down;

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
        moving = false;

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

        currentPath.Clear();
        nextPath.Clear();
    }
}