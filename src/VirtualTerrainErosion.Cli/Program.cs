using System;
using System.Threading;
using VirtualTerrainErosion.Core;
using VirtualTerrainErosion.Core.Simulation;
using VirtualTerrainErosion.Core.Database;

namespace VirtualTerrainErosion.Cli
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Virtual Terrain Erosion Simulation (CLI Mode)");
            Console.WriteLine("-------------------------------------------");

            // Load settings
            string configPath = "config.toml";
            if (!System.IO.File.Exists(configPath))
            {
                 if (System.IO.File.Exists("../../../../../config.toml")) configPath = "../../../../../config.toml";
            }
            var settings = AppSettings.Load(configPath);
            
            // Allow overriding connection string via args or env var if needed
            string envConn = Environment.GetEnvironmentVariable("GES_CONNECTION_STRING");
            if (!string.IsNullOrEmpty(envConn)) settings.ConnectionString = envConn;

            Console.WriteLine($"Grid Size: {settings.GridSize}x{settings.GridSize}");
            Console.WriteLine($"Database: {settings.ConnectionString}");

            var model = new ErosionModel(settings.GridSize);
            // Apply params
            model.P = settings.DefaultP;
            model.K = settings.DefaultK;
            model.D = settings.DefaultD;
            model.T = settings.DefaultT;
            model.U = settings.DefaultU;

            var dbLogger = new DbLogger(settings.ConnectionString);

            Console.WriteLine("Initializing Terrain...");
            // Initial stats
            var stats = model.CalculateStats();
            Console.WriteLine($"Initial Relief: {stats.maxRelief:F2}m, Mean Elev: {stats.meanElev:F2}m");

            Console.WriteLine("Starting Simulation...");
            
            int steps = settings.MaxSteps;
            for (int i = 1; i <= steps; i++)
            {
                model.Step();
                
                if (i % 10 == 0)
                {
                    stats = model.CalculateStats();
                    Console.WriteLine($"Step {i}: Relief={stats.maxRelief:F1}m, Mean={stats.meanElev:F1}m, DrainDen={stats.drainDen:F3}");
                    
                    // Log to DB
                    dbLogger.LogStep(i, model.P, model.K, model.D, model.T, model.U, 
                                     stats.maxRelief, stats.meanElev, stats.drainDen, stats.hackSlope, stats.concavity);
                }
                
                // Simple progress bar
                if (i % (steps/20) == 0) Console.Write(".");
            }
            Console.WriteLine("\nSimulation Complete.");

            // Export to ASCII Grid
            string exportPath = "terrain_output.asc";
            Console.WriteLine($"Exporting terrain to {exportPath}...");
            GeoExporter.ExportToAscii(model.Grid, exportPath);
            Console.WriteLine("Export finished.");
        }
    }
}
