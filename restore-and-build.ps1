# Restore MAUI workloads, restore NuGet packages, and build the solution
# Usage: Open PowerShell in repo root and run: .\restore-and-build.ps1

Write-Host "Running 'dotnet workload restore'..."
dotnet workload restore

Write-Host "Running 'dotnet restore'..."
dotnet restore

Write-Host "Building solution (all targets)..."
dotnet build -v minimal

Write-Host "Done."