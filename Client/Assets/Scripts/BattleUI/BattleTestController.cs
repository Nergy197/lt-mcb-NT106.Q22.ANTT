using System.Collections;
using UnityEngine;
using Game.Battle.UI;

namespace Game.Battle.Logic
{
    /// <summary>
    /// Script điều khiển toàn bộ trận chiến theo phong cách Turn-based RPG chuẩn.
    /// Nó như GameController, toàn quyền quyết định khi nào thì in chữ, chừng nào thì mở UI.
    /// </summary>
    public class BattleTestController : MonoBehaviour
    {
        [Header("Liên kết UI")]
        public EntityHUD playerHUD;
        public EntityHUD enemyHUD;

        [Header("Dữ liệu tạm của trận đấu")]
        private int playerHp = 500;
        private int playerMaxHp = 500;

        private int enemyHp = 1500;
        private int enemyMaxHp = 1500;

        // Cờ tín hiệu chờ Event
        private bool isTextFinished = false;
        private int chosenSkillIndex = -1;

        private void OnEnable()
        {
            BattleEvents.OnActionCompleted += DialogFinishedCallback;
            BattleEvents.OnPlayerUseSkill += SkillChosenCallback;
        }

        private void OnDisable()
        {
            BattleEvents.OnActionCompleted -= DialogFinishedCallback;
            BattleEvents.OnPlayerUseSkill -= SkillChosenCallback;
        }

        private void DialogFinishedCallback() => isTextFinished = true;
        private void SkillChosenCallback(int index) => chosenSkillIndex = index;

        private void Start()
        {
            // BattleTestController da bi vo hieu hoa.
            // Toan bo logic tran dau duoc xu ly boi BattleNetworkController.
            Debug.Log("[BattleTest] Disabled - using BattleNetworkController for real battles.");
            this.enabled = false;
            return;
        }

        // Hàm tiện ích: In Text và chờ cho đến khi gõ xong mới chạy code tiếp theo
        private IEnumerator PrintAndAwaitText(string text)
        {
            isTextFinished = false;
            // BẬT CỜ BÁO AUTO CLOSE LÀ TRUE ! Để nó chạy chữ xong tự đóng luôn.
            BattleEvents.OnPrintDialog?.Invoke(text, true);
            
            // Đợi cho đến khi Typewriter nháy xong chữ cuối
            yield return new WaitUntil(() => isTextFinished);
            
            // Chờ người chơi đọc text 1.6 giây (Khớp hoàn toàn với thời gian delay đóng hộp thoại sinh ra bên DialogPanel)
            // Nhờ đó bảo đảm Hộp thoại tự thu mình về, thì Trận đấu mới trừ máu!
            yield return new WaitForSeconds(1.6f); 
        }

        private IEnumerator BattleLoop()
        {
            // 1. INTRO
            yield return new WaitForSeconds(1f); // Load scene đợi xíu
            yield return PrintAndAwaitText("Một Mewtwo hoang dã cực tĩnh lặng xuất hiện!");
            yield return PrintAndAwaitText("Tiến lên Charizard, chiến đấu thôi!");

            // 2. VÒNG LẶP CHIẾN ĐẤU (BATTLE LOOP)
            while (playerHp > 0 && enemyHp > 0)
            {
                // A. LƯỢT NGƯỜI CHƠI
                chosenSkillIndex = -1;
                BattleEvents.OnPlayerTurnStart?.Invoke(); // Gọi mở UI

                // Dừng toàn bộ code ở đây, chờ cho đến khi Button chọn Skill được người ta bấm
                yield return new WaitUntil(() => chosenSkillIndex != -1);

                // Sau khi bấm xong, Skill Panel sẽ tự ẩn, ta chạy Text:
                string skillName = GetSkillName(chosenSkillIndex);
                yield return PrintAndAwaitText($"Charizard thi triển [ {skillName} ] mạnh mẽ!");
                
                // Trừ máu Địch
                int damage = Random.Range(300, 650);
                enemyHp -= damage;
                if (enemyHp < 0) enemyHp = 0;
                
                // Tung event UI tụt máu mượt
                BattleEvents.OnHealthChanged?.Invoke("Enemy", enemyHp, enemyMaxHp);
                
                // Đợi 1 tí cho hiệu ứng máu chạy
                yield return new WaitForSeconds(1f); 

                // Nếu máu hết thì thoát khỏi vòng lặp
                if (enemyHp <= 0) break;

                // B. LƯỢT KẺ ĐỊCH
                yield return PrintAndAwaitText("Mewtwo phản công sắc lẹm với [ Tia Sáng Vũ Trụ ]!");
                
                int eDamage = Random.Range(150, 300);
                playerHp -= eDamage;
                if (playerHp < 0) playerHp = 0;

                BattleEvents.OnHealthChanged?.Invoke("Player", playerHp, playerMaxHp);
                yield return new WaitForSeconds(1f);
            }

            // 3. OUTRO
            if (playerHp <= 0)
            {
                yield return PrintAndAwaitText("Charizard đã gục ngã... Bạn đã thua cuộc.");
            }
            else
            {
                yield return PrintAndAwaitText("Tuyệt vời! Bạn đã hạ gục Mewtwo!");
            }
        }

        private string GetSkillName(int index)
        {
            switch (index)
            {
                case 0: return "Phun Lửa";
                case 1: return "Cào Xé";
                case 2: return "Bão Rồng";
                case 3: return "Lốc Xoáy Bão Bay";
                default: return "Cú Đánh Bí Ẩn";
            }
        }
    }
}
