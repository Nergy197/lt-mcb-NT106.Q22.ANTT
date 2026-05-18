using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PokemonMMO.UI
{
    public class AuthUIManager : MonoBehaviour
    {
        // â”€â”€ Server â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        [Header("Server")]
        public string serverUrl     = "https://lt-mcb-nt106q22antt-production-cc69.up.railway.app";
        [Tooltip("Scene load sau khi login thÃ nh cÃ´ng. Äá»ƒ trá»‘ng = khÃ´ng chuyá»ƒn.")]
        public string gameSceneName = "Menu scene";

        // â”€â”€ Views â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        [Header("Views")]
        public GameObject mainMenuView;
        public GameObject loginView;
        public GameObject signUpView;
        public GameObject forgotPasswordView;
        public GameObject resetPasswordView;

        // â”€â”€ Login â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        [Header("Login")]
        public InputField loginUsernameInput;
        public InputField loginPasswordInput;
        public Text       loginFeedback;

        // â”€â”€ Sign Up â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        [Header("Sign Up")]
        public InputField signUpUsernameInput;
        public InputField signUpEmailInput;
        public InputField signUpPasswordInput;
        public Text       signUpFeedback;

        // â”€â”€ Forgot Password â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        [Header("Forgot Password")]
        public InputField forgotEmailInput;
        public Text       forgotFeedback;

        // â”€â”€ Reset Password â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        [Header("Reset Password")]
        public InputField resetTokenInput;
        public InputField resetNewPasswordInput;
        public Text       resetFeedback;

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private const string TokenKey = "jwt_token";
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        private readonly Queue<Action> _mainThread = new Queue<Action>();

        private void Awake()
        {
            // Äáº£m báº£o cÃ¡c Ã´ nháº­p liá»‡u khÃ´ng bá»‹ giá»›i háº¡n Ä‘á»™ dÃ i
            if (loginUsernameInput != null) loginUsernameInput.characterLimit = 0;
            if (signUpUsernameInput != null) signUpUsernameInput.characterLimit = 0;
            if (signUpEmailInput != null) signUpEmailInput.characterLimit = 0;
            if (forgotEmailInput != null) forgotEmailInput.characterLimit = 0;
        }

        private void Update()
        {
            lock (_mainThread)
                while (_mainThread.Count > 0)
                    _mainThread.Dequeue()?.Invoke();
        }

        // â”€â”€ View switching â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public void ShowMainMenuView()
        {
            SetActiveView(mainMenuView);
            ClearAll();
        }

        public void ShowLoginView()
        {
            SetActiveView(loginView);
            ClearAll();
        }

        public void ShowSignUpView()
        {
            SetActiveView(signUpView);
            ClearAll();
        }

        public void ShowForgotPasswordView()
        {
            SetActiveView(forgotPasswordView);
            ClearAll();
        }

        public void ShowResetPasswordView()
        {
            SetActiveView(resetPasswordView);
            ClearAll();
        }

        private void SetActiveView(GameObject target)
        {
            mainMenuView?.SetActive(mainMenuView == target);
            loginView?.SetActive(loginView == target);
            signUpView?.SetActive(signUpView == target);
            forgotPasswordView?.SetActive(forgotPasswordView == target);
            resetPasswordView?.SetActive(resetPasswordView == target);
        }

        // â”€â”€ Button handlers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public void OnLoginSubmit()
        {
            string username = loginUsernameInput?.text?.Trim() ?? "";
            string password = loginPasswordInput?.text?.Trim('\r', '\n', '\u200B') ?? "";

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                SetFeedback(loginFeedback, "Vui lÃ²ng Ä‘iá»n Ä‘áº§y Ä‘á»§ thÃ´ng tin.", isError: true);
                return;
            }

            SetFeedback(loginFeedback, "Äang Ä‘Äƒng nháº­p...", isError: false);
            SetInteractable(false);
            _ = LoginAsync(username, password);
        }

        public void OnSignUpSubmit()
        {
            string username = signUpUsernameInput?.text?.Trim() ?? "";
            string email    = signUpEmailInput?.text?.Trim()    ?? "";
            string password = signUpPasswordInput?.text?.Trim('\r', '\n', '\u200B') ?? "";

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                SetFeedback(signUpFeedback, "Vui lÃ²ng Ä‘iá»n Ä‘áº§y Ä‘á»§ thÃ´ng tin.", isError: true);
                return;
            }

            SetFeedback(signUpFeedback, "Äang Ä‘Äƒng kÃ½...", isError: false);
            SetInteractable(false);
            _ = RegisterAsync(username, email, password);
        }

        public void OnForgotPasswordSubmit()
        {
            string email = forgotEmailInput?.text?.Trim() ?? "";

            if (string.IsNullOrEmpty(email) || !email.Contains('@'))
            {
                SetFeedback(forgotFeedback, "Vui lÃ²ng nháº­p email há»£p lá»‡.", isError: true);
                return;
            }

            SetFeedback(forgotFeedback, "Äang gá»­i yÃªu cáº§u...", isError: false);
            SetInteractable(false);
            _ = ForgotPasswordAsync(email);
        }

        public void OnResetPasswordSubmit()
        {
            string token       = resetTokenInput?.text?.Trim()       ?? "";
            string newPassword = resetNewPasswordInput?.text          ?? "";

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(newPassword))
            {
                SetFeedback(resetFeedback, "Vui lÃ²ng Ä‘iá»n Ä‘áº§y Ä‘á»§ thÃ´ng tin.", isError: true);
                return;
            }

            if (newPassword.Length < 6)
            {
                SetFeedback(resetFeedback, "Máº­t kháº©u pháº£i cÃ³ Ã­t nháº¥t 6 kÃ½ tá»±.", isError: true);
                return;
            }

            SetFeedback(resetFeedback, "Äang Ä‘áº·t láº¡i máº­t kháº©u...", isError: false);
            SetInteractable(false);
            _ = ResetPasswordAsync(token, newPassword);
        }

        // â”€â”€ API tasks â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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
                        PlayerPrefs.SetString(TokenKey,      data.Token);
                        PlayerPrefs.SetString("username",    data.Username);
                        PlayerPrefs.SetString("account_id",  data.AccountId);
                        PlayerPrefs.SetString("player_id",   data.PlayerId);
                        PlayerPrefs.Save();
                        SetInteractable(true);
                        SetFeedback(loginFeedback, $"ChÃ o má»«ng, {data.Username}! Äang vÃ o game...", isError: false);
                        Debug.Log($"[Auth] Login OK â€“ AccountId: {data.AccountId}, PlayerId: {data.PlayerId}");
                        if (!string.IsNullOrEmpty(gameSceneName))
                            SceneManager.LoadScene(gameSceneName);
                    });
                }
                else
                {
                    string msg = ParseError(json) ?? "ÄÄƒng nháº­p tháº¥t báº¡i.";
                    Dispatch(() => { SetInteractable(true); SetFeedback(loginFeedback, msg, isError: true); });
                }
            }
            catch (Exception ex)
            {
                Dispatch(() => { SetInteractable(true); SetFeedback(loginFeedback, "KhÃ´ng káº¿t ná»‘i Ä‘Æ°á»£c server.", isError: true); });
                Debug.LogError($"[Auth] LoginAsync: {ex.Message}");
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
                        SetFeedback(signUpFeedback, "ÄÄƒng kÃ½ thÃ nh cÃ´ng! Äang chuyá»ƒn sang Ä‘Äƒng nháº­p...", isError: false);
                        Debug.Log("[Auth] Register OK");
                        await Task.Delay(1500);
                        Dispatch(() =>
                        {
                            ShowLoginView();
                            if (loginUsernameInput != null) loginUsernameInput.text = username;
                        });
                    });
                }
                else
                {
                    string msg = ParseError(json) ?? "ÄÄƒng kÃ½ tháº¥t báº¡i.";
                    Dispatch(() => { SetInteractable(true); SetFeedback(signUpFeedback, msg, isError: true); });
                }
            }
            catch (Exception ex)
            {
                Dispatch(() => { SetInteractable(true); SetFeedback(signUpFeedback, "KhÃ´ng káº¿t ná»‘i Ä‘Æ°á»£c server.", isError: true); });
                Debug.LogError($"[Auth] RegisterAsync: {ex.Message}");
            }
        }

        private async Task ForgotPasswordAsync(string email)
        {
            try
            {
                var body    = JsonUtility.ToJson(new ForgotPasswordDto { Email = email });
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var resp    = await Http.PostAsync($"{serverUrl}/api/auth/forgot-password", content);
                var json    = await resp.Content.ReadAsStringAsync();

                if (resp.IsSuccessStatusCode)
                {
                    var data = JsonUtility.FromJson<ForgotPasswordResponseDto>(json);
                    Dispatch(async () =>
                    {
                        SetInteractable(true);
                        // Server tráº£ token tháº³ng (mÃ´i trÆ°á»ng dev). Copy token vÃ o Ã´ reset.
                        if (data != null)
                        {
                            resetTokenInput.text = data.resetToken ?? "";
                        }
                        SetFeedback(forgotFeedback, "ÄÃ£ nháº­n token! Äang chuyá»ƒn sang Ä‘áº·t láº¡i máº­t kháº©u...", isError: false);
                        Debug.Log($"[Auth] ForgotPassword OK â€“ token: {data.resetToken}");
                        await Task.Delay(1200);
                        Dispatch(ShowResetPasswordView);
                    });
                }
                else
                {
                    string msg = ParseError(json) ?? "KhÃ´ng tÃ¬m tháº¥y email nÃ y.";
                    Dispatch(() => { SetInteractable(true); SetFeedback(forgotFeedback, msg, isError: true); });
                }
            }
            catch (Exception ex)
            {
                Dispatch(() => { SetInteractable(true); SetFeedback(forgotFeedback, "KhÃ´ng káº¿t ná»‘i Ä‘Æ°á»£c server.", isError: true); });
                Debug.LogError($"[Auth] ForgotPasswordAsync: {ex.Message}");
            }
        }

        private async Task ResetPasswordAsync(string token, string newPassword)
        {
            try
            {
                var body    = JsonUtility.ToJson(new ResetPasswordDto { Token = token, NewPassword = newPassword });
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var resp    = await Http.PostAsync($"{serverUrl}/api/auth/reset-password", content);
                var json    = await resp.Content.ReadAsStringAsync();

                if (resp.IsSuccessStatusCode)
                {
                    Dispatch(async () =>
                    {
                        SetInteractable(true);
                        SetFeedback(resetFeedback, "Äá»•i máº­t kháº©u thÃ nh cÃ´ng! Äang chuyá»ƒn vá» Ä‘Äƒng nháº­p...", isError: false);
                        Debug.Log("[Auth] ResetPassword OK");
                        await Task.Delay(1500);
                        Dispatch(ShowLoginView);
                    });
                }
                else
                {
                    string msg = ParseError(json) ?? "Token khÃ´ng há»£p lá»‡ hoáº·c Ä‘Ã£ háº¿t háº¡n.";
                    Dispatch(() => { SetInteractable(true); SetFeedback(resetFeedback, msg, isError: true); });
                }
            }
            catch (Exception ex)
            {
                Dispatch(() => { SetInteractable(true); SetFeedback(resetFeedback, "KhÃ´ng káº¿t ná»‘i Ä‘Æ°á»£c server.", isError: true); });
                Debug.LogError($"[Auth] ResetPasswordAsync: {ex.Message}");
            }
        }

        // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void Dispatch(Action action)
        {
            lock (_mainThread) _mainThread.Enqueue(action);
        }

        private static string ParseError(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var e = JsonUtility.FromJson<ErrorDto>(json);
                return string.IsNullOrEmpty(e?.message) ? null : e.message;
            }
            catch { return null; }
        }

        private void SetFeedback(Text label, string msg, bool isError)
        {
            if (label == null) return;
            label.text  = msg;
            label.color = isError ? new Color(1f, 0.35f, 0.35f) : new Color(0.35f, 1f, 0.55f);
        }

        private void ClearAll()
        {
            if (loginFeedback        != null) loginFeedback.text        = "";
            if (signUpFeedback       != null) signUpFeedback.text       = "";
            if (forgotFeedback       != null) forgotFeedback.text       = "";
            if (resetFeedback        != null) resetFeedback.text        = "";
        }

        private void SetInteractable(bool on)
        {
            if (loginUsernameInput    != null) loginUsernameInput.interactable    = on;
            if (loginPasswordInput    != null) loginPasswordInput.interactable    = on;
            if (signUpUsernameInput   != null) signUpUsernameInput.interactable   = on;
            if (signUpEmailInput      != null) signUpEmailInput.interactable      = on;
            if (signUpPasswordInput   != null) signUpPasswordInput.interactable   = on;
            if (forgotEmailInput      != null) forgotEmailInput.interactable      = on;
            if (resetTokenInput       != null) resetTokenInput.interactable       = on;
            if (resetNewPasswordInput != null) resetNewPasswordInput.interactable = on;
        }

        // â”€â”€ DTOs â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        [Serializable] private class LoginRequestDto           { public string Username; public string Password; }
        [Serializable] private class RegisterRequestDto        { public string Username; public string Email; public string Password; }
        [Serializable] private class ForgotPasswordDto         { public string Email; }
        [Serializable] private class ResetPasswordDto          { public string Token; public string NewPassword; }
        [Serializable] private class AuthResponseDto           { public string Token; public string Username; public string AccountId; public string PlayerId; }
        [Serializable] private class ForgotPasswordResponseDto { public string message; public string resetToken; }
        [Serializable] private class ErrorDto                  { public string message; }
    }
}

