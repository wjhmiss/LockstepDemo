$platform = "linux-x64"

function PublishLinux {
    dotnet publish ET.sln -r $platform --framework net10.0 --no-self-contained --no-dependencies -c Debug

    $path = "Publish/"
    Remove-Item $path/ -Recurse -ErrorAction Ignore

    $dest1 = "Publish/Bin/"
    if (!(Test-Path $dest1)) { New-Item -ItemType Directory -Path $dest1 -Force | Out-Null }
    Move-Item ./Bin/$platform/publish/* $dest1 -Force

    $dest2 = "Publish/Packages/cn.etetet.loader/CodeMode/Loader/Server"
    if (!(Test-Path $dest2)) { New-Item -ItemType Directory -Path $dest2 -Force | Out-Null }
    Copy-Item Packages/cn.etetet.loader/CodeMode/Loader/Server/NLog.config -Destination $dest2 -Force -ErrorAction Ignore

    $dest3 = "Publish/Packages/cn.etetet.wow/Bundles/Bson"
    if (!(Test-Path $dest3)) { New-Item -ItemType Directory -Path $dest3 -Force | Out-Null }
    Copy-Item Packages/cn.etetet.wow/Bundles/Bson/* -Destination $dest3 -Recurse -Force -ErrorAction Ignore

    $dest4 = "Publish/Packages/cn.etetet.config/Bundles/Luban/Config/Server"
    if (!(Test-Path $dest4)) { New-Item -ItemType Directory -Path $dest4 -Force | Out-Null }
    Copy-Item ./Packages/cn.etetet.config/Bundles/Luban/Config/Server/* -Destination $dest4 -Recurse -Force -ErrorAction Ignore

    $dest5 = "Publish/Packages/cn.etetet.startconfig/Bundles/Luban/Release"
    if (!(Test-Path $dest5)) { New-Item -ItemType Directory -Path $dest5 -Force | Out-Null }
    Copy-Item ./Packages/cn.etetet.startconfig/Bundles/Luban/Release/* -Destination $dest5 -Recurse -Force -ErrorAction Ignore
    pause
}

PublishLinux