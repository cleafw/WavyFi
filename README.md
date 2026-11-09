# WavyFi

简体中文 | [English](README.en.md)

<img src="docs/icon.png" width="128" alt="WavyFi 标志"/>

> Windows WiFi 扫描与优化工具，同时发现附近的 WiFi Direct 设备：手机、电视和 Miracast 接收器等。

WavyFi 扫描 Windows 电脑周围的无线网络，并给出可执行的建议：自己的 WiFi 应该使用哪个信道、是否值得切换频段，以及哪些安全设置需要改进。它也能显示许多 WiFi 分析工具忽略的 **WiFi Direct** 对等设备，例如手机、电视、打印机和 Miracast 接收器。

本分支：[cleafw/WavyFi](https://github.com/cleafw/WavyFi)。基于 [zhgzhg/WavyFi](https://github.com/zhgzhg/WavyFi)，保留上游作者 zhgzhg 的版权声明及 MIT 许可证。[英文原文](README.en.md)继续保留，便于对照。

![WavyFi 主窗口：网络列表、信道占用图、优化建议、WiFi Direct 设备和信号历史](docs/WavyFiScreenshot.png)

上图为上游项目的英文界面截图；本分支同时提供简体中文界面。

## 下载与使用

适用于 Windows 10 版本 2004（内部版本 19041）及以上，包括 Windows 11。

| 系统架构 | 免安装包（推荐） | 精简包 |
| --- | --- | --- |
| Windows x64（Intel / AMD） | [WavyFi-1.1.0-win-x64.zip](https://github.com/cleafw/WavyFi/releases/download/v1.1.0/WavyFi-1.1.0-win-x64.zip) | [WavyFi-1.1.0-win-x64-framework-dependent.zip](https://github.com/cleafw/WavyFi/releases/download/v1.1.0/WavyFi-1.1.0-win-x64-framework-dependent.zip) |
| Windows ARM64 | [WavyFi-1.1.0-win-arm64.zip](https://github.com/cleafw/WavyFi/releases/download/v1.1.0/WavyFi-1.1.0-win-arm64.zip) | [WavyFi-1.1.0-win-arm64-framework-dependent.zip](https://github.com/cleafw/WavyFi/releases/download/v1.1.0/WavyFi-1.1.0-win-arm64-framework-dependent.zip) |

解压 ZIP，双击 `WavyFi.exe` 即可扫描真实 Wi-Fi。推荐包已包含运行时，精简包需要同架构的 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)。`--demo` 用于展示模拟网络。

在 [GitHub Releases](https://github.com/cleafw/WavyFi/releases) 查看版本说明和 SHA-256 校验文件。WavyFi 的界面和无线扫描使用 Windows 接口，目前仅支持 Windows。

信道占用视图的灵感来自 inSSIDer，WavyFi 则着重于：

- **建议优先**：拥塞评分引擎告诉你可以如何改善网络。
- **WiFi Direct 发现**：与普通无线网络并排显示对等设备。
- **多网卡扫描**：同时使用多个无线网卡，每个网卡单独显示接收到的网络，便于比较接收效果。
- **持续显示**：暂时消失的网络逐渐淡出；WiFi Direct 设备在整个会话中保留，适应其间歇广播特性。
- **完整命令行**：`scan` / `p2p` 支持筛选、排序和 CSV 导出，与图形界面共用扫描引擎。
- 使用 C# / WPF，**无第三方运行时依赖**。

## 功能

- **附近网络**：显示 SSID、信号强度（%、dBm）、信道、频率、频段（2.4/5/6 GHz）、安全方式、BSSID 和距上次发现的时间。当前连接的网络会高亮。暂时未扫描到的条目会淡出，连续 2 分钟未发现后移除。通过原生 WiFi API（`wlanapi.dll`）每 5 秒自动刷新。
- **安全详情**：显示认证方式（WPA2/WPA3 等）、加密算法（AES-CCMP、TKIP 等），以及从 WPS 信息元素解析的 WPS 版本；没有 WPS 时显示 `-`。右键单击表头可选择显示的列。
- **速率**：“最高速率”是从 HT/VHT/HE 能力信息计算出的理论最高 PHY 速率（MCS 上限 × 空间流数 × 工作带宽 × 保护间隔）；“传统速率”来自信标的 Supported Rates 元素，列出 a/b/g 兼容速率。这类列表最高为 54 Mbps；现代速率使用 MCS 映射表示。
- **厂商识别**：通过内嵌的 IEEE OUI 数据快照，查询 BSSID 和 WiFi Direct 设备对应的厂商。
- **筛选**：支持按 SSID/BSSID/厂商搜索，选择频段和最低信号强度，以及按距上次发现的时间筛选（对数刻度，5 秒至 168 小时）。列表和图表都遵循当前筛选条件。
- **信号历史**：选择一个或多个网络，查看最近 5 分钟的 RSSI 曲线，颜色与信道图一致。WiFi Direct 设备的测量往往不连续，因此以散点显示实际收到的信号值。
- **信道占用图**：每个频段使用类似 inSSIDer 的占用曲线，自行绘制，无需图表库。曲线覆盖网络实际绑定的带宽（20 至 320 MHz，由 HT/VHT/HE/EHT Operation 元素解析），峰值为 RSSI，按 BSSID 着色，过期网络淡出，选中网络突出显示。信道 14 按实际 2484 MHz 位置绘制。
- **WiFi Direct 设备**：通过 `Windows.Devices.WiFiDirect` 实时发现正在广播的手机、电视、Miracast 接收器和打印机等。可排序列表显示名称、RSSI（设备提供时）、设备类型、配对状态、厂商、MAC 和距上次发现的时间；搜索及最低信号筛选也适用于此列表。设备在整个会话中保留，过期设备会淡出并持续累计未发现时长。
- **优化建议**：对拥塞程度评分，推荐 2.4 GHz 信道（1/6/11）及 5 GHz 非 DFS 信道；如果 DFS 更空闲则给出提示。同时分析自身网络的信道重叠，并提出安全升级建议。

## 构建、运行与测试

需要 **Windows 10 19041 或更高版本**以及 **.NET 10 SDK**。

```powershell
dotnet build                     # 构建整个解决方案
dotnet run --project src/WavyFi   # 启动图形界面
dotnet test                      # 单元测试：解析器、速率、建议引擎等
```

如果网络列表为空，请在 **Windows 设置 > 隐私和安全性 > 位置**中开启位置访问权限。Windows 会通过该权限控制 WiFi 扫描结果的访问。

## 中文与英文

图形界面的语言选择器支持 **简体中文**和 **English**，可用于切换界面语言。

命令行使用 `--lang zh-CN` 输出简体中文，使用 `--lang en` 输出英文，例如：

```powershell
WavyFi.exe scan --lang zh-CN --advise
WavyFi.exe --help --lang en
```

命令、选项名和排序键保持英文；SSID、MAC、协议名、单位及其他技术标识保持原样。本文中的命令可直接使用，无需翻译参数名。

本地化范围与检查方法见[本地化说明](docs/LOCALIZATION.md)。

## 命令行模式

带命令行参数运行程序时，使用终端扫描而不打开窗口；`--demo` 为图形演示模式例外。输出连接到父控制台，支持重定向和管道。

```text
WavyFi.exe scan [选项]       扫描 WiFi 网络（默认命令）
WavyFi.exe p2p  [选项]       列出正在广播的 WiFi Direct 设备
WavyFi.exe --demo            使用模拟数据打开图形演示界面

  --lang zh-CN|en           命令行输出和帮助的语言
  --adapters all|N[,M...]   （scan）使用的网卡，默认第一个
  --list-adapters          （scan）列出网卡及其索引后退出
  --band 2.4|5|6            （scan）仅显示指定频段
  --advise                 （scan）附加信道建议
  --search TEXT            按名称/BSSID/MAC/厂商子字符串筛选
  --min-signal DBM         隐藏信号弱于此值的条目，例如 -75
  --csv                    输出 CSV，而不是表格
  --sort COL[:asc|desc][,COL...]
                           按一列或多列排序，例如 channel:asc,rssi:desc
  --watch [SECONDS]        持续扫描，默认间隔 5 秒
  --timeout SECONDS        每轮最长等待时间，scan 默认 6 秒，p2p 默认 10 秒
  -v, --version            显示程序版本
  -h, --help               显示使用帮助
```

示例：

```powershell
WavyFi.exe scan --lang zh-CN --band 2.4 --min-signal -80 --advise
WavyFi.exe scan --adapters all --csv > networks.csv
WavyFi.exe p2p --sort type --watch 10 --lang en
```

程序使用 GUI 子系统，因此 cmd/PowerShell 可能在输出结束前显示提示符。WavyFi 会在交互式控制台输出完成后触发提示符重绘；管道和重定向输出不受影响。

## 使用说明

- **扫描开关**：启动或暂停每 5 秒一次的扫描。重新开启会清空先前累积的数据，开始新会话。扫描期间网卡选择器锁定；请先暂停扫描再切换网卡。
- **网卡选择器**：使用复选框多选，默认选中找到的第一个网卡。选择多个网卡后，同一网络会按接收到它的网卡分别显示，便于比较信号；默认隐藏的“网卡”列显示 `[索引] 名称`，图表标签也带有 `[索引]` 后缀。每次打开选择器都会重新枚举设备，因此能发现后来插入的 USB 网卡，并自动移除已断开的网卡。
- **网络列表**：单击行进行选择；再次单击已选行可取消选择，单击空白区域清除选择。选中的网络会在信号历史图中绘制，并在信道图中突出显示。右键单击表头选择可见列，悬停表头查看列说明。右键单击单元格可复制当前单元格、所选行或整个筛选表格（CSV）；Ctrl+C 复制所选行。
- **筛选器**：搜索名称、BSSID 和厂商；频段和最低信号强度筛选同时作用于列表与图表。优化建议始终基于全部扫描数据。
- **条目保留**：本轮未发现的网络淡出，连续 2 分钟未发现后移除，“上次发现”显示已过去的时间。WiFi Direct 设备也会淡出，但保留到会话结束；因其广播具有间歇性，暂时未发现通常不能说明设备已经离开。
- **WiFi Direct 列表**：支持排序，RSSI 按数值排序。右键单击行使用复制功能，右键单击表头选择可见列。只有提高最低信号阈值时，才会隐藏没有信号读数的设备。

## 已知限制

- WiFi Direct 使用 Windows 自动选择的无线网卡，无法确定每个设备由哪张网卡发现。
- 不支持监听模式抓包；所有数据来自标准扫描，足以支撑建议引擎。
- 无线网卡每轮扫描通常耗时 2–5 秒，DFS/6 GHz 信道使用被动监听。除 5 秒定时刷新外，驱动发出扫描完成通知（`WlanRegisterNotification`）后也会立即刷新。
- 支持 WiFi 7（802.11be）：从 EHT Operation 元素解析最高 320 MHz 带宽，从 EHT Capabilities MCS 映射计算最高速率，包括 MCS 12/13、4096-QAM。
- 尚不识别 WiFi 8（802.11bn）：草案元素 ID 尚未形成公开稳定的依据，猜测并硬编码可能误解析其他元素。此类 AP 暂按其 WiFi 7 能力显示；待 Wireshark 解析器提供相应支持后可重新评估。
- “自身网络”分组通过 MAC 启发式识别路由器的其他无线接口，并将其排除在邻居拥塞评分之外。MAC 无关联的 Mesh 节点仍可能被计为邻居。

## OUI 厂商数据库

“厂商”列及 WiFi Direct 设备详情使用内嵌的 **Wireshark `manuf` 文件**，即由 Wireshark 项目整理和发布的 IEEE Registration Authority OUI 注册表。

- 原始来源：[Wireshark manuf 数据](https://www.wireshark.org/download/automated/data/manuf)。
- 数据版权归 IEEE Registration Authority 所有，由 Wireshark 项目整理维护。上游每周重新生成，按其要求，请勿以高于每周一次的频率下载。

快照位于 `src/WavyFi.Core/Resources/manuf.gz`，作为资源嵌入程序；原项目记录的更新时间为 2026-07-21。更新方法（需 `curl` 和 `gzip`）：

```sh
curl -sL https://www.wireshark.org/download/automated/data/manuf -o src/WavyFi.Core/Resources/manuf
gzip -9 -f src/WavyFi.Core/Resources/manuf
dotnet build
```

仅使用普通的 24 位 OUI 行，跳过较少见的 /28 和 /36 子分配。本地管理的 MAC（例如随机或虚拟 BSSID）不在注册表内，会显示为未知厂商。

## 项目结构

| 路径 | 用途 |
|---|---|
| `src/WavyFi.Core/` | 核心引擎，无 UI 依赖 |
| `src/WavyFi.Core/Wlan/` | 原生 WiFi API 绑定、扫描器、信标 IE 解析和 PHY 速率计算 |
| `src/WavyFi.Core/WifiDirect/` | 基于 `DeviceWatcher` 的 WiFi Direct 设备发现 |
| `src/WavyFi.Core/Analysis/` | 信道拥塞评分和建议文本 |
| `src/WavyFi.Core/Models/` | 数据模型、网络/设备持久化存储和格式化 |
| `src/WavyFi.Core/Data/` | 内嵌 OUI 数据库加载器，将 MAC 前缀映射到厂商 |
| `src/WavyFi/` | WPF 图形界面和命令行入口 |
| `src/WavyFi/Controls/` | 自行绘制的信道占用图和信号历史图 |
| `src/WavyFi/Cli/` | `scan` / `p2p` 命令行命令 |
| `src/WavyFi/Themes/` | 深色主题资源字典 |
| `src/WavyFi/Settings/` | 窗口、列布局和字体偏好的注册表持久化 |
| `tests/WavyFi.Core.Tests/` | 单元测试：模拟信标解析、速率、建议和存储 |
| `tools/ScanTest/` | 无需图形界面即可实测扫描器的控制台工具 |
| `tools/IconGen/` | 重建图标：`dotnet run --project tools/IconGen -- src/WavyFi/Resources/WavyFi.ico` |

## 许可证与致谢

采用 [MIT 许可证](LICENSE)，原始版权为 **Copyright (c) 2026 zhgzhg**。本分支保留原作者署名和许可证；上游项目为 [zhgzhg/WavyFi](https://github.com/zhgzhg/WavyFi)。内嵌 OUI 数据快照的来源与归属见上文。
