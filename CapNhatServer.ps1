Write-Host "========================================="
Write-Host "   CONG CU CAP NHAT SERVER POKEMON MMO   "
Write-Host "========================================="

$AzCmd = "C:\Program Files (x86)\Microsoft SDKs\Azure\CLI2\wbin\az.cmd"
if (-not (Test-Path $AzCmd)) {
    $AzCmd = "az"
}

$RG = "Pokemon-RG"
$WebApp = "pokemon-mmo-server-123"
$AcrName = "pokemonmmoacr7929"
$ServerUrl = "$AcrName.azurecr.io"

Write-Host "1. Dang lay mat khau nha kho (ACR)..."
$AcrPwd = (& $AzCmd acr credential show -n $AcrName --query "passwords[0].value" -o tsv).Trim()

if (-not $AcrPwd) {
    Write-Error "Khong the lay mat khau! Hay chac chan ban da chay lenh 'az login' hoac 'az login --use-device-code' truoc."
    Read-Host "Nhan Enter de thoat"
    exit
}

Write-Host "2. Dang dang nhap vao Docker..."
docker login $ServerUrl -u $AcrName -p $AcrPwd

Write-Host "3. Dang dong goi (Build) Code Server moi nhat..."
Set-Location -Path "$PSScriptRoot\Server"
docker build -t "$ServerUrl/pokemon-mmo-server:latest" .
if ($LASTEXITCODE -ne 0) { 
    Write-Error "Loi khi Build Docker! Hay kiem tra lai code cua ban."
    Read-Host "Nhan Enter de thoat"
    exit 
}

Write-Host "4. Dang day (Push) ban cap nhat len Azure..."
docker push "$ServerUrl/pokemon-mmo-server:latest"
if ($LASTEXITCODE -ne 0) { 
    Write-Error "Loi khi day Docker len may!"
    Read-Host "Nhan Enter de thoat"
    exit 
}
Set-Location -Path $PSScriptRoot

Write-Host "5. Dang khoi dong lai Server..."
& $AzCmd webapp restart -n $WebApp -g $RG

Write-Host "========================================="
Write-Host "  CAP NHAT THANH CONG! VAO GAME THOI!    "
Write-Host "========================================="
Read-Host "Nhan Enter de ket thuc"
