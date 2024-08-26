#!/usr/bin/env bash

dotnet restore

versions=("0.5.x" "0.6.x")
subfolders=(plugin installer)

if [ ! -d "build" ]; then
    mkdir build
fi

# ------------------------------ Functions ------------------------------ #

create_plugin_structure() {
    (
        cd build

        for subfolder in "${subfolders[@]}"; do
            if [ ! -d "$subfolder" ]; then
                mkdir $subfolder
            fi
        done

        for version in "${versions[@]}"; do
            if [ ! -d "./plugin/$version" ]; then
                mkdir ./plugin/$version
            else
                rm -rf ./plugin/$version/*
            fi
        done

        for version in "${versions[@]}"; do
            if [ ! -d "./installer/$version" ]; then
                mkdir ./installer/$version
            else
                rm -rf ./installer/$version/*
            fi
        done
    )
}

build_installer() {
    echo ""
    echo "Building the installer ($version)"
    echo ""

    if ! dotnet publish Native-Gestures.Installer-$version -c Debug -o temp/installer/$version --no-restore -v q
    then
        echo "Failed to build the installer"
        exit 1
    fi

    mv temp/installer/$version/Native-Gestures.Installer.dll ./build/installer/$version/Native-Gestures.Installer.dll

    (
        cd ./build/installer/$version

        # Zip the installer
        if ! zip -r Native-Gestures.Installer-$version.zip *
        then
            echo "Failed to zip the installer"
            exit 1
        fi
    )

}

build_plugin() {
    echo ""
    echo "Building the plugin ($version)"
    echo ""

    if ! dotnet publish Native-Gestures-$version -c Debug -o temp/plugin/$version --no-restore -v q
    then
        echo "Failed to build the plugin"
        exit 1
    fi

    echo ""
    echo "Zipping the plugin ($version)"
    echo ""

    mv temp/plugin/$version/Native-Gestures.dll build/plugin/$version/Native-Gestures.dll
    mv temp/plugin/$version/Native-Gestures.pdb build/plugin/$version/Native-Gestures.pdb
    mv temp/plugin/$version/Native-Gestures.Lib.dll build/plugin/$version/Native-Gestures.Lib.dll
    mv temp/plugin/$version/Native-Gestures.Lib.pdb build/plugin/$version/Native-Gestures.Lib.pdb

    (
        cd ./build/plugin/$version

        if ! zip -r Native-Gestures-$version.zip *
        then
            echo "Failed to zip the plugin"
            exit 1
        fi
    )

}

# ------------------------------ Main ------------------------------ #

# Re-create hashes.txt
> "./build/hashes.txt"

create_plugin_structure

for version in "${versions[@]}"; do

    build_plugin $version
    build_installer $version

    (
        cd ./build/installer/$version

        # Compute checksums
        sha256sum Native-Gestures.Installer-$version.zip > ../../hashes.txt
    )

done