param(
    $Repo = "ravendb/ravendb",
    $ArtifactsDir = "..\artifacts",
    $RavenDockerSettingsPath = "..\src\Raven.Server\Properties\Settings\settings.docker.windows.json",
    $WinVer = "1809",
    $DockerfileDir = "./ravendb-nanoserver")

$ErrorActionPreference = "Stop"

. ".\common.ps1"

function BuildWindowsDockerImage ($version, $WinVer) {
    $packageFileName = "RavenDB-$version-windows-x64.zip"
    $artifactsPackagePath = Join-Path -Path $ArtifactsDir -ChildPath $packageFileName

    if ([string]::IsNullOrEmpty($artifactsPackagePath))
    {
        throw "PackagePath cannot be empty."
    }

    if ($(Test-Path $artifactsPackagePath) -eq $False) {
        throw "Package file does not exist."
    }
    
    $dockerPackagePath = Join-Path -Path $DockerfileDir -ChildPath "RavenDB.zip"
    Copy-Item -Path $artifactsPackagePath -Destination $dockerPackagePath -Force
    Copy-Item -Path $RavenDockerSettingsPath -Destination $(Join-Path -Path $DockerfileDir -ChildPath "settings.json") -Force


    write-host "Build docker image: $version"
    $tags = GetWindowsImageTags $repo $version $WinVer
    $fullNameTag = $tags[0]

    write-host "Tags: $tags"

    docker build -t $fullNameTag -f "$DockerfileDir/Dockerfile.$WinVer" "$DockerfileDir" --load
    CheckLastExitCode

    foreach ($tag in $tags[1..$tags.Length]) {
        write-host "Tag $fullNameTag as $tag"
        docker tag "$fullNameTag" $tag
        CheckLastExitCode
    }

    Remove-Item -Path $dockerPackagePath
}


BuildWindowsDockerImage $(GetVersionFromArtifactName) $WinVer
