# Builds DateEverythingAccess-Setup.exe with the C# compiler that comes with Windows.
param([string]$Package, [string]$Out, [string]$Log)
$ErrorActionPreference = "Stop"
try {
    "Setup build started $(Get-Date)" | Set-Content -LiteralPath $Log
    $provider = New-Object Microsoft.CSharp.CSharpCodeProvider
    $params = New-Object System.CodeDom.Compiler.CompilerParameters
    $params.GenerateExecutable = $true
    $params.GenerateInMemory = $false
    $params.OutputAssembly = $Out
    $params.CompilerOptions = "/target:winexe /optimize"
    [void]$params.ReferencedAssemblies.Add("System.dll")
    [void]$params.ReferencedAssemblies.Add("System.Windows.Forms.dll")
    [void]$params.EmbeddedResources.Add((Join-Path $Package "payload.zip"))
    [void]$params.EmbeddedResources.Add((Join-Path $Package "install.ps1"))
    if (Test-Path -LiteralPath $Out) { Remove-Item -LiteralPath $Out -Force }
    $result = $provider.CompileAssemblyFromFile($params, @((Join-Path $Package "SetupStub.cs")))
    foreach ($e in $result.Errors) { "$($e.ErrorNumber) line $($e.Line): $($e.ErrorText)" | Add-Content -LiteralPath $Log }
    if ($result.Errors.HasErrors -or -not (Test-Path -LiteralPath $Out)) {
        "Setup build FAILED" | Add-Content -LiteralPath $Log
        exit 1
    }
    "Setup build finished OK: $Out" | Add-Content -LiteralPath $Log
    exit 0
} catch {
    "Setup build FAILED: $($_.Exception.Message)" | Add-Content -LiteralPath $Log
    exit 1
}
