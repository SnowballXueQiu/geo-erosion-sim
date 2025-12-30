# Virtual Terrain Erosion Simulation

这是一个跨平台的虚拟地形侵蚀模拟项目，支持 Windows (GUI) 和 macOS (CLI)。
核心算法（Core）是通用的，实现了“降水-流向-坡度-侵蚀/沉积”的物理模型。

## 项目结构

- `src/VirtualTerrainErosion.Core`: **核心算法库**。包含地形网格、侵蚀物理模型、数据库日志逻辑。所有平台共用这一套代码。
- `src/VirtualTerrainErosion.WinForms`: **Windows 客户端**。使用 WinForms 提供可视化界面，支持实时交互、绘图。
- `src/VirtualTerrainErosion.Cli`: **Mac/Linux 客户端**。使用控制台（Terminal）运行模拟，输出统计数据并写入数据库。

## 如何运行

### 1. 数据库准备 (SQL Server)

确保你已经启动了 SQL Server Docker 容器。
如果端口是 1433，密码是 `gespwd`，则无需修改配置。

首次运行前，请执行 `setup.sql` 脚本建库建表：
你可以使用 Azure Data Studio 连接数据库并执行，或者如果安装了 `sqlcmd`：
```bash
sqlcmd -S localhost -U sa -P gespwd -i setup.sql
```

### 2. 在 macOS 上运行

直接运行根目录下的脚本：
```bash
./run_mac.sh
```
或者手动运行：
```bash
dotnet run --project src/VirtualTerrainErosion.Cli/VirtualTerrainErosion.Cli.csproj
```
程序会输出每 10 万年的统计数据，并尝试写入数据库。

### 3. 在 Windows 上运行

双击打开 `VirtualTerrainErosion.sln` (使用 VS2022)，将 `VirtualTerrainErosion.WinForms` 设为启动项目，点击运行即可看到图形界面。

## 核心代码说明

- **ErosionModel.cs**: 实现了 D8 流向算法、汇水面积计算、以及 `Δh = K(QS - T)` 的侵蚀公式。
- **TerrainGrid.cs**: 存储 65x65 的高程、水深、流向等数据。
- **DbLogger.cs**: 负责将模拟结果（最大高差、沟壑密度、Hack 定律斜率等）写入 SQL Server。

## 常见问题

- **连接数据库失败**: 请检查 Docker 容器是否运行，端口是否映射为 1433。如果端口不同，请在 `src/VirtualTerrainErosion.Core/AppSettings.cs` 或环境变量中修改连接字符串。
