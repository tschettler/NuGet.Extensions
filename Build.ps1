$cmdname = "NuGet.Extensions"

$slndir = (Split-Path -Path $MyInvocation.MyCommand.Path -Parent) + "\"
$sln = $slndir + $cmdname + ".sln"
$nuget = $slndir + ".nuget\NuGet.exe"

$msbuild = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe"

Write-Output 'Building NuGet.Extensions...';

# restore nuget packages
& $nuget Restore, $sln

# build the solution
& $msbuild $sln, /p:Configuration=Debug

Write-Output 'NuGet.Extensions build completed. Press any key to continue...';
$x = $host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")