using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // Bắt buộc phải có để xử lý sự kiện

// Script này phải được gắn vào đối tượng có component Button.
// Nó thực hiện các Interface IPointerDownHandler và IPointerUpHandler để bắt sự kiện nhấn/thả chuột.
public class UIButtonPressedEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    // Tham chiếu đến GameObject con chứa hình ảnh hiệu ứng nhấn.
    [Header("References")]
    [SerializeField] private GameObject pressedEffectObject; // Kéo GameObject con chứa hiệu ứng vào đây trong Inspector.

    // Hàm được gọi khi nút chuột được nhấn xuống trên đối tượng.
    public void OnPointerDown(PointerEventData eventData)
    {
        // Kiểm tra xem tham chiếu có tồn tại không.
        if (pressedEffectObject != null)
        {
            // Bật đối tượng chứa hiệu ứng để hiển thị nó.
            pressedEffectObject.SetActive(true);
        }
    }

    // Hàm được gọi khi nút chuột được thả ra (kể cả khi con trỏ đã di chuyển ra ngoài nút).
    public void OnPointerUp(PointerEventData eventData)
    {
        if (pressedEffectObject != null)
        {
            // Tắt đối tượng chứa hiệu ứng để ẩn nó đi.
            pressedEffectObject.SetActive(false);
        }
    }

    // Đảm bảo hiệu ứng được ẩn khi bắt đầu (để đề phòng bạn quên tắt nó trong Inspector).
    private void Start()
    {
        if (pressedEffectObject != null)
        {
            pressedEffectObject.SetActive(false);
        }
    }
}