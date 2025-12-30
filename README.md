# Virtual Terrain Erosion Simulator (GeoErosionSim)

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey)
![.NET](https://img.shields.io/badge/.NET-10.0-purple)

A high-performance, cross-platform geological simulation engine that models hydraulic erosion, tectonic uplift, and lithological hardness variations in real-time. Designed for academic research, GIS analysis, and procedural terrain generation.

## 🌟 Key Features

*   **Physically-Based Erosion Model**: Implements a hydraulic erosion algorithm simulating rain droplets, sediment transport, and deposition.
*   **Real-Time Visualization**:
    *   **Height Map**: Color-coded elevation rendering (Vegetation -> Rock -> Snow).
    *   **Hillshade**: 3D-like relief shading for analyzing terrain morphology.
    *   **Water Flow**: Dynamic visualization of water accumulation and river formation.
*   **Advanced Geology**:
    *   **Lithology Hardness**: Simulates different rock types (soft vs. hard rock) affecting erosion rates.
    *   **Tectonic Uplift**: Continuous terrain uplift simulation.
*   **High Performance**:
    *   **Parallel Computing**: Utilizes `.NET TPL (Task Parallel Library)` for multi-threaded grid processing.
    *   **Direct Bitmap Manipulation**: Unsafe code blocks for millisecond-level rendering latency.
*   **Cross-Platform GUI**: Built with **Avalonia UI**, providing a native desktop experience on macOS, Windows, and Linux.
*   **GIS Integration**: Exports terrain data to **ESRI ASCII Grid (.asc)** format, compatible with QGIS, ArcGIS, and other geospatial software.
*   **Cloud Database Integration**: Logs simulation statistics (Relief, Drainage Density) to a remote SQL Server for long-term analysis.

## 🛠 Technology Stack

*   **Core Engine**: C# 12, .NET 10.0
*   **GUI Framework**: Avalonia UI 11.0 (XAML-based, Cross-Platform)
*   **Data Storage**: SQL Server (Remote Azure/VPS instance), TOML Configuration
*   **Optimization**: `System.Threading.Tasks.Parallel`, `unsafe` pointers for image processing
*   **Architecture**: Clean Architecture (Core Logic separated from UI/CLI)

## 🚀 Getting Started

### Prerequisites
*   .NET 10.0 SDK

### Running the Simulation

1.  **Clone the repository**
    ```bash
    git clone https://github.com/yourusername/geo-erosion-sim.git
    cd geo-erosion-sim
    ```

2.  **Configure Settings** (Optional)
    Edit `config.toml` to adjust grid size, erosion rates, or database connection.

3.  **Run the Desktop App**
    ```bash
    ./run.sh
    ```

## 🎮 Controls

*   **Start/Stop**: Toggle the simulation loop.
*   **Reset**: Regenerate the terrain with a new random seed.
*   **Simulation Speed**: Adjust how many simulation steps run per frame (1x - 50x).
*   **View Mode**:
    *   *Height Map*: Standard topographic view.
    *   *Hillshade*: Simulates light casting shadows to reveal terrain details.
    *   *Water Flow*: Highlights areas with high water accumulation.
*   **Export .ASC**: Save the current state to a file for external analysis.

## 📊 Scientific Value

Unlike traditional "Student Management Systems" or simple CRUD apps, this project demonstrates:
1.  **Computational Physics**: Implementing differential equations for mass balance on a discrete grid.
2.  **High-Performance Computing**: Managing CPU-bound tasks efficiently in a managed language.
3.  **Geospatial Data Handling**: Generating and exporting standard GIS formats.
4.  **Interactive Systems**: Bridging the gap between backend simulation and frontend visualization.

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
