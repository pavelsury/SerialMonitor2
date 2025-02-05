Param($SolutionDir)

$publishDir = $SolutionDir + '_publish\'
Remove-Item $publishDir -Recurse -ErrorAction Ignore
New-Item -ItemType directory -Path $publishDir | Out-Null

$manifestPath = $SolutionDir + 'SerialMonitor.VS2019\source.extension.vsixmanifest'
$manifestXml = [xml](Get-Content $manifestPath -Raw)
$version = $manifestXml.PackageManifest.Metadata.Identity.Version
$vsixSrcFilename = $SolutionDir + 'SerialMonitor.VS2019\bin\Publish\SerialMonitor2_VS2019.vsix'
$vsixDstFilename = $publishDir + 'SerialMonitor2_v' + $version + '.vsix'
Copy-Item $vsixSrcFilename -Destination $vsixDstFilename

$manifestPath = $SolutionDir + 'SerialMonitor.VS2022\source.extension.vsixmanifest'
$manifestXml = [xml](Get-Content $manifestPath -Raw)
$version = $manifestXml.PackageManifest.Metadata.Identity.Version
$vsixSrcFilename = $SolutionDir + 'SerialMonitor.VS2022\bin\Publish\SerialMonitor2_VS2022.vsix'
$vsixDstFilename = $publishDir + 'SerialMonitor2_v' + $version + '_VS2022.vsix'
Copy-Item $vsixSrcFilename -Destination $vsixDstFilename

$appSrcFilename = $SolutionDir + 'SerialMonitor.Win.App\bin\Publish\net8.0-windows\publish\net_latest\SerialMonitor2.exe'
$appDstFilename = $publishDir + 'SerialMonitor2_v' + $version + '.exe'
Copy-Item $appSrcFilename -Destination $appDstFilename

$global:ProgressPreference = 'SilentlyContinue'
$pipeScriptsSrc = $SolutionDir + 'PipeScripts\*.*'
$pipeScriptsDst = $publishDir + 'PipeScripts_v' + $version + '.zip'
Compress-Archive -Path $pipeScriptsSrc -DestinationPath $pipeScriptsDst