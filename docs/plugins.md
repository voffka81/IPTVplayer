# Channel provider plugins

The Avalonia player loads trusted provider plugins from:

```text
%LOCALAPPDATA%\TVPlayer\Plugins\<plugin-folder>\
```

Plugins are .NET 10 class libraries that reference `TV Player Plugin.Abstractions`. Implement `IChannelProviderPlugin`; the Settings page uses its `Settings` definitions to build a source form. The host saves those values and calls `CreateProvider` for each configured source. Providers return channel metadata from `GetChannelsAsync` and resolve the actual stream URL from `ResolveStreamAsync` when a channel is selected. The provider ID must match the configured source ID.

Plugin assemblies run in the player process with the current user's permissions. Install only plugins you trust. Restart the player after installing or updating a plugin.

### Development builds

Debug builds of the Avalonia app automatically build the example HTML plugin and copy its output and dependencies beside the app under `bin/Debug/net10.0/Plugins/HtmlPageStreamsPlugin`. The app scans this folder on startup as well as the normal per-user plugin folder. Edit the example plugin, rebuild and relaunch the app; no manual DLL copying is needed. Release builds do not include the development plugin.

The same development assembly includes a `Klubnichka.live` provider. It reads `a.channel-card` entries from the catalog, follows each channel page's player iframe chain, and extracts either the literal `generatedFile` value or a quoted `file` value from a Playerjs configuration. If `generatedFile` lists multiple HTTP(S) URLs separated by ` or `, the player tries them in order. It does not generate or guess authorization values; the page supplies signed URLs when needed. The provider also downloads a public XMLTV guide by default and maps known linear channels to guide IDs. Change the `XMLTV guide URL` source setting to use another guide; catalog entries without a known EPG ID remain without schedule data.

## HTML page streams example

A working example is in `Examples/HtmlPageStreamsPlugin`. It uses AngleSharp to parse server-returned HTML and exposes these settings:

- `Page URL`: the HTTP or HTTPS page to fetch.
- `Stream CSS selector`: elements that contain stream URLs, defaulting to `a[href]`.
- `Stream URL attribute`: the attribute holding the URL, defaulting to `href`.
- `Channel name selector`: optional child selector within a stream element; falls back to the element text and then its hostname.
- `Group selector`: optional child selector within a stream element.

Build it from the repository root:

```powershell
dotnet build "Examples\HtmlPageStreamsPlugin\HtmlPageStreamsPlugin.csproj" --configuration Release
```

Copy the contents of `Examples/HtmlPageStreamsPlugin/bin/Release/net10.0` into a new subfolder under `%LOCALAPPDATA%\TVPlayer\Plugins`. Keep `HtmlPageStreamsPlugin.dll`, its `.deps.json`, and AngleSharp dependency DLLs together. Restart the app, open Settings, select **HTML page streams**, enter a source name and page URL/selectors, add the source, then save Settings.

This example handles links present in the HTML response. Pages that build links in JavaScript, require authentication/tokens, or fetch a separate API need a plugin tailored to that site's flow. A page URL alone is not enough when the site hides stream URLs behind those mechanisms.
