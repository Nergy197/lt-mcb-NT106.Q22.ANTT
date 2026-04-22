using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class ChatUIFixer
{
    [MenuItem("Tools/Fix Chat Prefabs (Click Here!)")]
    public static void RunFix()
    {
        // Tìm FriendMessageItem
        GameObject friendMsg = GameObject.Find("FriendMessageItem");
        if (friendMsg == null)
        {
            EditorUtility.DisplayDialog("Lỗi", "Không tìm thấy 'FriendMessageItem' trong Scene. Hãy chắc chắn bạn đang mở Menu scene.", "OK");
            return;
        }

        Debug.Log("Đang tự động cấu hình Layout cho FriendMessageItem...");

        // 1. Cấu hình FriendMessageItem (Khuôn A)
        ConfigureFriendMessage(friendMsg);

        // 2. Tạo và cấu hình MyMessageItem (Khuôn B) nếu chưa có
        GameObject myMsg = GameObject.Find("MyMessageItem");
        if (myMsg == null)
        {
            myMsg = GameObject.Instantiate(friendMsg, friendMsg.transform.parent);
            myMsg.name = "MyMessageItem";
        }
        ConfigureMyMessage(myMsg);

        // Chỉnh sửa thêm cho Content (nếu Content bị bóp méo FriendMessageItem)
        if (friendMsg.transform.parent != null && friendMsg.transform.parent.name == "Content")
        {
            var contentLayout = GetOrAddComponent<VerticalLayoutGroup>(friendMsg.transform.parent.gameObject);
            contentLayout.childControlHeight = false; // ĐỂ TIN NHẮN KHÔNG BỊ ÉP CHIỀU CAO VỀ 0
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childForceExpandWidth = true;

            var contentFitter = GetOrAddComponent<ContentSizeFitter>(friendMsg.transform.parent.gameObject);
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        // Đánh dấu scene đã thay đổi để lưu lại
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Thành công", "Đã fix xong! Hãy xem sự thay đổi của FriendMessageItem và MyMessageItem.", "Tuyệt vời");
        Debug.Log("✅ Đã hoàn thành Giai đoạn 1: Dựng Khuôn Tin Nhắn!");
    }

    static void ConfigureFriendMessage(GameObject item)
    {
        // Xóa VerticalLayoutGroup nếu có (vì đây là ngang)
        var oldVLayout = item.GetComponent<VerticalLayoutGroup>();
        if (oldVLayout != null) GameObject.DestroyImmediate(oldVLayout);

        // 1. Cấu hình phần tử gốc (FriendMessageItem)
        var hLayout = GetOrAddComponent<HorizontalLayoutGroup>(item);
        hLayout.childAlignment = TextAnchor.UpperLeft;
        hLayout.childControlWidth = false; // KHÔNG ÉP BUBBLE HAY AVATAR
        hLayout.childControlHeight = false;
        hLayout.childForceExpandWidth = false;
        hLayout.childForceExpandHeight = false;
        hLayout.spacing = 10;
        hLayout.padding = new RectOffset(10, 10, 10, 10);

        var fitter = GetOrAddComponent<ContentSizeFitter>(item);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Xóa LayoutElement trên item gốc nếu có (vì fitter lo rồi)
        var oldLE = item.GetComponent<LayoutElement>();
        if (oldLE != null) GameObject.DestroyImmediate(oldLE);

        // 2. Cấu hình Avatar
        Transform avatar = item.transform.Find("Avatar");
        if (avatar != null)
        {
            var layoutElement = GetOrAddComponent<LayoutElement>(avatar.gameObject);
            layoutElement.minWidth = 50;
            layoutElement.minHeight = 50;
            layoutElement.preferredWidth = 50;
            layoutElement.preferredHeight = 50;
            layoutElement.flexibleWidth = 0;
            layoutElement.flexibleHeight = 0;
        }

        // 3. Cấu hình Bubble
        Transform bubble = item.transform.Find("Bubble");
        if (bubble != null)
        {
            var bLayout = GetOrAddComponent<HorizontalLayoutGroup>(bubble.gameObject);
            bLayout.childAlignment = TextAnchor.MiddleLeft;
            bLayout.childControlWidth = true; // Cho phép điều khiển text bên trong
            bLayout.childControlHeight = true;
            bLayout.childForceExpandWidth = false;
            bLayout.childForceExpandHeight = false;
            bLayout.padding = new RectOffset(15, 15, 10, 10);

            var bFitter = GetOrAddComponent<ContentSizeFitter>(bubble.gameObject);
            bFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            bFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var bImage = bubble.GetComponent<Image>();
            if (bImage != null)
            {
                bImage.color = Color.white;
            }

            // 4. Cấu hình MessageText
            Transform text = bubble.Find("MessageText");
            if (text != null)
            {
                var txtComp = text.GetComponent<TextMeshProUGUI>();
                if (txtComp != null)
                {
                    txtComp.enableWordWrapping = true;
                    txtComp.alignment = TextAlignmentOptions.TopLeft;
                    txtComp.color = Color.black;
                }

                var layoutElement = GetOrAddComponent<LayoutElement>(text.gameObject);
                layoutElement.minWidth = 0;
                layoutElement.minHeight = 0;
                layoutElement.flexibleWidth = 0;
                layoutElement.flexibleHeight = 0;
                
                var sizeLimit = text.GetComponent("ChatBubbleSizeLimit") as MonoBehaviour;
                if (sizeLimit == null)
                {
                    System.Type type = System.Type.GetType("ChatBubbleSizeLimit, Assembly-CSharp");
                    if (type != null) text.gameObject.AddComponent(type);
                }
            }
        }
    }

    static void ConfigureMyMessage(GameObject item)
    {
        var hLayout = item.GetComponent<HorizontalLayoutGroup>();
        if (hLayout != null)
        {
            hLayout.childAlignment = TextAnchor.UpperRight;
        }

        Transform avatar = item.transform.Find("Avatar");
        if (avatar != null)
        {
            GameObject.DestroyImmediate(avatar.gameObject);
        }

        Transform bubble = item.transform.Find("Bubble");
        if (bubble != null)
        {
            var bImage = bubble.GetComponent<Image>();
            if (bImage != null)
            {
                Color myColor;
                ColorUtility.TryParseHtmlString("#0084FF", out myColor); 
                bImage.color = myColor;
            }

            Transform text = bubble.Find("MessageText");
            if (text != null)
            {
                var txtComp = text.GetComponent<TextMeshProUGUI>();
                if (txtComp != null)
                {
                    txtComp.color = Color.white;
                    txtComp.alignment = TextAlignmentOptions.TopRight;
                }
            }
        }
    }

    static T GetOrAddComponent<T>(GameObject obj) where T : Component
    {
        T comp = obj.GetComponent<T>();
        if (comp == null)
            comp = obj.AddComponent<T>();
        return comp;
    }
}
