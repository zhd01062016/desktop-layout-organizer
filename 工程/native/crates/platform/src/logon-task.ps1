$ErrorActionPreference = 'Stop'
$service = New-Object -ComObject 'Schedule.Service'
$service.Connect()
$folder = $service.GetFolder('\')
$sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$name = 'PecoFence Logon ' + $sid
$task = $null
try { $task = $folder.GetTask($name) }
catch {
    if ($_.Exception.HResult -ne -2147024894) { throw }
}
switch ($env:PECOFENCE_TASK_MODE) {
    'Enable' {
        $definition = $service.NewTask(0)
        $definition.RegistrationInfo.Description = 'Start desktop fences immediately at user logon.'
        $definition.Principal.UserId = $sid
        $definition.Principal.LogonType = 3 # Interactive user token
        $definition.Principal.RunLevel = 0
        $trigger = $definition.Triggers.Create(9) # User logon
        $trigger.UserId = $sid
        $trigger.Delay = 'PT0S'
        $action = $definition.Actions.Create(0)
        $action.Path = $env:PECOFENCE_TASK_EXE
        $action.Arguments = $env:PECOFENCE_TASK_ARGS
        $action.WorkingDirectory = Split-Path -LiteralPath $env:PECOFENCE_TASK_EXE
        $definition.Settings.Enabled = $true
        $definition.Settings.Priority = 4 # Normal process priority
        $definition.Settings.DisallowStartIfOnBatteries = $false
        $definition.Settings.StopIfGoingOnBatteries = $false
        $definition.Settings.ExecutionTimeLimit = 'PT0S'
        $definition.Settings.MultipleInstances = 2 # Ignore a second task invocation
        $definition.Settings.AllowDemandStart = $true
        $registered = $folder.RegisterTaskDefinition($name, $definition, 6, $sid, $null, 3)
        if (!$registered.Enabled -or $registered.Definition.Actions.Item(1).Path -ne $env:PECOFENCE_TASK_EXE) {
            throw 'Logon task verification failed.'
        }
    }
    'Disable' {
        if ($task) { $folder.DeleteTask($name, 0) }
    }
    default { throw 'Unknown logon task operation.' }
}
