using System;
using System.IO;
using System.Text;
using VirtualTerrainErosion.Core.Simulation;

namespace VirtualTerrainErosion.Core
{
    public static class GeoExporter
    {
        /// <summary>
        /// Exports the terrain elevation to an ESRI ASCII Grid file (.asc).
        /// This format is widely supported by GIS software like QGIS, ArcGIS.
        /// </summary>
        public static void ExportToAscii(TerrainGrid grid, string filePath)
        {
            using (var writer = new StreamWriter(filePath, false, Encoding.ASCII))
            {
                // Header
                writer.WriteLine($"ncols         {grid.Width}");
                writer.WriteLine($"nrows         {grid.Height}");
                writer.WriteLine($"xllcorner     0");
                writer.WriteLine($"yllcorner     0");
                writer.WriteLine($"cellsize      30"); // Assuming 30m resolution
                writer.WriteLine($"NODATA_value  -9999");

                // Data
                // ESRI ASCII Grid starts from top-left (North-West)
                // Our grid (0,0) is usually bottom-left or top-left depending on convention.
                // Let's assume (0,0) is top-left for simplicity in array indexing.
                
                for (int j = 0; j < grid.Height; j++)
                {
                    for (int i = 0; i < grid.Width; i++)
                    {
                        writer.Write($"{grid.H[i, j]:F2} ");
                    }
                    writer.WriteLine();
                }
            }
        }
    }
}
