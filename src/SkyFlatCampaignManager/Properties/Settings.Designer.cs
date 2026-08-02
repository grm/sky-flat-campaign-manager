namespace NINA.Plugin.SkyFlatCampaignManager.Properties {
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "17.0.0.0")]
    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        public static Settings Default => defaultInstance;

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool UpdateSettings {
            get => ((bool)(this["UpdateSettings"]));
            set => this["UpdateSettings"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool PluginEnabled {
            get => ((bool)(this["PluginEnabled"]));
            set => this["PluginEnabled"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("60")]
        public int ValidityDays {
            get => ((int)(this["ValidityDays"]));
            set => this["ValidityDays"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool AutoStartExpiredCampaign {
            get => ((bool)(this["AutoStartExpiredCampaign"]));
            set => this["AutoStartExpiredCampaign"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("Default")]
        public string CampaignName {
            get => ((string)(this["CampaignName"]));
            set => this["CampaignName"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string StateDirectory {
            get => ((string)(this["StateDirectory"]));
            set => this["StateDirectory"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool DetailedLogging {
            get => ((bool)(this["DetailedLogging"]));
            set => this["DetailedLogging"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool DryRun {
            get => ((bool)(this["DryRun"]));
            set => this["DryRun"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("50")]
        public int DefaultTargetCount {
            get => ((int)(this["DefaultTargetCount"]));
            set => this["DefaultTargetCount"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("38.15")]
        public double DefaultTargetHistogramPercent {
            get => ((double)(this["DefaultTargetHistogramPercent"]));
            set => this["DefaultTargetHistogramPercent"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("10")]
        public double DefaultTargetTolerancePercent {
            get => ((double)(this["DefaultTargetTolerancePercent"]));
            set => this["DefaultTargetTolerancePercent"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("30")]
        public double SunSafetySeparationDegrees {
            get => ((double)(this["SunSafetySeparationDegrees"]));
            set => this["SunSafetySeparationDegrees"] = value;
        }
    }
}
