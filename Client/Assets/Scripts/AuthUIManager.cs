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
        // ── Server ────────────────────────────────────────────────────────────
        [Header("Server")]
        public string serverUrl     = "http://127.0.0.1:2567";
        [Tooltip("Scene load sau khi login thành công. Để trống = không chuyển.")]
        public string gameSceneName = "Menu scene";

        // ── Views ─────────────────────────────────────────────────────────────
        [Header("Views")]
        public GameObject mainMenuView;
        public GameObject loginView;
        public GameObject signUpView;
        public GameObject forgotPasswordView;
        public GameObject resetPasswordView;

        // ── Login ─────────────────────────────────────────────────────────────
        [Header("Login")]
        public InputField loginUsernameInput;
        public InputField loginPasswordInput;
        public Text       loginFeedback;

        // ── Sign Up ───────────────────────────────────────────────────────────
        [Header("Sign Up")]
        public InputField signUpUsernameInput;
        public InputField signUpEmailInput;
        public InputField signUpPasswordInput;
        public Text       signUpFeedback;

        // ── Forgot Password ───────────────────────────────────────────────────
        [Header("Forgot Password")]
        public InputField forgotEmailInput;
        public Text       forgotFeedback;

        // ── Reset Password ────────────────────────────────────────────────────
        [Header("Reset Password")]
        public InputField resetTokenInput;
        public InputField resetNewPasswordInput;
        public Text       resetFeedback;

        // ─────────────────────────────────────────────────────────────────────
        private const string TokenKey = "jwt_token";
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        private readonly Queue<Action> _mainThread = new Queue<Action>();

        private void Update()
        {
            lock (_mainThread)
                while (_mainThread.Count > 0)
                    _mainThread.Dequeue()?.Invoke();
        }

        // ── View switching ────────────────────────────────────────────────────

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

        // ── Button handlers ───────────────────────────────────────────────────

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
            string email    = signUpEmailInput?.text?.Trim()    ?? "";
            string password = signUpPasswordInput?.text         ?? "";

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                SetFeedback(signUpFeedback, "Vui lòng điền đầy đủ thông tin.", isError: true);
                return;
            }

            SetFeedback(signUpFeedback, "Đang đăng ký...", isError: false);
            SetInteractable(false);
            _ = RegisterAsync(username, email, password);
        }

        public void OnForgotPasswordSubmit()
        {
            string email = forgotEmailInput?.text?.Trim() ?? "";

            if (string.IsNullOrEmpty(email) || !email.Contains('@'))
            {
                SetFeedback(forgotFeedback, "Vui lòng nhập email hợp lệ.", isError: true);
                return;
            }

            SetFeedback(forgotFeedback, "Đang gửi yêu cầu...", isError: false);
            SetInteractable(false);
            _ = ForgotPasswordAsync(email);
        }

        public void OnResetPasswordSubmit()
        {
            string token       = resetTokenInput?.text?.Trim()       ?? "";
            string newPassword = resetNewPasswordInput?.text          ?? "";

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(newPassword))
            {
                SetFeedback(resetFeedback, "Vui lòng điền đầy đủ thông tin.", isError: true);
                return;
            }

            if (newPassword.Length < 6)
            {
                SetFeedback(resetFeedback, "Mật khẩu phải có ít nhất 6 ký tự.", isError: true);
                return;
            }

            SetFeedback(resetFeedback, "Đang đặt lại mật khẩu...", isError: false);
            SetInteractable(false);
            _ = ResetPasswordAsync(token, newPassword);
        }

        // ── API tasks ─────────────────────────────────────────────────────────

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
                        PlayerPrefs.Save();
                        SetInteractable(true);
                        SetFeedback(loginFeedback, $"Chào mừng, {data.Username}! Đang vào game...", isError: false);
                        Debug.Log($"[Auth] Login OK – AccountId: {data.AccountId}");
                        if (!string.IsNullOrEmpty(gameSceneName))
                            SceneManager.LoadScene(gameSceneName);
                    });
                }
                else
                {
                    string msg = ParseError(json) ?? "Đăng nhập thất bại.";
                    Dispatch(() => { SetInteractable(true); SetFeedback(loginFeedback, msg, isError: true); });
                }
            }
            catch (Exception ex)
            {
                Dispatch(() => { SetInteractable(true); SetFeedback(loginFeedback, "Không kết nối được server.", isError: true); });
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
                        SetFeedback(signUpFeedback, "Đăng ký thành công! Đang chuyển sang đăng nhập...", isError: false);
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
                    string msg = ParseError(json) ?? "Đăng ký thất bại.";
                    Dispatch(() => { SetInteractable(true); SetFeedback(signUpFeedback, msg, isError: true); });
                }
            }
            catch (Exception ex)
            {
                Dispatch(() => { SetInteractable(true); SetFeedback(signUpFeedback, "Không kết nối được server.", isError: true); });
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
                        // Server trả token thẳng (môi trường dev). Copy token vào ô reset.
                        if (resetTokenInput != null)
                            resetTokenInput.text = data.ResetToken ?? "";

                        SetFeedback(forgotFeedback, "Đã nhận token! Đang chuyển sang đặt lại mật khẩu...", isError: false);
                        Debug.Log($"[Auth] ForgotPassword OK – token: {data.ResetToken?[..8]}…");
                        await Task.Delay(1200);
                        Dispatch(ShowResetPasswordView);
                    });
                }
                else
                {
                    string msg = ParseError(json) ?? "Không tìm thấy email này.";
                    Dispatch(() => { SetInteractable(true); SetFeedback(forgotFeedback, msg, isError: true); });
                }
            }
            catch (Exception ex)
            {
                Dispatch(() => { SetInteractable(true); SetFeedback(forgotFeedback, "Không kết nối được server.", isError: true); });
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
                        SetFeedback(resetFeedback, "Đổi mật khẩu thành công! Đang chuyển về đăng nhập...", isError: false);
                        Debug.Log("[Auth] ResetPassword OK");
                        await Task.Delay(1500);
                        Dispatch(ShowLoginView);
                    });
                }
                else
                {
                    string msg = ParseError(json) ?? "Token không hợp lệ hoặc đã hết hạn.";
                    Dispatch(() => { SetInteractable(true); SetFeedback(resetFeedback, msg, isError: true); });
                }
            }
            catch (Exception ex)
            {
                Dispatch(() => { SetInteractable(true); SetFeedback(resetFeedback, "Không kết nối được server.", isError: true); });
                Debug.LogError($"[Auth] ResetPasswordAsync: {ex.Message}");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

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

        // ── DTOs ──────────────────────────────────────────────────────────────
        [Serializable] private class LoginRequestDto           { public string Username; public string Password; }
        [Serializable] private class RegisterRequestDto        { public string Username; public string Email; public string Password; }
        [Serializable] private class ForgotPasswordDto         { public string Email; }
        [Serializable] private class ResetPasswordDto          { public string Token; public string NewPassword; }
        [Serializable] private class AuthResponseDto           { public string Token; public string Username; public string AccountId; }
        [Serializable] private class ForgotPasswordResponseDto { public string message; public string ResetToken; }
        [Serializable] private class ErrorDto                  { public string message; }
    }
}
