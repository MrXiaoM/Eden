namespace Eden
{
    /// <summary>
    /// 未来框架协议信息
    /// </summary>
    public class MiraiProtocol
    {
        public string? appIdPhone = null;
        public string? appIdPad = null;
        public string? appKey = null;
        public string? sortVersionName = null;
        public string? buildTime = null;
        public string? apkSign = "a6b745bf24a2c277527716f6f36eb68d";
        public string? sdkVersion = null;
        public string? ssoVersion = null;
        public string? miscBitmap = null;
        public string? mainSigMap = null;
        public string? subSigMap = null;
        public string? qua = null;

        public bool IsAnyMiss
        {
            get
            {
                return appIdPhone == null
                    || appIdPad == null
                    || appKey == null
                    || sortVersionName == null
                    || buildTime == null
                    || apkSign == null
                    || sdkVersion == null
                    || ssoVersion == null
                    || miscBitmap == null
                    || mainSigMap == null
                    || subSigMap == null
                    || qua == null;
            }
        }

        public string json(bool pad = false)
        {
            // 这种固定格式的就懒得加json解析了
            return @$"{"{"}
    ""apk_id"": ""com.tencent.mobileqq"",
    ""app_id"": {((!pad ? appIdPhone : appIdPad) ?? "null")},
    ""sub_app_id"": {((!pad ? appIdPhone : appIdPad) ?? "null")},
    ""app_key"": ""{(appKey ?? "null")}"",
    ""sort_version_name"": ""{(sortVersionName ?? "null")}"",
    ""build_time"": {(buildTime ?? "null")},
    ""apk_sign"": ""{(apkSign ?? "null")}"",
    ""sdk_version"": ""{(sdkVersion ?? "null")}"",
    ""sso_version"": {(ssoVersion ?? "null")},
    ""misc_bitmap"": {(miscBitmap ?? "null")},
    ""main_sig_map"": {(mainSigMap ?? "null")},
    ""sub_sig_map"": {(subSigMap ?? "null")},
    ""dump_time"": ""{(buildTime ?? "null")}"",
    ""qua"": ""{(qua ?? "null")}"",
    ""protocol_type"": {(!pad ? "1" : "6")}
{"}"}
";
        }
    }
}
