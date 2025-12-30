using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VirtualTerrainErosion.Core.Simulation
{
    public class ErosionModel
    {
        private TerrainGrid _grid;
        private Random _rand = new Random();
        
        // Parameters
        public double P { get; set; } = 100;   // Rain mm/10kyr
        public double K { get; set; } = 0.005; // Erosion coeff
        public double D { get; set; } = 0.003; // Deposition coeff
        public double T { get; set; } = 20;    // Threshold
        public double U { get; set; } = 10;    // Uplift mm/10kyr
        
        public int Steps { get; private set; } = 0;

        public ErosionModel(int size = 65)
        {
            _grid = new TerrainGrid(size, size);
            InitializeTerrain();
        }

        public TerrainGrid Grid => _grid;

        public void InitializeTerrain()
        {
            int w = _grid.Width;
            int h = _grid.Height;
            
            // Generate Fractal Noise (Perlin-like)
            // Sum of sines with random phases/frequencies
            int octaves = 6;
            double persistence = 0.45; // Reduced persistence for smoother terrain
            double lacunarity = 2.0;
            double scale = 0.015; // Reduced scale for larger features
            
            double[] offsetsX = new double[octaves];
            double[] offsetsY = new double[octaves];
            for(int k=0; k<octaves; k++) {
                offsetsX[k] = _rand.NextDouble() * 1000;
                offsetsY[k] = _rand.NextDouble() * 1000;
            }

            for (int i = 0; i < w; i++)
            {
                for (int j = 0; j < h; j++)
                {
                    double amplitude = 1000.0;
                    double frequency = scale;
                    double height = 0;
                    
                    for(int k=0; k<octaves; k++)
                    {
                        // Simple "noise" using sin/cos
                        double nx = (i + offsetsX[k]) * frequency;
                        double ny = (j + offsetsY[k]) * frequency;
                        
                        // Mix sin/cos to look less regular
                        double val = Math.Sin(nx) * Math.Cos(ny);
                        
                        height += val * amplitude;
                        
                        amplitude *= persistence;
                        frequency *= lacunarity;
                    }
                    
                    // Add a central mountain shape to bias it
                    double dx = i - w/2.0;
                    double dy = j - h/2.0;
                    double dist = Math.Sqrt(dx*dx + dy*dy);
                    double mountain = 2000.0 * Math.Exp(-(dist*dist) / (2 * (w*0.4)*(w*0.4)));
                    
                    _grid.H[i, j] = Math.Max(0, 500 + mountain + height);
                    _grid.W[i, j] = 0;
                    
                    // Uniform Hardness to prevent horizontal stripes
                    _grid.Hardness[i, j] = 1.0; 
                }
            }
            Steps = 0;
        }

        public void Step()
        {
            Steps++;
            ApplyRainfall();
            CalculateFlowDirection();
            CalculateFlowAccumulation();
            CalculateSlope();
            ErodeAndDeposit();
            ThermalErosion(); // Add diffusion
            ApplyUplift();
        }

        private void ThermalErosion()
        {
            // Hillslope Diffusion (Thermal Erosion)
            // Smooths terrain and transports material downhill
            int w = _grid.Width;
            int h = _grid.Height;
            double Kt = 0.05; // Diffusion coefficient
            
            // Use a temporary buffer to store changes to avoid race conditions/bias
            double[,] changes = new double[w, h];

            Parallel.For(1, w - 1, i =>
            {
                for (int j = 1; j < h - 1; j++)
                {
                    double hVal = _grid.H[i, j];
                    double d1 = _grid.H[i+1, j] - hVal;
                    double d2 = _grid.H[i-1, j] - hVal;
                    double d3 = _grid.H[i, j+1] - hVal;
                    double d4 = _grid.H[i, j-1] - hVal;
                    
                    // Laplacian
                    double laplacian = d1 + d2 + d3 + d4;
                    
                    changes[i, j] = Kt * laplacian;
                }
            });
            
            // Apply changes
            Parallel.For(1, w - 1, i =>
            {
                for (int j = 1; j < h - 1; j++)
                {
                    _grid.H[i, j] += changes[i, j];
                }
            });
        }

        private void ApplyRainfall()
        {
            Parallel.For(0, _grid.Width, i =>
            {
                for (int j = 0; j < _grid.Height; j++)
                    _grid.W[i, j] += P;
            });
        }

        private void CalculateFlowDirection()
        {
            // D8
            int w = _grid.Width;
            int h = _grid.Height;
            int[] dx = { 1, 1, 0, -1, -1, -1, 0, 1 };
            int[] dy = { 0, 1, 1, 1, 0, -1, -1, -1 };
            double distDiag = Math.Sqrt(2);

            Parallel.For(0, w, i =>
            {
                for (int j = 0; j < h; j++)
                {
                    double maxSlope = 0;
                    int bestDir = -1;

                    for (int k = 0; k < 8; k++)
                    {
                        int ni = i + dx[k];
                        int nj = j + dy[k];

                        if (ni >= 0 && ni < w && nj >= 0 && nj < h)
                        {
                            double drop = _grid.H[i, j] - _grid.H[ni, nj];
                            if (drop > 0)
                            {
                                double dist = (dx[k] != 0 && dy[k] != 0) ? distDiag : 1.0;
                                double slope = drop / dist;
                                if (slope > maxSlope)
                                {
                                    maxSlope = slope;
                                    bestDir = k;
                                }
                            }
                        }
                    }
                    _grid.Dir[i, j] = bestDir; // -1 means sink
                }
            });
        }

        private void CalculateFlowAccumulation()
        {
            // Sort cells by elevation descending
            int w = _grid.Width;
            int h = _grid.Height;
            var cells = new List<(int i, int j, double h)>();
            for (int i = 0; i < w; i++)
                for (int j = 0; j < h; j++)
                {
                    _grid.Q[i, j] = 1.0; // Each cell has 1 unit of area initially
                    cells.Add((i, j, _grid.H[i, j]));
                }

            cells.Sort((a, b) => b.h.CompareTo(a.h));

            int[] dx = { 1, 1, 0, -1, -1, -1, 0, 1 };
            int[] dy = { 0, 1, 1, 1, 0, -1, -1, -1 };

            foreach (var cell in cells)
            {
                int dir = _grid.Dir[cell.i, cell.j];
                if (dir != -1)
                {
                    int ni = cell.i + dx[dir];
                    int nj = cell.j + dy[dir];
                    if (ni >= 0 && ni < w && nj >= 0 && nj < h)
                    {
                        _grid.Q[ni, nj] += _grid.Q[cell.i, cell.j];
                    }
                }
            }
        }

        private void CalculateSlope()
        {
            int w = _grid.Width;
            int h = _grid.Height;
            int[] dx = { 1, 1, 0, -1, -1, -1, 0, 1 };
            int[] dy = { 0, 1, 1, 1, 0, -1, -1, -1 };
            double distDiag = Math.Sqrt(2) * 30.0; // Grid size 30m
            double distOrtho = 30.0;

            Parallel.For(0, w, i =>
            {
                for (int j = 0; j < h; j++)
                {
                    int dir = _grid.Dir[i, j];
                    if (dir != -1)
                    {
                        int ni = i + dx[dir];
                        int nj = j + dy[dir];
                        double drop = _grid.H[i, j] - _grid.H[ni, nj];
                        double dist = (dx[dir] != 0 && dy[dir] != 0) ? distDiag : distOrtho;
                        _grid.S[i, j] = Math.Max(0, drop / dist);
                    }
                    else
                    {
                        _grid.S[i, j] = 0;
                    }
                }
            });
        }

        private void ErodeAndDeposit()
        {
            int w = _grid.Width;
            int h = _grid.Height;
            double dt = 1.0; // Time step unit

            Parallel.For(0, w, i =>
            {
                for (int j = 0; j < h; j++)
                {
                    double qs = _grid.Q[i, j] * _grid.S[i, j];
                    double dh = 0;
                    
                    // Lithology effect:
                    // Harder rock (Hardness > 1) -> Less erosion (K decreases), Higher threshold (T increases)
                    double hardness = _grid.Hardness[i, j];
                    if (hardness <= 0) hardness = 1.0; // Safety

                    double effectiveK = K / hardness;
                    double effectiveT = T * hardness;

                    if (qs > effectiveT)
                    {
                        dh = effectiveK * (qs - effectiveT) * dt; // Erosion
                        
                        // Stability Clamp: Don't erode more than 5 units per step
                        if (dh > 5.0) dh = 5.0;
                        
                        _grid.H[i, j] -= dh;
                    }
                    else
                    {
                        dh = -D * (effectiveT - qs) * dt; // Deposition
                        
                        // Stability Clamp: Don't deposit more than 5 units per step
                        // dh is negative here, so we check if it's less than -5.0
                        if (dh < -5.0) dh = -5.0;
                        
                        _grid.H[i, j] -= dh;
                    }
                    
                    // NaN/Infinity Safety Check
                    if (double.IsNaN(_grid.H[i, j]) || double.IsInfinity(_grid.H[i, j]))
                    {
                        _grid.H[i, j] = 0.0;
                    }
                }
            });
        }
        private void ApplyUplift()
        {
            int w = _grid.Width;
            int h = _grid.Height;
            Parallel.For(0, w, i =>
            {
                for (int j = 0; j < h; j++)
                    _grid.H[i, j] += U;
            });
        }

        public (double maxRelief, double meanElev, double drainDen, double hackSlope, double concavity) CalculateStats()
        {
            int w = _grid.Width;
            int h = _grid.Height;
            double minH = double.MaxValue, maxH = double.MinValue, sumH = 0;
            int channelCells = 0;

            for (int i = 0; i < w; i++)
                for (int j = 0; j < h; j++)
                {
                    double val = _grid.H[i, j];
                    if (val < minH) minH = val;
                    if (val > maxH) maxH = val;
                    sumH += val;
                    if (_grid.Q[i, j] > 100) channelCells++; // Threshold for channel
                }

            double maxRelief = maxH - minH;
            double meanElev = sumH / (w * h);
            double drainDen = (double)channelCells / (w * h);

            // Hack's Law & Concavity
            double hackSlope = 0.0;
            double concavity = 0.0;

            var (hackData, slopeAreaData) = GetRiverStats();

            if (hackData.Count > 2)
            {
                // Regress log(L) = h * log(A) + c
                hackSlope = CalculateSlope(hackData);
            }

            if (slopeAreaData.Count > 2)
            {
                // Regress log(S) = -theta * log(A) + k
                // Slope is -theta, so theta = -Slope
                double s = CalculateSlope(slopeAreaData);
                concavity = -s;
            }

            return (maxRelief, meanElev, drainDen, hackSlope, concavity);
        }

        private double CalculateSlope(List<(double x, double y)> data)
        {
            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            int n = data.Count;
            foreach (var p in data)
            {
                sumX += p.x;
                sumY += p.y;
                sumXY += p.x * p.y;
                sumX2 += p.x * p.x;
            }
            return (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        }

        public (List<(double logA, double logL)> hackData, List<(double logA, double logS)> slopeAreaData) GetRiverStats()
        {
            var hackData = new List<(double, double)>();
            var slopeAreaData = new List<(double, double)>();

            var outlet = FindOutlet();
            if (outlet.i == -1) return (hackData, slopeAreaData);

            var path = TraceLongestPath(outlet.i, outlet.j);
            // Path is returned as Outlet -> Source (upstream trace)
            // For Hack's Law, L is usually length from the divide (source).
            // So L=0 at the end of the list (source), and L=Max at the start (outlet).
            
            // Let's iterate from Source (end of list) to Outlet (start of list)
            double currentL = 0;
            
            for (int k = path.Count - 1; k >= 0; k--)
            {
                var (i, j) = path[k];
                
                // Update L
                if (k < path.Count - 1)
                {
                    var (prevI, prevJ) = path[k+1]; // Previous point in loop (upstream)
                    double dist = (Math.Abs(i - prevI) + Math.Abs(j - prevJ) == 2) ? 1.414 : 1.0;
                    currentL += dist;
                }
                else
                {
                    currentL = 1.0; // Start with some length
                }

                double A = _grid.Q[i, j];
                double S = _grid.S[i, j];

                if (A > 0)
                {
                    double logA = Math.Log10(A);
                    
                    // Hack's Law: L vs A
                    if (currentL > 0)
                    {
                        hackData.Add((logA, Math.Log10(currentL)));
                    }

                    // Slope-Area: S vs A
                    if (S > 0.0001)
                    {
                        slopeAreaData.Add((logA, Math.Log10(S)));
                    }
                }
            }

            return (hackData, slopeAreaData);
        }

        private (int i, int j) FindOutlet()
        {
            int w = _grid.Width;
            int h = _grid.Height;
            double maxQ = -1;
            int bi = -1, bj = -1;
            for (int i = 0; i < w; i++)
                for (int j = 0; j < h; j++)
                    if (_grid.Q[i, j] > maxQ)
                    {
                        maxQ = _grid.Q[i, j];
                        bi = i; bj = j;
                    }
            return (bi, bj);
        }

        private List<(int i, int j)> TraceLongestPath(int startI, int startJ)
        {
            // Trace UPSTREAM to find longest path
            // This is a bit complex for a simple snippet, but let's try a greedy approach:
            // Always go to the neighbor that flows INTO current cell with MAX Q.
            
            var path = new List<(int i, int j)>();
            int currI = startI;
            int currJ = startJ;
            int w = _grid.Width;
            int h = _grid.Height;
            int[] dx = { 1, 1, 0, -1, -1, -1, 0, 1 };
            int[] dy = { 0, 1, 1, 1, 0, -1, -1, -1 };

            while (true)
            {
                path.Add((currI, currJ));
                
                // Find neighbors flowing into current
                double maxUpQ = -1;
                int nextI = -1, nextJ = -1;

                // Check all 8 neighbors
                // Neighbor (ni, nj) flows into (currI, currJ) if Dir[ni, nj] points to (currI, currJ)
                for (int k = 0; k < 8; k++)
                {
                    // Neighbor coordinates
                    // Note: dx/dy are for flow OUT.
                    // If neighbor at (ni, nj) has dir 'd', it flows to (ni+dx[d], nj+dy[d]).
                    // We want ni+dx[d] == currI && nj+dy[d] == currJ.
                    
                    // Let's just iterate all neighbors
                    int ni = currI + dx[k]; // This is just checking neighbors
                    int nj = currJ + dy[k];
                    
                    if (ni >= 0 && ni < w && nj >= 0 && nj < h)
                    {
                        int d = _grid.Dir[ni, nj];
                        if (d != -1)
                        {
                            int flowToI = ni + dx[d];
                            int flowToJ = nj + dy[d];
                            if (flowToI == currI && flowToJ == currJ)
                            {
                                if (_grid.Q[ni, nj] > maxUpQ)
                                {
                                    maxUpQ = _grid.Q[ni, nj];
                                    nextI = ni;
                                    nextJ = nj;
                                }
                            }
                        }
                    }
                }

                if (nextI != -1)
                {
                    currI = nextI;
                    currJ = nextJ;
                    
                    // Loop detection (simple)
                    if (path.Count > w * h) break;
                }
                else
                {
                    break; // Reached source
                }
            }
            return path;
        }
    }
}
