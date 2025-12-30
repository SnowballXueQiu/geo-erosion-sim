namespace VirtualTerrainErosion.Core
{
    public class AppSettings
    {
        public string ConnectionString { get; set; } = "Server=main.vastsea.cc,1433;Database=GeoErosionDB;User Id=sa;Password=gespwd;TrustServerCertificate=True;Encrypt=False;";
        public int GridSize { get; set; } = 65;
        public int MaxSteps { get; set; } = 100;
        
        // Default Simulation Parameters
        public double DefaultP { get; set; } = 100;
        public double DefaultK { get; set; } = 0.005;
        public double DefaultD { get; set; } = 0.003;
        public double DefaultT { get; set; } = 20;
        public double DefaultU { get; set; } = 10;
    }
}
