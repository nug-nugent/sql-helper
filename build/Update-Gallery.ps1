<#
.SYNOPSIS
    Copies a built .vsix into a private extension gallery folder and writes the gallery's atom.xml feed.

.DESCRIPTION
    Visual Studio and SSMS can watch an Atom feed of extensions (Tools > Options > Environment > Extensions >
    Additional Extension Galleries) and install newer versions automatically. Add the feed as a file URL, e.g.
    file:///C:/Dev/sql-helper/gallery/atom.xml. Run after every build of SqlHelper.VisualStudio (the project does
    this itself); the feed's version comes from the .vsix's own manifest.

    The release workflow also uses it, with -VsixUrl set to the release's download link, so the feed attached to each
    GitHub release points at that release's .vsix.
#>
param(
    [Parameter(Mandatory)] [string] $VsixPath,
    [Parameter(Mandatory)] [string] $GalleryDirectory,
    # Where the feed says to download the .vsix from; by default the copy next to atom.xml.
    [string] $VsixUrl
)

$ErrorActionPreference = 'Stop'
$GalleryDirectory = [System.IO.Path]::GetFullPath($GalleryDirectory)
Add-Type -AssemblyName System.IO.Compression.FileSystem

$zip = [System.IO.Compression.ZipFile]::OpenRead($VsixPath)
try {
    $entry = $zip.GetEntry('extension.vsixmanifest')
    $reader = New-Object System.IO.StreamReader($entry.Open())
    try { [xml] $manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
} finally {
    $zip.Dispose()
}

$metadata = $manifest.PackageManifest.Metadata
$id = $metadata.Identity.Id
$version = $metadata.Identity.Version
$vsixName = Split-Path $VsixPath -Leaf
if (-not $VsixUrl) { $VsixUrl = $vsixName }
$updated = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
function Escape([string] $text) { [System.Security.SecurityElement]::Escape($text) }

New-Item -ItemType Directory -Force $GalleryDirectory | Out-Null
Copy-Item $VsixPath (Join-Path $GalleryDirectory $vsixName) -Force

$feed = @"
<?xml version="1.0" encoding="utf-8"?>
<feed xmlns="http://www.w3.org/2005/Atom">
  <title type="text">$(Escape $metadata.DisplayName) gallery</title>
  <id>$(Escape $id).gallery</id>
  <updated>$updated</updated>
  <entry>
    <id>$(Escape $id)</id>
    <title type="text">$(Escape $metadata.DisplayName)</title>
    <summary type="text">$(Escape $metadata.Description.'#text')</summary>
    <published>$updated</published>
    <updated>$updated</updated>
    <author>
      <name>$(Escape $metadata.Identity.Publisher)</name>
    </author>
    <content type="application/octet-stream" src="$(Escape $VsixUrl)" />
    <Vsix xmlns="http://schemas.microsoft.com/developer/vsx-syndication-schema/2010" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
      <Id>$(Escape $id)</Id>
      <Version>$(Escape $version)</Version>
      <References />
      <Rating xsi:nil="true" />
      <RatingCount xsi:nil="true" />
      <DownloadCount xsi:nil="true" />
    </Vsix>
  </entry>
</feed>
"@

[System.IO.File]::WriteAllText((Join-Path $GalleryDirectory 'atom.xml'), $feed, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Gallery: $id $version -> $(Join-Path $GalleryDirectory 'atom.xml')"