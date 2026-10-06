using System.Collections;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerGridMovement player;
    [SerializeField] private MineCamera mineCamera;
    [SerializeField] private MineGenerator mineGenerator;

    [Header("Surface")]
    [SerializeField] private Transform surfacePlayerPosition;
    [SerializeField] private GameObject surfaceUI;

    private bool isMining;
    private bool isTransitioning;

    public bool IsMining => isMining;

    private void Start()
    {
        EnterSurface();
    }

    public void EnterSurface()
    {
        isMining = false;
        isTransitioning = false;

        mineCamera.StopFollowing();

        player.SetInputEnabled(false);

        if (surfacePlayerPosition != null)
        {
            player.transform.position =
                surfacePlayerPosition.position;
        }

        if (surfaceUI != null)
        {
            surfaceUI.SetActive(true);
        }

        if (mineGenerator != null)
        {
            mineGenerator.PrepareSurfaceMine();
        }
    }

    public void StartRun()
    {
        if (isMining || isTransitioning)
            return;

        StartCoroutine(
            StartRunSequence()
        );
    }

    private IEnumerator StartRunSequence()
    {
        isTransitioning = true;

        // Hide Store / Options / Play immediately.
        if (surfaceUI != null)
        {
            surfaceUI.SetActive(false);
        }

        // Reset the mine and create the player's
        // starting opening.
        if (mineGenerator != null)
        {
            mineGenerator.StartNewRun();
        }

        // Put the player at the mine entrance.
        player.StartMiningRun();

        // Keep controls disabled while the camera
        // travels down into the mine.
        player.SetInputEnabled(false);

        // Wait for the camera transition to finish.
        yield return StartCoroutine(
            mineCamera.EnterMine()
        );

        isMining = true;
        isTransitioning = false;

        // Mining starts now.
        player.SetInputEnabled(true);
    }
}