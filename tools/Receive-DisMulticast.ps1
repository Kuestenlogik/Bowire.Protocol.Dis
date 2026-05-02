#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Subscribe to a DIS multicast group and dump received PDUs.

.DESCRIPTION
    Joins the configured IPv4 multicast group, prints a short header
    per received packet (length + first 16 bytes in hex), and continues
    until Ctrl+C. Intended as the partner process for the smoke test
    in SMOKE.md — run this first, then start the mock in another
    terminal.

.PARAMETER Group
    IPv4 multicast group to join. Defaults to the sample recording's
    group, 239.1.2.3.

.PARAMETER Port
    UDP port to listen on. Defaults to the sample's 3000.

.PARAMETER MaxPackets
    Stop after receiving this many packets. Default: unbounded
    (Ctrl+C to stop).

.EXAMPLE
    ./tools/Receive-DisMulticast.ps1
    ./tools/Receive-DisMulticast.ps1 -Group 239.1.2.3 -Port 3000 -MaxPackets 5
#>

[CmdletBinding()]
param(
    [string]$Group = '239.1.2.3',
    [int]$Port = 3000,
    [int]$MaxPackets = 0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$groupAddr = [System.Net.IPAddress]::Parse($Group)
$localEndpoint = [System.Net.IPEndPoint]::new([System.Net.IPAddress]::Any, $Port)

$udp = New-Object System.Net.Sockets.UdpClient
$udp.ExclusiveAddressUse = $false
$udp.Client.SetSocketOption(
    [System.Net.Sockets.SocketOptionLevel]::Socket,
    [System.Net.Sockets.SocketOptionName]::ReuseAddress,
    $true)
$udp.Client.Bind($localEndpoint)
$udp.JoinMulticastGroup($groupAddr)

Write-Host "listening on $Group:$Port (Ctrl+C to stop)" -ForegroundColor Cyan

$count = 0
try {
    while ($true) {
        $remote = [System.Net.IPEndPoint]::new([System.Net.IPAddress]::Any, 0)
        $bytes = $udp.Receive([ref]$remote)
        $count++

        $hex = ($bytes[0..([System.Math]::Min(15, $bytes.Length - 1))] |
                ForEach-Object { $_.ToString('X2') }) -join ' '

        # DIS PDU header: byte 2 is PDU type. 1 = Entity State, 2 = Fire,
        # 3 = Detonation, etc. We only annotate the common ones.
        $pduType = if ($bytes.Length -ge 3) { $bytes[2] } else { 0 }
        $pduLabel = switch ($pduType) {
            1 { 'EntityState' }
            2 { 'Fire' }
            3 { 'Detonation' }
            4 { 'Collision' }
            default { "type-$pduType" }
        }

        Write-Host ("#{0,-3} {1,-4} bytes  {2,-12}  {3}..." -f $count, $bytes.Length, $pduLabel, $hex)

        if ($MaxPackets -gt 0 -and $count -ge $MaxPackets) { break }
    }
}
finally {
    try { $udp.DropMulticastGroup($groupAddr) } catch { }
    $udp.Dispose()
    Write-Host "closed — received $count packet(s)." -ForegroundColor Cyan
}
