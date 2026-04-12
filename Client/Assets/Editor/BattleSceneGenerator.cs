using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using PokemonMMO.UI.Battle;
using PokemonMMO.Battle;

public class BattleSceneGenerator : EditorWindow
{
    [MenuItem("Tools/PokemonMMO/Generate Final Battle Scene")]
    public static void Generate()
    {
        // 1. Tạo Canvas chính
        GameObject canvasObj = new GameObject("BattleCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        // 2. Background
        GameObject background = CreateUIObject("BattleBackground", canvasObj.transform);
        SetRectFullStretch(background.GetComponent<RectTransform>());
        background.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f);

        // 3. Pokemon Area
        GameObject pokemonArea = CreateUIObject("PokemonArea", canvasObj.transform);
        SetRectFullStretch(pokemonArea.GetComponent<RectTransform>());
        SetupPokemonSlot(CreateUIObject("PlayerPokemonSlot", pokemonArea.transform), new Vector2(-450, -150));
        SetupPokemonSlot(CreateUIObject("EnemyPokemonSlot", pokemonArea.transform), new Vector2(450, 200));

        // 4. HUD Area
        GameObject hudArea = CreateUIObject("HUDArea", canvasObj.transform);
        SetRectFullStretch(hudArea.GetComponent<RectTransform>());
        GameObject playerHUD = InstantiatePrefab("BTLUI_PokemonHUD", hudArea.transform, "PlayerHUD", new Vector2(550, -350));
        GameObject enemyHUD = InstantiatePrefab("BTLUI_PokemonHUD", hudArea.transform, "EnemyHUD", new Vector2(-550, 350));

        // 5. Dialog
        GameObject dialogBox = InstantiatePrefab("BTLUI_DialogBox", canvasObj.transform, "DialogBox", new Vector2(0, -420));

        // 6. Menu Area
        GameObject menuArea = CreateUIObject("MenuArea", canvasObj.transform);
        SetRectFullStretch(menuArea.GetComponent<RectTransform>());

        // --- Command Panel ---
        GameObject commandPanelObj = CreateUIObject("CommandPanel", menuArea.transform);
        var commandPanel = commandPanelObj.AddComponent<PBS_CommandPanel>();
        
        GameObject fBtnObj = InstantiatePrefab("CmdBtn", commandPanelObj.transform, "FightButton", new Vector2(-150, 0));
        commandPanel.fightButton = fBtnObj.GetComponent<Button>() ?? fBtnObj.AddComponent<Button>();

        GameObject pBtnObj = InstantiatePrefab("CmdBtn", commandPanelObj.transform, "PokemonButton", new Vector2(150, 0));
        commandPanel.pokemonButton = pBtnObj.GetComponent<Button>() ?? pBtnObj.AddComponent<Button>();

        // --- Fight Panel ---
        GameObject fightPanelObj = CreateUIObject("FightPanel", menuArea.transform);
        var fightPanel = fightPanelObj.AddComponent<PBS_FightPanel>();
        fightPanel.moveButtons = new PBS_MoveButton[4];
        Vector2[] movePos = { new Vector2(-200, 50), new Vector2(200, 50), new Vector2(-200, -50), new Vector2(200, -50) };
        for(int i=0; i<4; i++) {
            GameObject btn = InstantiatePrefab("FightBtn", fightPanelObj.transform, $"Move{i+1}", movePos[i]);
            fightPanel.moveButtons[i] = btn.GetComponent<PBS_MoveButton>() ?? btn.AddComponent<PBS_MoveButton>();
        }
        fightPanelObj.SetActive(false);

        // --- Party Panel ---
        GameObject partyPanelObj = CreateUIObject("PartyPanel", menuArea.transform);
        var partyPanel = partyPanelObj.AddComponent<PBS_PartyPanel>();
        partyPanel.partyButtons = new PBS_PartyButton[6];
        for(int i=0; i<6; i++) {
            float x = (i % 2 == 0) ? -350 : 350;
            float y = 150 - (Mathf.Floor(i / 2) * 120);
            GameObject btn = InstantiatePrefab("PartyBtn", partyPanelObj.transform, $"Slot{i+1}", new Vector2(x, y));
            partyPanel.partyButtons[i] = btn.GetComponent<PBS_PartyButton>() ?? btn.AddComponent<PBS_PartyButton>();
        }
        partyPanelObj.SetActive(false);

        // 7. Battle Manager
        GameObject manager = new GameObject("BattleManager");
        var adapter = manager.AddComponent<SignalRBattleAdapter>();
        var dataProvider = manager.AddComponent<BattleDataProvider>();
        var controller = manager.AddComponent<PBS_BattleUIController>();

        adapter.serverUrl = "http://127.0.0.1:2567";
        dataProvider.serverUrl = "http://127.0.0.1:2567";

        controller.adapter = adapter;
        controller.dataProvider = dataProvider;
        controller.playerHUD = playerHUD.GetComponent<PBS_PokemonHUD>() ?? playerHUD.AddComponent<PBS_PokemonHUD>();
        controller.enemyHUD = enemyHUD.GetComponent<PBS_PokemonHUD>() ?? enemyHUD.AddComponent<PBS_PokemonHUD>();
        controller.dialogBox = dialogBox.GetComponent<PBS_DialogBox>() ?? dialogBox.AddComponent<PBS_DialogBox>();
        controller.commandPanel = commandPanel;
        controller.fightPanel = fightPanel;
        controller.partyPanel = partyPanel;

        Selection.activeGameObject = manager;
        Debug.Log("🏁 Final Battle Scene Ready!");
    }

    private static GameObject CreateUIObject(string name, Transform parent) {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static void SetRectFullStretch(RectTransform rt) {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static void SetupPokemonSlot(GameObject go, Vector2 pos) {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(500, 500);
        go.AddComponent<Image>().color = new Color(1, 1, 1, 0.05f);
    }

    private static GameObject InstantiatePrefab(string prefabName, Transform parent, string name, Vector2 pos) {
        string[] guids = AssetDatabase.FindAssets(prefabName + " t:Prefab");
        if (guids.Length > 0) {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            instance.GetComponent<RectTransform>().anchoredPosition = pos;
            return instance;
        }
        GameObject placeholder = CreateUIObject(name, parent);
        placeholder.GetComponent<RectTransform>().anchoredPosition = pos;
        placeholder.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 80);
        placeholder.AddComponent<Image>().color = Color.gray;
        return placeholder;
    }
}
