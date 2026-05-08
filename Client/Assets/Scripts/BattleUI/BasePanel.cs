using UnityEngine;

namespace Game.Battle.UI
{
    /// <summary>
    /// Lớp cơ sở cho mọi Panel (Command, Skill, Target). 
    /// Giúp tính mở rộng cao (Resilience): khi thêm GUI mới ta chỉ việc tạo class con kế thừa BasePanel.
    /// </summary>
    public abstract class BasePanel : MonoBehaviour
    {
        public BattlePanelType PanelType;

        public virtual void Show()
        {
            gameObject.SetActive(true);
        }

        public virtual void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
