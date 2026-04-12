using UnityEngine;
using UnityEngine.UI;
using TMPro; // Dùng TextMeshPro

// 1. Khai báo các Hệ chiêu thức (Bạn có thể thêm bớt tùy game của bạn)
// Khai báo đầy đủ 18 Hệ chiêu thức trong Pokémon
public enum MoveType
{
    Normal,     // Thường
    Fire,       // Lửa
    Water,      // Nước
    Grass,      // Cỏ
    Electric,   // Điện
    Ice,        // Băng
    Fighting,   // Giác đấu
    Poison,     // Độc
    Ground,     // Đất
    Flying,     // Bay
    Psychic,    // Siêu năng
    Bug,        // Bọ
    Rock,       // Đá
    Ghost,      // Ma
    Dragon,     // Rồng
    Dark,       // Bóng tối
    Steel,      // Thép
    Fairy       // Tiên
}

// 2. Cấu trúc lưu dữ liệu của 1 chiêu thức (Tên + Hệ)
[System.Serializable]
public struct MoveData
{
    public string moveName;
    public MoveType moveType;
}

// 3. Cấu trúc để map Hệ với Sprite trong Inspector
[System.Serializable]
public struct TypeSpriteMapping
{
    public MoveType type;
    public Sprite backgroundSprite;
}

public class BattleFightMenu : MonoBehaviour
{
    [Header("UI References (Kéo Object vào đây)")]
    public Button[] moveButtons = new Button[4];
    public TextMeshProUGUI[] moveTexts = new TextMeshProUGUI[4];

    [Header("Sprite Settings (Kéo Sprite từ folder vào đây)")]
    // Tạo danh sách để bạn gán hình ảnh cho từng hệ
    public TypeSpriteMapping[] typeSprites;

    private void Start()
    {
        // TẠM THỜI: Set sẵn 4 chiêu (Kèm theo hệ) để test giao diện
        MoveData[] testMoves = new MoveData[]
        {
            new MoveData { moveName = "Thunderbolt", moveType = MoveType.Electric },
            new MoveData { moveName = "Quick Attack", moveType = MoveType.Normal },
        };
        
        SetupMoves(testMoves);
    }

    /// <summary>
    /// Hàm truyền dữ liệu chiêu thức (Tên + Hình ảnh hệ) vào 4 nút
    /// </summary>
    public void SetupMoves(MoveData[] moves)
    {
        for (int i = 0; i < moveButtons.Length; i++)
        {
            if (i < moves.Length)
            {
                // Hiện nút lên
                moveButtons[i].gameObject.SetActive(true);
                
                // 1. Set tên chiêu thức
                moveTexts[i].text = moves[i].moveName;
                
                // 2. Set hình nền (Sprite) dựa vào hệ
                Sprite typeSprite = GetSpriteForType(moves[i].moveType);
                if (typeSprite != null)
                {
                    // Component Button trong Unity có sẵn thuộc tính 'image' trỏ tới Image component của nó
                    moveButtons[i].image.sprite = typeSprite;
                }
            }
            else
            {
                // Nếu Pokémon chỉ có 2 hoặc 3 chiêu, ẩn các nút thừa đi
                moveButtons[i].gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Hàm phụ trợ: Tìm đúng Sprite tương ứng với Hệ được truyền vào
    /// </summary>
    private Sprite GetSpriteForType(MoveType typeToFind)
    {
        foreach (var mapping in typeSprites)
        {
            if (mapping.type == typeToFind)
            {
                return mapping.backgroundSprite;
            }
        }
        
        Debug.LogWarning("Chưa gán Sprite cho hệ: " + typeToFind);
        return null;
    }
}