# Interface languages

`translations.json` is embedded in Core and shared by MAUI and Avalonia.
Every resource has Russian (`ru`) and English (`en`) values. Stable resource IDs
are used by the `loc:T` markup extensions and `I18n.Get` / `I18n.Format` in code.
Keep numbered format arguments identical in both translations.

The first launch uses Russian for a Russian system UI culture and English for
other cultures. `SettingsModel.Language` stores an explicit choice in the existing
settings file. Language selection is applied on Save: MAUI replaces its navigation
root, and Avalonia recreates the destination screen. Existing user source names,
channel names, guide content, provider IDs, settings keys, and disk paths are not translated.

Provider and operating-system error details may be displayed in their original
language after the localized application message. Logs are diagnostic text.
