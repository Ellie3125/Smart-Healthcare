# Chạy từng phần của hệ thống trên máy dev: ./dev api | worker | web (mỗi lệnh một cửa sổ terminal).
param([Parameter(Mandatory)][ValidateSet('api', 'worker', 'web')][string]$What)

switch ($What) {
    'api'    { dotnet run --project "$PSScriptRoot/backend/src/Bootstrapper/SmartHealthcare.Api" }
    'worker' { dotnet run --project "$PSScriptRoot/backend/src/Bootstrapper/SmartHealthcare.Worker" }
    # cd thay vì --prefix: npm.cmd làm hỏng đường dẫn tuyệt đối có ký tự tiếng Việt.
    'web'    { Push-Location "$PSScriptRoot/frontend/web"; try { npm run dev } finally { Pop-Location } }
}
