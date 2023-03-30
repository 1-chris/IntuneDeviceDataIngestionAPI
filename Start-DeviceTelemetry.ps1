<#
    Sample multiplatform script to send device data to a DeviceDataEndpoint, which can be used to monitor device health.
#>

$endpoint = "https://your-hostname-here.com/DeviceDataEndpoint"
$OSEnvironment = [System.Environment]::OSVersion.Platform

if ($OSEnvironment -eq 'Win32NT') {
    # Intune device id
    $deviceId = Get-ItemPropertyValue HKLM:\SOFTWARE\Microsoft\Provisioning\Diagnostics\Autopilot\EstablishedCorrelations -Name EntDMID -ErrorAction SilentlyContinue
    $dsregCmdStatus = dsregcmd /status
    if($dsregCmdStatus -match "DeviceId") {
        $azureAdDeviceId = $dsregCmdStatus -match "DeviceID"
        $azureAdDeviceId = ($azureAdDeviceId.Split(":").trim())
        $azureAdDeviceId = $azureAdDeviceId[1]
    }
    $lastBootupDateTime = (Get-CimInstance Win32_OperatingSystem).LastBootUpTime
}

if ($OSEnvironment -eq 'Unix') {
    $deviceId = '00000000-0000-0000-0000-000000000000'
    $azureAdDeviceId = '00000000-0000-0000-0000-000000000000'
    $uptime = (uptime -s).Trim()
    $lastBootupDateTime = [DateTime]::ParseExact($uptime, 'yyyy-MM-dd HH:mm:ss', $null)
}

$headers = @{ 
    "Authorization" = "ClientAuthenticationToken" 
    "DeviceId" = $deviceId
}

$VerbosePreference = "SilentlyContinue"
$WarningPreference = "SilentlyContinue"
$PSDefaultParameterValues['Test-NetConnection:InformationLevel'] = 'Quiet'
$ProgressPreference = 'SilentlyContinue'

while ($true) {
    if ($OSEnvironment -eq 'Unix') { 
        $freeMemory = (free -m | awk 'NR==2{print $4}')
        $cpuLoad = (top -bn 1 | grep "Cpu(s)" | awk '{print $2+$4}') 
        $pingMs = ((ping -c 4 1.1.1.1 | tail -1) | awk '{print $4}' | cut -d '/' -f 2)
        $freeStorage = (Get-PSDrive /).Free / 1MB
        $computerName = hostname
    }
    if ($OSEnvironment -eq 'Win32NT') { 
        $freeMemory = [Math]::Round((Get-CIMInstance Win32_OperatingSystem).FreePhysicalMemory / 1024, 2) 
        $cpuLoad = (Get-CimInstance Win32_Processor | Measure-Object -Property LoadPercentage -Average).Average
        $pingMs = (Test-NetConnection -ComputerName 1.1.1.1).PingReplyDetails.RoundTripTime
        $freeStorage = (Get-PSDrive C).Free / 1MB
        $computerName = $env:COMPUTERNAME
    }

    $processes = Get-Process 
    $data = @{
        "deviceId" = $deviceId
        "azureAdDeviceId" = $azureAdDeviceId
        "ComputerName" = $computerName
        "LastBootUpTime" = $lastBootupDateTime
        "UptimeTotalDays" = ((Get-Date) - $lastBootupDateTime).TotalDays
        "FreePhysicalMemoryMB" = $freeMemory
        "CpuLoad" = $cpuLoad
        "ProcessCount" = $processes.Count
        "FreeStorage" = $freeStorage
        "PingMs" = $pingMs
        "OSEnvironment" = $OSEnvironment 
    } | ConvertTo-Json -Depth 2 -Compress
    
    Write-Host "Sending data $data"
    $response = Invoke-RestMethod -Uri $endpoint -Method Post -Headers $headers -Body $data -ContentType "application/json"

    Start-Sleep -Seconds 60
}
