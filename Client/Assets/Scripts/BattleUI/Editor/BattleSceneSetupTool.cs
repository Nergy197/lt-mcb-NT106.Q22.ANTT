#if UNITY_EDITOR 
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro; 
using Game.Battle.UI;
using Game.Battle.Logic;

namespace Game.Battle.EditorTools
{
    public class BattleSceneSetupTool
    {
        [MenuItem("Tools/Battle UI/🏆 Xây UI Giống Ảnh Pokemon Champion Nhất")]
        public static void GenerateBattleScene()
        {
            // Cài đặt Camera nền nhìn xịn hơn (Dark Mode)
            if (Camera.main != null)
            {
                Camera.main.backgroundColor = new Color(0.12f, 0.15f, 0.2f); // Xám đen bóng đêm
            }

            // ==========================================
            // 0. ARENA (NƠI ĐỨNG CỦA SPRITE POKEMON 3D/2D)
            // ==========================================
            GameObject arenaRoot = new GameObject("🌍 BATTLE ARENA (Thả quái vật vào đây)");
            
            // Khôi phục lại tọa độ gốc theo ý bạn, các khoảng cách ban đầu đã hài hòa sẵn
            CreatePokemonSlot(arenaRoot.transform, "Player_Lead_Slot", new Vector3(-3f, -1.5f, 0), true);
            CreatePokemonSlot(arenaRoot.transform, "Player_Sub2_Slot", new Vector3(-7f, -3f, 0), true);
            
            CreatePokemonSlot(arenaRoot.transform, "Enemy_Lead_Slot", new Vector3(3f, 1.5f, 0), false);
            CreatePokemonSlot(arenaRoot.transform, "Enemy_Sub2_Slot", new Vector3(7f, 3f, 0), false);

            Canvas canvas = GameObject.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("BattleCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080); 
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // [LỖI SCALE] Ép Canvas UI lồng ghép trực tiếp vào không gian Camera thực tế thay vì chế độ Overlay khổng lồ
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            if (Camera.main != null)
            {
                canvas.worldCamera = Camera.main;
                canvas.planeDistance = 10f; // UI nằm xa mặt Camera 10 Unity Units, Pokemon thì nằm ở tọa độ 0
                Camera.main.orthographic = true;
                Camera.main.orthographicSize = 5f;
            }

            if (GameObject.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            GameObject managerObj = new GameObject("🎮 BattleSystemRoot");
            managerObj.transform.SetParent(canvas.transform, false);
            SetRect(managerObj, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            BattleUIManager uiManager = managerObj.AddComponent<BattleUIManager>();
            uiManager.panels = new System.Collections.Generic.List<BasePanel>();

            GameObject globalHudRoot = new GameObject("GlobalHUDs_Root (Nhóm Quản Lý Tắt Bật Nhanh)");
            globalHudRoot.transform.SetParent(managerObj.transform, false);
            SetRect(globalHudRoot, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            uiManager.globalHudRoot = globalHudRoot; 

            Sprite baseSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            Sprite circleSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            Color oppColor = new Color(0.85f, 0.1f, 0.38f, 0.95f); 
            Color playerColor = new Color(0.2f, 0.2f, 0.8f, 0.95f); 

            // ==========================================
            // 1. THANH MÁU VÀ TIMERS GROUP (Hệ Thống Tĩnh)
            // ==========================================
            GenerateScreenshotHUD(globalHudRoot.transform, "Enemy1_HUD", "Steelix",      oppColor, baseSprite, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-600, -70));
            GenerateScreenshotHUD(globalHudRoot.transform, "Enemy2_HUD", "Drampa",       oppColor, baseSprite, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-250, -70));
            CreateTimers(globalHudRoot.transform, "Enemy_Timer_Pokeball", "07:00", new Vector2(1, 1), new Vector2(-600, -140), true);

            GenerateScreenshotHUD(globalHudRoot.transform, "Player1_HUD", "Delphox",     playerColor, baseSprite, new Vector2(0, 0), new Vector2(0, 0), new Vector2(200, 70));
            GenerateScreenshotHUD(globalHudRoot.transform, "Player2_HUD", "Victreebel",  playerColor, baseSprite, new Vector2(0, 0), new Vector2(0, 0), new Vector2(550, 70));
            CreateTimers(globalHudRoot.transform, "Player_Timer_Pokeball", "06:58", new Vector2(0, 0), new Vector2(250, 140), false);

            // ==========================================
            // 2. NÚT EXIT VÀ DIALOG (BẤT TỬ KHÔNG BAO GIỜ BỊ ẨN)
            // ==========================================
            GameObject forfeitObj = new GameObject("ForfeitBtn");
            forfeitObj.transform.SetParent(managerObj.transform, false);
            SetRect(forfeitObj, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(100, -50), new Vector2(120, 50));
            Image forfeitImg = forfeitObj.AddComponent<Image>(); forfeitImg.sprite = baseSprite; forfeitImg.type = Image.Type.Sliced; forfeitImg.color = new Color(0f, 0f, 0f, 0.8f);
            Button fBtn = forfeitObj.AddComponent<Button>();
            GameObject txtFObj = new GameObject("Text"); txtFObj.transform.SetParent(forfeitObj.transform, false);
            SetRect(txtFObj, new Vector2(0,0), new Vector2(1,1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI tmpF = txtFObj.AddComponent<TextMeshProUGUI>();
            tmpF.text = "[Exit]"; tmpF.color = Color.white; tmpF.fontSize = 24; tmpF.alignment = TextAlignmentOptions.Center;

            GameObject dialogObj = new GameObject("DialogPanel");
            dialogObj.transform.SetParent(managerObj.transform, false);
            SetRect(dialogObj, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(-200, 120));
            BattleDialogPanel dialogPanel = dialogObj.AddComponent<BattleDialogPanel>();
            dialogPanel.PanelType = BattlePanelType.Dialog;
            
            Image dialogBg = dialogObj.AddComponent<Image>();
            dialogBg.sprite = baseSprite; dialogBg.type = Image.Type.Sliced;
            dialogBg.color = new Color(0, 0, 0, 0.85f);

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(dialogObj.transform, false);
            SetRect(textObj, new Vector2(0,0), new Vector2(1,1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-40, -40));
            TextMeshProUGUI logTxt = textObj.AddComponent<TextMeshProUGUI>();
            logTxt.text = ""; logTxt.color = Color.white; logTxt.fontSize = 32; logTxt.alignment = TextAlignmentOptions.Left;
            dialogPanel.dialogText = logTxt;
            uiManager.panels.Add(dialogPanel);

            // ==========================================
            // 3. ACTION VÀ SKILL MENU
            // ==========================================
            GameObject actionPanelObj = new GameObject("ActionMenuPanel (Nút bóng tròn)");
            actionPanelObj.transform.SetParent(managerObj.transform, false);
            SetRect(actionPanelObj, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            
            BattleCommandPanel cmdPanel = actionPanelObj.AddComponent<BattleCommandPanel>();
            cmdPanel.PanelType = BattlePanelType.Command;
            cmdPanel.forfeitButton = fBtn; 

            cmdPanel.infoButton = CreateActionButton(actionPanelObj.transform, "InfoBtn", "<b>MOVE TIME       <size=40>43</size></b>\n<color=#BBBBBB>[X] Battle Info</color>", baseSprite, new Color(0,0,0, 0.6f), new Vector2(1, 0.5f), new Vector2(-150, 0), new Vector2(280, 80), 22);
            cmdPanel.fightButton = CreateCircleButton(actionPanelObj.transform, "FightBtn", "FIGHT", circleSprite, new Color(0.7f, 0.95f, 0f), new Vector2(1, 0), new Vector2(-150, 320), 180);
            cmdPanel.fightButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(0.1f, 0.2f, 0.7f); 
            cmdPanel.pokemonButton = CreateCircleButton(actionPanelObj.transform, "PokemonBtn", "POKÉMON", circleSprite, new Color(0.4f, 0.3f, 0.95f), new Vector2(1, 0), new Vector2(-150, 120), 150);
            
            uiManager.panels.Add(cmdPanel);

            GameObject skillObj = new GameObject("SkillMenuPanel");
            skillObj.transform.SetParent(managerObj.transform, false);
            SetRect(skillObj, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-150, 250), new Vector2(500, 200));
            
            var moveGrid = skillObj.AddComponent<GridLayoutGroup>();
            moveGrid.cellSize = new Vector2(240, 80);
            moveGrid.spacing = new Vector2(15, 15);
            moveGrid.childAlignment = TextAnchor.MiddleCenter;

            // XÂY DỰNG FILE CON TRƯỚC (Để né lỗi Awake() quá sớm khi cài script vào)
            Button[] stBtns = new Button[4];
            stBtns[0] = CreateMoveButton(skillObj.transform, "Move1", "Mystical Fire", baseSprite, new Color(0.9f, 0.4f, 0.2f));
            stBtns[1] = CreateMoveButton(skillObj.transform, "Move2", "Psychic", baseSprite, new Color(0.9f, 0.3f, 0.6f));
            stBtns[2] = CreateMoveButton(skillObj.transform, "Move3", "Energy Ball", baseSprite, new Color(0.4f, 0.8f, 0.3f));
            stBtns[3] = CreateMoveButton(skillObj.transform, "Move4", "Protect", baseSprite, new Color(0.6f, 0.6f, 0.6f));
            Button bBtn = CreateActionButton(skillObj.transform, "BackBtn", "CANCEL", baseSprite, new Color(0.2f,0.2f,0.2f), new Vector2(0.5f, 0), new Vector2(0, -60), new Vector2(200, 50), 24);
            
            // GẮN SCRIPT VÀO SAU KHI ĐÃ CẦM ĐỦ 4 CON
            BattleSkillPanel skillPanel = skillObj.AddComponent<BattleSkillPanel>();
            skillPanel.PanelType = BattlePanelType.Skill;
            skillPanel.skillButtons = stBtns;
            skillPanel.backButton = bBtn;
            
            uiManager.panels.Add(skillPanel);

            BattleTestController controller = managerObj.AddComponent<BattleTestController>();
            controller.playerHUD = globalHudRoot.transform.Find("Player1_HUD").GetComponent<EntityHUD>();
            controller.enemyHUD = globalHudRoot.transform.Find("Enemy1_HUD").GetComponent<EntityHUD>();

            Selection.activeGameObject = managerObj;
            Debug.Log("<color=green>Update Bãi Đấu Pokemon Hoàn Tất!</color> Đã tạo các Slot đứng quái vật trong không gian 3D/2D.");
        }

        // ==========================================
        // CÁC HÀM TIỆN ÍCH DÀN TRANG
        // ==========================================
        private static void CreatePokemonSlot(Transform parent, string name, Vector3 pos, bool isPlayer)
        {
            GameObject slot = new GameObject(name);
            slot.transform.SetParent(parent);
            slot.transform.position = pos;
            
            // [CẬP NHẬT] Đặt Scale khối to đùng mặc định trước để User thấy mà bấm vào
            slot.transform.localScale = new Vector3(5f, 5f, 1f); 

            // Dùng khối ngọc đặc (UIMask) thay vì khối trong suốt Background
            SpriteRenderer sr = slot.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd");
            sr.color = isPlayer ? new Color(0.2f, 0.3f, 1f, 0.5f) : new Color(1f, 0.3f, 0.2f, 0.5f);
            
            // Trả lại Sorting Fake đơn giản như phiên bản gốc
            sr.sortingOrder = pos.y > 0 ? -1 : 1; 

            // Cái bóng đen dưới đít giống trong ảnh Screenshot
            GameObject shadow = new GameObject("Ground_Shadow");
            shadow.transform.SetParent(slot.transform);
            shadow.transform.localPosition = new Vector3(0, -0.6f, 0);
            shadow.transform.localScale = new Vector3(1f, 0.3f, 1f);
            SpriteRenderer shadowSr = shadow.AddComponent<SpriteRenderer>();
            shadowSr.color = new Color(0, 0, 0, 0.4f);
            shadowSr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            
            // Text nổi 3D
            GameObject txtObj = new GameObject("Label_Slot");
            txtObj.transform.SetParent(slot.transform);
            txtObj.transform.localPosition = new Vector3(0, 0.8f, 0);
            txtObj.transform.localScale = new Vector3(0.04f, 0.04f, 1f); 
            TextMeshPro text = txtObj.AddComponent<TextMeshPro>();
            text.text = isPlayer ? "[Kéo thả\nPokemon Ta]" : "[Kéo thả\nPokemon Địch]";
            text.fontSize = 24; text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
        }

        private static void SetRect(GameObject go, Vector2 aMin, Vector2 aMax, Vector2 p, Vector2 pos, Vector2 size)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = p;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        private static void GenerateScreenshotHUD(Transform parent, string name, string pokeName, Color bgColor, Sprite bgSprite, Vector2 anchor, Vector2 pivot, Vector2 pos)
        {
            GameObject hudObj = new GameObject(name);
            hudObj.transform.SetParent(parent, false);
            SetRect(hudObj, anchor, anchor, pivot, pos, new Vector2(320, 80));

            EntityHUD hud = hudObj.AddComponent<EntityHUD>();
            hud.entityId = pokeName;

            Image bg = hudObj.AddComponent<Image>();
            bg.sprite = bgSprite; bg.type = Image.Type.Sliced;
            bg.color = bgColor;
            Outline outL = hudObj.AddComponent<Outline>();
            outL.effectColor = Color.white; outL.effectDistance = new Vector2(2, -2);

            GameObject avtObj = new GameObject("Avatar_Box"); avtObj.transform.SetParent(hudObj.transform, false);
            SetRect(avtObj, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(-20, 0), new Vector2(60, 60));
            Image avtImg = avtObj.AddComponent<Image>(); avtImg.sprite = bgSprite; avtImg.color = new Color(0.9f, 0.9f, 0.6f);
            
            // [CẬP NHẬT MỚI] Đóng hộp cắt viền (Mask) để ảnh mini không bị lòi ra ngoài khung vàng
            avtObj.AddComponent<RectMask2D>(); 
            
            GameObject iconObj = new GameObject("Icon"); iconObj.transform.SetParent(avtObj.transform, false);
            SetRect(iconObj, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60));
            iconObj.AddComponent<Image>().color = new Color(1, 1, 1, 0); // Kính ảo màu trong suốt chờ data nạp vào

            GameObject nameObj = new GameObject("Name"); nameObj.transform.SetParent(hudObj.transform, false);
            SetRect(nameObj, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(30, -10), new Vector2(-60, 40));
            TextMeshProUGUI tmpN = nameObj.AddComponent<TextMeshProUGUI>();
            tmpN.text = $"<i>{pokeName}</i>   <color=#4FC3F7>♂</color>"; 
            tmpN.color = Color.white; tmpN.fontSize = 24; tmpN.fontStyle = FontStyles.Italic | FontStyles.Bold;

            GameObject hpBarBgObj = new GameObject("HP_Fill_BG"); hpBarBgObj.transform.SetParent(hudObj.transform, false);
            SetRect(hpBarBgObj, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(30, 20), new Vector2(-60, 20));
            hpBarBgObj.AddComponent<Image>().color = new Color(0.1f, 0.3f, 0.1f);
            
            GameObject hpFillObj = new GameObject("HP_Fill_Image"); hpFillObj.transform.SetParent(hpBarBgObj.transform, false);
            SetRect(hpFillObj, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image fillImg = hpFillObj.AddComponent<Image>(); 
            fillImg.sprite = bgSprite; fillImg.color = new Color(0.5f, 0.9f, 0.1f);
            fillImg.type = Image.Type.Filled; fillImg.fillMethod = Image.FillMethod.Horizontal; fillImg.fillOrigin = 0; fillImg.fillAmount = 1f;

            hud.hpFillImage = fillImg;

            GameObject hpTextObj = new GameObject("HP_Value"); hpTextObj.transform.SetParent(hpBarBgObj.transform, false);
            SetRect(hpTextObj, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, -15), new Vector2(200, 30));
            hud.hpText = hpTextObj.AddComponent<TextMeshProUGUI>();
            hud.hpText.text = "100 / 100";
            hud.hpText.color = Color.white; hud.hpText.fontSize = 20; hud.hpText.fontStyle = FontStyles.Bold; hud.hpText.alignment = TextAlignmentOptions.Right;
        }

        private static void CreateTimers(Transform parent, string name, string time, Vector2 anchor, Vector2 pos, bool isEnemy)
        {
            GameObject hudObj = new GameObject(name);
            hudObj.transform.SetParent(parent, false);
            SetRect(hudObj, anchor, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(200, 40));

            TextMeshProUGUI tmp = hudObj.AddComponent<TextMeshProUGUI>();
            tmp.text = isEnemy ? $"Time: {time}   <color=#76FF03>OOOO</color>" : $"<color=#76FF03>OOOO</color>   Time: {time}";
            tmp.color = Color.white; tmp.fontSize = 22; tmp.fontStyle = FontStyles.Bold;
        }

        private static Button CreateActionButton(Transform parent, string name, string txt, Sprite bgSprite, Color bgColor, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize)
        {
            GameObject btnObj = new GameObject(name); btnObj.transform.SetParent(parent, false);
            SetRect(btnObj, anchor, anchor, new Vector2(0.5f, 0.5f), pos, size);
            
            Image img = btnObj.AddComponent<Image>(); img.sprite = bgSprite; img.type = Image.Type.Sliced; img.color = bgColor;
            Button btn = btnObj.AddComponent<Button>();

            GameObject txtObj = new GameObject("Text"); txtObj.transform.SetParent(btnObj.transform, false);
            SetRect(txtObj, new Vector2(0,0), new Vector2(1,1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
            tmp.text = txt; tmp.color = Color.white; tmp.fontSize = fontSize; tmp.alignment = TextAlignmentOptions.Center;
            return btn;
        }

        private static Button CreateCircleButton(Transform parent, string name, string txt, Sprite bgSprite, Color bgColor, Vector2 anchor, Vector2 pos, int size)
        {
            GameObject btnObj = new GameObject(name); btnObj.transform.SetParent(parent, false);
            SetRect(btnObj, anchor, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            
            Image img = btnObj.AddComponent<Image>(); img.sprite = bgSprite; img.color = bgColor;
            Outline outL = btnObj.AddComponent<Outline>(); outL.effectColor = Color.white; outL.effectDistance = new Vector2(2, -2); 
            Button btn = btnObj.AddComponent<Button>();

            GameObject txtObj = new GameObject("Text"); txtObj.transform.SetParent(btnObj.transform, false);
            SetRect(txtObj, new Vector2(0,0), new Vector2(1,1), new Vector2(0.5f, 0.5f), new Vector2(0, -20), Vector2.zero);
            TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
            tmp.text = txt; tmp.color = Color.white; tmp.fontSize = 32; tmp.fontStyle = FontStyles.Bold; tmp.alignment = TextAlignmentOptions.Top;
            return btn;
        }

        private static Button CreateMoveButton(Transform parent, string name, string moveName, Sprite bg, Color typeColor)
        {
            GameObject btnObj = new GameObject(name); btnObj.transform.SetParent(parent, false);
            Image img = btnObj.AddComponent<Image>(); img.sprite = bg; img.type = Image.Type.Sliced; img.color = typeColor;
            Button btn = btnObj.AddComponent<Button>();

            GameObject txtObj = new GameObject("Text"); txtObj.transform.SetParent(btnObj.transform, false);
            SetRect(txtObj, new Vector2(0,0), new Vector2(1,1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
            tmp.text = moveName; tmp.color = Color.white; tmp.fontSize = 28; tmp.fontStyle = FontStyles.Bold; tmp.alignment = TextAlignmentOptions.Center;
            return btn;
        }
    }
}
#endif
