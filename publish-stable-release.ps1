param(
    [switch]$DryRun = $false
)

$ErrorActionPreference = "Stop"

. '.\scripts\getScriptDirectory.ps1'
. '.\scripts\version.ps1'
. '.\scripts\githubReleases.ps1'

$PROJECT_DIR = Get-ScriptDirectory

$versionObj = GetVersionInfo

Write-Host $versionObj

$version = $versionObj.Version
$buildType = $versionObj.BuildType.ToLower()

if ($buildType -eq 'stable') {
    CreateRelease $version "hiddenshadow21" "ravendb" $env:vcsRootBranch $env:GH_TOKEN $env:ravendbChangelog $DryRun
    BumpVersion $PROJECT_DIR $versionObj.VersionPrefix $buildType $DryRun
}