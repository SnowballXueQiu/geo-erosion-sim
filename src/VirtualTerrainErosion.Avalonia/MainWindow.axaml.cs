using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System;
using System.IO;
using System.Threading;
using VirtualTerrainErosion.Core;
using VirtualTerrainErosion.Core.Simulation;

namespace VirtualTerrainErosion.Avalonia;

public partial class MainWindow : Window
{
    private ErosionModel _model = null!;
    private Timer? _timer;
    private bool _isRunning;
    private int _stepCount;
    private int _stepsPerFrame = 1;
    private AppSettings _settings;

    public MainWindow()
    {
        InitializeComponent();
        
        // Load settings from config.toml in the current directory (project root when running via dotnet run)
        // Or try to find it by going up directories if needed.
        string configPath = "config.toml";
        if (!File.Exists(configPath))
        {
            // Try looking up a few levels if running from bin/Debug/...
            if (File.Exists("../../../../../config.toml")) configPath = "../../../../../config.toml";
        }
        
        _settings = AppSettings.Load(configPath);
        
        InitializeModel();
    }

    private void InitializeModel()
    {
        _model = new ErosionModel(_settings.GridSize);
        _model.P = _settings.DefaultP;
        _model.K = _settings.DefaultK;
        _model.D = _settings.DefaultD;
        _model.T = _settings.DefaultT;
        _model.U = _settings.DefaultU;
        
        _model.CalculateStats();
        UpdateView();
    }

    private void OnStartClick(object? sender, RoutedEventArgs e)
    {
        if (_isRunning) return;
        _isRunning = true;
        _timer = new Timer(Tick, null, 0, 50); // 50ms interval
        TxtStatus.Text = "Running...";
    }

    private void StopSimulation()
    {
        _isRunning = false;
        _timer?.Dispose();
        _timer = null;
        TxtStatus.Text = "Paused";
    }

    private void OnStopClick(object? sender, RoutedEventArgs e)
    {
        StopSimulation();
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        StopSimulation();
        InitializeModel();
        _stepCount = 0;
        UpdateView();
        TxtStatus.Text = "Reset";
    }

    private void OnExportClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), $"terrain_{_stepCount}.asc");
            GeoExporter.ExportToAscii(_model.Grid, path);
            TxtStatus.Text = $"Exported to {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Error: {ex.Message}";
        }
    }

    private void OnSpeedChanged(object? sender, global::Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        _stepsPerFrame = (int)e.NewValue;
        if (TxtSpeedValue != null)
        {
            TxtSpeedValue.Text = _stepsPerFrame.ToString();
        }
    }

    private void Tick(object? state)
    {
        if (!_isRunning) return;
        
        // Run multiple steps per frame based on speed setting
        for(int i=0; i<_stepsPerFrame; i++)
        {
            _model.Step();
            _stepCount++;
        }
        
        // Update UI on UI thread
        Dispatcher.UIThread.Post(() =>
        {
            UpdateView();
        });
    }

    private void UpdateView()
    {
        TxtStep.Text = $"Step: {_stepCount}";
        var stats = _model.CalculateStats();
        TxtRelief.Text = $"Relief: {stats.maxRelief:F1}m";

        // Render bitmap
        ImgTerrain.Source = CreateBitmap(_model.Grid);
    }

    private WriteableBitmap CreateBitmap(TerrainGrid grid)
    {
        int w = grid.Width;
        int h = grid.Height;
        // Use global::Avalonia to avoid namespace collision with current namespace
        var bmp = new WriteableBitmap(new global::Avalonia.PixelSize(w, h), new global::Avalonia.Vector(96, 96), global::Avalonia.Platform.PixelFormat.Bgra8888, global::Avalonia.Platform.AlphaFormat.Opaque);

        using (var buf = bmp.Lock())
        {
            double min = double.MaxValue;
            double max = double.MinValue;
            for(int y=0; y<h; y++)
                for(int x=0; x<w; x++)
                {
                    double val = grid.H[x,y];
                    if(val < min) min = val;
                    if(val > max) max = val;
                }
            double range = max - min;
            if (range < 0.001) range = 1;

            unsafe
            {
                uint* ptr = (uint*)buf.Address;
                int stride = buf.RowBytes / 4;

                // Parallel rendering for performance
                System.Threading.Tasks.Parallel.For(0, h, y =>
                {
                    for (int x = 0; x < w; x++)
                    {
                        double val = grid.H[x, y];
                        double t = (val - min) / range; // Normalized 0..1
                        
                        // Color Palette Interpolation
                        // 0.0 - 0.3: Green (Vegetation)
                        // 0.3 - 0.6: Brown (Earth)
                        // 0.6 - 0.8: Grey (Rock)
                        // 0.8 - 1.0: White (Snow)
                        
                        byte r, g, b;
                        
                        if (t < 0.3)
                        {
                            // Dark Green (34, 139, 34) to Light Green (100, 200, 50)
                            double localT = t / 0.3;
                            r = (byte)(34 + (100 - 34) * localT);
                            g = (byte)(139 + (200 - 139) * localT);
                            b = (byte)(34 + (50 - 34) * localT);
                        }
                        else if (t < 0.6)
                        {
                            // Light Green (100, 200, 50) to Brown (139, 69, 19)
                            double localT = (t - 0.3) / 0.3;
                            r = (byte)(100 + (139 - 100) * localT);
                            g = (byte)(200 + (69 - 200) * localT);
                            b = (byte)(50 + (19 - 50) * localT);
                        }
                        else if (t < 0.8)
                        {
                            // Brown (139, 69, 19) to Grey (100, 100, 100)
                            double localT = (t - 0.6) / 0.2;
                            r = (byte)(139 + (100 - 139) * localT);
                            g = (byte)(69 + (100 - 69) * localT);
                            b = (byte)(19 + (100 - 19) * localT);
                        }
                        else
                        {
                            // Grey (100, 100, 100) to White (255, 255, 255)
                            double localT = (t - 0.8) / 0.2;
                            r = (byte)(100 + (255 - 100) * localT);
                            g = (byte)(100 + (255 - 100) * localT);
                            b = (byte)(100 + (255 - 100) * localT);
                        }

                        // BGRA
                        uint pixel = (uint)((255 << 24) | (r << 16) | (g << 8) | b);
                        ptr[y * stride + x] = pixel;
                    }
                });
            }
        }
        return bmp;
    }
}