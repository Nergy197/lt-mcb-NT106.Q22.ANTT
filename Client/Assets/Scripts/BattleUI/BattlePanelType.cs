namespace Game.Battle.UI
{
    public enum BattlePanelType
    {
        None,
        Command,    // Bảng 4 nút (Fight, Pokemon, Info, Forfeit)
        Skill,      // Bảng 4 chiêu
        Target,     // Chọn quái VGC (Double)
        Dialog,     // Tường thuật
        Info,       // VGC Battle Info (Weather, Trick Room...)
        TeamPreview // Bắt đầu trận 90 giây chọn 4/6
    }
}
