using System;

namespace Game.Battle.UI
{
    public static class BattleEvents
    {
        public static Action<string, int, int> OnHealthChanged;
        public static Action<string, bool> OnPrintDialog;
        public static Action OnPlayerTurnStart;
        public static Action OnActionCompleted;

        // SỰ KIỆN MỚI: Bắn tín hiệu chọn xong Skill về Logic Tổng
        public static Action<int> OnPlayerUseSkill; 
    }
}
