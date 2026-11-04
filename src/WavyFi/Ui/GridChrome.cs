using WavyFi.Localization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace WavyFi.Ui;

/// <summary>
/// Per-grid header dressing: a column-chooser context menu built from the
/// grid's own columns, plus a descriptive tooltip per header. Compact grids
/// get tighter header padding.
/// </summary>
public static class GridChrome
{
    private static readonly Dictionary<string, string> ColumnDescriptions = new()
    {
        ["(E)SSID"] = "Extended Service Set ID — the network name broadcast by the access point; \"(hidden)\" when suppressed.",
        ["Signal (%)"] = "Signal quality as a percentage derived from RSSI.",
        ["RSSI (dBm)"] = "Received signal strength — closer to 0 is stronger (-40 excellent, -85 weak).",
        ["Ch"] = "Channel — the number of the primary 20 MHz channel the AP beacons on.",
        ["Width (MHz)"] = "Total bonded channel width: 20/40/80/160, or up to 320 on WiFi 7 (802.11be).",
        ["Max rate (Mbps)"] = "Theoretical top PHY rate from the AP's advertised MCS map, spatial streams and operating width (0.8 µs GI). Real throughput is roughly half.",
        ["Freq (MHz)"] = "Center frequency of the primary channel.",
        ["Band (GHz)"] = "Frequency band: 2.4, 5 or 6 GHz.",
        ["802.11"] = "Supported 802.11 generations: b/g/a legacy, n = WiFi 4, ac = WiFi 5, ax = WiFi 6, be = WiFi 7.",
        ["Generation"] = "Friendly name of the newest supported standard: WiFi 4 = n, 5 = ac, 6 = ax (6E on the 6 GHz band), 7 = be. WiFi 1-3 are informal retro-names for b/a/g.",
        ["Security"] = "Authentication method (WPA2/WPA3 Personal or Enterprise, Open, WEP...).",
        ["Cipher"] = "Encryption cipher. AES-CCMP/GCMP are current; TKIP and WEP are legacy and weak.",
        ["WPS"] = "WiFi Protected Setup version advertised by the AP, or \"-\" when not supported.",
        ["Legacy rates (Mbps)"] = "802.11a/b/g compatibility rates from the beacon's Supported Rates elements — modern rates are expressed as MCS instead; see Max rate.",
        ["BSSID"] = "MAC address of this access point radio (one SSID can have several).",
        ["Vendor"] = "Manufacturer resolved from the MAC prefix (embedded IEEE OUI registry).",
        ["Adapter"] = "The WLAN adapter that heard this reading, as [index] and name — one row per adapter when scanning with several.",
        ["Last seen (s)"] = "Seconds since last seen; faded rows are stale.",
        ["Name"] = "Device name advertised over WiFi Direct.",
        ["Paired"] = "Whether this device is paired with Windows.",
        ["Type"] = "Device category from the WPS Primary Device Type attribute.",
        ["MAC"] = "MAC address of the device's WiFi Direct interface.",
    };

    private static readonly Dictionary<string, string> ChineseDescriptions = new()
    {
        ["(E)SSID"] = "接入点广播的无线网络名称；未广播时显示“隐藏网络”。",
        ["Signal (%)"] = "根据接收信号强度计算的信号质量百分比。",
        ["RSSI (dBm)"] = "接收信号强度，越接近 0 越强（−40 很好，−85 较弱）。",
        ["Ch"] = "主信道：接入点发送信标使用的 20 MHz 信道编号。",
        ["Width (MHz)"] = "合并后的总频宽：20、40、80、160 MHz；Wi-Fi 7 可达 320 MHz。",
        ["Max rate (Mbps)"] = "根据接入点公布的 MCS、空间流及频宽估算的最高物理速率（保护间隔 0.8 微秒）；实际吞吐量通常明显低于该值。",
        ["Freq (MHz)"] = "主信道的中心频率。",
        ["Band (GHz)"] = "无线频段：2.4、5 或 6 GHz。",
        ["802.11"] = "支持的标准：b/g/a 为早期标准，n 为 Wi-Fi 4，ac 为 Wi-Fi 5，ax 为 Wi-Fi 6，be 为 Wi-Fi 7。",
        ["Generation"] = "支持的最新 Wi-Fi 代际：4=n，5=ac，6=ax（6 GHz 为 6E），7=be；Wi-Fi 1～3 是 b/a/g 的非正式称呼。",
        ["Security"] = "身份认证方式，如 WPA2/WPA3 个人版、企业版、开放网络或 WEP。",
        ["Cipher"] = "加密算法：AES-CCMP/GCMP 为现代算法；TKIP 和 WEP 较旧且安全性弱。",
        ["WPS"] = "接入点公布的 WPS 快速配网版本；不支持时显示“-”。",
        ["Legacy rates (Mbps)"] = "信标中公布的 802.11a/b/g 兼容速率；现代速率使用 MCS 表示，请参考最高速率列。",
        ["BSSID"] = "接入点无线接口的 MAC 地址；同一网络名称可能对应多个地址。",
        ["Vendor"] = "根据 MAC 地址前缀及内置 IEEE OUI 数据库识别的厂商。",
        ["Adapter"] = "接收到此信号的无线网卡编号和名称；多网卡扫描时，每个网卡分别显示一行。",
        ["Last seen (s)"] = "距离上次发现的秒数；长时间未发现的行会淡出。",
        ["Name"] = "设备通过 Wi-Fi 直连公布的名称。",
        ["Paired"] = "设备是否已与 Windows 配对。",
        ["Type"] = "WPS 主设备类型属性中的设备类别。",
        ["MAC"] = "设备 Wi-Fi 直连接口的 MAC 地址。",
    };

    public static void Setup(DataGrid grid, bool compact)
    {
        var menu = BuildColumnChooserMenu(grid);
        var baseStyle = (Style)grid.FindResource(typeof(DataGridColumnHeader));
        foreach (var col in grid.Columns)
        {
            var style = new Style(typeof(DataGridColumnHeader), baseStyle);
            style.Setters.Add(new Setter(FrameworkElement.ContextMenuProperty, menu));
            if (compact)
                style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 3, 6, 3)));
            var header = ColumnKey.GetId(col) ?? col.Header?.ToString() ?? "";
            if (ColumnDescriptions.TryGetValue(header, out var description))
                style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,
                    Locale.T(description, ChineseDescriptions.GetValueOrDefault(header, description))));
            col.HeaderStyle = style;
        }
    }

    private static ContextMenu BuildColumnChooserMenu(DataGrid grid)
    {
        var menu = new ContextMenu();
        foreach (var column in grid.Columns)
        {
            var col = column;
            var item = new MenuItem
            {
                Header = col.Header?.ToString(),
                IsCheckable = true,
                IsChecked = col.Visibility == Visibility.Visible,
                StaysOpenOnClick = true,
            };
            item.Checked += (_, _) => col.Visibility = Visibility.Visible;
            item.Unchecked += (_, _) => col.Visibility = Visibility.Collapsed;
            menu.Items.Add(item);
        }
        return menu;
    }
}
