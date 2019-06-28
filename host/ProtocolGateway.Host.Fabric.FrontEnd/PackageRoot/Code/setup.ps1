.\ProtocolGateway.Host.FabricSetup.CounterSetup.exe

# Installing .NET Framework 4.8
#.\InstallDotNet48.ps1 -norestart

# Tweaking SChannel User Reference Context settings to prevent excessive contention on SChannel context lookup
$listCount = 4096;
$lockCount = 256;
$props = Get-ItemProperty -Path Registry::HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\SecurityProviders\SCHANNEL;
$oldListCount = $props.UserContextListCount;
$oldLockCount = $props.UserContextLockCount;
if ($oldListCount -ne $listCount -or $oldLockCount -ne $lockCount) {
    Write-Output "Updating SChannel settings. Old UserContextListCount: $oldListCount; old UserContextLockCount: $oldLockCount; New UserContextListCount: $listCount; new UserContextLockCount: $lockCount.";
    New-ItemProperty -Path Registry::HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\SecurityProviders\SCHANNEL -Name UserContextListCount -PropertyType "DWord" -Value $listCount -Force;
    New-ItemProperty -Path Registry::HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\SecurityProviders\SCHANNEL -Name UserContextLockCount -PropertyType "DWord" -Value $lockCount -Force;
    Restart-Computer;
}
else {
    Write-Output "SChannel settings are up to date"
}