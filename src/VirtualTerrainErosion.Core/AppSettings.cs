using System;
using System.IO;
using Tomlyn;
using Tomlyn.Model;

namespace VirtualTerrainErosion.Core
{
    public class AppSettings
    {
        public string ConnectionString { get; set; } = "";
        public int GridSize { get; set; } = 256;
        public int MaxSteps { get; set; } = 100;
        
        // Default Simulation Parameters
        public double DefaultP { get; set; } = 100;
        public double DefaultK { get; set; } = 0.005;
        public double DefaultD { get; set; } = 0.003;
        public double DefaultT { get; set; } = 20;
        public double DefaultU { get; set; } = 10;

        public static AppSettings Load(string configPath = "config.toml")
        {
            var settings = new AppSettings();
            
            if (!File.Exists(configPath))
            {
                // Fallback or throw? Let's return defaults but warn if possible.
                // For now, just return defaults, but maybe with empty connection string.
                Console.WriteLine($"Warning: Config file '{configPath}' not found. Using defaults.");
                return settings;
            }

            try
            {
                string tomlContent = File.ReadAllText(configPath);
                var model = Toml.ToModel(tomlContent);

                if (model.ContainsKey("database") && model["database"] is TomlTable db)
                {
                    if (db.ContainsKey("connection_string")) 
                        settings.ConnectionString = (string)db["connection_string"];
                }

                if (model.ContainsKey("simulation") && model["simulation"] is TomlTable sim)
                {
                    if (sim.ContainsKey("grid_size")) settings.GridSize = Convert.ToInt32(sim["grid_size"]);
                    if (sim.ContainsKey("max_steps")) settings.MaxSteps = Convert.ToInt32(sim["max_steps"]);
                    if (sim.ContainsKey("rain_p")) settings.DefaultP = Convert.ToDouble(sim["rain_p"]);
                    if (sim.ContainsKey("erosion_k")) settings.DefaultK = Convert.ToDouble(sim["erosion_k"]);
                    if (sim.ContainsKey("deposition_d")) settings.DefaultD = Convert.ToDouble(sim["deposition_d"]);
                    if (sim.ContainsKey("threshold_t")) settings.DefaultT = Convert.ToDouble(sim["threshold_t"]);
                    if (sim.ContainsKey("uplift_u")) settings.DefaultU = Convert.ToDouble(sim["uplift_u"]);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing config.toml: {ex.Message}");
            }

            return settings;
        }
    }
}
