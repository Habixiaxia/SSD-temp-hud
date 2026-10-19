# 硬盘温度悬浮监控 · 技术设计

| 项目 | 内容 |
|---|---|
| 产品名称 | 盘温（内部代号 SsdTempHud） |
| 版本 | v0.1 |
| 日期 | 2026-10-19 |
| 状态 | 待确认 |
| 依据 | 需求规格 v0.1（已确认）+ 实机探测结果 |

---

## 0. 实机探测结论（技术选型的依据）

| 探测项 | 结果 | 结论 |
|---|---|---|
| 磁盘 | 2 × NVMe：Micron 3400 512G（C: D:）、Kingston NV2 1T（E: F:） | 只做 NVMe 通道即可覆盖当前机器 |
| 温度 · 原生 IOCTL | 51°C / 58°C，返回 40 字节、SensorCount=1 | **作为主通道** |
| 温度 · WMI | 60°C / 60°C，两块盘相同，且 MaxTemp=0 | 数据可疑，仅作降级备选 |
| 活动率 · 性能计数器 | `0 C: D:` 1.2%，`1 E: F:` 0% | **作为主通道，无需提权** |
| 活动率 · IOCTL_DISK_PERFORMANCE | `err=1`（功能未启用） | 不采用 |
| 管理员权限 | 非管理员打开 PhysicalDrive 返回 err=5 | 温度采集必须提权 |

---

## 1. 技术选型

| 决策 | 选择 | 理由 |
|---|---|---|
| 语言 / 框架 | C# 5 + WPF（.NET Framework 4.8） | 系统自带运行时；WPF 文字渲染、透明、DPI 支持最好 |
| 编译器 | 系统自带 `csc.exe` | 本机无 .NET SDK，无需安装任何东西 |
| 温度采集 | `DeviceIoControl` + `IOCTL_STORAGE_QUERY_PROPERTY`（PropertyId=52） | 原生、无驱动、实测可用 |
| 活动率采集 | `PerformanceCounter("PhysicalDisk", "% Disk Time", instance)` | 轻量、无需提权、实例名可直接映射磁盘序号 |
| 磁盘元数据 | WMI（`Win32_DiskDrive` + 分区映射），启动时读一次 | 拿型号与盘符；失败降级为 `Disk N` |
| 配置 | JSON + `DataContractJsonSerializer` | .NET Framework 内置，无第三方依赖 |
| 托盘 / 菜单 | WinForms `NotifyIcon` + `ContextMenuStrip` | 成熟稳定，WPF 无原生托盘 |
| 打包 | 单 exe + 应用清单 + 图标 | 绿色免安装 |

**排除的方案**：
- Electron / Tauri：体积 100 MB 起、内存高，不符合“游戏时低干扰”。
- Python + Qt：打包体积大、依赖多、透明置顶窗口在 Windows 上问题多。
- LibreHardwareMonitor / 第三方库：会加载内核驱动，存在反作弊与驱动黑名单风险。
- WinForms 自绘：圆角、抗锯齿文字、Per-Monitor DPI 都要手写，效果不如 WPF。

---

## 2. 总体架构

```
┌────────────────────────── SsdTempHud.exe ──────────────────────────┐
│                                                                    │
│  UI 线程（WPF Dispatcher）                                          │
│  ┌──────────────┐   ┌──────────────┐   ┌────────────────────────┐  │
│  │  HudWindow   │   │  TrayIcon    │   │  HotkeyService         │  │
│  │  顶部横条     │   │  托盘菜单     │   │  Ctrl+Alt+H / M        │  │
│  └──────▲───────┘   └──────┬───────┘   └───────────┬────────────┘  │
│         │ 渲染快照           │ 命令                  │ 命令            │
│         │                  ▼                       ▼                │
│  ┌──────┴───────────────────────────────────────────────────────┐   │
│  │                    AppController（应用状态）                   │   │
│  │   可见性 / 穿透 / 配置变更 / 退出                             │   │
│  └──────▲───────────────────────────────────────────────────────┘   │
│         │ ImmutableSnapshot（1s 更新）                               │
│  ┌──────┴───────────────────────────────────────────────────────┐   │
│  │              SensorService（后台线程 + Timer）                 │   │
│  │  TemperatureSensor（3s）  ActivitySensor（1s）  DiskCatalog（60s）│   │
│  └──────────────────────────────────────────────────────────────┘   │
│                                                                    │
│  ConfigService（JSON）      LogService（滚动日志）                   │
└────────────────────────────────────────────────────────────────────┘
```

**原则**：采集全部在后台线程；UI 线程只做渲染；任何单个传感器失败都不影响整体。

---

## 3. 数据采集设计

### 3.1 温度（TemperatureSensor）

```
打开 \\.\PhysicalDriveN
  → DeviceIoControl(0x2D1400, PropertyId=52, QueryType=0)
  → 解析 STORAGE_TEMPERATURE_DATA_DESCRIPTOR
  → 关闭句柄
```

返回结构（实测 40 字节）：

| 偏移 | 字段 | 说明 |
|---|---|---|
| 0 | Version | 实测 40 |
| 4 | Size | 实测 40 |
| 8 | CriticalTemperature | 实测 0x8000，**不可信，忽略** |
| 10 | WarningTemperature | 实测 0x8000，**不可信，忽略** |
| 12 | InfoCount | 传感器数量，实测 1 |
| 24 + i×16 | TemperatureInfo[i] | Index(+0)、Temperature(+2, short, °C) |

- 磁盘枚举：从 `PhysicalDrive0` 递增尝试到 `PhysicalDrive15`，打开成功即视为存在。
- 打开权限：先 `GENERIC_READ`，失败再试 `GENERIC_READ|GENERIC_WRITE`。
- 失败处理：记录 Win32 错误码，该项显示 `--`，下一个周期重试；连续失败 10 次写日志。
- 温度单位：摄氏度；实测值 51 / 58，处于合理区间。
- 阈值：**不使用盘内阈值**，用本地配置（默认 65°C 偏高 / 75°C 危险）。

### 3.2 活动率（ActivitySensor）

- 计数器路径：`PhysicalDisk` / `% Disk Time` / 实例名（如 `0 C: D:`）。
- 实例名前缀数字 = 物理磁盘序号，用于与温度数据对齐。
- 预热：首次 `NextValue()` 只建立基线，不显示；之后每次采样得到区间值。
- 采样间隔 1 秒；数值可能超过 100%，显示时截断为 100%。
- 每 30 秒核对一次实例列表：磁盘增减时重建计数器对象。
- 计数器不可用时：活动率显示 `--`，温度不受影响。

### 3.3 磁盘元数据（DiskCatalog）

- WMI 查询：`Win32_DiskDrive`（Index、Model、Size）。
- 盘符映射：`Win32_DiskDriveToDiskPartition` + `Win32_LogicalDiskToPartition`。
- 启动时读取一次，之后每 60 秒刷新；WMI 调用超过 3 秒视为超时，沿用旧数据。
- 型号简称规则：去掉 `NVMe ` 前缀，超过 18 字符截断加 `…`。
- 失败降级：显示 `Disk N`，盘符留空，不影响温度与活动率。

### 3.4 刷新节奏与性能预算

| 数据 | 周期 | 单次成本（估算） |
|---|---|---|
| 活动率 | 1 s | < 1 ms（每盘一次 NextValue） |
| 温度 | 3 s | 2–5 ms（每盘一次 IOCTL） |
| 元数据 | 60 s | 50–200 ms（WMI，后台） |
| UI 渲染 | 1 s | 重绘 10–30 个元素，无动画 |

预算：CPU ≤ 0.5%（单核）、内存 ≤ 80 MB、GDI 对象数稳定不增长。

---

## 4. 界面规格

视觉预览见 [`ui-mockup.html`](ui-mockup.html)（浏览器打开）。

### 4.1 尺寸与位置

| 项 | 值 |
|---|---|
| 高度 | 34 DIP（随 DPI 缩放） |
| 宽度 | 内容自适应，最小 320，最大不超过屏幕宽度 − 24 |
| 位置 | 屏幕顶部居中，`top = 0`（配置可调 0–8 px） |
| 圆角 | 底部两角 10 px，顶部贴边 |
| 边距 | 左右 14 px，内部元素间距 18 px |

### 4.2 颜色

| 用途 | 色值 |
|---|---|
| 背景 | `rgba(13,16,22,0.75)`，设置窗口可调 0%–100% |
| 边框 | `rgba(255,255,255,0.12)`，1 px |
| 主文字 | `#E6EAF2` |
| 次要文字 | `#8B93A7` |
| 温度正常 | `#4ADE80` |
| 温度偏高 | `#FBBF24` |
| 温度危险 | `#F87171` |
| 无数据 | `#6B7280` |
| 活动率条 | `#38BDF8`，轨道 `#1E293B` |

### 4.3 单块磁盘的布局

```
●  C:   51°C   ▬▬▬▬▬▬▬░░░   12%
│   │    │          │         └─ 活动率百分比，11 px，右对齐
│   │    │          └─────────── 活动率条 44×4 px
│   │    └────────────────────── 温度，13 px 粗体，按阈值着色
│   └─────────────────────────── 盘符，12 px 半粗体
└─────────────────────────────── 状态点 6 px：绿=数据正常，琥珀=数据过期，灰=读取失败
```

多块磁盘之间用 1×14 px 的半透明竖线分隔。

### 4.4 状态与交互

| 场景 | 行为 |
|---|---|
| 无磁盘 | 显示“未检测到磁盘” |
| 温度读取失败 | 温度显示 `--`，状态点变灰，tooltip 显示错误码 |
| 数据超过 5 秒未更新 | 状态点变琥珀色 |
| 鼠标悬停 | 显示完整型号、容量、温度、活动率（仅穿透关闭时） |
| 单击托盘图标 | 弹出菜单 |
| `Ctrl+Alt+H` | 显示 / 隐藏横条 |
| `Ctrl+Alt+M` | 切换点击穿透 |
| 显示器分辨率/数量变化 | 重新定位到目标屏幕顶部 |

### 4.5 窗口行为

- `Topmost = true`，`ShowInTaskbar = false`，`WindowStyle = None`，`AllowsTransparency = true`。
- 扩展样式：`WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT`（穿透开启时）。
- 穿透开启时鼠标完全作用于下层游戏；关闭后窗口可悬停、可右键菜单。
- 不抢占焦点：显示与刷新都不调用 `Activate()`。

---

## 5. 配置

路径：`%APPDATA%\SsdTempHud\config.json`

```json
{
  "screenIndex": 0,
  "topOffset": 0,
  "opacity": 0.75,
  "clickThrough": true,
  "showActivity": true,
  "activityRefreshMs": 1000,
  "temperatureRefreshMs": 3000,
  "warnTemperatureC": 65,
  "criticalTemperatureC": 75,
  "autostart": false,
  "logging": true
}
```

规则：
- 首次启动生成默认配置。
- 文件损坏：备份为 `config.bad.json`，重建默认配置，托盘提示一次。
- 数值越界：钳制到合法范围（如 opacity 0.0–1.0）。

---

## 6. 启动与退出流程

**启动**：
1. 检查单实例互斥体，已存在则退出。
2. 加载配置（失败用默认值）。
3. 启动 WMI 元数据读取（后台）。
4. 创建横条窗口并显示。
5. 启动采集定时器。
6. 注册全局热键、创建托盘图标。
7. 输出日志：版本、配置摘要、磁盘列表。

**退出**：
1. 停止定时器，等待采集线程结束（最多 500 ms）。
2. 注销热键、释放托盘图标、释放磁盘句柄。
3. 保存配置。
4. 释放互斥体。

---

## 7. 权限与安全

- 应用清单声明 `requireAdministrator`，双击即弹 UAC。
- 只读访问：不写磁盘、不改分区、不加载驱动。
- 不联网：无任何网络代码。
- 日志只记录错误码、磁盘序号，不记录用户文件路径。
- 开机自启用**计划任务**（`schtasks /RL HIGHEST /SC ONLOGON`），避免每次登录弹 UAC；由用户手动开启。

---

## 8. 异常处理与日志

| 异常 | 处理 |
|---|---|
| 单盘温度失败 | 该项 `--`，其余正常，下轮重试 |
| 性能计数器实例消失 | 重建实例列表，缺失项显示 `--` |
| WMI 超时/失败 | 沿用上次元数据，60 秒后重试 |
| 配置文件损坏 | 备份 + 重建默认 |
| 未捕获异常 | 写日志 + 托盘气泡提示，不弹模态框 |
| 日志 | `%APPDATA%\SsdTempHud\log.txt`，单文件上限 1 MB，滚动 |

---

## 9. 工程结构与构建

```
ssd-temp-hud/
├── src/
│   ├── Program.cs          入口、单实例、--selftest
│   ├── AppController.cs    应用状态与命令
│   ├── HudWindow.cs        横条窗口
│   ├── TrayIcon.cs         托盘
│   ├── Hotkey.cs           全局热键
│   ├── Config.cs           配置模型与读写
│   ├── Log.cs              日志
│   ├── Native.cs           P/Invoke 声明
│   └── Sensors/
│       ├── SensorService.cs
│       ├── TemperatureSensor.cs
│       ├── ActivitySensor.cs
│       └── DiskCatalog.cs
├── app.manifest            提权 + DPI 声明
├── build.cmd               csc 编译脚本
├── bin/SsdTempHud.exe      产物
└── docs/                   需求、设计、预览
```

编译命令（由 `build.cmd` 封装）：

```
csc /nologo /target:winexe /platform:anycpu /optimize+ ^
    /out:bin\SsdTempHud.exe /win32manifest:app.manifest ^
    /r:PresentationFramework.dll /r:PresentationCore.dll /r:WindowsBase.dll ^
    /r:System.Xaml.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
    /r:System.Management.dll /r:System.Runtime.Serialization.dll ^
    src\*.cs
```

诊断模式：`SsdTempHud.exe --selftest` 在控制台输出一次采集结果（不显示窗口），用于验收与排障。

---

## 10. 测试计划

| 用例 | 方法 | 对应验收 |
|---|---|---|
| 温度准确性 | 与 CrystalDiskInfo / HWiNFO 同时读数对比 | AC-02 |
| 活动率一致性 | 复制大文件时对比任务管理器 | AC-03 |
| 置顶与穿透 | 无边框窗口游戏 + 桌面点击测试 | AC-04 |
| 单盘失败 | 禁用某盘设备或拔盘（可选） | AC-05 |
| 长稳 | 挂机 8 小时，记录内存/GDI 对象 | AC-06 |
| 退出 | 托盘退出后检查进程与热键 | AC-07 |
| 配置 | 修改阈值/透明度后重启 | AC-08 |
| 多屏 / DPI | 双屏不同缩放比例 | NFR-07 |

---

## 11. 里程碑

| 阶段 | 内容 | 交付 |
|---|---|---|
| **M1 UI 骨架** | 横条窗口 + 假数据 + 置顶/穿透 + 托盘 + 热键 | 可运行、可看效果 |
| **M2 真实数据** | 温度 + 活动率接入，失败降级 | 数据与系统工具一致 |
| **M3 完善** | 配置持久化、多屏、开机自启、日志 | 日常可用 |
| **M4 发布** | 打包、--selftest、8 小时长稳、使用说明 | 最终交付 |

---

## 12. 风险与备选

| 风险 | 备选方案 |
|---|---|
| WPF 透明窗口在个别显卡驱动上闪烁 | 关闭 `AllowsTransparency`，改用不透明矩形窗口 |
| 性能计数器实例名在别的机器上不同 | 已按“前缀数字”解析，不依赖盘符文本 |
| 用户加装 SATA 硬盘 | 温度通道预留 `TemperatureProvider` 接口，后续补 SMART(194) 实现 |
| 独占全屏不可见 | 已声明不支持；建议游戏使用无边框窗口 |
| UAC 每次弹窗 | 开机自启走计划任务；手动启动仍需确认一次 |

---

## 13. 变更记录

| 版本 | 日期 | 说明 |
|---|---|---|
| v0.1 | 2026-10-19 | 初稿，基于实机探测结果 |
