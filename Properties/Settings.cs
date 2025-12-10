// Decompiled with JetBrains decompiler
// Type: PakViewer.Properties.Settings
// Assembly: PakViewer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 1B8FBB7F-36BB-4233-90DD-580453361518
// Assembly location: C:\Users\TonyQ\Downloads\PakViewer.exe

using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace PakViewer.Properties
{
  [CompilerGenerated]
  [GeneratedCode("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "9.0.0.0")]
  internal sealed class Settings : ApplicationSettingsBase
  {
    private static Settings defaultInstance = (Settings) SettingsBase.Synchronized((SettingsBase) new Settings());

    public static Settings Default
    {
      get
      {
        return Settings.defaultInstance;
      }
    }

    [DebuggerNonUserCode]
    [DefaultSettingValue("-c")]
    [UserScopedSetting]
    public string DefaultLang
    {
      get
      {
        return (string) this["DefaultLang"];
      }
      set
      {
        this["DefaultLang"] = (object) value;
      }
    }

    [DebuggerNonUserCode]
    [DefaultSettingValue("")]
    [UserScopedSetting]
    public string LastFolder
    {
      get
      {
        return (string) this["LastFolder"];
      }
      set
      {
        this["LastFolder"] = (object) value;
      }
    }

    [DebuggerNonUserCode]
    [DefaultSettingValue("")]
    [UserScopedSetting]
    public string LastIdxFile
    {
      get
      {
        return (string) this["LastIdxFile"];
      }
      set
      {
        this["LastIdxFile"] = (object) value;
      }
    }

    [DebuggerNonUserCode]
    [DefaultSettingValue("False")]
    [UserScopedSetting]
    public bool SkipSaveConfirmation
    {
      get
      {
        return (bool) this["SkipSaveConfirmation"];
      }
      set
      {
        this["SkipSaveConfirmation"] = (object) value;
      }
    }

    [DebuggerNonUserCode]
    [DefaultSettingValue("")]
    [UserScopedSetting]
    public string LastSprListFile
    {
      get
      {
        return (string) this["LastSprListFile"];
      }
      set
      {
        this["LastSprListFile"] = (object) value;
      }
    }

    private void SettingChangingEventHandler(object sender, SettingChangingEventArgs e)
    {
    }

    private void SettingsSavingEventHandler(object sender, CancelEventArgs e)
    {
    }
  }
}
