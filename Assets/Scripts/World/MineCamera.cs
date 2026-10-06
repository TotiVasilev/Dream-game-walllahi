using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class MineCamera : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private MineGrid grid;

    [Header("Mining View")]
    [Min(0)]
    [SerializeField] private int rowsAbovePlayerAtStart = 1;

    [Min(0)]
    [SerializeField] private int rowsVisibleBelowPlayer = 3;

    [Min(0f)]
    [SerializeField] private float horizontalPadding = 0.25f;

    [Header("Mine Entry Transition")]
    [Min(0f)]
    [SerializeField] private float mineEntryDuration = 1f;

    [Header("Following")]
    [SerializeField] private bool followPlayer = false;

    private Camera cam;
    private float gameplayCameraX;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
    }

    public IEnumerator EnterMine()
    {
        followPlayer = false;

        FitCameraToMineWidth();

        gameplayCameraX =
            grid.transform.position.x;

        float halfHeight =
            cam.orthographicSize;

        float desiredPlayerDistanceFromTop =
            (rowsAbovePlayerAtStart + 0.5f) *
            grid.TileSize;

        float targetCameraY =
            player.position.y -
            halfHeight +
            desiredPlayerDistanceFromTop;

        Vector3 startPosition =
            transform.position;

        Vector3 targetPosition =
            new Vector3(
                gameplayCameraX,
                targetCameraY,
                transform.position.z
            );

        if (mineEntryDuration <= 0f)
        {
            transform.position =
                targetPosition;
        }
        else
        {
            float elapsed = 0f;

            while (elapsed < mineEntryDuration)
            {
                elapsed += Time.deltaTime;

                float t =
                    Mathf.Clamp01(
                        elapsed /
                        mineEntryDuration
                    );

                float smoothT =
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        t
                    );

                transform.position =
                    Vector3.Lerp(
                        startPosition,
                        targetPosition,
                        smoothT
                    );

                yield return null;
            }

            transform.position =
                targetPosition;
        }

        followPlayer = true;
    }

    public void StopFollowing()
    {
        followPlayer = false;
    }

    private void LateUpdate()
    {
        if (!followPlayer)
            return;

        float halfHeight =
            cam.orthographicSize;

        float cameraBottom =
            transform.position.y -
            halfHeight;

        // This is the lowest point the player's CENTER
        // is allowed to reach before the camera follows.
        //
        // Example with 3:
        //
        // Player
        // [ ][ ][ ]
        // Camera bottom
        //
        // The camera begins moving only when the player
        // reaches this line.
        float followLineY =
            cameraBottom +
            (rowsVisibleBelowPlayer + 0.5f) *
            grid.TileSize;

        // World Y decreases as the player digs downward.
        // If the player crosses below the follow line,
        // move the camera down by exactly that difference.
        if (player.position.y < followLineY)
        {
            float difference =
                followLineY -
                player.position.y;

            float newCameraY =
                transform.position.y -
                difference;

            transform.position =
                new Vector3(
                    gameplayCameraX,
                    newCameraY,
                    transform.position.z
                );
        }
    }

    private void FitCameraToMineWidth()
    {
        float mineWidth =
            grid.PlayableColumns *
            grid.TileSize;

        float desiredWidth =
            mineWidth +
            horizontalPadding * 2f;

        float aspect =
            (float)Screen.width /
            Screen.height;

        cam.orthographicSize =
            desiredWidth /
            (2f * aspect);
    }
}