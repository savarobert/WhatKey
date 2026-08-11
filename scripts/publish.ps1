param(
    [ValidateSet("win-x64", "linux-x64")]
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
dotnet publish "$PSScriptRoot\..\WhatKey\WhatKey.csproj" -c Release -r $Runtime
