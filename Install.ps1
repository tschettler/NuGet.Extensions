$nugetdir = $env:LocalAppData + "\NuGet\"
$nugetcmddir = $nugetdir + "Commands\"
$cmdname = "NuGet.Extensions"

$slndir = (Split-Path -Path $MyInvocation.MyCommand.Path -Parent) + "\"
$refmap = $slndir + "referencemap.json"
$nuget = $slndir + ".nuget\NuGet.exe"
$packdir = $slndir + "package"

$regpath="Registry::HKCU\Environment"
$utilsdir="C:\utilities"

Write-Output 'Installing NuGet.Extensions...';

# Get the current user path variable
$path=(Get-ItemProperty -Path $regpath -Name PATH -ErrorAction Ignore).Path

# If the path does not contain the utils dir, add it
if (!($path -like "*" + $utilsdir + "*")){
	$path = if ($path) { $path+";" } else { $path }
    $path+= $utilsdir
    Set-ItemProperty -Path $regpath -Name PATH –Value $path

    Update-EnvironmentVariables
}

# Create the utils dir and copy the nuget.exe file to it
New-Item $utilsdir -type directory -ErrorAction Ignore
Copy-Item $nuget $utilsdir -Force -Verbose

# Remove the existing NuGet.Extensions command
Remove-Item -path ($nugetcmddir + $cmdname) -Recurse -Force -ErrorAction Ignore

# Install the updated command
& $nuget Install, -ExcludeVersion, -OutputDir, $nugetcmddir, -Source, $packdir, $cmdname

# Copy the reference map
Copy-Item $refmap $nugetdir -Force -Verbose

Write-Output 'NuGet.Extensions installed. Press any key to continue...';
$x = $host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

function Update-EnvironmentVariables
{
    #requires -version 2

    if (-not ("win32.nativemethods" -as [type])) {
        # import sendmessagetimeout from win32
        add-type -Namespace Win32 -Name NativeMethods -MemberDefinition @"
[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
public static extern IntPtr SendMessageTimeout(
    IntPtr hWnd, uint Msg, UIntPtr wParam, string lParam,
    uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);
"@
    }

    $HWND_BROADCAST = [intptr]0xffff;
    $WM_SETTINGCHANGE = 0x1a;
    $result = [uintptr]::zero

    # notify all windows of environment block change
    [win32.nativemethods]::SendMessageTimeout($HWND_BROADCAST, $WM_SETTINGCHANGE,
	    [uintptr]::Zero, "Environment", 2, 5000, [ref]$result);
}