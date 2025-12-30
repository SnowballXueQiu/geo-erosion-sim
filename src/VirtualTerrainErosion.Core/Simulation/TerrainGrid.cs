using System;

namespace VirtualTerrainErosion.Core.Simulation
{
    public class TerrainGrid
    {
        public int Width { get; }
        public int Height { get; }
        public double[,] H { get; set; } // Elevation
        public double[,] W { get; set; } // Water depth
        public int[,] Dir { get; set; }  // Flow direction (0-7)
        public double[,] Q { get; set; } // Flow accumulation
        public double[,] S { get; set; } // Slope
        public double[,] Hardness { get; set; } // Lithology hardness (1.0 = normal)

        public TerrainGrid(int width, int height)
        {
            Width = width;
            Height = height;
            H = new double[width, height];
            W = new double[width, height];
            Dir = new int[width, height];
            Q = new double[width, height];
            S = new double[width, height];
            Hardness = new double[width, height];
        }
    }
}
