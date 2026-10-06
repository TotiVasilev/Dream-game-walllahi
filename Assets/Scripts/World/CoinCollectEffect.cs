using System.Collections;
using UnityEngine;

public class CoinCollectEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MineGrid mineGrid;
    [SerializeField] private Transform player;
    [SerializeField] private Camera mineCamera;
    [SerializeField] private GameObject coinPrefab;

    [Header("Pop")]
    [Tooltip("How far the coin initially bursts away from the broken tile.")]
    [Min(0f)]
    [SerializeField] private float popDistance = 0.8f;

    [Tooltip("How long the initial pop takes.")]
    [Min(0.01f)]
    [SerializeField] private float popDuration = 0.18f;

    [Tooltip("Coin scale when it first appears.")]
    [Min(0f)]
    [SerializeField] private float startingScale = 0.05f;

    [Tooltip("Largest scale reached during the pop.")]
    [Min(0f)]
    [SerializeField] private float fullScale = 1f;

    [Header("Collection")]
    [Tooltip("Small pause at the end of the pop before flying to the player.")]
    [Min(0f)]
    [SerializeField] private float collectDelay = 0.05f;

    [Tooltip("How long the coin takes to fly back to the player.")]
    [Min(0.01f)]
    [SerializeField] private float collectDuration = 0.22f;

    [Tooltip("How small the coin becomes as it reaches the player.")]
    [Min(0f)]
    [SerializeField] private float collectedScale = 0.15f;

    [Header("Direction")]
    [Tooltip("Adds randomness to the direction of the initial burst.")]
    [Range(0f, 1f)]
    [SerializeField] private float directionRandomness = 0.35f;

    [Header("Screen Boundary")]
    [Tooltip("Keeps the coin this far inside the camera edges.")]
    [Min(0f)]
    [SerializeField] private float screenPadding = 0.25f;

    public void SpawnCoin(
        Vector2Int brokenGridPosition)
    {
        if (coinPrefab == null)
            return;

        if (player == null)
            return;

        Vector3 spawnPosition =
            mineGrid.GridToWorld(
                brokenGridPosition
            );

        GameObject coin =
            Instantiate(
                coinPrefab,
                spawnPosition,
                Quaternion.identity
            );

        StartCoroutine(
            AnimateCoin(
                coin.transform,
                spawnPosition
            )
        );
    }

    private IEnumerator AnimateCoin(
        Transform coin,
        Vector3 spawnPosition)
    {
        if (coin == null)
            yield break;

        /*
         * Start tiny.
         */
        coin.localScale =
            Vector3.one *
            startingScale;

        /*
         * Work out the direction AWAY
         * from the player.
         */
        Vector2 awayDirection =
            (Vector2)(
                spawnPosition -
                player.position
            );

        /*
         * If the player and tile happen to be
         * practically on top of each other,
         * use a random direction instead.
         */
        if (awayDirection.sqrMagnitude <
            0.001f)
        {
            awayDirection =
                Random.insideUnitCircle;

            if (awayDirection.sqrMagnitude <
                0.001f)
            {
                awayDirection =
                    Vector2.up;
            }
        }

        awayDirection.Normalize();

        /*
         * Add a little random sideways variation
         * so multiple coins don't all perform
         * exactly the same animation.
         */
        Vector2 randomDirection =
            Random.insideUnitCircle.normalized;

        awayDirection =
            Vector2.Lerp(
                awayDirection,
                randomDirection,
                directionRandomness
            ).normalized;

        /*
         * If we're near a screen edge, prevent
         * the burst from going outside it.
         */
        awayDirection =
            KeepDirectionInsideScreen(
                spawnPosition,
                awayDirection
            );

        Vector3 popTarget =
            spawnPosition +
            (Vector3)(
                awayDirection *
                popDistance
            );

        popTarget =
            ClampInsideScreen(
                popTarget
            );

        /*
         * PHASE 1:
         *
         * Coin pops away from the destroyed tile
         * while growing from tiny to full size.
         */
        float elapsed = 0f;

        while (elapsed < popDuration)
        {
            if (coin == null)
                yield break;

            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    popDuration
                );

            /*
             * Ease-out gives the pop a punchy start
             * and softer ending.
             */
            float easedT =
                1f -
                Mathf.Pow(
                    1f - t,
                    3f
                );

            coin.position =
                Vector3.Lerp(
                    spawnPosition,
                    popTarget,
                    easedT
                );

            float scale =
                Mathf.Lerp(
                    startingScale,
                    fullScale,
                    easedT
                );

            coin.localScale =
                Vector3.one *
                scale;

            yield return null;
        }

        coin.position =
            popTarget;

        coin.localScale =
            Vector3.one *
            fullScale;

        /*
         * Tiny pause makes the two stages
         * visually readable.
         */
        if (collectDelay > 0f)
        {
            yield return new WaitForSeconds(
                collectDelay
            );
        }

        /*
         * PHASE 2:
         *
         * Fly toward the CURRENT player position.
         *
         * We intentionally read player.position
         * every frame because the player may be
         * falling or power-drilling while the coin
         * is being collected.
         */
        Vector3 collectStartPosition =
            coin.position;

        elapsed = 0f;

        while (elapsed < collectDuration)
        {
            if (coin == null)
                yield break;

            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    collectDuration
                );

            /*
             * Starts a little slower and accelerates
             * toward the player.
             */
            float easedT =
                t * t;

            Vector3 currentPlayerPosition =
                player.position;

            coin.position =
                Vector3.Lerp(
                    collectStartPosition,
                    currentPlayerPosition,
                    easedT
                );

            float scale =
                Mathf.Lerp(
                    fullScale,
                    collectedScale,
                    easedT
                );

            coin.localScale =
                Vector3.one *
                scale;

            yield return null;
        }

        Destroy(
            coin.gameObject
        );
    }

    private Vector2 KeepDirectionInsideScreen(
        Vector3 spawnPosition,
        Vector2 direction)
    {
        if (mineCamera == null)
            return direction;

        Vector3 viewportPosition =
            mineCamera.WorldToViewportPoint(
                spawnPosition
            );

        /*
         * Near left edge:
         * don't allow a leftward burst.
         */
        if (viewportPosition.x < 0.15f &&
            direction.x < 0f)
        {
            direction.x =
                Mathf.Abs(
                    direction.x
                );
        }

        /*
         * Near right edge:
         * don't allow a rightward burst.
         */
        if (viewportPosition.x > 0.85f &&
            direction.x > 0f)
        {
            direction.x =
                -Mathf.Abs(
                    direction.x
                );
        }

        /*
         * Near bottom:
         * push upward.
         */
        if (viewportPosition.y < 0.12f &&
            direction.y < 0f)
        {
            direction.y =
                Mathf.Abs(
                    direction.y
                );
        }

        /*
         * Near top:
         * push downward.
         */
        if (viewportPosition.y > 0.88f &&
            direction.y > 0f)
        {
            direction.y =
                -Mathf.Abs(
                    direction.y
                );
        }

        if (direction.sqrMagnitude <
            0.001f)
        {
            direction =
                Vector2.up;
        }

        return direction.normalized;
    }

    private Vector3 ClampInsideScreen(
        Vector3 worldPosition)
    {
        if (mineCamera == null)
            return worldPosition;

        Vector3 viewportPosition =
            mineCamera.WorldToViewportPoint(
                worldPosition
            );

        float verticalPadding =
            screenPadding /
            (mineCamera.orthographicSize * 2f);

        float horizontalWorldSize =
            mineCamera.orthographicSize *
            2f *
            mineCamera.aspect;

        float horizontalPadding =
            screenPadding /
            horizontalWorldSize;

        viewportPosition.x =
            Mathf.Clamp(
                viewportPosition.x,
                horizontalPadding,
                1f - horizontalPadding
            );

        viewportPosition.y =
            Mathf.Clamp(
                viewportPosition.y,
                verticalPadding,
                1f - verticalPadding
            );

        Vector3 clampedWorldPosition =
            mineCamera.ViewportToWorldPoint(
                viewportPosition
            );

        clampedWorldPosition.z =
            worldPosition.z;

        return clampedWorldPosition;
    }
}