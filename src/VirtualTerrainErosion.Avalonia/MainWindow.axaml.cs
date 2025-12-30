using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        
        // Sync sliders
        if (SldP != null) SldP.Value = _model.P;
        if (SldK != null) SldK.Value = _model.K;
        if (SldD != null) SldD.Value = _model.D;
        if (SldT != null) SldT.Value = _model.T;
        if (SldU != null) SldU.Value = _model.U;

        _model.CalculateStats();
        UpdateView();
    }

    private void OnParamChanged(object? sender, global::Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_model == null) return;
        if (sender == SldP) _model.P = e.NewValue;
        if (sender == SldK) _model.K = e.NewValue;
        if (sender == SldD) _model.D = e.NewValue;
        if (sender == SldT) _model.T = e.NewValue;
        if (sender == SldU) _model.U = e.NewValue;
    }

    private void OnViewModeChanged(object? sender, RoutedEventArgs e)
    {
        if (CboViewMode != null)
        {
            _viewMode = CboViewMode.SelectedIndex;
            UpdateView();
        }
    }

    private void OnStartClick(object? sender, RoutedEventArgs e)
    {
        if (_isRunning) return;
        _isRunning = true;
        _timer = new Timer(Tick, null, 0, 50); // 50ms interval
        TxtStatus.Text = "运行中...";
    }

    private void StopSimulation()
    {
        _isRunning = false;
        _timer?.Dispose();
        _timer = null;
        TxtStatus.Text = "已暂停";
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
        TxtStatus.Text = "已重置";
    }

    private void OnExportClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            string path = System.IO.Path.Combine(Directory.GetCurrentDirectory(), $"terrain_{_stepCount}.asc");
            GeoExporter.ExportToAscii(_model.Grid, path);
            TxtStatus.Text = $"已导出至 {System.IO.Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"错误: {ex.Message}";
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

    private int _viewMode = 1; // 0=Height, 1=Hillshade, 2=Water
    private WriteableBitmap? _bmpBuffer;

    private void OnViewModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CboViewMode != null)
        {
            _viewMode = CboViewMode.SelectedIndex;
            UpdateView();
        }
    }

    private int _isTicking = 0;

    private void Tick(object? state)
    {
        if (!_isRunning) return;
        
        // Prevent re-entry if previous tick is still running
        if (Interlocked.CompareExchange(ref _isTicking, 1, 0) != 0) return;

        try
        {
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
        finally
        {
            _isTicking = 0;
        }
    }

    private void UpdateView()
    {
        TxtStep.Text = $"Step: {_stepCount}";
        var stats = _model.CalculateStats();
        TxtRelief.Text = $"Relief: {stats.maxRelief:F1}m";
        if (TxtHack != null) TxtHack.Text = $"Hack Slope: {stats.hackSlope:F2}";
        if (TxtConcavity != null) TxtConcavity.Text = $"Concavity: {stats.concavity:F2}";

        // Render bitmap
        ImgTerrain.Source = CreateBitmap(_model.Grid);
        ImgTerrain.InvalidateVisual();

        // Draw Charts
        var (hackData, slopeAreaData) = _model.GetRiverStats();
        DrawChart(CanvasHack, hackData, "Log(A)", "Log(L)", Brushes.Cyan);
        DrawChart(CanvasSlopeArea, slopeAreaData, "Log(A)", "Log(S)", Brushes.Orange);
    }

    private void DrawChart(Canvas canvas, List<(double x, double y)> data, string xLabel, string yLabel, IBrush color)
    {
        if (canvas == null) return;
        canvas.Children.Clear();
        if (data == null || data.Count < 2) return;

        double w = canvas.Bounds.Width;
        double h = canvas.Bounds.Height;
        if (w == 0 || h == 0) return; // Not laid out yet

        // Find min/max
        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;

        foreach (var p in data)
        {
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }

        double rangeX = maxX - minX;
        double rangeY = maxY - minY;
        if (rangeX < 0.001) rangeX = 1;
        if (rangeY < 0.001) rangeY = 1;

        var polyline = new Polyline
        {
            Stroke = color,
            StrokeThickness = 2
        };

        foreach (var p in data)
        {
            double px = (p.x - minX) / rangeX * w;
            double py = h - (p.y - minY) / rangeY * h; // Invert Y
            polyline.Points.Add(new global::Avalonia.Point(px, py));
        }

        canvas.Children.Add(polyline);
    }

    private WriteableBitmap CreateBitmap(TerrainGrid grid)
    {
        int w = grid.Width;
        int h = grid.Height;
        
        // Reuse bitmap buffer if possible
        if (_bmpBuffer == null || _bmpBuffer.PixelSize.Width != w || _bmpBuffer.PixelSize.Height != h)
        {
            _bmpBuffer?.Dispose();
            _bmpBuffer = new WriteableBitmap(new global::Avalonia.PixelSize(w, h), new global::Avalonia.Vector(96, 96), global::Avalonia.Platform.PixelFormat.Bgra8888, global::Avalonia.Platform.AlphaFormat.Opaque);
        }
        
        var bmp = _bmpBuffer;

        bool showRivers = ChkRivers?.IsChecked ?? false;

        using (var buf = bmp.Lock())
        {
            // Optimization: Skip sorting for min/max, use sampling or simple min/max
            // Sorting 65k items every frame is slow.
            
            double min = double.MaxValue;
            double max = double.MinValue;
            double maxWater = 0;
            
            // Sample 1/16th of pixels for speed
            int step = 4;
            for(int y=0; y<h; y+=step)
                for(int x=0; x<w; x+=step)
                {
                    double val = grid.H[x,y];
                    if (!double.IsNaN(val) && !double.IsInfinity(val)) {
                        if (val < min) min = val;
                        if (val > max) max = val;
                    }
                    if(grid.W[x,y] > maxWater) maxWater = grid.W[x,y];
                }
            
            if (min == double.MaxValue) { min = 0; max = 100; }
            
            double range = max - min;
            if (range < 0.001) range = 1;
            if (maxWater < 0.001) maxWater = 1;

            unsafe
            {
                uint* ptr = (uint*)buf.Address;
                int stride = buf.RowBytes / 4;

                System.Threading.Tasks.Parallel.For(0, h, y =>
                {
                    for (int x = 0; x < w; x++)
                    {
                        uint pixel = 0;
                        bool isRiver = showRivers && grid.Q[x, y] > 100;

                        if (isRiver)
                        {
                            // Blue highlight for rivers (Cyan-ish)
                            pixel = unchecked((uint)((255 << 24) | (0 << 16) | (191 << 8) | (255 << 0)));
                        }
                        else if (_viewMode == 1) // Hillshade
                        {
                            // Simple hillshade calculation
                            double dzdx = 0;
                            double dzdy = 0;
                            
                            double hC = grid.H[x, y];
                            double hL = (x > 0) ? grid.H[x-1, y] : hC;
                            double hR = (x < w - 1) ? grid.H[x+1, y] : hC;
                            double hU = (y > 0) ? grid.H[x, y-1] : hC;
                            double hD = (y < h - 1) ? grid.H[x, y+1] : hC;

                            dzdx = (hR - hL) / 2.0;
                            dzdy = (hD - hU) / 2.0;
                            
                            double nx = -dzdx;
                            double ny = -dzdy;
                            double nz = 1.0;
                            double len = Math.Sqrt(nx*nx + ny*ny + nz*nz);
                            if (len > 0) { nx /= len; ny /= len; nz /= len; }
                            
                            double lx = -0.707; 
                            double ly = -0.707;
                            double lz = 0.707; 
                            
                            double dot = nx*lx + ny*ly + nz*lz;
                            if (dot < 0) dot = 0;
                            
                            byte intensity = (byte)(dot * 255);
                            pixel = (uint)((255 << 24) | (intensity << 16) | (intensity << 8) | intensity);
                        }
                        else if (_viewMode == 2) // Water Flow
                        {
                            double water = grid.W[x, y];
                            double t = Math.Min(water / (maxWater * 0.1), 1.0); 
                            
                            byte r = (byte)(20 + (0 - 20) * t);
                            byte g = (byte)(20 + (100 - 20) * t);
                            byte b = (byte)(50 + (255 - 50) * t);
                            
                            double hVal = (grid.H[x, y] - min) / range;
                            if (hVal < 0) hVal = 0; if (hVal > 1) hVal = 1;
                            
                            byte hByte = (byte)(hVal * 100); 
                            
                            if (t > 0.01)
                                pixel = (uint)((255 << 24) | (r << 16) | (g << 8) | b);
                            else
                                pixel = (uint)((255 << 24) | (hByte << 16) | (hByte << 8) | hByte);
                        }
                        else // Height Map (Color)
                        {
                            double val = grid.H[x, y];
                            double t = (val - min) / range;
                            if (t < 0) t = 0; if (t > 1) t = 1; 
                            
                            byte r, g, b;
                            if (t < 0.3) { // Green
                                double localT = t / 0.3;
                                r = (byte)(34 + (100 - 34) * localT);
                                g = (byte)(139 + (200 - 139) * localT);
                                b = (byte)(34 + (50 - 34) * localT);
                            } else if (t < 0.6) { // Brown
                                double localT = (t - 0.3) / 0.3;
                                r = (byte)(100 + (139 - 100) * localT);
                                g = (byte)(200 + (69 - 200) * localT);
                                b = (byte)(50 + (19 - 50) * localT);
                            } else if (t < 0.8) { // Grey
                                double localT = (t - 0.6) / 0.2;
                                r = (byte)(139 + (100 - 139) * localT);
                                g = (byte)(69 + (100 - 69) * localT);
                                b = (byte)(19 + (100 - 19) * localT);
                            } else { // White
                                double localT = (t - 0.8) / 0.2;
                                r = (byte)(100 + (255 - 100) * localT);
                                g = (byte)(100 + (255 - 100) * localT);
                                b = (byte)(100 + (255 - 100) * localT);
                            }
                            pixel = (uint)((255 << 24) | (r << 16) | (g << 8) | b);
                        }
                        
                        ptr[y * stride + x] = pixel;
                    }
                });
            }
        }
        return bmp;
    }
}