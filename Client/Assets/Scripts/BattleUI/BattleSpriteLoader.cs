using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Game.Battle.Logic
{
    public class BattleSpriteLoader : MonoBehaviour
    {
        // Nhận thêm tham số "hudName" để biết gắn ảnh icon mini vào thanh máu nào
        public void LoadSpriteForSlot(string slotName, string hudName, int dexNumber, bool isBackSprite)
        {
            StartCoroutine(DownloadSpriteCoroutine(slotName, hudName, dexNumber, isBackSprite));
        }

        private IEnumerator DownloadSpriteCoroutine(string slotName, string hudName, int dexNumber, bool isBackSprite)
        {
            // ================== BƯỚC 1: TẢI SPRITE 3D ĐỨNG TRÊN CHUỒNG ĐẤU ==================
            string mainUrl = isBackSprite ? 
                $"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/back/{dexNumber}.png" :
                $"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/{dexNumber}.png";

            using (UnityWebRequest uwr = UnityWebRequest.Get(mainUrl))
            {
                yield return uwr.SendWebRequest();

                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    byte[] imageBytes = uwr.downloadHandler.data;
                    Texture2D texture = new Texture2D(2, 2);
                    texture.LoadImage(imageBytes); 
                    texture.filterMode = FilterMode.Point; 
                    
                    Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                    
                    GameObject slot = GameObject.Find(slotName);
                    if (slot != null && slot.TryGetComponent<SpriteRenderer>(out var sr))
                    {
                        sr.sprite = sprite;
                        sr.color = Color.white; 
                        var txtObj = slot.transform.Find("Label_Slot");
                        if (txtObj) txtObj.gameObject.SetActive(false);
                        
                        // [ FIX ] Không hardcode Scale nữa. Preserve Transform Scale của Editor để User tự căng chỉnh!
                    }
                }
            }

            // ================== BƯỚC 2: TẢI ICON MINI THẾ HỆ 8 CHUYÊN ĐỂ GẮN VÀO THANH MÁU ==================
            string iconUrl = $"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/versions/generation-viii/icons/{dexNumber}.png";
            using (UnityWebRequest uwrIcon = UnityWebRequest.Get(iconUrl))
            {
                yield return uwrIcon.SendWebRequest();

                if (uwrIcon.result == UnityWebRequest.Result.Success)
                {
                    byte[] imageBytes = uwrIcon.downloadHandler.data;
                    Texture2D texture = new Texture2D(2, 2);
                    texture.LoadImage(imageBytes); 
                    texture.filterMode = FilterMode.Point; 
                    
                    Sprite iconSp = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    
                    GameObject hudObj = GameObject.Find(hudName);
                    if (hudObj != null)
                    {
                        Transform iconBox = hudObj.transform.Find("Avatar_Box/Icon");
                        if (iconBox != null && iconBox.TryGetComponent<Image>(out var img))
                        {
                            img.sprite = iconSp;
                            img.color = Color.white; 
                            
                            // [ FIX ] Không ép Scale 1.8x nữa. Chừa quyền chỉnh kích cỡ Icon cho bạn trên Editor.
                        }
                    }
                }
            }
        }
    }
}
