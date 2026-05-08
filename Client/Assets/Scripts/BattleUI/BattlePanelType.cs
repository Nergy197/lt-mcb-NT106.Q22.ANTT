namespace Game.Battle.UI
{
    public enum BattlePanelType
    {
        None,
        Command,     // 4 nút (Fight, Pokemon, Info, Forfeit)
        Skill,       // 4 chiêu + nút Terastallize
        Dialog,      // Tường thuật
        TeamPreview, // Chọn 4/6 Pokemon trước trận
        Party,       // Bench picker (switch tự nguyện / forced switch)
        Result,      // Kết quả thắng / thua
    }
}
