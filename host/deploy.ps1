Param(
    [Parameter(Mandatory=$true,Position=0)]
    $serviceName,
    [Parameter(Mandatory=$true,Position=1)]
    $storageAccountName,
    [Parameter(Mandatory=$true,Position=2)]
    $location,
    [Parameter(Mandatory=$true,Position=3)]
    $tlsCertificatePath,
    [Parameter(Mandatory=$true,Position=4)]
    $tlsCertificatePassword,
    [Parameter(Mandatory=$true,Position=5)]
    $iotHubConnectionString,
    
    #common override
    $packagePath = "ProtocolGateway.Samples.Cloud\bin\Release\app.publish\ProtocolGateway.Samples.Cloud.cspkg",
    $configurationPath = "ProtocolGateway.Samples.Cloud\bin\Release\app.publish\ServiceConfiguration.Cloud.cscfg",
    $diagnosticsConfigurationPath = "ProtocolGateway.Samples.Cloud\bin\Release\app.publish\Extensions\PaaSDiagnostics.ProtocolGateway.Samples.Cloud.Host.PubConfig.xml",
    [string] $subscriptionName = $null,
    [string] $diagnostics = $null,
    $slot = "Staging",
    $vmCount = "2",

    #seldom used
    $deploymentLabel = "",
    [switch]
    $vipSwap = $true
    )

#$ErrorActionPreference = "Stop"

function Execute-Command([scriptblock]$Command, [Alias('EEL')][Array]$ExpectedExceptionList, $maxCommandRetries=5, $writeStatus=$true)
{
    $currentRetry = 0
    $success = $false
    $returnvalue = $null
    do {
        try
        {
            if ($writeStatus) { Write-Host "$(Get-Date -f $timeStampFormat) - Executing $($Command.ToString())" }
            $returnvalue = Invoke-Command -ScriptBlock $Command -EV ex -EA Stop 2> $null
            $success = $true
            if ($writeStatus) { Write-Host "$(Get-Date -f $timeStampFormat) - Successfully executed $($Command.ToString())" }
        }
        catch [exception]
        {
            Write-Verbose "$(Get-Date -f $timeStampFormat) - Exception occurred while executing $($Command.ToString()). Exception is: $ex"
            Write-Verbose ("Exception: {0}" -f $ex.Exception)
            Write-Verbose ("ErrorDetails: {0}" -f $ex.ErrorDetails)
            Write-Verbose ("PSMessageDetails: {0}" -f $ex.PSMessageDetails)
            Write-Verbose ("InnerException: {0}" -f $ex.Exception.InnerException)
            Write-Verbose ("InnerException Status: {0}" -f $ex.Exception.InnerException.Status)
            Write-Verbose ("Message: {0}" -f $ex.Exception.Message)
            Write-Verbose ("Status: {0}" -f $ex.Exception.Status)
            Write-Verbose ("Error code: {0}" -f $ex.Exception.ErrorCode)
            Write-Verbose "ExceptionList: $($ExpectedExceptionList -ne $Null)"

            if ($ExpectedExceptionList)
            {
                if ($ExpectedExceptionList.Contains($ex.Exception.getType()))
                {
                    Write-Host "$(Get-Date -f $timeStampFormat) - Expected Exception"
                    break
                }
                if ($ExpectedExceptionList.Contains($ex.Exception.getType().Name))
                {
                    Write-Host "$(Get-Date -f $timeStampFormat) - Expected Exception"
                    break
                }
                if ($ExpectedExceptionList.Contains($global:resourceNotFound))
                {
                    Write-Verbose "Checking for resource not found..."
                    if ((IsResourceNotFound $ex))
                    {
                        Write-Host "$(Get-Date -f $timeStampFormat) - Resource not found"
                        break
                    }
                }
            }

            if ($currentRetry++ -gt $maxCommandRetries)
            {
                $message = "Can not execute $Command . The error is:  $ex"
                Write-Warning "$(Get-Date -f $timeStampFormat) $message"
                throw $message
            }

            switch ($ex.Exception.GetType().Name)
            {
                "CommunicationException"
                {
                    Start-Sleep 30
                    Write-Warning "$(Get-Date -f $timeStampFormat) - Caught communication error. Will retry";
                }

                "TimeoutException"
                {
                    Start-Sleep 30
                    Write-Warning ("$(Get-Date -f $timeStampFormat) - $serviceBaseName - Caught communication error. Will retry " + (5 - $numServiceRetries) + " more times");
                }

                "IOException"
                {
                    Write-Warning "$(Get-Date -f $timeStampFormat) - Caught IOException. Will retry";
                    Start-Sleep 30
                }

                "StorageException"
                {
                    Write-Warning "$(Get-Date -f $timeStampFormat) - Caught StorageException. Will retry";
                    Start-Sleep 30
                }

                "WebException"
                {
                    switch ($ex.Exception.Status)
                    {
                        "ConnectFailure"
                        {
                            Start-Sleep 30
                            Write-Warning ("$(Get-Date -f $timeStampFormat) - $serviceBaseName - Caught communication error. Will retry " + (5 - $numServiceRetries) + " more times");
                        }
                        "SecureChannelFailure"
                        {
                            Write-Warning  "SecureChannelFailure - reloading subscription"
                            # Reload subscription
                            $subscriptions = Get-AzureSubscription -EA SilentlyContinue
                            $subscriptions | Remove-AzureSubscription -Confirm:$false -Force -EA SilentlyContinue
                            LoadSubscription($false)
                        }
                        default
                        {
                            throw $ex.Exception
                        }
                    }
                }

                "HttpRequestException"
                {
                    switch ($ex.Exception.InnerException.Status)
                    {
                        "ConnectFailure"
                        {
                            Start-Sleep 30
                            Write-Warning ("$(Get-Date -f $timeStampFormat) - $serviceBaseName - Caught communication error. Will retry " + (5 - $numServiceRetries) + " more times");
                        }
                        "SecureChannelFailure"
                        {
                            Write-Warning  "SecureChannelFailure - reloading subscription"
                            # Reload subscription
                            $subscriptions = Get-AzureSubscription -EA SilentlyContinue
                            $subscriptions | Remove-AzureSubscription -Confirm:$false -Force -EA SilentlyContinue
                            LoadSubscription($false)
                        }
                        default
                        {
                            throw $ex.Exception.InnerException
                        }
                    }
                }

                default
                {
                    throw $ex
                }
            }
        }
    } while (!$success)
    return $returnvalue
}

function IsResourceNotFound()
{
    Param(
        [Parameter(Mandatory=$true,Position=0)] $exc
    )

    write-verbose "GetType.Name $($exc.Exception.GetType().Name)"
    if ($exc.Exception.GetType().Name -eq "ResourceNotFoundException")
    {
        return $true
    }
    write-verbose "ErrorCode $($exc.Exception.ErrorCode)"
    if ($exc.Exception.ErrorCode -eq "ResourceNotFound")
    {
        return $true
    }
    write-verbose "Messsage $($exc.Exception.Message)"
    if ($exc.Exception.Message.StartsWith("ResourceNotFound"))
    {
        return $true
    }

    if ($exc.Exception.GetType().Name -ne "ServiceManagementClientException")
    {
        return $false
    }
    write-verbose "ErrorDetails.Code $($exc.Exception.ErrorDetails.Code)"
    return ($exc.Exception.ErrorDetails.Code -eq 'ResourceNotFound')
}

Add-AzureAccount
#Import-AzurePublishSettingsFile $publishSettingsFile

if ($subscriptionName)
{
    Select-AzureSubscription -SubscriptionName $subscriptionName -Current
}
$subscription = Get-AzureSubscription -Current
    
if (!$deploymentLabel)
{
    $deploymentLabel = "AutoDeploy $(Get-Date -f $timeStampFormat)"
}

Write-Host "$(Get-Date -f $timeStampFormat) - Service name: $serviceName"
Write-Host "$(Get-Date -f $timeStampFormat) - Storage account name: $storageAccountName"
write-host "$(Get-Date -f $timeStampFormat) - slot: $slot";
write-host "$(Get-Date -f $timeStampFormat) - Vip Swap: $vipSwap";
write-host "$(Get-Date -f $timeStampFormat) - deploymentLabel: $deploymentLabel";

$packagePath = (Resolve-Path $packagePath).ToString()
$configurationPath = (Resolve-Path $configurationPath).ToString()
$diagnosticsConfigurationPath = (Resolve-Path $diagnosticsConfigurationPath).ToString()
$tlsCertificatePath = (Resolve-Path $tlsCertificatePath).ToString()

# Storage account for storing deployment package and MQTT session state management
$storageAccount = Execute-Command -Command { Get-AzureStorageAccount $storageAccountName } -EEL @($global:resourceNotFound)
if (!$storageAccount)
{
    Write-Host "storage acc value: $storageAccount"
    $storageAccount = New-AzureStorageAccount $storageAccountName -Location $location
}
$storageAccountKey = (Get-AzureStorageKey -StorageAccountName $storageAccountName).Primary

Set-AzureSubscription -SubscriptionName $subscription.SubscriptionName -CurrentStorageAccountName $storageAccountName

$service = Execute-Command -Command { Get-AzureService $serviceName } -EEL @($global:resourceNotFound)
if (!$service)
{
    $service = Execute-Command -Command { New-AzureService $serviceName -Location $location }
}

# uploading TLS certificate if necessary
$tlsCertificateObject = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2
$tlsCertificateObject.Import((Resolve-Path $tlsCertificatePath), $tlsCertificatePassword, [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::DefaultKeySet)
$tlsCertificateThumbprint = $tlsCertificateObject.Thumbprint
$tlsCertificate = Execute-Command -Command { Get-AzureCertificate $serviceName -Thumbprint $tlsCertificateThumbprint -ThumbprintAlgorithm "sha1" } -EEL @($global:resourceNotFound)
if (!$tlsCertificate)
{
    Execute-Command -Command { Add-AzureCertificate $serviceName $tlsCertificatePath -Password $tlsCertificatePassword } -EEL @($global:resourceNotFound)
}

# finalizing service configuration
[Xml]$configuration = ((Get-Content $configurationPath).
    Replace("[parameters('iotHubConnectionString')]", $iotHubConnectionString).
    Replace("[parameters('storageAccountName')]", $storageAccountName).
    Replace("[parameters('storageAccountKey')]", $storageAccountKey).
    Replace("[parameters('tlsCertficateThumbprint')]", $tlsCertificateThumbprint).
    Replace("[parameters('storageAccountKey')]", $storageAccountKey))
$tlsCertificateElement = (Select-Xml -Xml $configuration -Namespace @{sc="http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceConfiguration"} `
    -XPath "/sc:ServiceConfiguration/sc:Role/sc:Certificates/sc:Certificate[@name='TlsCertificate']")[0].Node
$tlsCertificateElement.thumbprint = $tlsCertificateThumbprint
$instancesElement = (Select-Xml -Xml $configuration -Namespace @{sc="http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceConfiguration"} `
    -XPath "/sc:ServiceConfiguration/sc:Role/sc:Instances")[0].Node
$instancesElement.count = $vmCount
$configurationPath = (Resolve-Path $configurationPath).ToString() + ".final"
$configuration.Save($configurationPath)

# finalizing diagnostics configuration
[Xml]$diagConfigXml = Get-Content $diagnosticsConfigurationPath
if ($diagConfigXml.PublicConfig.StorageAccount)
{
    $diagConfigXml.PublicConfig.StorageAccount = $storageAccountName
}
$diagnosticsConfigurationPath = (Resolve-Path $diagnosticsConfigurationPath).ToString() + ".final"
$diagConfigXml.Save($diagnosticsConfigurationPath)

$storageContext = New-AzureStorageContext -StorageAccountName $storageAccountName -StorageAccountKey $storageAccountKey
$diagConfig = New-AzureServiceDiagnosticsExtensionConfig -StorageContext $storageContext -DiagnosticsConfigurationPath $diagnosticsConfigurationPath

# upgrading or creating a deployment
$deployment = Execute-Command -Command { Get-AzureDeployment $serviceName $slot } -EEL @($global:resourceNotFound)
if ($deployment)
{
    Write-Host "$(Get-Date -f $timeStampFormat) - Deployment exists in $servicename.  Upgrading deployment."
    Execute-Command -Command { Remove-AzureServiceDiagnosticsExtension -ServiceName $serviceName -Slot $slot }
    Execute-Command -Command { Set-AzureDeployment -Upgrade -ServiceName $serviceName -Slot $slot -Package $packagePath -Configuration $configurationPath -ExtensionConfiguration @($diagConfig) -Label "$deploymentLabel" -Force }
}
else
{
    Execute-Command -Command { New-AzureDeployment -ServiceName $serviceName -Slot $slot -Package $packagePath -Configuration $configurationPath -ExtensionConfiguration @($diagConfig) -Label "$deploymentLabel" } -EEL @([System.TimeoutException])
}
$deployment = Execute-Command -Command { Get-AzureDeployment -ServiceName $serviceName -Slot $slot }

if ($vipSwap)
{
    if ($deployment) 
    { 
        $moveStatus = Move-AzureDeployment -ServiceName $serviceName
        Write-Host "$(Get-Date -f $timeStampFormat) - Vip swap of $serviceName status: $($moveStatus.OperationStatus)"
        $vipSlot = "Production"
        if ($slot -eq $vipSlot)
        {
            $vipSlot = "Staging"
        }
        $deployment = Get-AzureDeployment -slot $vipSlot -serviceName $serviceName
    }
    else 
    { 
        Write-Output "$(Get-Date -f $timeStampFormat) - There is no deployment in $slot slot of $serviceName to swap."
        exit 1
    }
}

Write-Host "$(Get-Date -f $timeStampFormat) - deployment completed."
exit 0