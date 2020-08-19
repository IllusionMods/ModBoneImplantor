if ($PSScriptRoot -match '.+?\\bin\\?') {
    $dir = $PSScriptRoot + "\"
}
else {
    $dir = $PSScriptRoot + "\bin\"
}

$array = @("KK", "EC")

function CreateZip ($element)
{
    $path = $dir + "\" + $element

    $ver = "v" + (Get-ChildItem -Path ($path) -Filter "*.dll" -Recurse -Force)[0].VersionInfo.FileVersion.ToString()

    Compress-Archive -Path ($path + "\BepInEx") -Force -CompressionLevel "Optimal" -DestinationPath ($dir + $element + "_ModBoneImplantor_" + $ver + ".zip")
}

foreach ($element in $array) 
{
    try
    {
        CreateZip ($element)
    }
    catch 
    {
        # retry
        CreateZip ($element)
    }
}
