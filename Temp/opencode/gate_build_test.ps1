[Console]::OutputEncoding=[Text.Encoding]::UTF8
$ErrorActionPreference='Continue'
Set-Location C:\MNSOFT\SIGOV-PLUS
dotnet build sigov.sln --nologo -v minimal *>&1 | Out-String | Out-File -Encoding utf8 Temp\opencode\gate_build.log
$buildExit = $LASTEXITCODE
dotnet test sigov.sln --no-build --nologo *>&1 | Out-String | Out-File -Encoding utf8 Temp\opencode\gate_test.log
$testExit = $LASTEXITCODE
"BUILD_EXIT=$buildExit TEST_EXIT=$testExit" | Out-File -Append -Encoding utf8 Temp\opencode\gate_test.log
"BUILD_EXIT=$buildExit TEST_EXIT=$testExit"
