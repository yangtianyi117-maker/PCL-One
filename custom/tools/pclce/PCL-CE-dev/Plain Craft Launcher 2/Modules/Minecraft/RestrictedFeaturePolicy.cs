using System;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.App.Region;

namespace PCL;

/// <summary>
/// PCL 定制版 的显式开关：默认不解除，需用户主动开启并自行承担合规风险。
/// </summary>
internal static class PclProPolicy
{
    internal static bool DisableRegionRestriction
    {
        get
        {
            // 单 exe 默认解除；显式设置 PCL_PRO_DISABLE_REGION_RESTRICTION=0 可恢复限制。
            var env = Environment.GetEnvironmentVariable("PCL_PRO_DISABLE_REGION_RESTRICTION");
            return !string.Equals(env, "0", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>PCL 定制版 不再弹启动环境警告。</summary>
    internal static bool SuppressEnvironmentWarning => true;

    /// <summary>PCL 定制版 允许直接新建离线/第三方档案，不强制先做微软验证。</summary>
    internal static bool AllowAnyProfile => true;
}

internal static class RestrictedFeaturePolicy
{
    internal static RegionAssessment Assess() => RegionPolicy.Evaluate(RegionPolicy.ReadWindowsSignals());

    internal static bool IsRestricted =>
        !PclProPolicy.DisableRegionRestriction && Assess().IsRestricted;

    internal static bool Check(string feature, bool showHint = true)
    {
        if (PclProPolicy.DisableRegionRestriction)
            return true;

        var assessment = Assess();
        if (!assessment.IsRestricted) return true;

        LauncherCommon.Log($"[RegionPolicy] 已阻止功能 {feature}，判定原因：{assessment.Reason}");
        if (showHint)
            HintService.Show(Hints.Error(Lang.Text("Region.Feature.Restricted")));
        return false;
    }

    internal static void ThrowIfRestricted(string feature)
    {
        if (Check(feature, false)) return;
        throw new CancelledException();
    }
}
