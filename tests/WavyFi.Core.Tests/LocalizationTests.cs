using System.Globalization;
using WavyFi.Analysis;
using WavyFi.Localization;
using WavyFi.Models;

namespace WavyFi.Core.Tests;

[CollectionDefinition("Language switching", DisableParallelization = true)]
public class LanguageSwitchingCollection;

[Collection("Language switching")]
public class LocalizationTests : IDisposable
{
    private readonly CultureInfo _previous = Locale.Culture;

    [Theory]
    [InlineData("zh-CN", true)]
    [InlineData("zh-TW", true)]
    [InlineData("en-US", false)]
    [InlineData("fr-FR", false)]
    public void LanguageSelectionNormalizesToSupportedLanguages(string culture, bool chinese)
    {
        Locale.Culture = CultureInfo.GetCultureInfo(culture);
        Assert.Equal(chinese, Locale.IsChinese);
        Assert.Equal(chinese ? "中文" : "English", Locale.T("English", "中文"));
        Assert.Equal(chinese ? "信道 36" : "Channel 36", Locale.F("Channel {0}", "信道 {0}", 36));
    }

    [Fact]
    public void SwitchingLanguageChangesAdviceWithoutChangingNetworkData()
    {
        var network = new WifiNetwork
        {
            Ssid = "我的 WiFi", Bssid = "AA:BB:CC:00:11:22", Band = "5 GHz",
            Security = "WPA2-Personal", Channel = 36, ChannelWidthMhz = 80,
            CenterChannel = 42, Rssi = -40, IsConnected = true,
        };
        var entries = new[] { new NetworkEntry(network, DateTime.Now) };
        Locale.Culture = CultureInfo.GetCultureInfo("en");
        var english = ChannelAdvisor.BuildReport(entries);
        Locale.Culture = CultureInfo.GetCultureInfo("zh-CN");
        var chinese = ChannelAdvisor.BuildReport(entries);
        Assert.NotEqual(english, chinese);
        Assert.Matches("[\\u4e00-\\u9fff]", chinese);
        Assert.Contains("我的 WiFi", chinese);
        Assert.Equal("5 GHz", entries[0].Band);
        Assert.Equal("WPA2-Personal", entries[0].Security);
        Locale.Culture = CultureInfo.GetCultureInfo("en");
        Assert.Equal(english, ChannelAdvisor.BuildReport(entries));
    }

    [Fact]
    public void EmptyReportUsesSelectedLanguage()
    {
        Locale.Culture = CultureInfo.GetCultureInfo("en");
        Assert.Equal("No scan data yet.", ChannelAdvisor.BuildReport([]));
        Locale.Culture = CultureInfo.GetCultureInfo("zh-CN");
        Assert.Matches("[\\u4e00-\\u9fff]", ChannelAdvisor.BuildReport([]));
    }

    public void Dispose() => Locale.Culture = _previous;
}
