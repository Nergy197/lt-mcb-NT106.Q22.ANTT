using TMPro;
using UnityEngine;

public class VPTextUI : MonoBehaviour
{
    [SerializeField] private TMP_Text vpText;

    private void Awake()
    {
        if (vpText == null)
            vpText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        if (VPManager.Instance != null)
        {
            VPManager.Instance.OnVPChanged += UpdateVPText;
            UpdateVPText(VPManager.Instance.CurrentVP); // cập nhật ngay khi bật UI
        }
    }

    private void OnDisable()
    {
        if (VPManager.Instance != null)
            VPManager.Instance.OnVPChanged -= UpdateVPText;
    }

    private void UpdateVPText(int value)
    {
        vpText.text = $"{value:N0} VP"; // ví dụ: 5,000 VP
        // muốn không dấu phẩy: vpText.text = $"{value} VP";
    }

}
