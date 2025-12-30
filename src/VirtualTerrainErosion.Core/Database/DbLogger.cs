using System;
using System.Data.SqlClient;
using Microsoft.Data.SqlClient;

namespace VirtualTerrainErosion.Core.Database
{
    public class DbLogger
    {
        private string _connectionString;

        public DbLogger(string connectionString)
        {
            _connectionString = connectionString;
            EnsureDatabaseAndTable();
        }

        private void EnsureDatabaseAndTable()
        {
            try 
            {
                // First connect to master to check/create DB
                var builder = new SqlConnectionStringBuilder(_connectionString);
                string originalDb = builder.InitialCatalog;
                builder.InitialCatalog = "master";
                
                using (var conn = new SqlConnection(builder.ConnectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand($"IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = '{originalDb}') CREATE DATABASE [{originalDb}]", conn);
                    cmd.ExecuteNonQuery();
                }

                // Now connect to the actual DB and create table
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string tableScript = @"
                        IF OBJECT_ID('ErosionLog', 'U') IS NULL
                        BEGIN
                            CREATE TABLE ErosionLog(
                                Step        int PRIMARY KEY,
                                Rain        real,
                                ErodeK      real,
                                DepositD    real,
                                ThresholdT  real,
                                UpliftU     real,
                                MaxRelief   real,
                                MeanElev    real,
                                DrainDen    real,
                                HackSlope   real,
                                Concavity   real
                            );
                        END";
                    var cmd = new SqlCommand(tableScript, conn);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not initialize database: {ex.Message}");
            }
        }

        public void LogStep(int step, double rain, double k, double d, double t, double u, 
                            double maxRelief, double meanElev, double drainDen, double hackSlope, double concavity)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                try
                {
                    conn.Open();
                    string query = @"
                        MERGE ErosionLog AS target
                        USING (SELECT @Step AS Step) AS source
                        ON (target.Step = source.Step)
                        WHEN MATCHED THEN
                            UPDATE SET 
                                Rain = @Rain, ErodeK = @K, DepositD = @D, ThresholdT = @T, UpliftU = @U,
                                MaxRelief = @MaxRelief, MeanElev = @MeanElev, DrainDen = @DrainDen,
                                HackSlope = @HackSlope, Concavity = @Concavity
                        WHEN NOT MATCHED THEN
                            INSERT (Step, Rain, ErodeK, DepositD, ThresholdT, UpliftU, MaxRelief, MeanElev, DrainDen, HackSlope, Concavity)
                            VALUES (@Step, @Rain, @K, @D, @T, @U, @MaxRelief, @MeanElev, @DrainDen, @HackSlope, @Concavity);";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Step", step);
                        cmd.Parameters.AddWithValue("@Rain", rain);
                        cmd.Parameters.AddWithValue("@K", k);
                        cmd.Parameters.AddWithValue("@D", d);
                        cmd.Parameters.AddWithValue("@T", t);
                        cmd.Parameters.AddWithValue("@U", u);
                        cmd.Parameters.AddWithValue("@MaxRelief", maxRelief);
                        cmd.Parameters.AddWithValue("@MeanElev", meanElev);
                        cmd.Parameters.AddWithValue("@DrainDen", drainDen);
                        cmd.Parameters.AddWithValue("@HackSlope", hackSlope);
                        cmd.Parameters.AddWithValue("@Concavity", concavity);

                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    // In a real app, log to file or show error. 
                    // For now, we might just print to console or ignore if DB is down to avoid crashing simulation.
                    Console.WriteLine("DB Error: " + ex.Message);
                }
            }
        }
    }
}
