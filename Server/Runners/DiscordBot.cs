using System.Diagnostics.CodeAnalysis;

using Discord;
using Discord.Rest;
using Discord.WebSocket;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using SixLabors.ImageSharp.PixelFormats;

namespace Server.Runners;

#pragma warning disable CA2254
[SuppressMessage("Performance", "CA1873:Avoid potentially expensive logging")]
public class DiscordBot(ILogger<DiscordBot> logger) : IHostedService
{
    private DiscordSocketClient? _client;
    private HttpClient? _httpClient;

    const string AvatarDataFolder = "/tmp/simpleprofilesync/avatars";
    const string AvatarUniqueFolder = "/tmp/simpleprofilesync/avatars/unique";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Hello, World!");

        CreateClient();

        if (_client is null)
            throw new Exception("Client was not initialized.");

        await _client.StartAsync();

        _client.Log += HandleClientLog;

        _client.UserUpdated += (user, socketUser) =>
        {
            logger.LogDebug($"User updated:\nUser 1: {user}\nUser 2: {socketUser}");
            return Task.CompletedTask;
        };

        _client.PresenceUpdated += (user, presence, _) => UpdateUserRole(user, presence, cancellationToken);

        var botToken = Environment.GetEnvironmentVariable("BOT__TOKEN");
        ArgumentException.ThrowIfNullOrEmpty(botToken);

        await _client.LoginAsync(TokenType.Bot, botToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Goodbye, World!");
        return Task.CompletedTask;
    }

    private void CreateClient()
    {
        if (_client is not null)
        {
            logger.LogWarning("Attempted to reinitialize {classname}. Instance is already initialized.",
                nameof(DiscordSocketClient));
        }

        var clientConfig = new DiscordSocketConfig
        {
            AlwaysDownloadUsers = false,
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildPresences |
                             GatewayIntents.GuildScheduledEvents | GatewayIntents.GuildInvites,
        };

        _client = new DiscordSocketClient(clientConfig);

        _httpClient = new HttpClient();

        var dirInfo = Directory.CreateDirectory(AvatarDataFolder);
        if (!dirInfo.Exists)
        {
            logger.LogWarning("Created avatar directory at '{dir}'", dirInfo.FullName);
        }

        dirInfo = Directory.CreateDirectory(AvatarUniqueFolder);
        if (!dirInfo.Exists)
        {
            logger.LogWarning("Created avatar downloaded flag directory at '{dir}'", dirInfo.FullName);
        }
    }

    private Task HandleClientLog(LogMessage log)
    {
        switch (log.Severity)
        {
            case LogSeverity.Error:
            case LogSeverity.Critical:
                logger.LogCritical(log.Exception, log.Message);
                break;
            case LogSeverity.Warning:
                logger.LogWarning(log.Message, log.Exception);
                break;
            case LogSeverity.Info:
                logger.LogInformation(log.Message.ToString());
                break;
            case LogSeverity.Verbose:
            case LogSeverity.Debug:
                logger.LogDebug(log.ToString());
                break;
            default:
                throw new Exception($"Unknown log severity '{log.Severity}'. Message: \"{log.Message}\"",
                    log.Exception);
        }

        return Task.CompletedTask;
    }

    private async Task UpdateUserRole(
        SocketUser user,
        SocketPresence presence,
        CancellationToken cancellationToken)
    {
        if (presence.Status == UserStatus.Offline || user.IsBot || user.IsWebhook) return;

        const byte avatarSize = 128;
        var userAvatarUrl = user.GetDisplayAvatarUrl(format: ImageFormat.WebP, size: avatarSize);

        // logger.LogDebug($"User avatar: {userAvatarUrl}\nAvatar: {user.GetAvatarUrl()}");

        var avatarUniquePath = $"{AvatarUniqueFolder}/{user.Id}-{user.AvatarId}";
        if (File.Exists(avatarUniquePath))
        {
            return;
        }

        byte r = 0, g = 0, b = 0;
        try
        {
            using var avatarResponse = await _httpClient!.GetAsync(userAvatarUrl, cancellationToken);

            avatarResponse.EnsureSuccessStatusCode();

            await using var avatarStream = await avatarResponse.Content.ReadAsStreamAsync(cancellationToken);

            using var image = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(avatarStream, cancellationToken);

            const ushort pxCount = avatarSize * avatarSize;

            foreach (var offset in Enumerable.Range(0, pxCount))
            {
                var col = offset % avatarSize;
                var row = offset / avatarSize;

                var pxData = image[row, col];

                r += pxData.R;
                g += pxData.G;
                b += pxData.B;
            }

            await File.Create(avatarUniquePath).DisposeAsync();
        }
        catch (Exception)
        {
            /* ignored */
        }

        logger.LogDebug($"Color updated for user '{user.Username}' ({user.Id}) to ({r},{g},{b}).");

        var userMutualGuilds = user.MutualGuilds;
        if (userMutualGuilds.Count == 0)
            return;

        foreach (var mutualGuild in userMutualGuilds)
        {
            await CreateOrUpdateUserRole((r, g, b), mutualGuild, user, cancellationToken);
        }
    }

    private async Task CreateOrUpdateUserRole(
        (byte r, byte g, byte b) roleColor,
        SocketGuild guild,
        SocketUser user,
        CancellationToken _)
    {
        try
        {
            var userRoleId = guild.Roles.FirstOrDefault(x => x.Name == user.Username)?.Id;

            var color = new Color(roleColor.r, roleColor.g, roleColor.b);

            RestRole userRole;
            if (userRoleId is null)
            {
                userRole = await guild.CreateRoleAsync(user.Username, color: new RoleColors(color));
                logger.LogInformation(
                    $"Created new role '{user.Username}' ({userRole.Id}) in guild '{guild.Name}' ({guild.Id}).");
                return;
            }

            userRole = await guild.GetRoleAsync(userRoleId.Value);

            await userRole.ModifyAsync(props =>
            {
                props.Color = color;
            });

            logger.LogInformation(
                $"Updated role '{user.Username}' ({userRole.Id}) in guild '{guild.Name}' ({guild.Id}).");

            await (user as IGuildUser)!.AddRoleAsync(userRole);
        }
        catch (Exception e)
        {
            logger.LogError(e, $"Failed to create or update for user {user.Id} in guild {guild.Id}.");
        }
    }
}