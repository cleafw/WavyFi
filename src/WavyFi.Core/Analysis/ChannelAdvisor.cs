using WavyFi.Localization;
using System.Text;
using WavyFi.Models;

namespace WavyFi.Analysis;

/// <summary>
/// Scores channel congestion from scan results and produces plain-language
/// recommendations for the user's own network.
/// </summary>
public static class ChannelAdvisor
{
    // Non-overlapping 2.4 GHz channels — the only sensible choices there.
    private static readonly int[] Preferred24 = { 1, 6, 11 };

    // Common 20/40 MHz anchor channels; non-DFS listed first on purpose.
    private static readonly int[] NonDfs5 = { 36, 40, 44, 48, 149, 153, 157, 161, 165 };
    private static readonly int[] Dfs5 = { 52, 56, 60, 64, 100, 104, 108, 112, 116, 120, 124, 128, 132, 136, 140, 144 };

    public static string BuildReport(IReadOnlyList<NetworkEntry> networks)
    {
        if (networks.Count == 0)
            return Locale.T("No scan data yet.", "暂无扫描数据。");

        // With several adapters selected the same AP appears once per adapter;
        // congestion must count each physical AP once (strongest reading wins).
        networks = networks
            .GroupBy(n => n.Bssid)
            .Select(g => g.OrderByDescending(n => n.IsConnected).ThenByDescending(n => n.Rssi).First())
            .ToList();

        var sb = new StringBuilder();
        var own = networks.FirstOrDefault(n => n.IsConnected);

        var band24 = networks.Where(n => n.Band == "2.4 GHz").ToList();
        var band5 = networks.Where(n => n.Band == "5 GHz").ToList();
        var band6 = networks.Where(n => n.Band == "6 GHz").ToList();

        sb.AppendLine(Locale.T($"Environment: {band24.Count} networks on 2.4 GHz, {band5.Count} on 5 GHz, {band6.Count} on 6 GHz.", $"周围环境：2.4 GHz 有 {band24.Count} 个网络，5 GHz 有 {band5.Count} 个，6 GHz 有 {band6.Count} 个。"));
        sb.AppendLine();

        // --- 2.4 GHz ---
        if (band24.Count > 0)
        {
            var best24 = Preferred24
                .OrderBy(ch => Congestion24(band24, ch, own))
                .First();
            sb.AppendLine(Locale.T($"2.4 GHz: best channel is {best24} ", $"2.4 GHz：推荐信道 {best24} ") +
                          Locale.T($"(least interference among 1/6/11; always avoid in-between channels).", $"（在 1/6/11 中干扰最少；请避免使用中间信道）。"));
        }

        // --- 5 GHz ---
        if (band5.Count > 0 || band24.Count > 0)
        {
            var best5 = NonDfs5
                .OrderBy(ch => Congestion5(band5, ch, own))
                .First();
            var quietDfs = Dfs5
                .OrderBy(ch => Congestion5(band5, ch, own))
                .First();
            sb.AppendLine(Locale.T($"5 GHz: best non-DFS channel is {best5}.", $"5 GHz：推荐非 DFS 信道 {best5}。"));
            if (Congestion5(band5, quietDfs, own) < Congestion5(band5, best5, own))
                sb.AppendLine(Locale.T($"       Channel {quietDfs} (DFS) is even quieter, if your router supports DFS.", $"       如果路由器支持 DFS，信道 {quietDfs}（DFS）的干扰更少。"));
        }

        // --- 6 GHz ---
        if (band6.Count > 0)
            sb.AppendLine(Locale.T($"6 GHz: in use nearby and still sparse — if your router and devices support WiFi 6E/7, it's the cleanest option.", $"6 GHz：周围已有网络使用，但数量较少。如果路由器和设备支持 WiFi 6E/7，这是干扰最少的选择。"));

        sb.AppendLine();

        // --- Advice about the user's own network ---
        if (own is not null)
        {
            sb.AppendLine(Locale.T($"Your network: \"{own.DisplayName}\" on channel {own.Channel} ({own.Band}), {DisplayText.Security(own.Security)}.", $"你的网络：\"{own.DisplayName}\"，信道 {own.Channel}（{own.Band}），安全方式 {DisplayText.Security(own.Security)}。"));

            var competitors = networks.Count(n =>
                !IsOwnRouter(n, own) && n.Band == own.Band && ChannelsOverlap(own, n));
            if (competitors > 0)
                sb.AppendLine(Locale.T($"  - {competitors} other network(s) overlap your channel — consider moving to the recommendation above.", $"  - 有 {competitors} 个其他网络与你的信道重叠，可考虑改用上面推荐的信道。"));
            else
                sb.AppendLine(Locale.T("  - No overlapping neighbors on your channel. Good spot.", "  - 没有邻近网络与你的信道重叠，当前选择较好。"));

            if (own.Band == "2.4 GHz" && band5.Count < band24.Count)
                sb.AppendLine(Locale.T("  - You're on the crowded 2.4 GHz band; prefer 5 GHz for devices that support it.", "  - 当前使用较拥挤的 2.4 GHz 频段；支持 5 GHz 的设备建议优先使用 5 GHz。"));

            if (own.Security is "Open" or "WEP" or "WPA-Personal")
                sb.AppendLine(Locale.T($"  - Security is {DisplayText.Security(own.Security)}: upgrade to WPA2 or WPA3 in your router settings.", $"  - 当前安全方式为 {DisplayText.Security(own.Security)}，请在路由器设置中改用 WPA2 或 WPA3。"));

            if (own.Cipher.Contains("TKIP") || own.Cipher.Contains("WEP"))
                sb.AppendLine(Locale.T($"  - Cipher is {DisplayText.Cipher(own.Cipher)} (legacy, slow, weak): switch to AES-CCMP in your router settings.", $"  - 当前加密算法为 {DisplayText.Cipher(own.Cipher)}（已过时、速度慢且安全性弱），请在路由器设置中改用 AES-CCMP。"));

            if (own.WpsVersion != "-")
                sb.AppendLine(Locale.T($"  - WPS {own.WpsVersion} is enabled: disable it if you don't use it — WPS PINs can be brute-forced.", $"  - WPS {own.WpsVersion} 已启用；如果不使用请关闭，WPS PIN 码存在被暴力破解的风险。"));
        }
        else
        {
            sb.AppendLine(Locale.T("Not connected to WiFi — connect to your own network to get personalized advice.", "尚未连接 WiFi。连接到你自己的网络后可获取针对性建议。"));
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>Weight neighbors by both spectral overlap (using their real
    /// bonded width) and signal strength. A network's energy reaches about
    /// width/10 channel numbers each side of its bonded center.</summary>
    private static double Congestion24(IEnumerable<NetworkEntry> nets, int channel, NetworkEntry? own)
    {
        double score = 0;
        foreach (var n in nets)
        {
            if (own is not null && IsOwnRouter(n, own)) continue;
            double reach = n.ChannelWidthMhz / 10.0 + 3; // their half-width + our half-width
            double overlap = Math.Max(0, 1.0 - Math.Abs(n.CenterChannel - channel) / reach);
            score += overlap * SignalWeight(n.Rssi);
        }
        return score;
    }

    /// <summary>5 GHz: competing when the candidate 20 MHz channel falls
    /// inside the neighbor's bonded span (plus its own half-width).</summary>
    private static double Congestion5(IEnumerable<NetworkEntry> nets, int channel, NetworkEntry? own)
    {
        double score = 0;
        foreach (var n in nets)
        {
            if (own is not null && IsOwnRouter(n, own)) continue;
            if (Math.Abs(n.CenterChannel - channel) <= n.ChannelWidthMhz / 10.0 + 2)
                score += SignalWeight(n.Rssi);
        }
        return score;
    }

    /// <summary>Heuristic: the same physical router's other radios and virtual
    /// BSSIDs share the middle four MAC octets — vendors derive per-radio MACs
    /// by tweaking the first and last octets. A 32-bit match makes accidental
    /// collisions with a true neighbor effectively impossible.</summary>
    private static bool IsOwnRouter(NetworkEntry n, NetworkEntry own)
    {
        if (n.Bssid == own.Bssid) return true;
        var a = n.Bssid.Split(':');
        var b = own.Bssid.Split(':');
        return a.Length == 6 && b.Length == 6 &&
               a[1] == b[1] && a[2] == b[2] && a[3] == b[3] && a[4] == b[4];
    }

    private static bool ChannelsOverlap(NetworkEntry a, NetworkEntry b) =>
        Math.Abs(a.CenterChannel - b.CenterChannel) <
        (a.ChannelWidthMhz + b.ChannelWidthMhz) / 10.0;

    /// <summary>A -50 dBm neighbor matters far more than a -90 dBm one.</summary>
    private static double SignalWeight(int rssi) =>
        Math.Clamp(rssi + 95, 0, 60) / 60.0;
}
