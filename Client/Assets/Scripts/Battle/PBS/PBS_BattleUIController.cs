using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PokemonMMO.Battle;

namespace PokemonMMO.UI.Battle
{
    public enum BattleUIState
    {
        Idle,           // Chờ input
        PlayingEvents,  // Đang play event queue
        WaitingAction,  // Đã submit action, chờ đối thủ
        BattleEnded
    }

    public class PBS_BattleUIController : MonoBehaviour
    {
        [Header("Networking")]
        public SignalRBattleAdapter adapter;
        public BattleDataProvider dataProvider;

        [Header("HUDs")]
        public PBS_PokemonHUD playerHUD;
        public PBS_PokemonHUD enemyHUD;
        public PBS_WeatherHUD weatherHUD;
        public PBS_DialogBox dialogBox;

        [Header("Menu Panels")]
        public PBS_CommandPanel commandPanel;
        public PBS_FightPanel fightPanel;
        public PBS_PartyPanel partyPanel;

        private BattleUIState _uiState = BattleUIState.Idle;
        private string _myPlayerId;
        private List<PartyPokemonSnapshot> _myPartyCache = new List<PartyPokemonSnapshot>();

        // ── Unity Lifecycle ─────────────────────────────────────────────────────

        private void Start()
        {
            if (adapter != null)
            {
                adapter.OnBattleSync      += HandleBattleSync;
                adapter.OnTurnResolved    += HandleTurnResolved;
                adapter.OnBattleEnded     += HandleBattleEnded;
                adapter.OnActionAccepted  += HandleActionAccepted;
                adapter.OnConnectionChanged += HandleConnection;
                adapter.OnServerError     += HandleError;
            }

            if (commandPanel != null) commandPanel.OnActionSelected += HandleCommandSelected;
            if (fightPanel   != null) fightPanel.OnMoveSelected     += HandleMoveSelected;
            if (partyPanel   != null)
            {
                partyPanel.OnPokemonSelected += HandleSwitchPokemon;
                partyPanel.OnBack            += ShowCommandMenu;
            }

            HideAllMenus();
            dialogBox?.SetTextInstant("Đang kết nối đến trận đấu...");
        }

        private void OnDestroy()
        {
            if (adapter != null)
            {
                adapter.OnBattleSync        -= HandleBattleSync;
                adapter.OnTurnResolved      -= HandleTurnResolved;
                adapter.OnBattleEnded       -= HandleBattleEnded;
                adapter.OnActionAccepted    -= HandleActionAccepted;
                adapter.OnConnectionChanged -= HandleConnection;
                adapter.OnServerError       -= HandleError;
            }
        }

        // ── Menu Helpers ────────────────────────────────────────────────────────

        private void HideAllMenus()
        {
            commandPanel?.Hide();
            fightPanel?.Hide();
            partyPanel?.Hide();
        }

        private void ShowCommandMenu()
        {
            HideAllMenus();
            if (commandPanel != null && _uiState == BattleUIState.Idle)
                commandPanel.Show();
        }

        // ── SignalR Callbacks ───────────────────────────────────────────────────

        private void HandleConnection(bool isConnected, string msg)
        {
            dialogBox?.SetTextInstant(msg);
        }

        private void HandleError(string err)
        {
            dialogBox?.SetTextInstant($"Error: {err}");
            _uiState = BattleUIState.Idle;
            ShowCommandMenu();
        }

        private void HandleBattleSync(BattleSession session)
        {
            _uiState    = BattleUIState.Idle;
            _myPlayerId = PlayerPrefs.GetString("account_id");

            bool isPlayer1  = session.Player1Id == _myPlayerId;
            var  myTeam     = isPlayer1 ? session.Team1 : session.Team2;
            var  enemyTeam  = isPlayer1 ? session.Team2 : session.Team1;

            _myPartyCache.Clear();

            // Lấy chỉ số Pokemon đang ra sân (index 0 trong Single / Double)
            int myActiveIdx    = (isPlayer1 ? session.ActiveIndices1 : session.ActiveIndices2)?[0] ?? 0;
            int enemyActiveIdx = (isPlayer1 ? session.ActiveIndices2 : session.ActiveIndices1)?[0] ?? 0;

            // === Player HUD ===
            if (myActiveIdx >= 0 && myActiveIdx < myTeam.Count)
            {
                var myActive = myTeam[myActiveIdx];
                playerHUD?.Initialize(
                    myActive.DisplayName,
                    myActive.MaxHp, myActive.CurrentHp, myActive.Level,
                    myActive.NonVolatileStatus != PokemonStatusCondition.None
                        ? myActive.NonVolatileStatus.ToString()
                        : "NONE"
                );

                // Setup Fight Panel
                if (fightPanel != null)
                {
                    var displayMoves = new List<MoveDisplayInfo>();
                    foreach (var m in myActive.Moves)
                    {
                        displayMoves.Add(new MoveDisplayInfo
                        {
                            MoveId    = m.MoveId,
                            Name      = $"Move {m.MoveId}",  // TODO: BattleDataProvider
                            Type      = "Normal",
                            Category  = "Physical",
                            Power     = 50,
                            Accuracy  = 100,
                            CurrentPp = m.CurrentPp,
                            MaxPp     = 20
                        });
                    }
                    fightPanel.SetMoves(displayMoves);
                    fightPanel.SetSpecialButton(false, false);
                }
            }

            // === Enemy HUD ===
            if (enemyActiveIdx >= 0 && enemyActiveIdx < enemyTeam.Count)
            {
                var enemyActive = enemyTeam[enemyActiveIdx];
                enemyHUD?.Initialize(
                    $"Foe {enemyActive.DisplayName}",
                    enemyActive.MaxHp, enemyActive.CurrentHp, enemyActive.Level,
                    enemyActive.NonVolatileStatus != PokemonStatusCondition.None
                        ? enemyActive.NonVolatileStatus.ToString()
                        : "NONE"
                );
            }

            // === Party Cache (cho menu đổi Pokemon) ===
            for (int i = 0; i < myTeam.Count; i++)
            {
                var p = myTeam[i];
                _myPartyCache.Add(new PartyPokemonSnapshot
                {
                    Index           = i,
                    SpeciesId       = p.SpeciesId.ToString(),
                    Nickname        = p.Nickname,
                    Level           = p.Level,
                    CurrentHp       = p.CurrentHp,
                    MaxHp           = p.MaxHp,
                    StatusCondition = p.NonVolatileStatus != PokemonStatusCondition.None
                                        ? p.NonVolatileStatus.ToString()
                                        : "NONE"
                });
            }

            // === Weather HUD ===
            weatherHUD?.UpdateWeather(session.Weather.ToString(), session.WeatherTurnsLeft);

            dialogBox?.SetTextInstant("What will you do?");
            ShowCommandMenu();
        }

        private void HandleActionAccepted(string action)
        {
            _uiState = BattleUIState.WaitingAction;
            HideAllMenus();
            dialogBox?.SetTextInstant("Đang đợi người chơi khác...");
        }

        private void HandleBattleEnded(string winnerPlayerId, string reason)
        {
            _uiState = BattleUIState.BattleEnded;
            HideAllMenus();

            string msg;
            if (string.IsNullOrEmpty(winnerPlayerId))
                msg = "Draw!";
            else
                msg = (winnerPlayerId == _myPlayerId) ? "You won!" : "You lost!";

            dialogBox?.SetTextInstant(msg);
        }

        private void HandleTurnResolved(BattleTurnResult result)
        {
            _uiState = BattleUIState.PlayingEvents;
            HideAllMenus();
            StartCoroutine(PlayTurnEvents(result));
        }

        // ── Turn Event Playback ─────────────────────────────────────────────────

        private IEnumerator PlayTurnEvents(BattleTurnResult result)
        {
            if (result.typedEvents != null)
            {
                foreach (var evt in result.typedEvents)
                    yield return StartCoroutine(PlaySingleEvent(evt));
            }

            if (result.State == BattleState.Ended)
            {
                HandleBattleEnded(result.WinnerPlayerId, "Resolved");
            }
            else
            {
                _uiState = BattleUIState.Idle;
                dialogBox?.SetTextInstant("What will you do?");
                ShowCommandMenu();
            }
        }

        private IEnumerator PlaySingleEvent(BattleEventBase evt)
        {
            // === Cập nhật UI trực tiếp ===
            switch (evt)
            {
                case WeatherChangedEvent wce:
                    weatherHUD?.UpdateWeather(wce.NewWeather.ToString(), wce.TurnsLeft);
                    break;
                case WeatherEndedEvent _:
                    weatherHUD?.UpdateWeather("NONE", 0);
                    break;
                case PokemonDamageEvent pde:
                    GetTargetHUD(pde.PlayerId)?.UpdateHP(pde.HpAfter, pde.MaxHp);
                    break;
                case StatusInflictedEvent sie:
                    GetTargetHUD(sie.PlayerId)?.UpdateStatus(sie.Status.ToString());
                    break;
                case StatusHealedEvent she:
                    GetTargetHUD(she.PlayerId)?.UpdateStatus("NONE");
                    break;
                case StatChangeEvent sce:
                {
                    var hud = GetTargetHUD(sce.PlayerId);
                    if (hud != null)
                        yield return StartCoroutine(hud.FlashStatChange(sce.Stat.ToString(), sce.Stages));
                    break;
                }
            }

            // === Hiển thị Text ===
            string msg = GetEventMessage(evt);
            if (!string.IsNullOrEmpty(msg) && dialogBox != null)
                yield return StartCoroutine(dialogBox.DrawTextAndWait(msg));
        }

        private PBS_PokemonHUD GetTargetHUD(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return null;
            return (playerId == _myPlayerId) ? playerHUD : enemyHUD;
        }

        private static string GetEventMessage(BattleEventBase evt)
        {
            switch (evt)
            {
                case MoveUsedEvent e:          return $"{e.PokemonName} used {e.MoveName}!";
                case MoveMissedEvent e:         return $"{e.PokemonName}'s attack missed!";
                case MoveNoEffectEvent e:       return $"It had no effect on {e.TargetName}!";
                case PokemonFaintEvent e:       return $"{e.PokemonName} fainted!";
                case StatusInflictedEvent e:    return $"{e.PokemonName} was inflicted with {e.Status}.";
                case StatusHealedEvent e:       return $"{e.PokemonName} was cured of {e.Status}.";
                case SuperEffectiveEvent _:     return "It's super effective!";
                case NotVeryEffectiveEvent _:   return "It's not very effective...";
                case ParalysisStuckEvent e:     return $"{e.PokemonName} is fully paralyzed!";
                case SleepSkipEvent e:          return $"{e.PokemonName} is fast asleep.";
                case FreezeThawEvent e:         return $"{e.PokemonName} thawed out!";
                case WeatherDamageEvent e:      return $"{e.PokemonName} was buffeted by the {e.Weather}!";
                case BattleEndEvent e:          return e.WinnerPlayerId != null ? "Battle concluded!" : "Draw!";
                case MessageEvent e:            return e.Message;
                default:                        return "";
            }
        }

        // ── User Input Actions ──────────────────────────────────────────────────

        private void HandleCommandSelected(string actionType)
        {
            HideAllMenus();
            switch (actionType)
            {
                case "FIGHT":
                    // Dùng cache moves từ fightPanel (đã được SetMoves lúc sync)
                    fightPanel?.gameObject.SetActive(true);
                    break;
                case "POKEMON":
                    partyPanel?.Show(_myPartyCache, null);
                    break;
                default:
                    ShowCommandMenu();
                    break;
            }
        }

        private void HandleMoveSelected(int slot)
        {
            if (_uiState != BattleUIState.Idle) return;
            adapter?.ChooseMoveAsync(slot);
        }

        private void HandleSwitchPokemon(int partyIndex)
        {
            if (_uiState != BattleUIState.Idle) return;
            if (partyIndex < 0 || partyIndex >= _myPartyCache.Count) return;

            var p = _myPartyCache[partyIndex];
            if (p.IsFainted)
            {
                dialogBox?.SetTextInstant("There is no will to fight!");
                partyPanel?.Show(_myPartyCache, null);
                return;
            }

            adapter?.SwitchPokemonAsync(partyIndex);
        }
    }
}
