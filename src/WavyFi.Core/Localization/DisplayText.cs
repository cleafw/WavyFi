namespace WavyFi.Localization;

/// <summary>Localizes presentation without changing canonical scanner/CSV values.</summary>
public static class DisplayText
{
    public static string Security(string value) => value switch
    {
        "Open" => Locale.T(value, "开放网络"),
        "WPA-Personal" => Locale.T(value, "WPA 个人版"),
        "WPA2-Personal" => Locale.T(value, "WPA2 个人版"),
        "WPA2/WPA3-Personal" => Locale.T(value, "WPA2/WPA3 个人版"),
        "WPA/WPA2-Personal" => Locale.T(value, "WPA/WPA2 个人版"),
        "WPA3-Personal" => Locale.T(value, "WPA3 个人版"),
        "WPA-Enterprise" => Locale.T(value, "WPA 企业版"),
        "WPA2-Enterprise" => Locale.T(value, "WPA2 企业版"),
        "WPA3-Enterprise" => Locale.T(value, "WPA3 企业版"),
        "WPA3-Enterprise 192-bit" => Locale.T(value, "WPA3 企业版（192 位）"),
        "WPA-None" => Locale.T(value, "WPA 无认证"),
        "OWE (Enhanced Open)" => Locale.T(value, "OWE（增强型开放网络）"),
        _ => value,
    };
    public static string Cipher(string value) => value switch
    {
        "None" => Locale.T(value, "无"),
        "Group" => Locale.T(value, "组加密"),
        _ => value,
    };
    public static string Vendor(string value) => value == "(unknown vendor)" ? Locale.T(value, "（未知厂商）") : value;
    public static string DeviceType(string value) => value switch
    {
        "Computer" => Locale.T(value, "电脑"),
        "Input device" => Locale.T(value, "输入设备"),
        "Printer/Scanner" => Locale.T(value, "打印机／扫描仪"),
        "Camera" => Locale.T(value, "摄像设备"),
        "Storage" => Locale.T(value, "存储设备"),
        "Network device" => Locale.T(value, "网络设备"),
        "Display/TV" => Locale.T(value, "显示器／电视"),
        "Multimedia device" => Locale.T(value, "多媒体设备"),
        "Gaming device" => Locale.T(value, "游戏设备"),
        "Phone" => Locale.T(value, "手机"),
        "Audio device" => Locale.T(value, "音频设备"),
        "Dock" => Locale.T(value, "扩展坞"),
        "Other device" => Locale.T(value, "其他设备"),
        _ => value,
    };
    public static string PeerName(string value) => value == "(unnamed)" ? Locale.T(value, "（未命名）") : value;
}
