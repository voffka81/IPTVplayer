using TV_Player;

namespace TV_Player.Maui.Services;

public sealed record ChannelTile(string Name, string Logo, M3UInfo? Channel = null);
