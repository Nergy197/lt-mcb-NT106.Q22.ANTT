using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class RankFriendsListLoader : MonoBehaviour
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
    public bool useSampleData;
    public bool useGeneratedScoresWhenMissing;
    public int sampleFriendCount = 10;
    public string friendsApiUrl = "https://pokemon-mmo-server-123-gkaqfbejgycbcwfb.southeastasia-01.azurewebsites.net/api/rank/friends";

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

        if (useSampleData)
            PopulateUI(CreateSampleEntries());
        else
            StartCoroutine(FetchFriendsFromServer());
    }

    private IEnumerator FetchFriendsFromServer()
    {
        string token = PlayerPrefs.GetString("jwt_token", "");
        if (string.IsNullOrWhiteSpace(token))
        {
            PopulateUI(CreateSampleEntries());
            yield break;
        }

        using UnityWebRequest request = UnityWebRequest.Get(friendsApiUrl);
        request.SetRequestHeader("Authorization", "Bearer " + token);
        request.timeout = 10;

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Lỗi lấy bảng xếp hạng bạn bè: {request.error} | HTTP {request.responseCode}");
            PopulateUI(CreateSampleEntries());
            yield break;
        }

        try
        {
            List<FriendRankEntry> entries =
                JsonConvert.DeserializeObject<List<FriendRankEntry>>(request.downloadHandler.text)
                ?? new List<FriendRankEntry>();

            if (useGeneratedScoresWhenMissing)
                FillMissingScores(entries);

            entries = entries
                .OrderByDescending(ResolveScore)
                .ThenBy(e => e.playerName)
                .ToList();

            PopulateUI(entries);
        }
        catch (Exception ex)
        {
            Debug.LogError("Lỗi parse JSON bảng xếp hạng bạn bè: " + ex.Message);
            PopulateUI(CreateSampleEntries());
        }
    }

    private void PopulateUI(List<FriendRankEntry> entries)
    {
        foreach (Transform child in container)
            Destroy(child.gameObject);

        for (int i = 0; i < entries.Count; i++)
        {
            int rank = i + 1;
            FriendRankEntry entry = entries[i];
            GameObject item = itemPrefab != null
                ? Instantiate(itemPrefab, container)
                : CreateDefaultItem(container);

            RankTop100ItemUI itemUI = item.GetComponent<RankTop100ItemUI>();
            if (itemUI == null)
                itemUI = item.AddComponent<RankTop100ItemUI>();

            itemUI.SetData(
                rank,
                FormatPlayerName(entry),
                ResolveScore(entry),
                GetFrameSprite(rank),
                GetFallbackFrameColor(rank),
                rank <= 3 ? topRowHeight : defaultRowHeight);
        }

        StartCoroutine(RebuildLayoutNextFrame());
    }

    private GameObject CreateDefaultItem(Transform parent)
    {
        GameObject row = new("RankFriends_Item", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement), typeof(RankTop100ItemUI));
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

    private void AddSelfEntry(List<FriendRankEntry> entries)
    {
        string selfPlayerId = PlayerPrefs.GetString("player_id", "");
        string selfName = PlayerPrefs.GetString("username", "Bạn");

        if (!string.IsNullOrWhiteSpace(selfPlayerId) &&
            entries.Any(e => e.playerId == selfPlayerId))
            return;

        entries.Add(new FriendRankEntry
        {
            playerId = selfPlayerId,
            playerName = string.IsNullOrWhiteSpace(selfName) ? "Bạn" : selfName,
            isSelf = true
        });
    }

    private void FillMissingScores(List<FriendRankEntry> entries)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (ResolveScore(entries[i]) > 0)
                continue;

            bool isSelf = IsSelf(entries[i]);
            entries[i].score = isSelf ? 2500 : Mathf.Max(1000, 2380 - i * 27);
        }
    }

    private List<FriendRankEntry> CreateSampleEntries()
    {
        List<FriendRankEntry> entries = new();
        AddSelfEntry(entries);

        int count = Mathf.Clamp(sampleFriendCount, 0, 99);
        for (int i = 0; i < count; i++)
        {
            entries.Add(new FriendRankEntry
            {
                playerId = $"sample_friend_{i + 1}",
                playerName = $"Friend {i + 1:00}",
                score = Mathf.Max(1000, 2380 - i * 27)
            });
        }

        return entries
            .OrderByDescending(ResolveScore)
            .ThenBy(e => e.playerName)
            .ToList();
    }

    private static string FormatPlayerName(FriendRankEntry entry)
    {
        string name = string.IsNullOrWhiteSpace(entry.playerName) ? "Unknown Trainer" : entry.playerName;
        return IsSelf(entry) ? $"{name} (Bạn)" : name;
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

    private static int ResolveScore(FriendRankEntry entry)
    {
        if (entry.score > 0) return entry.score;
        if (entry.rankPoints > 0) return entry.rankPoints;
        if (entry.mmr > 0) return entry.mmr;
        return entry.rankedWins;
    }

    private static bool IsSelf(FriendRankEntry entry)
    {
        if (entry.isSelf)
            return true;

        string selfPlayerId = PlayerPrefs.GetString("player_id", "");
        return !string.IsNullOrWhiteSpace(selfPlayerId) && entry.playerId == selfPlayerId;
    }

    [Serializable]
    public class FriendRankEntry
    {
        public string playerId;
        public string playerName;
        public int rank;
        public bool isOnline;
        public string lastSeenAt;
        public int score;
        public int rankPoints;
        public int mmr;
        public int rankedWins;
        public int rankedMatches;
        public bool isSelf;
    }
}
