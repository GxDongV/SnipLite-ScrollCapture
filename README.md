# SnipLite Scroll Capture

Scrolling screenshot plugin for SnipLite 0.3.16 or later. It supports automatic and manual vertical scrolling.

In SnipLite 0.3.17 or later, open **Preferences → Plugins → Discover plugins** to install or update Scroll Capture. You can also download the `.snipplugin` from Releases and import it under **Preferences → Plugins**, including on SnipLite 0.3.16. Restart SnipLite to apply an update.

Build with the .NET 10 SDK on Windows. Download `SnipLite.Plugin.Abstractions.1.0.0.nupkg` from Releases into a local folder, then pass that folder to the packaging script.

```powershell
./package.ps1 -SdkSource ./sdk
```

The SDK provides the compile-time contract. SnipLite provides the SDK assembly at runtime. See the [SnipLite plugin guide](https://github.com/GxDongV/SnipLite/blob/v0.3.16/docs/PLUGIN_DEVELOPMENT.zh-CN.md) for the package format.
