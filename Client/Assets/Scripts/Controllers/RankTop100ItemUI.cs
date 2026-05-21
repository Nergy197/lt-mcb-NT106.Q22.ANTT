using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RankTop100ItemUI : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private Image frameImage;
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private LayoutElement layoutElement;

    public void BindViews(
        Image frame,
        TMP_Text rank,
        TMP_Text playerName,
        TMP_Text score,
        LayoutElement layout)
    {
        frameImage = frame;
        rankText = rank;
        playerNameText = playerName;
        scoreText = score;
        layoutElement = layout;
    }

    public void SetData(int rank, string playerName, int score, Sprite frameSprite, Color fallbackFrameColor, float rowHeight)
    {
        if (rankText != null)
            rankText.text = rank <= 3 ? string.Empty : $"#{rank}";

        if (playerNameText != null)
            playerNameText.text = string.IsNullOrWhiteSpace(playerName) ? "Unknown Trainer" : playerName;

        if (scoreText != null)
            scoreText.text = score.ToString();

        if (frameImage != null)
        {
            frameImage.sprite = frameSprite;
            frameImage.type = frameSprite == null ? Image.Type.Simple : Image.Type.Sliced;
            frameImage.color = frameSprite == null ? fallbackFrameColor : Color.white;
        }

        if (layoutElement != null)
            layoutElement.preferredHeight = rowHeight;
    }
}
