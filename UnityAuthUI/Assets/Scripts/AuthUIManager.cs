using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace PokemonMMO.UI
{
    public class AuthUIManager : MonoBehaviour
    {
        // ── Server config ──────────────────────────────────────────────────────
        [Header("Server")]
        public string serverUrl = "http://localhost:2567";

        // ── Views ──────────────────────────────────────────────────────────────
        [Header("Views")]
        public GameObject loginView;
        public GameObject signUpView;

        // ── Login inputs ───────────────────────────────────────────────────────
        [Header("Login")]
        public InputField loginUsernameInput;
        public InputField loginPasswordInput;
        public Text       loginFeedback;

        // ── Sign Up inputs ─────────────────────────────────────────────────────
        [Header("Sign Up")]
        public InputField signUpUsernameInput;
        public InputField signUpEmailInput;
        public InputField signUpPasswordInput;
        public Text       signUpFeedback;

        private const string TokenKey = "jwt_token";

        // HttpClient is reusable — one instance per MonoBehaviour
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        // Thread-safe queue to dispatch callbacks back onto Unity's main thread
        private readonly Queue<Action> _mainThread = new Queue<Action>();

        // ── Unity lifecycle ────────────────────────────────────────────────────
        // NOTE: no Start() — initial view state is set by the generator/caller
        // to avoid overriding the intended view when the panel is first activated.

        private void Update()
        {
            lock (_mainThread)
                while (_mainThread.Count > 0)
                    _mainThread.Dequeue()?.Invoke();
        }

        // ── View switching ─────────────────────────────────────────────────────
        public void ShowLoginView()
        {
            loginView?.SetActive(true);
            signUpView?.SetActive(false);
            ClearFeedback();
        }

        public void ShowSignUpView()
        {
            loginView?.SetActive(false);
            signUpView?.SetActive(true);
            ClearFeedback();
        }

        // ── Button handlers ────────────────────────────────────────────────────
        public void OnLoginSubmit()
        {
            string username = loginUsernameInput?.text?.Trim() ?? "";
            string password = loginPasswordInput?.text ?? "";

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                SetFeedback(loginFeedback, "Vui lòng điền đầy đủ thông tin.", isError: true);
                return;
            }

            SetFeedback(loginFeedback, "Đang đăng nhập...", isError: false);
            SetInteractable(false);
            _ = LoginAsync(username, password);
        }

        public void OnSignUpSubmit()
        {
            string username = signUpUsernameInput?.text?.Trim() ?? "";
            string email    = signUpEmailInput?.text?.Trim() ?? "";
            string password = signUpPasswordInput?.text ?? "";

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                SetFeedback(signUpFeedback, "Vui lòng điền đầy đủ thông tin.", isError: true);
                return;
            }

            SetFeedback(signUpFeedback, "Đang đăng ký...", isError: false);
            SetInteractable(false);
            _ = RegisterAsync(username, email, password);
        }

        // ── API Tasks ──────────────────────────────────────────────────────────

        private async Task LoginAsync(string username, string password)
        {
            try
            {
                var body    = JsonUtility.ToJson(new LoginRequestDto { Username = username, Password = password });
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var resp    = await Http.PostAsync($"{serverUrl}/api/auth/login", content);
                var json    = await resp.Content.ReadAsStringAsync();

                if (resp.IsSuccessStatusCode)
                {
                    var data = JsonUtility.FromJson<AuthResponseDto>(json);
                    Dispatch(() =>
                    {
                        PlayerPrefs.SetString(TokenKey, data.Token);
                        PlayerPrefs.Save();
                        SetInteractable(true);
                        SetFeedback(loginFeedback, $"Chào mừng, {data.Username}!", isError: false);
                        Debug.Log($"[Auth] Login OK – AccountId: {data.AccountId}");
                        // TODO: SceneManager.LoadScene("GameScene");
                    });
                }
                else
                {
                    string msg = ParseErrorMessage(json) ?? "Đăng nhập thất bại.";
                    Dispatch(() => { SetInteractable(true); SetFeedback(loginFeedback, msg, isError: true); });
                }
            }
            catch (Exception ex)
            {
                Dispatch(() => { SetInteractable(true); SetFeedback(loginFeedback, "Không kết nối được server.", isError: true); });
                Debug.LogError($"[Auth] LoginAsync error: {ex.Message}");
            }
        }

        private async Task RegisterAsync(string username, string email, string password)
        {
            try
            {
                var body    = JsonUtility.ToJson(new RegisterRequestDto { Username = username, Email = email, Password = password });
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var resp    = await Http.PostAsync($"{serverUrl}/api/auth/register", content);
                var json    = await resp.Content.ReadAsStringAsync();

                if (resp.IsSuccessStatusCode)
                {
                    Dispatch(async () =>
                    {
                        SetInteractable(true);
                        SetFeedback(signUpFeedback, "Đăng ký thành công! Hãy đăng nhập.", isError: false);
                        Debug.Log("[Auth] Register OK");

                        await Task.Delay(1500); // chờ 1.5s rồi chuyển sang login
                        Dispatch(() =>
                        {
                            ShowLoginView();
                            if (loginUsernameInput != null) loginUsernameInput.text = username;
                        });
                    });
                }
                else
                {
                    string msg = ParseErrorMessage(json) ?? "Đăng ký thất bại.";
                    Dispatch(() => { SetInteractable(true); SetFeedback(signUpFeedback, msg, isError: true); });
                }
            }
            catch (Exception ex)
            {
                Dispatch(() => { SetInteractable(true); SetFeedback(signUpFeedback, "Không kết nối được server.", isError: true); });
                Debug.LogError($"[Auth] RegisterAsync error: {ex.Message}");
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>Enqueue an action to run on Unity's main thread next Update().</summary>
        private void Dispatch(Action action)
        {
            lock (_mainThread) _mainThread.Enqueue(action);
        }

        private static string ParseErrorMessage(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { var e = JsonUtility.FromJson<ErrorDto>(json); return string.IsNullOrEmpty(e?.message) ? null : e.message; }
            catch { return null; }
        }

        private void SetFeedback(Text label, string msg, bool isError)
        {
            if (label == null) return;
            label.text  = msg;
            label.color = isError ? new Color(1f, 0.40f, 0.40f) : new Color(0.40f, 1f, 0.60f);
        }

        private void ClearFeedback()
        {
            if (loginFeedback  != null) loginFeedback.text  = "";
            if (signUpFeedback != null) signUpFeedback.text = "";
        }

        private void SetInteractable(bool value)
        {
            if (loginUsernameInput  != null) loginUsernameInput.interactable  = value;
            if (loginPasswordInput  != null) loginPasswordInput.interactable  = value;
            if (signUpUsernameInput != null) signUpUsernameInput.interactable = value;
            if (signUpEmailInput    != null) signUpEmailInput.interactable    = value;
            if (signUpPasswordInput != null) signUpPasswordInput.interactable = value;
        }

        // ── DTOs ──────────────────────────────────────────────────────────────
        [Serializable] private class LoginRequestDto    { public string Username; public string Password; }
        [Serializable] private class RegisterRequestDto { public string Username; public string Email; public string Password; }
        [Serializable] private class AuthResponseDto    { public string Token; public string Username; public string AccountId; }
        [Serializable] private class ErrorDto           { public string message; }
    }
}
