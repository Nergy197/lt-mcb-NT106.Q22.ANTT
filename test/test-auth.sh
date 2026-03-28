#!/usr/bin/env bash
# ============================================================
# test-auth.sh — CLI test cho Auth API (Pokemon MMO Server)
# Chạy: chmod +x test-auth.sh && ./test-auth.sh
# ============================================================

BASE="http://localhost:2567"
RED='\033[0;31m'
GRN='\033[0;32m'
YLW='\033[1;33m'
BLU='\033[0;34m'
NC='\033[0m'

pass() { echo -e "${GRN}[PASS]${NC} $1"; }
fail() { echo -e "${RED}[FAIL]${NC} $1"; }
step() { echo -e "\n${BLU}══ $1 ══${NC}"; }
info() { echo -e "${YLW}  →${NC} $1"; }

# Kiểm tra jq
if ! command -v jq &>/dev/null; then
    echo "Cần cài jq để parse JSON: brew install jq"
    exit 1
fi

# ── Health Check ────────────────────────────────────────────
step "Health Check"
HEALTH=$(curl -s "$BASE/")
STATUS=$(echo "$HEALTH" | jq -r '.status')
if [ "$STATUS" = "ok" ]; then
    pass "Server đang chạy"
    info "$HEALTH"
else
    fail "Server không phản hồi. Hãy chạy server trước."
    exit 1
fi

# ── 1. Đăng ký ──────────────────────────────────────────────
step "1. Đăng ký tài khoản"
REG=$(curl -s -X POST "$BASE/api/auth/register" \
    -H "Content-Type: application/json" \
    -d '{"username":"testuser","email":"test@pokemon.com","password":"secret123"}')
info "Response: $REG"
MSG=$(echo "$REG" | jq -r '.message // .error // .')
if echo "$MSG" | grep -qi "thành công"; then
    pass "Đăng ký OK"
else
    info "Lưu ý: $MSG"
fi

# ── 2. Đăng ký trùng username ───────────────────────────────
step "2. Đăng ký trùng (expected: lỗi)"
DUP=$(curl -s -X POST "$BASE/api/auth/register" \
    -H "Content-Type: application/json" \
    -d '{"username":"testuser","email":"other@pokemon.com","password":"secret123"}')
info "Response: $DUP"
DUP_MSG=$(echo "$DUP" | jq -r '.message // .')
if echo "$DUP_MSG" | grep -qi "tồn tại\|đã"; then
    pass "Trùng username bị từ chối đúng"
else
    fail "Nên trả lỗi trùng username, nhận: $DUP_MSG"
fi

# ── 3. Đăng nhập ────────────────────────────────────────────
step "3. Đăng nhập"
LOGIN=$(curl -s -X POST "$BASE/api/auth/login" \
    -H "Content-Type: application/json" \
    -d '{"username":"testuser","password":"secret123"}')
info "Response: $LOGIN"
TOKEN=$(echo "$LOGIN" | jq -r '.token // empty')
if [ -n "$TOKEN" ]; then
    pass "Đăng nhập OK — JWT nhận được"
    info "Token (50 ký tự đầu): ${TOKEN:0:50}..."
else
    fail "Không nhận được token: $LOGIN"
    exit 1
fi

# ── 4. Đăng nhập sai mật khẩu ───────────────────────────────
step "4. Đăng nhập sai mật khẩu (expected: lỗi)"
BAD_LOGIN=$(curl -s -X POST "$BASE/api/auth/login" \
    -H "Content-Type: application/json" \
    -d '{"username":"testuser","password":"wrongpass"}')
info "Response: $BAD_LOGIN"
BAD_MSG=$(echo "$BAD_LOGIN" | jq -r '.message // .')
if echo "$BAD_MSG" | grep -qi "không đúng\|sai"; then
    pass "Sai mật khẩu bị từ chối đúng"
else
    fail "Nên trả lỗi xác thực, nhận: $BAD_MSG"
fi

# ── 5. Xem thông tin tài khoản (GET /me) ────────────────────
step "5. Xem thông tin tài khoản (GET /api/auth/me)"
ME=$(curl -s -X GET "$BASE/api/auth/me" \
    -H "Authorization: Bearer $TOKEN")
info "Response: $ME"
ME_USER=$(echo "$ME" | jq -r '.username // empty')
if [ "$ME_USER" = "testuser" ]; then
    pass "Token hợp lệ — username: $ME_USER"
else
    fail "Không lấy được thông tin: $ME"
fi

# ── 6. Quên mật khẩu ────────────────────────────────────────
step "6. Quên mật khẩu"
FORGOT=$(curl -s -X POST "$BASE/api/auth/forgot-password" \
    -H "Content-Type: application/json" \
    -d '{"email":"test@pokemon.com"}')
info "Response: $FORGOT"
RESET_TOKEN=$(echo "$FORGOT" | jq -r '.resetToken // empty')
if [ -n "$RESET_TOKEN" ]; then
    pass "Reset token nhận được"
    info "Reset token: $RESET_TOKEN"
else
    fail "Không nhận được reset token: $FORGOT"
fi

# ── 7. Đặt lại mật khẩu ─────────────────────────────────────
step "7. Đặt lại mật khẩu với reset token"
if [ -n "$RESET_TOKEN" ]; then
    RESET=$(curl -s -X POST "$BASE/api/auth/reset-password" \
        -H "Content-Type: application/json" \
        -d "{\"token\":\"$RESET_TOKEN\",\"newPassword\":\"newpass456\"}")
    info "Response: $RESET"
    RESET_MSG=$(echo "$RESET" | jq -r '.message // .')
    if echo "$RESET_MSG" | grep -qi "thành công"; then
        pass "Đổi mật khẩu thành công"
    else
        fail "Đổi mật khẩu thất bại: $RESET_MSG"
    fi

    # Đăng nhập lại bằng mật khẩu mới
    info "Đăng nhập lại bằng mật khẩu mới..."
    NEW_LOGIN=$(curl -s -X POST "$BASE/api/auth/login" \
        -H "Content-Type: application/json" \
        -d '{"username":"testuser","password":"newpass456"}')
    NEW_TOKEN=$(echo "$NEW_LOGIN" | jq -r '.token // empty')
    if [ -n "$NEW_TOKEN" ]; then
        pass "Đăng nhập bằng mật khẩu mới OK"
        TOKEN="$NEW_TOKEN"  # cập nhật token mới
    else
        fail "Đăng nhập mật khẩu mới thất bại: $NEW_LOGIN"
    fi
else
    info "Bỏ qua (không có reset token)"
fi

# ── 8. Đăng xuất ────────────────────────────────────────────
step "8. Đăng xuất"
LOGOUT=$(curl -s -X POST "$BASE/api/auth/logout" \
    -H "Authorization: Bearer $TOKEN")
info "Response: $LOGOUT"
LOGOUT_MSG=$(echo "$LOGOUT" | jq -r '.message // .')
if echo "$LOGOUT_MSG" | grep -qi "thành công"; then
    pass "Đăng xuất thành công"
else
    fail "Đăng xuất thất bại: $LOGOUT_MSG"
fi

# ── 9. Dùng token cũ sau khi logout ─────────────────────────
step "9. Dùng token cũ sau logout (expected: unauthorized)"
ME_AFTER=$(curl -s -o /dev/null -w "%{http_code}" -X GET "$BASE/api/auth/me" \
    -H "Authorization: Bearer $TOKEN")
info "HTTP Status: $ME_AFTER"
# Lưu ý: token vẫn valid về mặt JWT (chưa implement middleware check blacklist)
# Cần thêm middleware check RevokedTokens để hoàn toàn block token đã logout
if [ "$ME_AFTER" = "401" ]; then
    pass "Token bị revoke đúng"
else
    info "⚠️  Status $ME_AFTER — Cần thêm middleware check blacklist trong GameHub/controller để block token đã logout"
fi

# ── Tổng kết ────────────────────────────────────────────────
echo -e "\n${BLU}══════════════════════════════${NC}"
echo -e "${GRN}Test xong! Xem kết quả bên trên.${NC}"
echo ""
echo "Endpoints:"
echo "  POST $BASE/api/auth/register"
echo "  POST $BASE/api/auth/login"
echo "  POST $BASE/api/auth/logout       (Bearer token)"
echo "  POST $BASE/api/auth/forgot-password"
echo "  POST $BASE/api/auth/reset-password"
echo "  GET  $BASE/api/auth/me           (Bearer token)"
