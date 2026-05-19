#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class FriendListScrollViewMenu
{
    private const string FriendRowPrefabPath = "Assets/Prefabs/Friend(List-Friend).prefab";

    [MenuItem("GameObject/UI/Friends List Scroll View", false, 11)]
    private static void CreateFriendsListScrollView(MenuCommand menuCommand)
    {
        Transform parentTransform = menuCommand.context as Transform;
        if (parentTransform == null && Selection.activeTransform != null)
            parentTransform = Selection.activeTransform;

        GameObject parentObject = parentTransform != null ? parentTransform.gameObject : null;

        GameObject scrollRoot = new("Friends_ScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        Undo.RegisterCreatedObjectUndo(scrollRoot, "Create Friends List Scroll View");
        GameObjectUtility.SetParentAndAlign(scrollRoot, parentObject);

        RectTransform scrollRect = scrollRoot.GetComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(24f, 24f);
        scrollRect.offsetMax = new Vector2(-24f, -24f);

        Image scrollImage = scrollRoot.GetComponent<Image>();
        scrollImage.color = new Color(1f, 1f, 1f, 0.01f);
        scrollImage.raycastTarget = true;

        GameObject viewport = new("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        Undo.RegisterCreatedObjectUndo(viewport, "Create Viewport");
        GameObjectUtility.SetParentAndAlign(viewport, scrollRoot);

        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewportRect.pivot = new Vector2(0f, 1f);

        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = Color.white;
        Mask mask = viewport.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject content = new(
            "FriendsList_Container",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter),
            typeof(FriendListLoader));
        Undo.RegisterCreatedObjectUndo(content, "Create Content");
        GameObjectUtility.SetParentAndAlign(content, viewport);

        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 8, 8);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        FriendListLoader loader = content.GetComponent<FriendListLoader>();
        loader.container = content.transform;
        loader.friendPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FriendRowPrefabPath);

        ScrollRect scroll = scrollRoot.GetComponent<ScrollRect>();
        scroll.content = contentRect;
        scroll.viewport = viewportRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 20f;

        Selection.activeGameObject = scrollRoot;
    }
}
#endif
