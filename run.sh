#!/bin/bash
echo "Building Avalonia Desktop App..."
dotnet build src/VirtualTerrainErosion.Avalonia/VirtualTerrainErosion.Avalonia.csproj

echo "Starting Desktop App..."
dotnet run --project src/VirtualTerrainErosion.Avalonia/VirtualTerrainErosion.Avalonia.csproj
