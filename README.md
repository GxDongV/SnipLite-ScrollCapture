# SnipLite Scroll Capture

Scrolling screenshot plugin for SnipLite. The 0.1.7 release starts independent distribution of the plugin. It requires SnipLite 0.3.16 or later and keeps the same plugin and command IDs as the bundled version.

Install the `.snipplugin` from Releases in SnipLite Preferences → Plugins. Restart SnipLite to activate an update to the bundled plugin.

Build with the .NET 10 SDK on Windows. Download the `SnipLite.Plugin.Abstractions.1.0.0.nupkg` asset from the v0.1.7 release into a local folder, then pass that folder to the packaging script.

```powershell
./package.ps1 -SdkSource ./sdk
```

The SDK provides the compile-time contract. SnipLite provides the SDK assembly at runtime. See the [SnipLite plugin guide](https://github.com/GxDongV/SnipLite/blob/v0.3.16/docs/PLUGIN_DEVELOPMENT.zh-CN.md) for the package format.
