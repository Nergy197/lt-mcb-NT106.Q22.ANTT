using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class RankTop100ListLoader : MonoBehaviour
{
    [Header("UI")]
    public Transform container;
    public GameObject itemPrefab;

    [Header("Rank Frame Sprites")]
    public Sprite top1Frame;
    public Sprite top2Frame;
    public Sprite top3Frame;
    public Sprite defaultFrame;

    [Header("Layout")]
    public float topRowHeight = 92f;
    public float defaultRowHeight = 76f;

    [Header("Data")]
    public bool useSampleData = true;
    public int sampleCount = 100;
    public string top100ApiUrl = "https://pokemon-mmo-server-123-gkaqfbejgycbcwfb.southeastasia-01.azurewebsites.net/api/rank/top100";

    private static readonly Color Top1FallbackColor = new(1f, 0.78f, 0.18f, 0.95f);
    private static readonly Color Top2FallbackColor = new(0.78f, 0.82f, 0.9f, 0.95f);
    private static readonly Color Top3FallbackColor = new(0.78f, 0.46f, 0.2f, 0.95f);
    private static readonly Color DefaultFallbackColor = new(0.18f, 0.22f, 0.32f, 0.9f);

    private void OnEnable()
    {
        Refresh();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    public void Refresh()
    {
        if (container == null)
            return;

        StopAllCoroutines();

        if (!useSampleData && !string.IsNullOrWhiteSpace(top100ApiUrl))
            StartCoroutine(FetchTop100FromServer());
        else
            PopulateUI(CreateSampleEntries());
    }

    private IEnumerator FetchTop100FromServer()
    {
        using UnityWebRequest request = UnityWebRequest.Get(top100ApiUrl);

        string token = PlayerPrefs.GetString("jwt_token", "");
        if (!string.IsNullOrWhiteSpace(token))
            request.SetRequestHeader("Authorization", "Bearer " + token);

        request.timeout = 10;
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Lỗi lấy Online Top 100: {request.error} | HTTP {request.responseCode}");
            yield break;
        }

        try
        {
            List<RankTop100Entry> entries =
                JsonConvert.DeserializeObject<List<RankTop100Entry>>(request.downloadHandler.text);
            PopulateUI(entries ?? new List<RankTop100Entry>());
        }
        catch (Exception ex)
        {
            Debug.LogError("Lỗi parse JSON Online Top 100: " + ex.Message);
        }
    }

    private void PopulateUI(List<RankTop100Entry> entries)
    {
        foreach (Transform child in container)
            Destroy(child.gameObject);

        int count = Mathf.Min(entries.Count, 100);
        for (int i = 0; i < count; i++)
        {
            int rank = i + 1;
            RankTop100Entry entry = entries[i];
            GameObject item = itemPrefab != null
                ? Instantiate(itemPrefab, container)
                : CreateDefaultItem(container);

            RankTop100ItemUI itemUI = item.GetComponent<RankTop100ItemUI>();
            if (itemUI == null)
                itemUI = item.AddComponent<RankTop100ItemUI>();

            itemUI.SetData(
                rank,
                entry.playerName,
                ResolveScore(entry),
                GetFrameSprite(rank),
                GetFallbackFrameColor(rank),
                rank <= 3 ? topRowHeight : defaultRowHeight);
        }

        StartCoroutine(RebuildLayoutNextFrame());
    }

    private GameObject CreateDefaultItem(Transform parent)
    {
        GameObject row = new("RankTop100_Item", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement), typeof(RankTop100ItemUI));
        row.transform.SetParent(parent, false);

        Image frame = row.GetComponent<Image>();
        frame.type = Image.Type.Sliced;
        frame.raycastTarget = true;

        LayoutElement layout = row.GetComponent<LayoutElement>();
        layout.flexibleWidth = 1f;

        HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.padding = new RectOffset(24, 24, 8, 8);
        rowLayout.spacing = 18f;
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;

        TMP_Text rankText = CreateText("RankText", row.transform, "#1", 34f, TextAlignmentOptions.Center);
        AddLayout(rankText.gameObject, 120f, 1f);

        TMP_Text nameText = CreateText("NameText", row.transform, "Trainer", 30f, TextAlignmentOptions.MidlineLeft);
        AddLayout(nameText.gameObject, 0f, 4f);

        TMP_Text scoreText = CreateText("ScoreText", row.transform, "1000", 28f, TextAlignmentOptions.Center);
        AddLayout(scoreText.gameObject, 160f, 1f);

        row.GetComponent<RankTop100ItemUI>().BindViews(frame, rankText, nameText, scoreText, layout);
        return row;
    }

    private static TMP_Text CreateText(string objectName, Transform parent, string text, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI textComponent = textObject.GetComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.color = Color.white;
        textComponent.alignment = alignment;
        textComponent.enableWordWrapping = false;
        textComponent.overflowMode = TextOverflowModes.Ellipsis;

        return textComponent;
    }

    private static void AddLayout(GameObject target, float preferredWidth, float flexibleWidth)
    {
        LayoutElement layout = target.AddComponent<LayoutElement>();
        layout.preferredWidth = preferredWidth;
        layout.flexibleWidth = flexibleWidth;
    }

    private IEnumerator RebuildLayoutNextFrame()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(container.GetComponent<RectTransform>());
    }

    private Sprite GetFrameSprite(int rank)
    {
        return rank switch
        {
            1 => top1Frame,
            2 => top2Frame,
            3 => top3Frame,
            _ => defaultFrame
        };
    }

    private static Color GetFallbackFrameColor(int rank)
    {
        return rank switch
        {
            1 => Top1FallbackColor,
            2 => Top2FallbackColor,
            3 => Top3FallbackColor,
            _ => DefaultFallbackColor
        };
    }

    private static int ResolveScore(RankTop100Entry entry)
    {
        // Luôn hiển thị ĐIỂM RANK. Không fallback sang MMR/wins để tránh người 0 điểm hiện nhầm 1000.
        if (entry.rankPoints != 0) return entry.rankPoints;
        return entry.score; // server đồng bộ score = rankPoints; dữ liệu mẫu chỉ set score
    }

    private List<RankTop100Entry> CreateSampleEntries()
    {
        int count = Mathf.Clamp(sampleCount, 1, 100);
        List<RankTop100Entry> entries = new(count);

        for (int i = 0; i < count; i++)
        {
            entries.Add(new RankTop100Entry
            {
                playerName = $"Trainer {i + 1:000}",
                score = Mathf.Max(1000, 2500 - i * 12),
                mmr = Mathf.Max(1000, 2500 - i * 12),
                rankedWins = Mathf.Max(0, 120 - i)
            });
        }

        return entries;
    }

    [Serializable]
    public class RankTop100Entry
    {
        public string playerId;
        public string playerName;
        public int score;
        public int rankPoints;
        public int mmr;
        public int rankedWins;
        public int rankedMatches;
    }
}
