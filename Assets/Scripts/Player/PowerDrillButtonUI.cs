using UnityEngine;
using UnityEngine.UI;

public class PowerDrillButtonUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerGridMovement player;
    [SerializeField] private Button button;
    [SerializeField] private Image buttonImage;

    [Header("Visuals")]
    [SerializeField] private Color chargingColor =
        new Color(0.35f, 0.35f, 0.35f, 1f);

    [SerializeField] private Color readyColor =
        Color.white;

    [SerializeField] private Color armedColor =
        new Color(1f, 0.85f, 0.2f, 1f);

    private void Update()
    {
        if (player == null)
            return;

        UpdateButtonState();
    }

    private void UpdateButtonState()
    {
        bool isReady =
            player.IsPowerDrillReady;

        bool isArmed =
            player.IsPowerDrillArmed;

        if (button != null)
        {
            button.interactable =
                isReady && !isArmed;
        }

        if (buttonImage == null)
            return;

        if (isArmed)
        {
            buttonImage.color =
                armedColor;
        }
        else if (isReady)
        {
            buttonImage.color =
                readyColor;
        }
        else
        {
            buttonImage.color =
                chargingColor;
        }
    }
}