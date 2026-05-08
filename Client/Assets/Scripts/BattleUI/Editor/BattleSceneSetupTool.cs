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
        // ── Palette (từ HTML battle-scene.html) ──────────────────────────────
        static readonly Color BgDark      = new Color(0.027f, 0.035f, 0.102f, 0.93f);
        static readonly Color BgMid       = new Color(0.051f, 0.071f, 0.188f, 0.93f);
        static readonly Color AllyColor   = new Color(0.243f, 0.784f, 0.831f, 1f);    // teal
        static readonly Color AllyDeep    = new Color(0.133f, 0.459f, 0.490f, 1f);
        static readonly Color EnemyColor  = new Color(0.882f, 0.376f, 0.686f, 1f);    // magenta
        static readonly Color EnemyDeep   = new Color(0.502f, 0.165f, 0.373f, 1f);
        static readonly Color InkColor    = new Color(0.910f, 0.925f, 1.000f, 1f);
        static readonly Color InkDim      = new Color(0.541f, 0.572f, 0.780f, 1f);
        static readonly Color LineColor   = new Color(0.165f, 0.184f, 0.361f, 1f);
        static readonly Color HpHigh      = new Color(0.463f, 0.839f, 0.471f, 1f);    // green
        static readonly Color HpMid       = new Color(0.957f, 0.773f, 0.263f, 1f);    // yellow
        static readonly Color HpLow       = new Color(0.902f, 0.322f, 0.165f, 1f);    // orange-red
        static readonly Color FightColor  = new Color(0.941f, 0.871f, 0.224f, 1f);    // yellow
        static readonly Color FightDeep   = new Color(0.682f, 0.588f, 0.078f, 1f);
        static readonly Color PkmColor    = new Color(0.384f, 0.306f, 0.863f, 1f);    // purple
        static readonly Color PkmDeep     = new Color(0.196f, 0.141f, 0.604f, 1f);
        static readonly Color Transparent = new Color(0, 0, 0, 0);

        // ── Type colors ──────────────────────────────────────────────────────
        static Color TypeColor(string type) => type?.ToLower() switch
        {
            "fire"     => new Color(0.863f, 0.447f, 0.220f),
            "water"    => new Color(0.282f, 0.580f, 0.863f),
            "grass"    => new Color(0.427f, 0.820f, 0.420f),
            "electric" => new Color(0.941f, 0.851f, 0.220f),
            "ice"      => new Color(0.663f, 0.878f, 0.929f),
            "fighting" => new Color(0.722f, 0.302f, 0.180f),
            "poison"   => new Color(0.600f, 0.200f, 0.600f),
            "ground"   => new Color(0.753f, 0.647f, 0.337f),
            "flying"   => new Color(0.522f, 0.522f, 0.859f),
            "psychic"  => new Color(0.882f, 0.278f, 0.416f),
            "bug"      => new Color(0.620f, 0.780f, 0.220f),
            "rock"     => new Color(0.580f, 0.537f, 0.361f),
            "ghost"    => new Color(0.478f, 0.298f, 0.600f),
            "dragon"   => new Color(0.361f, 0.220f, 0.780f),
            "dark"     => new Color(0.416f, 0.376f, 0.298f),
            "steel"    => new Color(0.600f, 0.620f, 0.722f),
            "fairy"    => new Color(0.922f, 0.682f, 0.780f),
            _          => new Color(0.698f, 0.678f, 0.620f), // normal
        };

        static Color TypeTextColor(string type) => type?.ToLower() switch
        {
            "water" or "fighting" or "poison" or "ghost" or "dragon" or "dark" => Color.white,
            _ => new Color(0.047f, 0.047f, 0.102f),
        };

        // ─────────────────────────────────────────────────────────────────────
        [MenuItem("Pokemon/3. Setup Battle Scene (Old Style)")]
        public static void GenerateBattleScene()
        {
            if (Camera.main != null)
            {
                Camera.main.backgroundColor = new Color(0.027f, 0.035f, 0.102f);
                Camera.main.orthographic = true;
                Camera.main.orthographicSize = 5f;
            }

            // ── Arena slots (world space) ─────────────────────────────────
            GameObject arenaRoot = new GameObject("🌍 BATTLE ARENA");
            CreatePokemonSlot(arenaRoot.transform, "Player_Lead_Slot",  new Vector3(-3f, -1.5f, 0), true);
            CreatePokemonSlot(arenaRoot.transform, "Player_Sub2_Slot",  new Vector3(-7f, -3f,   0), true);
            CreatePokemonSlot(arenaRoot.transform, "Enemy_Lead_Slot",   new Vector3( 3f,  1.5f, 0), false);
            CreatePokemonSlot(arenaRoot.transform, "Enemy_Sub2_Slot",   new Vector3( 7f,  3f,   0), false);

            // ── Canvas ───────────────────────────────────────────────────
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject cObj = new GameObject("BattleCanvas");
                canvas = cObj.AddComponent<Canvas>();
                CanvasScaler scaler = cObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                cObj.AddComponent<GraphicRaycaster>();
            }
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            if (Camera.main != null)
            {
                canvas.worldCamera = Camera.main;
                canvas.planeDistance = 10f;
            }

            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // ── BattleSystemRoot ─────────────────────────────────────────
            GameObject root = new GameObject("BattleSystemRoot", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            SetStretch(root);
            BattleUIManager uiManager = root.AddComponent<BattleUIManager>();
            uiManager.panels = new System.Collections.Generic.List<BasePanel>();

            // GlobalHUDs root
            GameObject globalHud = new GameObject("GlobalHUDs_Root", typeof(RectTransform));
            globalHud.transform.SetParent(root.transform, false);
            SetStretch(globalHud);
            uiManager.globalHudRoot = globalHud;

            Sprite bg9  = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            // ── Enemy HUD cards (top-right) ───────────────────────────────
            // E2 = rightmost, E1 = second from right (matches HTML layout)
            var enemyHUD1 = BuildHudCard(globalHud.transform, "Enemy1_HUD", bg9, EnemyColor, false,
                new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-28 - 360 - 22, -28), new Vector2(360, 130));

            var enemyHUD2 = BuildHudCard(globalHud.transform, "Enemy2_HUD", bg9, EnemyColor, false,
                new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-28, -28), new Vector2(360, 130));

            // ── Ally HUD cards (bottom-left) ──────────────────────────────
            var playerHUD1 = BuildHudCard(globalHud.transform, "Player1_HUD", bg9, AllyColor, true,
                new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(28, 28), new Vector2(360, 130));

            var playerHUD2 = BuildHudCard(globalHud.transform, "Player2_HUD", bg9, AllyColor, true,
                new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(28 + 360 + 22, 28), new Vector2(360, 130));

            // ── Timer panels (right side) ─────────────────────────────────
            BuildTimerPanel(globalHud.transform, "MatchTimer", "⏱  MATCH", "07:00",
                LineColor, new Vector2(1, 1), new Vector2(-28, -180), new Vector2(200, 85));

            BuildTimerPanel(globalHud.transform, "MoveTimer", "MOVE TIME", "45",
                AllyColor, new Vector2(1, 1), new Vector2(-28, -285), new Vector2(200, 85));

            // ── Top-left corner buttons (Forfeit / Info) ─────────────────
            GameObject cornerGroup = new GameObject("CornerUI", typeof(RectTransform));
            cornerGroup.transform.SetParent(globalHud.transform, false);
            SetRect(cornerGroup, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(28, -28), new Vector2(260, 44));

            GameObject forfeitBtn = BuildCornerButton(cornerGroup.transform, "ForfeitBtn", "↩  RUN",
                bg9, LineColor, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(120, 44));
            BuildCornerButton(cornerGroup.transform, "InfoBtn", "ℹ",
                bg9, LineColor, new Vector2(0, 0), new Vector2(0, 0), new Vector2(130, 0), new Vector2(44, 44));

            // ── Action buttons (bottom-right) ─────────────────────────────
            GameObject actionsRoot = new GameObject("ActionMenuPanel (Nút bóng tròn)", typeof(RectTransform));
            actionsRoot.transform.SetParent(root.transform, false);
            SetStretch(actionsRoot);
            BattleCommandPanel cmdPanel = actionsRoot.AddComponent<BattleCommandPanel>();
            cmdPanel.PanelType = BattlePanelType.Command;
            cmdPanel.forfeitButton = forfeitBtn.GetComponent<Button>();

            // FIGHT button – circular, yellow-green, bottom-right
            cmdPanel.fightButton = BuildActionButton(actionsRoot.transform, "FightBtn",
                "⚔", "FIGHT", knob, FightColor, FightDeep,
                new Vector2(1, 0), new Vector2(-125, 280), 140);

            // POKÉMON button – circular, purple, below FIGHT
            cmdPanel.pokemonButton = BuildActionButton(actionsRoot.transform, "PokemonBtn",
                "◈", "POKÉMON", knob, PkmColor, PkmDeep,
                new Vector2(1, 0), new Vector2(-125, 110), 120);

            // Info button forwarded (reuse corner info btn for now)
            cmdPanel.infoButton = forfeitBtn.GetComponent<Button>(); // placeholder

            uiManager.panels.Add(cmdPanel);

            // ── Dialog box (bottom, stays always visible) ─────────────────
            GameObject dialogObj = new GameObject("DialogPanel", typeof(RectTransform));
            dialogObj.transform.SetParent(root.transform, false);
            var dialogRt = dialogObj.AddComponent<RectTransform>();
            dialogRt.anchorMin = new Vector2(0, 0);
            dialogRt.anchorMax = new Vector2(1, 0);
            dialogRt.pivot     = new Vector2(0.5f, 0);
            dialogRt.offsetMin = new Vector2(28, 28);
            dialogRt.offsetMax = new Vector2(-320, 28 + 110);

            Image dialogBg = dialogObj.AddComponent<Image>();
            dialogBg.sprite = bg9; dialogBg.type = Image.Type.Sliced;
            dialogBg.color = new Color(0.059f, 0.039f, 0.110f, 0.96f);
            Outline dialogOutline = dialogObj.AddComponent<Outline>();
            dialogOutline.effectColor = AllyColor;
            dialogOutline.effectDistance = new Vector2(2, -2);

            BattleDialogPanel dialogPanel = dialogObj.AddComponent<BattleDialogPanel>();
            dialogPanel.PanelType = BattlePanelType.Dialog;

            GameObject dTextObj = new GameObject("Text", typeof(RectTransform));
            dTextObj.transform.SetParent(dialogObj.transform, false);
            SetRect(dTextObj, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-40, -30));
            TextMeshProUGUI dTmp = dTextObj.AddComponent<TextMeshProUGUI>();
            dTmp.text = ""; dTmp.color = InkColor; dTmp.fontSize = 28;
            dTmp.fontStyle = FontStyles.Bold;
            dTmp.alignment = TextAlignmentOptions.TopLeft;
            dialogPanel.dialogText = dTmp;
            uiManager.panels.Add(dialogPanel);

            // ── Fight / Skill panel (2×2 move grid, bottom-right overlay) ─
            GameObject skillObj = new GameObject("SkillMenuPanel", typeof(RectTransform));
            skillObj.transform.SetParent(root.transform, false);
            SetRect(skillObj, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-28, 28), new Vector2(940, 220));

            Image skillBg = skillObj.AddComponent<Image>();
            skillBg.sprite = bg9; skillBg.type = Image.Type.Sliced;
            skillBg.color = new Color(0.059f, 0.039f, 0.110f, 0.96f);
            Outline skillOutline = skillObj.AddComponent<Outline>();
            skillOutline.effectColor = AllyColor; skillOutline.effectDistance = new Vector2(2, -2);

            // Header label
            GameObject skillHeader = new GameObject("Header", typeof(RectTransform));
            skillHeader.transform.SetParent(skillObj.transform, false);
            SetRect(skillHeader, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
                new Vector2(0, -4), new Vector2(-20, 28));
            TextMeshProUGUI headerTmp = skillHeader.AddComponent<TextMeshProUGUI>();
            headerTmp.text = "SELECT A MOVE";
            headerTmp.color = AllyColor; headerTmp.fontSize = 11;
            headerTmp.fontStyle = FontStyles.Bold;
            headerTmp.alignment = TextAlignmentOptions.Center;

            // 2×2 grid container
            GameObject gridObj = new GameObject("MovesGrid", typeof(RectTransform));
            gridObj.transform.SetParent(skillObj.transform, false);
            SetRect(gridObj, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f),
                new Vector2(0, -14), new Vector2(-20, -36));
            GridLayoutGroup grid = gridObj.AddComponent<GridLayoutGroup>();
            grid.cellSize    = new Vector2(455, 80);
            grid.spacing     = new Vector2(12, 12);
            grid.padding     = new RectOffset(6, 6, 6, 6);
            grid.constraint  = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment  = TextAnchor.MiddleCenter;

            string[] defaultMoves = { "Mystical Fire", "Psyshock", "Flamethrower", "Calm Mind" };
            string[] defaultTypes = { "fire", "psychic", "fire", "psychic" };
            Button[] skillBtns = new Button[4];
            TextMeshProUGUI[] ppTexts = new TextMeshProUGUI[4];

            for (int i = 0; i < 4; i++)
            {
                skillBtns[i] = BuildMoveButton(gridObj.transform, $"Move{i + 1}",
                    defaultMoves[i], defaultTypes[i], "Special", $"PP 10/10", bg9, out ppTexts[i]);
            }

            // Back button (sits outside grid, bottom of panel)
            Button backBtn = BuildTextButton(skillObj.transform, "BackBtn", "← BACK",
                bg9, new Color(0.15f, 0.15f, 0.3f, 0.9f), InkDim,
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(-6, 6), new Vector2(110, 32), 16);

            BattleSkillPanel skillPanel = skillObj.AddComponent<BattleSkillPanel>();
            skillPanel.PanelType = BattlePanelType.Skill;
            skillPanel.skillButtons = skillBtns;
            skillPanel.ppTexts = ppTexts;
            skillPanel.backButton = backBtn;
            uiManager.panels.Add(skillPanel);

            // ── Wire up network controller ────────────────────────────────
            BattleNetworkController netCtrl = root.AddComponent<BattleNetworkController>();
            netCtrl.playerHUD1 = playerHUD1;
            netCtrl.playerHUD2 = playerHUD2;
            netCtrl.enemyHUD1  = enemyHUD1;
            netCtrl.enemyHUD2  = enemyHUD2;
            netCtrl.skillPanel = skillPanel;

            // ── Test controller (disabled at runtime) ─────────────────────
            BattleTestController testCtrl = root.AddComponent<BattleTestController>();
            testCtrl.playerHUD = playerHUD1;
            testCtrl.enemyHUD  = enemyHUD1;

            Selection.activeGameObject = root;
            Debug.Log("<color=lime>[BattleUI] Champions-style scene generated!</color>");
        }

        // ═════════════════════════════════════════════════════════════════════
        // HUD CARD
        // ═════════════════════════════════════════════════════════════════════
        static EntityHUD BuildHudCard(Transform parent, string name, Sprite bg9, Color accentColor,
            bool showHpNumbers, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            GameObject card = new GameObject(name);
            card.transform.SetParent(parent, false);
            SetRect(card, anchorMin, anchorMax, anchorMin, pos, size);

            // Background image
            Image cardBg = card.AddComponent<Image>();
            cardBg.sprite = bg9; cardBg.type = Image.Type.Sliced;
            cardBg.color = BgDark;

            // Border glow via Outline
            Outline outline = card.AddComponent<Outline>();
            outline.effectColor = accentColor;
            outline.effectDistance = new Vector2(2, -2);

            // ── Row 1: Icon | Name+Level | Gender ──────────────────────────
            // Icon frame (56×56, top-left)
            GameObject iconFrame = new GameObject("Avatar_Box", typeof(RectTransform));
            iconFrame.transform.SetParent(card.transform, false);
            SetRect(iconFrame, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(10, -10), new Vector2(56, 56));
            Image iconFrameImg = iconFrame.AddComponent<Image>();
            iconFrameImg.sprite = bg9; iconFrameImg.type = Image.Type.Sliced;
            iconFrameImg.color = new Color(0.10f, 0.12f, 0.25f, 0.8f);
            iconFrame.AddComponent<RectMask2D>();
            var iconOutline = iconFrame.AddComponent<Outline>();
            iconOutline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.35f);
            iconOutline.effectDistance = new Vector2(1, -1);

            GameObject iconImg = new GameObject("Icon", typeof(RectTransform));
            iconImg.transform.SetParent(iconFrame.transform, false);
            SetStretch(iconImg);
            Image iconImage = iconImg.AddComponent<Image>();
            iconImage.color = Transparent; // filled by loader

            // Name text
            GameObject nameObj = new GameObject("Name", typeof(RectTransform));
            nameObj.transform.SetParent(card.transform, false);
            SetRect(nameObj, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(76, -10), new Vector2(-50, 26));
            TextMeshProUGUI nameTmp = nameObj.AddComponent<TextMeshProUGUI>();
            nameTmp.text = name.Replace("_HUD", "").Replace("Enemy", "").Replace("Player", "").Replace("1", "").Replace("2", "");
            nameTmp.color = Color.white; nameTmp.fontSize = 14;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.alignment = TextAlignmentOptions.TopLeft;

            // Level text
            GameObject lvlObj = new GameObject("Level", typeof(RectTransform));
            lvlObj.transform.SetParent(card.transform, false);
            SetRect(lvlObj, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(76, -32), new Vector2(-50, 18));
            TextMeshProUGUI lvlTmp = lvlObj.AddComponent<TextMeshProUGUI>();
            lvlTmp.text = "LV 50"; lvlTmp.color = InkDim; lvlTmp.fontSize = 11;
            lvlTmp.alignment = TextAlignmentOptions.TopLeft;

            // Gender text (top-right of card)
            GameObject genderObj = new GameObject("Gender", typeof(RectTransform));
            genderObj.transform.SetParent(card.transform, false);
            SetRect(genderObj, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-10, -10), new Vector2(28, 22));
            TextMeshProUGUI genderTmp = genderObj.AddComponent<TextMeshProUGUI>();
            genderTmp.text = "♂"; genderTmp.color = new Color(0.42f, 0.72f, 1f); genderTmp.fontSize = 14;
            genderTmp.alignment = TextAlignmentOptions.TopRight;

            // ── Row 2: HP bar ──────────────────────────────────────────────
            // "HP" label
            GameObject hpLabel = new GameObject("HP_Label", typeof(RectTransform));
            hpLabel.transform.SetParent(card.transform, false);
            SetRect(hpLabel, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(10, 40), new Vector2(30, 18));
            TextMeshProUGUI hpLblTmp = hpLabel.AddComponent<TextMeshProUGUI>();
            hpLblTmp.text = "HP"; hpLblTmp.color = InkColor; hpLblTmp.fontSize = 10;
            hpLblTmp.fontStyle = FontStyles.Bold;
            hpLblTmp.alignment = TextAlignmentOptions.Left;

            // HP bar background
            GameObject hpBarBg = new GameObject("HP_Fill_BG", typeof(RectTransform));
            hpBarBg.transform.SetParent(card.transform, false);
            SetRect(hpBarBg, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0),
                new Vector2(46, 40), new Vector2(-100, 16));
            Image hpBarBgImg = hpBarBg.AddComponent<Image>();
            hpBarBgImg.color = new Color(0.039f, 0.051f, 0.133f, 0.9f);

            // HP fill image
            GameObject hpFill = new GameObject("HP_Fill_Image", typeof(RectTransform));
            hpFill.transform.SetParent(hpBarBg.transform, false);
            SetStretch(hpFill);
            Image hpFillImg = hpFill.AddComponent<Image>();
            hpFillImg.sprite = bg9; hpFillImg.type = Image.Type.Sliced;
            hpFillImg.color = HpHigh;
            hpFillImg.type = Image.Type.Filled;
            hpFillImg.fillMethod = Image.FillMethod.Horizontal;
            hpFillImg.fillOrigin = 0;
            hpFillImg.fillAmount = 1f;

            // HP shine line (decorative, not interactive)
            GameObject hpShine = new GameObject("HP_Shine", typeof(RectTransform));
            hpShine.transform.SetParent(hpBarBg.transform, false);
            SetRect(hpShine, new Vector2(0, 0.6f), new Vector2(1, 1), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            Image shineImg = hpShine.AddComponent<Image>();
            shineImg.color = new Color(1, 1, 1, 0.06f);

            // HP number (right side)
            GameObject hpNum = new GameObject("HP_Value", typeof(RectTransform));
            hpNum.transform.SetParent(card.transform, false);
            SetRect(hpNum, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-8, 38), new Vector2(90, 20));
            TextMeshProUGUI hpTmp = hpNum.AddComponent<TextMeshProUGUI>();
            hpTmp.text = showHpNumbers ? "100 / 100" : "100%";
            hpTmp.color = InkColor; hpTmp.fontSize = 11;
            hpTmp.fontStyle = FontStyles.Bold;
            hpTmp.alignment = TextAlignmentOptions.Right;

            // ── Row 3: Type badges ─────────────────────────────────────────
            GameObject typesRow = new GameObject("TypeBadges");
            typesRow.transform.SetParent(card.transform, false);
            SetRect(typesRow, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0),
                new Vector2(10, 14), new Vector2(-10, 22));
            HorizontalLayoutGroup typesHLG = typesRow.AddComponent<HorizontalLayoutGroup>();
            typesHLG.spacing = 4; typesHLG.childForceExpandWidth = false;
            typesHLG.childForceExpandHeight = false;
            typesHLG.childAlignment = TextAnchor.MiddleLeft;

            var (type1Img, type1Txt) = BuildTypeBadge(typesRow.transform, "fire", bg9);
            var (type2Img, type2Txt) = BuildTypeBadge(typesRow.transform, "normal", bg9);

            // ── Wire up EntityHUD ──────────────────────────────────────────
            EntityHUD hud = card.AddComponent<EntityHUD>();
            hud.entityId = name;
            hud.nameText = nameTmp;
            hud.hpText = hpTmp;
            hud.hpFillImage = hpFillImg;
            hud.levelText = lvlTmp;
            hud.iconImage = iconImage;
            hud.type1BadgeImage = type1Img;
            hud.type2BadgeImage = type2Img;
            hud.type1BadgeText = type1Txt;
            hud.type2BadgeText = type2Txt;
            hud.highHpColor   = HpHigh;
            hud.mediumHpColor = HpMid;
            hud.lowHpColor    = HpLow;
            hud.showExactHp   = showHpNumbers;

            return hud;
        }

        // ── Type badge (pill-style) ───────────────────────────────────────
        static (Image img, TextMeshProUGUI txt) BuildTypeBadge(Transform parent, string typeName, Sprite bg9)
        {
            GameObject badge = new GameObject($"TypeBadge_{typeName}");
            badge.transform.SetParent(parent, false);

            LayoutElement le = badge.AddComponent<LayoutElement>();
            le.preferredWidth = 50; le.preferredHeight = 18; le.minWidth = 40;

            Image badgeImg = badge.AddComponent<Image>();
            badgeImg.sprite = bg9; badgeImg.type = Image.Type.Sliced;
            badgeImg.color = TypeColor(typeName);

            GameObject txtObj = new GameObject("Text", typeof(RectTransform));
            txtObj.transform.SetParent(badge.transform, false);
            SetStretch(txtObj);
            TextMeshProUGUI txtTmp = txtObj.AddComponent<TextMeshProUGUI>();
            txtTmp.text = typeName.ToUpper(); txtTmp.color = TypeTextColor(typeName);
            txtTmp.fontSize = 8; txtTmp.fontStyle = FontStyles.Bold;
            txtTmp.alignment = TextAlignmentOptions.Center;

            return (badgeImg, txtTmp);
        }

        // ═════════════════════════════════════════════════════════════════════
        // MOVE BUTTON (fight panel)
        // ═════════════════════════════════════════════════════════════════════
        static Button BuildMoveButton(Transform parent, string name, string moveName, string typeName,
            string category, string ppStr, Sprite bg9, out TextMeshProUGUI ppText)
        {
            GameObject btn = new GameObject(name);
            btn.transform.SetParent(parent, false);

            Image btnImg = btn.AddComponent<Image>();
            btnImg.sprite = bg9; btnImg.type = Image.Type.Sliced;
            btnImg.color = new Color(0.08f, 0.08f, 0.20f, 0.9f);
            Button button = btn.AddComponent<Button>();

            ColorBlock cb = button.colors;
            cb.highlightedColor = new Color(0.16f, 0.16f, 0.35f, 1f);
            cb.pressedColor = new Color(0.06f, 0.06f, 0.16f, 1f);
            button.colors = cb;

            // Left type accent bar (6px wide)
            GameObject accent = new GameObject("TypeAccent", typeof(RectTransform));
            accent.transform.SetParent(btn.transform, false);
            SetRect(accent, Vector2.zero, new Vector2(0, 1), new Vector2(0, 0.5f),
                Vector2.zero, new Vector2(6, 0));
            Image accentImg = accent.AddComponent<Image>();
            accentImg.color = TypeColor(typeName);

            // Move name
            GameObject nameObj = new GameObject("MoveName", typeof(RectTransform));
            nameObj.transform.SetParent(btn.transform, false);
            SetRect(nameObj, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(12, -8), new Vector2(-12, 28));
            TextMeshProUGUI nameTmp = nameObj.AddComponent<TextMeshProUGUI>();
            nameTmp.text = moveName; nameTmp.color = Color.white;
            nameTmp.fontSize = 15; nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.alignment = TextAlignmentOptions.TopLeft;

            // Meta row: type badge + category + PP
            GameObject metaRow = new GameObject("MetaRow", typeof(RectTransform));
            metaRow.transform.SetParent(btn.transform, false);
            SetRect(metaRow, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0),
                new Vector2(12, 8), new Vector2(-12, 22));
            HorizontalLayoutGroup metaHLG = metaRow.AddComponent<HorizontalLayoutGroup>();
            metaHLG.spacing = 6; metaHLG.childForceExpandWidth = false;
            metaHLG.childForceExpandHeight = false;
            metaHLG.childAlignment = TextAnchor.MiddleLeft;

            // Type badge
            var (_, _typeTxt) = BuildTypeBadge(metaRow.transform, typeName, bg9);

            // Category label
            GameObject catObj = new GameObject("Category", typeof(RectTransform));
            catObj.transform.SetParent(metaRow.transform, false);
            LayoutElement catLE = catObj.AddComponent<LayoutElement>();
            catLE.preferredWidth = 60; catLE.preferredHeight = 18;
            Image catBg = catObj.AddComponent<Image>();
            catBg.sprite = bg9; catBg.type = Image.Type.Sliced;
            catBg.color = new Color(1, 1, 1, 0.1f);
            GameObject catTxtObj = new GameObject("Text", typeof(RectTransform)); catTxtObj.transform.SetParent(catObj.transform, false);
            SetStretch(catTxtObj);
            TextMeshProUGUI catTmp = catTxtObj.AddComponent<TextMeshProUGUI>();
            catTmp.text = category.ToUpper(); catTmp.color = InkDim;
            catTmp.fontSize = 8; catTmp.fontStyle = FontStyles.Bold;
            catTmp.alignment = TextAlignmentOptions.Center;

            // PP label
            GameObject ppObj = new GameObject("PP", typeof(RectTransform));
            ppObj.transform.SetParent(metaRow.transform, false);
            LayoutElement ppLE = ppObj.AddComponent<LayoutElement>();
            ppLE.preferredWidth = 70; ppLE.preferredHeight = 18;
            ppText = ppObj.AddComponent<TextMeshProUGUI>();
            ppText.text = ppStr; ppText.color = InkColor;
            ppText.fontSize = 10; ppText.fontStyle = FontStyles.Bold;
            ppText.alignment = TextAlignmentOptions.Left;

            // Button hover border
            Outline moveOutline = btn.AddComponent<Outline>();
            moveOutline.effectColor = TypeColor(typeName);
            moveOutline.effectDistance = new Vector2(1, -1);

            return button;
        }

        // ═════════════════════════════════════════════════════════════════════
        // ACTION BUTTON (circular, FIGHT / POKÉMON)
        // ═════════════════════════════════════════════════════════════════════
        static Button BuildActionButton(Transform parent, string name, string glyph, string label,
            Sprite knob, Color mainColor, Color deepColor, Vector2 anchor, Vector2 pos, int size)
        {
            GameObject btn = new GameObject(name);
            btn.transform.SetParent(parent, false);
            SetRect(btn, anchor, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));

            Image img = btn.AddComponent<Image>();
            img.sprite = knob; img.color = mainColor;

            Outline shadow = btn.AddComponent<Outline>();
            shadow.effectColor = new Color(0, 0, 0, 0.5f);
            shadow.effectDistance = new Vector2(0, -4);

            Button button = btn.AddComponent<Button>();
            ColorBlock cb = button.colors;
            cb.highlightedColor = Color.white; cb.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            button.colors = cb;

            // Glyph text
            GameObject glyphObj = new GameObject("Glyph", typeof(RectTransform));
            glyphObj.transform.SetParent(btn.transform, false);
            SetRect(glyphObj, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, 10), Vector2.zero);
            TextMeshProUGUI glyphTmp = glyphObj.AddComponent<TextMeshProUGUI>();
            glyphTmp.text = glyph; glyphTmp.color = new Color(0, 0, 0, 0.7f);
            glyphTmp.fontSize = size * 0.25f; glyphTmp.fontStyle = FontStyles.Bold;
            glyphTmp.alignment = TextAlignmentOptions.Center;

            // Label below button
            GameObject labelObj = new GameObject("Label", typeof(RectTransform));
            labelObj.transform.SetParent(btn.transform, false);
            SetRect(labelObj, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 1),
                new Vector2(0, -18), new Vector2(0, 22));
            Image labelBg = labelObj.AddComponent<Image>();
            labelBg.sprite = knob; // reuse knob as rounded label bg
            labelBg.color = deepColor;
            GameObject labelTxtObj = new GameObject("Text", typeof(RectTransform));
            labelTxtObj.transform.SetParent(labelObj.transform, false);
            SetStretch(labelTxtObj);
            TextMeshProUGUI labelTmp = labelTxtObj.AddComponent<TextMeshProUGUI>();
            labelTmp.text = label; labelTmp.color = new Color(0, 0, 0, 0.85f);
            labelTmp.fontSize = 10; labelTmp.fontStyle = FontStyles.Bold;
            labelTmp.alignment = TextAlignmentOptions.Center;

            return button;
        }

        // ═════════════════════════════════════════════════════════════════════
        // TIMER PANEL
        // ═════════════════════════════════════════════════════════════════════
        static void BuildTimerPanel(Transform parent, string name, string label, string value,
            Color borderColor, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            Sprite bg9 = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            SetRect(panel, anchor, anchor, anchor, pos, size);

            Image bg = panel.AddComponent<Image>();
            bg.sprite = bg9; bg.type = Image.Type.Sliced; bg.color = BgDark;
            Outline border = panel.AddComponent<Outline>();
            border.effectColor = borderColor; border.effectDistance = new Vector2(1, -1);

            GameObject lblObj = new GameObject("Label", typeof(RectTransform));
            lblObj.transform.SetParent(panel.transform, false);
            SetRect(lblObj, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
                new Vector2(0, -6), new Vector2(-10, 18));
            TextMeshProUGUI lblTmp = lblObj.AddComponent<TextMeshProUGUI>();
            lblTmp.text = label; lblTmp.color = InkDim; lblTmp.fontSize = 8;
            lblTmp.fontStyle = FontStyles.Bold; lblTmp.alignment = TextAlignmentOptions.Center;

            GameObject valObj = new GameObject("Value", typeof(RectTransform));
            valObj.transform.SetParent(panel.transform, false);
            SetRect(valObj, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                new Vector2(0, 4), new Vector2(-10, -18));
            TextMeshProUGUI valTmp = valObj.AddComponent<TextMeshProUGUI>();
            valTmp.text = value; valTmp.color = borderColor == LineColor ? Color.white : borderColor;
            valTmp.fontSize = 30; valTmp.fontStyle = FontStyles.Bold;
            valTmp.alignment = TextAlignmentOptions.Center;
        }

        // ═════════════════════════════════════════════════════════════════════
        // CORNER BUTTON (top-left, forfeit / info)
        // ═════════════════════════════════════════════════════════════════════
        static GameObject BuildCornerButton(Transform parent, string name, string label,
            Sprite bg9, Color borderColor, Vector2 ancMin, Vector2 ancMax, Vector2 pos, Vector2 size)
        {
            GameObject btn = new GameObject(name);
            btn.transform.SetParent(parent, false);
            SetRect(btn, ancMin, ancMax, new Vector2(0, 0.5f), pos, size);

            Image img = btn.AddComponent<Image>();
            img.sprite = bg9; img.type = Image.Type.Sliced;
            img.color = new Color(0.08f, 0.09f, 0.22f, 0.85f);
            Outline border = btn.AddComponent<Outline>();
            border.effectColor = borderColor; border.effectDistance = new Vector2(1, -1);
            btn.AddComponent<Button>();

            GameObject txtObj = new GameObject("Text", typeof(RectTransform)); txtObj.transform.SetParent(btn.transform, false);
            SetStretch(txtObj);
            TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label; tmp.color = InkColor; tmp.fontSize = 14;
            tmp.fontStyle = FontStyles.Bold; tmp.alignment = TextAlignmentOptions.Center;

            return btn;
        }

        // ═════════════════════════════════════════════════════════════════════
        // TEXT BUTTON (small, e.g. Back)
        // ═════════════════════════════════════════════════════════════════════
        static Button BuildTextButton(Transform parent, string name, string label, Sprite bg9,
            Color bgColor, Color textColor, Vector2 ancMin, Vector2 ancMax, Vector2 pos, Vector2 size, float fontSize)
        {
            GameObject btn = new GameObject(name);
            btn.transform.SetParent(parent, false);
            SetRect(btn, ancMin, ancMax, new Vector2(1, 0), pos, size);
            Image img = btn.AddComponent<Image>();
            img.sprite = bg9; img.type = Image.Type.Sliced; img.color = bgColor;
            Button button = btn.AddComponent<Button>();

            GameObject txtObj = new GameObject("Text", typeof(RectTransform)); txtObj.transform.SetParent(btn.transform, false);
            SetStretch(txtObj);
            TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label; tmp.color = textColor; tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold; tmp.alignment = TextAlignmentOptions.Center;
            return button;
        }

        // ═════════════════════════════════════════════════════════════════════
        // POKEMON SLOT (world-space 3D/2D arena)
        // ═════════════════════════════════════════════════════════════════════
        static void CreatePokemonSlot(Transform parent, string name, Vector3 pos, bool isPlayer)
        {
            GameObject slot = new GameObject(name);
            slot.transform.SetParent(parent);
            slot.transform.position = pos;
            slot.transform.localScale = new Vector3(5f, 5f, 1f);

            SpriteRenderer sr = slot.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd");
            sr.color = isPlayer
                ? new Color(0.2f, 0.3f, 1f, 0.35f)
                : new Color(1f, 0.3f, 0.2f, 0.35f);
            sr.sortingOrder = pos.y > 0 ? -1 : 1;

            // Shadow ellipse
            GameObject shadow = new GameObject("Ground_Shadow", typeof(RectTransform));
            shadow.transform.SetParent(slot.transform);
            shadow.transform.localPosition = new Vector3(0, -0.6f, 0);
            shadow.transform.localScale = new Vector3(1f, 0.3f, 1f);
            SpriteRenderer shadowSr = shadow.AddComponent<SpriteRenderer>();
            shadowSr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            shadowSr.color = new Color(0, 0, 0, 0.4f);

            // World-space label
            GameObject lbl = new GameObject("Label_Slot", typeof(RectTransform));
            lbl.transform.SetParent(slot.transform);
            lbl.transform.localPosition = new Vector3(0, 0.8f, 0);
            lbl.transform.localScale = new Vector3(0.04f, 0.04f, 1f);
            TextMeshPro tmp = lbl.AddComponent<TextMeshPro>();
            tmp.text = isPlayer ? "[Kéo thả\nPokemon Ta]" : "[Kéo thả\nPokemon Địch]";
            tmp.fontSize = 24; tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
        }

        // ═════════════════════════════════════════════════════════════════════
        // RECT HELPERS
        // ═════════════════════════════════════════════════════════════════════
        static void SetRect(GameObject go, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            RectTransform rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        static void SetStretch(GameObject go)
        {
            // Unity 6: GetComponent sau khi SetParent canvas, hoặc đã có từ constructor
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            if (rt == null) { Debug.LogWarning($"[Setup] No RectTransform on {go.name}"); return; }
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.pivot     = new Vector2(0.5f, 0.5f);
        }

        // Tạo GameObject UI với RectTransform sẵn (Unity 6 cần typeof trong constructor)
        static GameObject UIObj(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}
#endif
