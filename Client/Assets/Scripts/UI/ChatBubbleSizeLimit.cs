using UnityEngine;
using UnityEngine.UI;
using TMPro;

[ExecuteAlways]
[RequireComponent(typeof(LayoutElement))]
public class ChatBubbleSizeLimit : MonoBehaviour
{
    [Tooltip("Text component của tin nhắn")]
    public TextMeshProUGUI textComponent;
    
    [Tooltip("Chiều rộng tối đa trước khi tự động xuống dòng")]
    public float maxWidth = 450f; 
    
    private LayoutElement layoutElement;

    void Awake()
    {
        layoutElement = GetComponent<LayoutElement>();
        if (textComponent == null) 
            textComponent = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        if (textComponent == null || layoutElement == null) return;
        
        // Lấy chiều rộng mong muốn của text (nếu hiển thị trên 1 dòng)
        float currentPrefWidth = textComponent.preferredWidth;

        // Nếu text dài hơn maxWidth -> Bật preferredWidth của LayoutElement để ép nó xuống dòng
        if (currentPrefWidth > maxWidth)
        {
            layoutElement.preferredWidth = maxWidth;
        }
        else
        {
            // Nếu text ngắn -> Tắt preferredWidth để khung tự co nhỏ lại vừa với chữ
            layoutElement.preferredWidth = -1; 
        }
    }
}
