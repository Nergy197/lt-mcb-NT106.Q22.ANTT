using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using Game.Battle.UI;
using Game.Battle.Logic;
using Game.Battle.Demo;
using System.Collections.Generic;

namespace Game.Battle.EditorTools {
    public class VGCBattleUIBuilder : EditorWindow {
        [MenuItem("Pokemon/Build VGC Battle Scene (New)")]
        public static void BuildNewScene() {
            if (Camera.main != null) {
                Camera.main.orthographic = true;
                Camera.main.orthographicSize = 5f;
            }

            var root = new GameObject("BATTLE CANVAS", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            root.AddComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            root.AddComponent<GraphicRaycaster>();

            GameObject arena = new GameObject("BATTLE ARENA");
            
            // Đưa Background vào World Space để nằm dưới Pokemon
            GameObject bgObj = new GameObject("Background_Holder (Kéo BG vào Sprite)");
            bgObj.transform.SetParent(arena.transform);
            bgObj.transform.position = new Vector3(0, 0, 10);
            bgObj.transform.localScale = new Vector3(1f, 1f, 1f); // Reset scale về 1 để ảnh tự nhiên
            var bgSr = bgObj.AddComponent<SpriteRenderer>();
            bgSr.sortingOrder = -100; // Đảm bảo luôn nằm dưới cùng
            bgSr.color = Color.white;

            BuildSystemLogic(arena.transform);

            var uiManager = root.AddComponent<BattleUIManager>();
            uiManager.panels = new List<BasePanel>(); // Khởi tạo list

            GameObject hudsRoot = new GameObject("GlobalHUDs_Root", typeof(RectTransform));
            hudsRoot.transform.SetParent(root.transform, false); SetStretch(hudsRoot);
            uiManager.globalHudRoot = hudsRoot; 

            var huds = BuildGlobalHUDs(hudsRoot.transform);
            
            // Tạo PanelManager và nạp panels vào UIManager
            GameObject panelManager = new GameObject("PanelManager", typeof(RectTransform));
            panelManager.transform.SetParent(root.transform, false); SetStretch(panelManager);

            var skill = BuildSkillPanel(panelManager.transform);
            var command = BuildCommandPanel(panelManager.transform);
            var dialog = BuildDialogPanel(panelManager.transform);
            var preview = BuildTeamPreviewPanel(panelManager.transform);
            var party = BuildPartyPanel(panelManager.transform);

            uiManager.panels.Add(skill);
            uiManager.panels.Add(command);
            uiManager.panels.Add(dialog);
            uiManager.panels.Add(preview);
            uiManager.panels.Add(party);

            // Cấu hình NetworkController
            var logicObj = GameObject.Find("BattleSystem_Logic");
            var nc = logicObj.AddComponent<BattleNetworkController>();
            nc.playerHUD1 = huds["Player1"]; nc.playerHUD2 = huds["Player2"];
            nc.enemyHUD1 = huds["Enemy1"]; nc.enemyHUD2 = huds["Enemy2"];
            nc.skillPanel = skill; nc.commandPanel = command; nc.dialogPanel = dialog;
            nc.teamPreviewPanel = preview; nc.partyPanel = party;

            // Tự động tạo EventSystem nếu chưa có
            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null) {
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
            }

            BuildDemo(root.transform);
            Selection.activeGameObject = root;
            Debug.Log("VGC Battle Scene Built Successfully!");
        }

        static void BuildSystemLogic(Transform parent) {
            var go = new GameObject("BattleSystem_Logic");
            go.transform.SetParent(parent);
            go.AddComponent<BattleSpriteLoader>();
            
            string[] slots = { "Player_Lead_Slot", "Player_Sub2_Slot", "Enemy_Lead_Slot", "Enemy_Sub2_Slot" };
            Vector3[] pos = { new(-4, -2, 0), new(-2, -2, 0), new(2, 2, 0), new(4, 2, 0) };
            
            for(int i=0; i<4; i++) {
                var s = new GameObject(slots[i]); s.transform.SetParent(parent); 
                s.transform.position = pos[i];
                s.transform.localScale = new Vector3(5f, 5f, 1f); // Tăng scale Pokemon lên giống tool cũ
                
                var sr = s.AddComponent<SpriteRenderer>(); 
                sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
                sr.color = i < 2 ? new Color(0, 0.5f, 1, 0.5f) : new Color(1, 0, 0, 0.5f);
            }
        }

        static Dictionary<string, EntityHUD> BuildGlobalHUDs(Transform parent) {
            var dict = new Dictionary<string, EntityHUD>();
            dict["Enemy1"] = BuildEntityHUD(parent, "Enemy1_HUD", "Enemy_Lead_Slot", new Vector2(1, 1), new Vector2(-40, -40));
            dict["Enemy2"] = BuildEntityHUD(parent, "Enemy2_HUD", "Enemy_Sub2_Slot", new Vector2(1, 1), new Vector2(-460, -40));
            dict["Player1"] = BuildEntityHUD(parent, "Player1_HUD", "Player_Lead_Slot", new Vector2(0, 0), new Vector2(40, 300));
            dict["Player2"] = BuildEntityHUD(parent, "Player2_HUD", "Player_Sub2_Slot", new Vector2(0, 0), new Vector2(460, 300));
            return dict;
        }

        static EntityHUD BuildEntityHUD(Transform parent, string name, string id, Vector2 anchor, Vector2 pos) {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false); SetRect(go, anchor, anchor, anchor, pos, new Vector2(380, 140));
            go.AddComponent<Image>().color = new Color(0, 0, 0, 0.6f);
            var hud = go.AddComponent<EntityHUD>();
            hud.entityId = id;
            hud.nameText = CreateText(go.transform, "Name", "POKEMON", 18, TextAlignmentOptions.Left);
            SetRect(hud.nameText.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(70, -15), new Vector2(-80, 25));
            hud.levelText = CreateText(go.transform, "Level", "Lv50", 14, TextAlignmentOptions.Left);
            SetRect(hud.levelText.gameObject, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -15), new Vector2(60, 20));
            
            GameObject hpBg = new GameObject("HP_Fill_BG", typeof(RectTransform)); hpBg.transform.SetParent(go.transform, false);
            SetRect(hpBg, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(-40, 20));
            hpBg.AddComponent<Image>().color = Color.gray;
            
            GameObject hpFill = new GameObject("HP_Fill_Image", typeof(RectTransform)); hpFill.transform.SetParent(hpBg.transform, false);
            SetStretch(hpFill); var img = hpFill.AddComponent<Image>(); img.type = Image.Type.Filled; img.fillMethod = Image.FillMethod.Horizontal; img.color = Color.green;
            hud.hpFillImage = img;
            
            hud.hpText = CreateText(hpBg.transform, "HP_Value", "100/100", 12, TextAlignmentOptions.Right);
            SetRect(hud.hpText.gameObject, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, -15), new Vector2(100, 20));

            GameObject iconBox = new GameObject("Avatar_Box", typeof(RectTransform)); iconBox.transform.SetParent(go.transform, false);
            SetRect(iconBox, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(35, 0), new Vector2(60, 60));
            iconBox.AddComponent<Image>().color = new Color(1, 1, 1, 0.1f);
            var icon = new GameObject("Icon", typeof(RectTransform)); icon.transform.SetParent(iconBox.transform, false); SetStretch(icon);
            hud.iconImage = icon.AddComponent<Image>();

            // Type Badges
            var t1Bg = new GameObject("Type1Badge", typeof(RectTransform)); t1Bg.transform.SetParent(go.transform, false);
            SetRect(t1Bg, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 20), new Vector2(45, 18));
            hud.type1BadgeImage = t1Bg.AddComponent<Image>();
            hud.type1BadgeText = CreateText(t1Bg.transform, "Text", "FIRE", 10, TextAlignmentOptions.Center);
            SetStretch(hud.type1BadgeText.gameObject);

            var t2Bg = new GameObject("Type2Badge", typeof(RectTransform)); t2Bg.transform.SetParent(go.transform, false);
            SetRect(t2Bg, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(70, 20), new Vector2(45, 18));
            hud.type2BadgeImage = t2Bg.AddComponent<Image>();
            hud.type2BadgeText = CreateText(t2Bg.transform, "Text", "FLYING", 10, TextAlignmentOptions.Center);
            SetStretch(hud.type2BadgeText.gameObject);

            // Gender Badge
            var gBg = new GameObject("GenderBadge", typeof(RectTransform)); gBg.transform.SetParent(go.transform, false);
            SetRect(gBg, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-10, -10), new Vector2(25, 25));
            hud.genderImage = gBg.AddComponent<Image>();
            hud.genderText = CreateText(gBg.transform, "GenderText", "♂", 16, TextAlignmentOptions.Center);
            SetStretch(hud.genderText.gameObject);

            // Status Badge
            var sBg = new GameObject("StatusBadge", typeof(RectTransform)); sBg.transform.SetParent(go.transform, false);
            SetRect(sBg, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-150, 10), new Vector2(40, 20));
            hud.statusBadgeImage = sBg.AddComponent<Image>();
            hud.statusBadgeText = CreateText(sBg.transform, "Text", "BRN", 12, TextAlignmentOptions.Center);
            SetStretch(hud.statusBadgeText.gameObject);
            
            return hud;
        }

        static BattleSkillPanel BuildSkillPanel(Transform parent) {
            GameObject go = new GameObject("SkillPanel", typeof(RectTransform));
            go.transform.SetParent(parent, false); 
            // Bottom-Right Anchor
            SetRect(go, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-28, 28), new Vector2(940, 220));
            go.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.1f, 0.95f);
            var panel = go.AddComponent<BattleSkillPanel>();
            panel.PanelType = BattlePanelType.Skill;
            panel.skillButtons = new Button[4];
            
            GameObject grid = new GameObject("Grid", typeof(RectTransform)); grid.transform.SetParent(go.transform, false); 
            SetStretch(grid); 
            var glg = grid.AddComponent<GridLayoutGroup>(); 
            glg.cellSize = new Vector2(440, 80); glg.spacing = new Vector2(12, 12);
            glg.padding = new RectOffset(20, 20, 20, 20);
            for(int i=0; i<4; i++) panel.skillButtons[i] = CreateButton(grid.transform, $"Skill_{i}", "MOVE");
            return panel;
        }

        static BattleCommandPanel BuildCommandPanel(Transform parent) {
            GameObject go = new GameObject("CommandPanel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            // Bottom-Right Anchor
            SetRect(go, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-60, 90), new Vector2(200, 380));

            var panel = go.AddComponent<BattleCommandPanel>();
            panel.PanelType = BattlePanelType.Command;
            panel.fightButton   = CreateButton(go.transform, "FightBtn",   "FIGHT");
            panel.pokemonButton = CreateButton(go.transform, "PkmnBtn",    "PKMN");
            panel.infoButton    = CreateButton(go.transform, "InfoBtn",    "INFO");
            panel.forfeitButton = CreateButton(go.transform, "ForfeitBtn", "FORFEIT");

            // Tô màu đỏ để phân biệt nút đầu hàng
            panel.forfeitButton.GetComponent<Image>().color = new Color(0.6f, 0.1f, 0.1f, 0.85f);

            // Layout theo chiều dọc ở bên phải
            SetRect(panel.fightButton.gameObject,   new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(0,  -20), new Vector2(140, 60));
            SetRect(panel.pokemonButton.gameObject,  new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -100), new Vector2(140, 60));
            SetRect(panel.infoButton.gameObject,     new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -180), new Vector2(140, 60));
            SetRect(panel.forfeitButton.gameObject,  new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -260), new Vector2(140, 60));
            return panel;
        }

        static BattleDialogPanel BuildDialogPanel(Transform parent) {
            GameObject go = new GameObject("DialogPanel", typeof(RectTransform));
            go.transform.SetParent(parent, false); 
            // Bottom-Left Anchor
            SetRect(go, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(28, 28), new Vector2(1200, 160));
            go.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.1f, 0.95f);
            var panel = go.AddComponent<BattleDialogPanel>();
            panel.PanelType = BattlePanelType.Dialog;
            panel.dialogText = CreateText(go.transform, "Text", "Waiting for server...", 24, TextAlignmentOptions.TopLeft);
            SetRect(panel.dialogText.gameObject, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-60, -40));
            return panel;
        }

        static BattleTeamPreviewPanel BuildTeamPreviewPanel(Transform parent) {
            GameObject go = new GameObject("TeamPreviewPanel", typeof(RectTransform));
            go.transform.SetParent(parent, false); SetStretch(go);
            go.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.1f, 1f);

            var panel = go.AddComponent<BattleTeamPreviewPanel>();
            panel.PanelType = BattlePanelType.TeamPreview;

            CreateText(go.transform, "Title", "TEAM PREVIEW", 32, TextAlignmentOptions.Center);
            panel.timerText = CreateText(go.transform, "Timer", "90s", 24, TextAlignmentOptions.Right);
            SetRect(panel.timerText.gameObject, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -40), new Vector2(200, 50));

            // 1. Enemy Grid (Top)
            CreateText(go.transform, "OppLabel", "OPPONENT TEAM", 20, TextAlignmentOptions.Left);
            SetRect(go.transform.Find("OppLabel").gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-350, 280), new Vector2(300, 40));

            GameObject eGrid = new GameObject("EnemyGrid", typeof(RectTransform));
            eGrid.transform.SetParent(go.transform, false);
            SetRect(eGrid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 180), new Vector2(1000, 150));
            var eglg = eGrid.AddComponent<GridLayoutGroup>(); eglg.cellSize = new Vector2(150, 100); eglg.spacing = new Vector2(15, 15);

            panel.enemySlots = new GameObject[6];
            for (int i = 0; i < 6; i++) {
                var slot = new GameObject($"ESlot_{i}", typeof(RectTransform));
                slot.transform.SetParent(eGrid.transform, false);
                slot.AddComponent<Image>().color = new Color(1, 1, 1, 0.05f);
                
                var icon = new GameObject("Icon", typeof(RectTransform));
                icon.transform.SetParent(slot.transform, false);
                SetRect(icon, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(80, 80));
                icon.AddComponent<Image>().raycastTarget = false;

                CreateText(slot.transform, "Name", "???", 14, TextAlignmentOptions.Center);
                CreateText(slot.transform, "Type1", "", 10, TextAlignmentOptions.Center);
                panel.enemySlots[i] = slot;
            }

            // 2. Player Grid (Bottom)
            CreateText(go.transform, "PlayerLabel", "YOUR TEAM (PICK 4)", 20, TextAlignmentOptions.Left);
            SetRect(go.transform.Find("PlayerLabel").gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-350, 20), new Vector2(300, 40));

            GameObject pGrid = new GameObject("PokemonGrid", typeof(RectTransform));
            pGrid.transform.SetParent(go.transform, false);
            SetRect(pGrid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -120), new Vector2(1000, 220));
            var pglg = pGrid.AddComponent<GridLayoutGroup>(); pglg.cellSize = new Vector2(150, 180); pglg.spacing = new Vector2(15, 15);

            panel.slotButtons = new Button[6];
            for(int i=0; i<6; i++) { 
                panel.slotButtons[i] = CreateButton(pGrid.transform, $"Slot_{i}", "POKEMON"); 
                panel.slotButtons[i].transform.Find("Text").name = "PokemonName"; 

                var icon = new GameObject("Icon", typeof(RectTransform));
                icon.transform.SetParent(panel.slotButtons[i].transform, false);
                SetRect(icon, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(100, 100));
                icon.AddComponent<Image>().raycastTarget = false;

                CreateText(panel.slotButtons[i].transform, "OrderBadge", "", 32, TextAlignmentOptions.Center); 
                CreateText(panel.slotButtons[i].transform, "Type1", "", 12, TextAlignmentOptions.Center);
            }

            panel.confirmButton = CreateButton(go.transform, "ConfirmBtn", "CONFIRM");
            SetRect(panel.confirmButton.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 100), new Vector2(300, 60));
            panel.confirmLabel = panel.confirmButton.GetComponentInChildren<TextMeshProUGUI>();
            return panel;
        }

        static BattlePartyPanel BuildPartyPanel(Transform parent) {
            GameObject go = new GameObject("PartyPanel", typeof(RectTransform));
            go.transform.SetParent(parent, false); SetStretch(go);
            go.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 1f);
            var panel = go.AddComponent<BattlePartyPanel>();
            panel.PanelType = BattlePanelType.Party;
            panel.headerText = CreateText(go.transform, "HeaderText", "SELECT POKEMON", 28, TextAlignmentOptions.Center);
            SetRect(panel.headerText.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(600, 60));
            
            GameObject list = new GameObject("SlotList", typeof(RectTransform)); list.transform.SetParent(go.transform, false); SetRect(list, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800, 500));
            list.AddComponent<VerticalLayoutGroup>().spacing = 10;
            panel.partyButtons = new Button[4];
            for(int i=0; i<4; i++) { 
                panel.partyButtons[i] = CreateButton(list.transform, $"Card_{i}", "PKMN"); 
                panel.partyButtons[i].gameObject.AddComponent<LayoutElement>().preferredHeight = 80; 
            }
            panel.backButton = CreateButton(go.transform, "BackBtn", "BACK");
            SetRect(panel.backButton.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(200, 50));
            return panel;
        }

        static void BuildDemo(Transform canvas) {
            var go = new GameObject("demo");
            go.transform.SetParent(canvas);
            
            // Đảm bảo không có script cũ gây xung đột
            go.AddComponent<BattleDemoSimulator>();
        }

        static void SetStretch(GameObject go) {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        static void SetRect(GameObject go, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size) {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        static TextMeshProUGUI CreateText(Transform parent, string name, string content, float size, TextAlignmentOptions align) {
            GameObject go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>(); tmp.text = content; tmp.fontSize = size; tmp.alignment = align; tmp.color = Color.white; return tmp;
        }

        static Button CreateButton(Transform parent, string name, string label) {
            GameObject go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = new Color(1, 1, 1, 0.15f);
            var btn = go.AddComponent<Button>(); CreateText(go.transform, "Text", label, 18, TextAlignmentOptions.Center); return btn;
        }
    }
}
