const readline = require('readline');

const BASE_URL = 'http://localhost:2567';
let currentToken = null;

const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout
});

const question = (query) => new Promise((resolve) => rl.question(query, resolve));

async function callApi(endpoint, method = 'GET', body = null, headers = {}) {
    try {
        const options = {
            method,
            headers: {
                'Content-Type': 'application/json',
                ...headers
            }
        };
        if (body) {
            options.body = JSON.stringify(body);
        }

        const response = await fetch(`${BASE_URL}${endpoint}`, options);
        const data = await response.json();

        if (!response.ok) {
            return { success: false, message: data.message || `HTTP error! status: ${response.status}`, data };
        }

        return { success: true, data };
    } catch (error) {
        return { success: false, message: error.message };
    }
}

async function register() {
    console.log('\n--- ĐĂNG KÍ TÀI KHOẢN ---');
    const username = await question('Username: ');
    const email = await question('Email: ');
    const password = await question('Password: ');

    const res = await callApi('/api/auth/register', 'POST', { username, email, password });
    if (res.success) {
        console.log('\x1b[32m%s\x1b[0m', `[SUCCESS] ${res.data.message}`);
    } else {
        console.log('\x1b[31m%s\x1b[0m', `[ERROR] ${res.message}`);
    }
}

async function login() {
    console.log('\n--- ĐĂNG NHẬP ---');
    const username = await question('Username: ');
    const password = await question('Password: ');

    const res = await callApi('/api/auth/login', 'POST', { username, password });
    if (res.success) {
        currentToken = res.data.token;
        console.log('\x1b[32m%s\x1b[0m', `[SUCCESS] Chào mừng, ${res.data.username}!`);
        console.log(`Token: ${currentToken.substring(0, 50)}...`);
    } else {
        console.log('\x1b[31m%s\x1b[0m', `[ERROR] ${res.message}`);
    }
}

async function getMe() {
    if (!currentToken) {
        console.log('\x1b[31m%s\x1b[0m', '[ERROR] Bạn cần đăng nhập trước!');
        return;
    }

    const res = await callApi('/api/auth/me', 'GET', null, { Authorization: `Bearer ${currentToken}` });
    if (res.success) {
        console.log('\n--- THÔNG TIN CÁ NHÂN ---');
        console.log(`Username: ${res.data.username}`);
        console.log(`Account ID: ${res.data.accountId}`);
    } else {
        console.log('\x1b[31m%s\x1b[0m', `[ERROR] ${res.message}`);
    }
}

async function forgotPassword() {
    console.log('\n--- QUÊN MẬT KHẨU ---');
    const email = await question('Email của bạn: ');

    const res = await callApi('/api/auth/forgot-password', 'POST', { email });
    if (res.success) {
        console.log('\x1b[32m%s\x1b[0m', `[SUCCESS] ${res.data.message}`);
        console.log(`Reset Token: ${res.data.resetToken}`);
        
        const changeNow = await question('Bạn muốn đổi mật khẩu ngay không? (y/n): ');
        if (changeNow.toLowerCase() === 'y') {
            await resetPassword(res.data.resetToken);
        }
    } else {
        console.log('\x1b[31m%s\x1b[0m', `[ERROR] ${res.message}`);
    }
}

async function resetPassword(token) {
    if (!token) {
        token = await question('Nhập Reset Token: ');
    }
    const newPassword = await question('Mật khẩu mới: ');

    const res = await callApi('/api/auth/reset-password', 'POST', { token, newPassword });
    if (res.success) {
        console.log('\x1b[32m%s\x1b[0m', `[SUCCESS] ${res.data.message}`);
    } else {
        console.log('\x1b[31m%s\x1b[0m', `[ERROR] ${res.message}`);
    }
}

async function logout() {
    if (!currentToken) {
        console.log('\x1b[31m%s\x1b[0m', '[ERROR] Bạn chưa đăng nhập!');
        return;
    }

    const res = await callApi('/api/auth/logout', 'POST', {}, { Authorization: `Bearer ${currentToken}` });
    if (res.success) {
        console.log('\x1b[32m%s\x1b[0m', `[SUCCESS] ${res.data.message}`);
        currentToken = null;
    } else {
        console.log('\x1b[31m%s\x1b[0m', `[ERROR] ${res.message}`);
    }
}

async function mainMenu() {
    while (true) {
        console.log('\n=======================================');
        console.log('   POKEMON MMO - AUTH CLI TESTER   ');
        console.log('=======================================');
        console.log('1. Đăng ký tài khoản');
        console.log('2. Đăng nhập');
        console.log('3. Xem thông tin cá nhân (Cần login)');
        console.log('4. Quên mật khẩu');
        console.log('5. Đặt lại mật khẩu');
        console.log('6. Đăng xuất');
        console.log('0. Thoát');
        console.log('=======================================');
        
        const choice = await question('Chọn tính năng: ');

        switch (choice) {
            case '1': await register(); break;
            case '2': await login(); break;
            case '3': await getMe(); break;
            case '4': await forgotPassword(); break;
            case '5': await resetPassword(); break;
            case '6': await logout(); break;
            case '0': 
                console.log('Tạm biệt!');
                rl.close();
                return;
            default:
                console.log('\x1b[33m%s\x1b[0m', 'Lựa chọn không hợp lệ!');
        }
    }
}

console.log('Đang khởi động CLI Tester...');
mainMenu();
