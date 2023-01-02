using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Izzy_Moonbot.Helpers;
using Izzy_Moonbot.Service;
using Izzy_Moonbot.Settings;
using Microsoft.Extensions.Logging;

namespace Izzy_Moonbot.EventListeners;

public class UserListener
{
    // This listener handles listening to user related events (join, leave, etc)
    // This is mostly used for logging and constructing user settings
    
    private readonly LoggingService _logger;
    private readonly Dictionary<ulong, User> _users;
    private readonly ModLoggingService _modLogger;
    private readonly ModService _mod;
    private readonly ScheduleService _schedule;
    private readonly Config _config;
    
    public UserListener(LoggingService logger, Dictionary<ulong, User> users, ModLoggingService modLogger, ModService mod, ScheduleService schedule, Config config)
    {
        _logger = logger;
        _users = users;
        _modLogger = modLogger;
        _mod = mod;
        _schedule = schedule;
        _config = config;
    }

    public void RegisterEvents(DiscordSocketClient client)
    {
        client.UserUnbanned += (user, guild) => Task.Run(async () => { await MemberUnbanEvent(user, guild); });
        client.UserJoined += (member) => Task.Run(async () => { await MemberJoinEvent(member); });
        client.UserLeft += (guild, user) => Task.Run(async () => { await MemberLeaveEvent(guild, user); });
        client.GuildMemberUpdated += (oldMember, newMember) => Task.Run(async () => { await MemberUpdateEvent(oldMember, newMember); });
    }

    public async Task MemberUnbanEvent(SocketUser user, SocketGuild guild)
    {
        if (guild.Id != DiscordHelper.DefaultGuild()) return;
        
        _logger.Log($"User was unbanned: {user.Username}#{user.Discriminator}.", level: LogLevel.Debug);
        var scheduledJobs = _schedule.GetScheduledJobs(job => 
            job.Action switch
            {
                ScheduledUnbanJob unbanJob => unbanJob.User == user.Id,
                _ => false
            }
        );
        _logger.Log($"Cancelling all scheduled unban jobs for this user", level: LogLevel.Debug);
        foreach (var scheduledJob in scheduledJobs)
        {
            await _schedule.DeleteScheduledJob(scheduledJob);
        }
        _logger.Log($"Cancelled all scheduled unban jobs for this user", level: LogLevel.Debug);
    }

    public async Task MemberJoinEvent(SocketGuildUser member, bool catchingUp = false)
    {
        if (member.Guild.Id != DiscordHelper.DefaultGuild()) return;
        
        _logger.Log($"New member join{(catchingUp ? " found after reboot" : "")}: {member.Username}#{member.DiscriminatorValue} ({member.Id})", level: LogLevel.Debug);
        if (!_users.ContainsKey(member.Id))
        {
            _logger.Log($"No user data entry for new user, generating one now...", level: LogLevel.Debug);
            User newUser = new User();
            newUser.Username = $"{member.Username}#{member.Discriminator}";
            newUser.Aliases.Add(member.Username);
            if (member.JoinedAt is DateTimeOffset join) newUser.Joins.Add(join);
            _users.Add(member.Id, newUser);
            await FileHelper.SaveUsersAsync(_users);
            _logger.Log($"New user data entry generated.", level: LogLevel.Debug);
        }
        else if (!catchingUp)
        {
            _logger.Log($"Found user data entry for new user, add new join date", level: LogLevel.Debug);
            if (member.JoinedAt is DateTimeOffset join) _users[member.Id].Joins.Add(join);
            await FileHelper.SaveUsersAsync(_users);
            _logger.Log($"Added new join date for new user", level: LogLevel.Debug);
        }
        
        List<ulong> roles = new List<ulong>();
        string expiresString = "";

        if (!_config.ManageNewUserRoles)
        {
            _logger.Log($"Skipping role management for new user join because ManageNewUserRoles is false", level: LogLevel.Debug);
        }
        else
        {
            _logger.Log($"Processing roles for new user join", level: LogLevel.Debug);

            if (_config.MemberRole == null || _config.MemberRole <= 0)
                _logger.Log($"ManageNewUserRoles is true but MemberRole is {_config.MemberRole}", level: LogLevel.Warning);
            else if (!(_config.AutoSilenceNewJoins || _users[member.Id].Silenced))
            {
                _logger.Log($"Adding Config.MemberRole ({_config.MemberRole}) to new user", level: LogLevel.Debug);
                roles.Add((ulong)_config.MemberRole);
            }

            if (_config.NewMemberRole == null || _config.NewMemberRole <= 0)
                _logger.Log($"ManageNewUserRoles is true but NewMemberRole is {_config.NewMemberRole}", level: LogLevel.Warning);
            else if ((!_config.AutoSilenceNewJoins || !_users[member.Id].Silenced))
            {
                _logger.Log($"Adding Config.NewMemberRole ({_config.NewMemberRole}) to new user", level: LogLevel.Debug);
                roles.Add((ulong)_config.NewMemberRole);
                expiresString = $"\nNew Member role expires in <t:{(DateTimeOffset.UtcNow + TimeSpan.FromMinutes(_config.NewMemberRoleDecay)).ToUnixTimeSeconds()}:R>";

                _logger.Log($"Adding scheduled job to remove Config.NewMemberRole from new user in {_config.NewMemberRoleDecay} minutes", level: LogLevel.Debug);
                var action = new ScheduledRoleRemovalJob(_config.NewMemberRole.Value, member.Id,
                    $"New member role removal, {_config.NewMemberRoleDecay} minutes (`NewMemberRoleDecay`) passed.");
                var task = new ScheduledJob(DateTimeOffset.UtcNow,
                    (DateTimeOffset.UtcNow + TimeSpan.FromMinutes(_config.NewMemberRoleDecay)), action);
                await _schedule.CreateScheduledJob(task);
                _logger.Log($"Added scheduled job for new user", level: LogLevel.Debug);
            }
        }

        _logger.Log($"Generating action reason", level: LogLevel.Debug);
        
        string autoSilence = $" (User autosilenced, `AutoSilenceNewJoins` is true.)";
        
        if (roles.Count != 0)
        {
            if (!_config.AutoSilenceNewJoins) autoSilence = "";
            if (_users[member.Id].Silenced)
                autoSilence = 
                    ", silenced (attempted silence bypass)";
            _logger.Log($"Generated action reason, executing action", level: LogLevel.Debug);

            await _mod.AddRoles(member, roles, $"New user join{autoSilence}.{expiresString}"); 
            _logger.Log($"Action executed, generating moderation log content", level: LogLevel.Debug);
        }

        autoSilence = ", silenced (`AutoSilenceNewJoins` is on)";
        if (!_config.AutoSilenceNewJoins) autoSilence = "";
        if (_users[member.Id].Silenced)
            autoSilence = 
                ", silenced (attempted silence bypass)";
        string joinedBefore = $", Joined {_users[member.Id].Joins.Count - 1} times before";
        if (_users[member.Id].Joins.Count <= 1) joinedBefore = "";

        var rolesAutoapplied = new List<string>();

        foreach (var roleId in _users[member.Id].RolesToReapplyOnRejoin)
        {
            var shouldAdd = true;
            
            if (!member.Guild.Roles.Select(role => role.Id).Contains(roleId))
            {
                _logger.Log(
                    $"{member.Username}#{member.Discriminator} ({member.Id}) had role which I would have reapplied on join but no longer exists: role id {roleId}");
                _users[member.Id].RolesToReapplyOnRejoin.Remove(roleId);
                _config.RolesToReapplyOnRejoin.Remove(roleId);
                await FileHelper.SaveConfigAsync(_config);
                await FileHelper.SaveUsersAsync(_users);
                shouldAdd = false;
            }
            else
            {

                if (!_config.RolesToReapplyOnRejoin.Contains(roleId))
                {
                    _logger.Log(
                        $"{member.Username}#{member.Discriminator} ({member.Id}) has role which will no longer reapply on join, role {member.Guild.Roles.Single(role => role.Id == roleId).Name} ({roleId})");
                    _users[member.Id].RolesToReapplyOnRejoin.Remove(roleId);
                    await FileHelper.SaveUsersAsync(_users);
                    shouldAdd = false;
                }
            }
            
            if(shouldAdd) rolesAutoapplied.Add($"<@&{roleId}>");
        }

        if(_users[member.Id].RolesToReapplyOnRejoin.Count != 0) 
            await _mod.AddRoles(member, _users[member.Id].RolesToReapplyOnRejoin,
                "Roles reapplied due to having them before leaving.");

        var rolesAutoappliedString = $", Reapplied roles (from `RolesToReapplyOnRejoin`): {string.Join(", ", rolesAutoapplied)}";

        if (rolesAutoapplied.Count == 0) rolesAutoappliedString = "";
        
        _logger.Log($"Generated moderation log content, posting log", level: LogLevel.Debug);
        
        await _modLogger.CreateModLog(member.Guild)
            .SetContent($"{(catchingUp ? "Catching up on ": "")}Join: <@{member.Id}> (`{member.Id}`), created <t:{member.CreatedAt.ToUnixTimeSeconds()}:R>{autoSilence}{joinedBefore}{rolesAutoappliedString}")
            .SetFileLogContent($"{(catchingUp ? "Catching up on ": "")}Join: {member.Username}#{member.Discriminator} (`{member.Id}`), created {member.CreatedAt:O}{autoSilence}{joinedBefore}{rolesAutoappliedString}")
            .Send();
        _logger.Log($"Log posted", level: LogLevel.Debug);
    }
    
    private async Task MemberLeaveEvent(SocketGuild guild, SocketUser user)
    {
        if (guild.Id != DiscordHelper.DefaultGuild()) return;
        
        _logger.Log($"Member leaving: {user.Username}#{user.Discriminator} ({user.Id}), getting last nickname", level: LogLevel.Debug);
        var lastNickname = "";
        try
        {
            lastNickname = _users[user.Id].Aliases.Last();
        }
        catch (InvalidOperationException)
        {
            lastNickname = "<UNKNOWN>";
        }

        _logger.Log($"Last nickname was {lastNickname}, checking whether user was kicked or banned", level: LogLevel.Debug);

        // Unfortunately Discord(.NET) doesn't tell us anything about why or how a user left a server, merely that they did.
        // To infer that they left *because* of a kick/ban, we arbitrarily assume that whenever a user is kicked/banned,
        // Discord will send the UserLeft event within 100 seconds, before 5 other kicks/bans take place, *and*
        // that the user will not be unbannned, re-join and re-leave all within 100 seconds.

        var kickAuditLog = guild.GetAuditLogsAsync(5, actionType: ActionType.Kick).FirstAsync()
            .GetAwaiter().GetResult()
            .Select(audit =>
            {
                var data = audit.Data as KickAuditLogData;
                if (data?.Target.Id == user.Id)
                {
                    if ((DateTimeOffset.UtcNow.ToUnixTimeSeconds() - audit.CreatedAt.ToUnixTimeSeconds()) <= 100)
                        return audit;
                }
                return null;
            }).Where(audit => audit != null).FirstOrDefault();

        var banAuditLog = guild.GetAuditLogsAsync(5, actionType: ActionType.Ban).FirstAsync()
            .GetAwaiter().GetResult()
            .Select(audit =>
            {
                var data = audit.Data as BanAuditLogData;
                if (data?.Target.Id == user.Id)
                {
                    if ((DateTimeOffset.UtcNow.ToUnixTimeSeconds() - audit.CreatedAt.ToUnixTimeSeconds()) <= 100)
                        return audit;
                }
                return null;
            }).Where(audit => audit != null).FirstOrDefault();

        _logger.Log($"Constructing moderation log content", level: LogLevel.Debug);
        var output = 
            $"Leave: {user.Username}#{user.Discriminator} ({lastNickname}) (`{user.Id}`) joined <t:{_users[user.Id].Joins.Last().ToUnixTimeSeconds()}:R>";
        var fileOutput = 
            $"Leave: {user.Username}#{user.Discriminator} ({lastNickname}) (`{user.Id}`) joined {_users[user.Id].Joins.Last():O}";

        if (banAuditLog != null)
        {
            _logger.Log($"User was banned, fetching the reason and moderator", level: LogLevel.Debug);
            _logger.Log($"Fetched, user was banned by {banAuditLog.User.Username}#{banAuditLog.User.Discriminator} ({banAuditLog.User.Id}) for \"{banAuditLog.Reason}\"", level: LogLevel.Debug);
            output =
                $"Leave (Ban): {user.Username}#{user.Discriminator} ({lastNickname}) (`{user.Id}`) joined <t:{_users[user.Id].Joins.Last().ToUnixTimeSeconds()}:R>, \"{banAuditLog.Reason}\" by {banAuditLog.User.Username}#{banAuditLog.User.Discriminator} ({guild.GetUser((ulong)banAuditLog.User.Id).DisplayName})";
            fileOutput =
                $"Leave (Ban): {user.Username}#{user.Discriminator} ({lastNickname}) (`{user.Id}`) joined {_users[user.Id].Joins.Last():O}, \"{banAuditLog.Reason}\" by {banAuditLog.User.Username}#{banAuditLog.User.Discriminator} ({guild.GetUser((ulong)banAuditLog.User.Id).DisplayName})";
        }

        if (kickAuditLog != null)
        {
            _logger.Log($"User was kicked, fetching the reason and moderator", level: LogLevel.Debug);
            _logger.Log($"Fetched, user was kicked by {kickAuditLog.User.Username}#{kickAuditLog.User.Discriminator} ({kickAuditLog.User.Id}) for \"{kickAuditLog.Reason}\"", level: LogLevel.Debug);
            output =
                $"Leave (Kick): {user.Username}#{user.Discriminator} ({lastNickname}) (`{user.Id}`) joined <t:{_users[user.Id].Joins.Last().ToUnixTimeSeconds()}:R>, \"{kickAuditLog.Reason}\" by {kickAuditLog.User.Username}#{kickAuditLog.User.Discriminator} ({guild.GetUser((ulong)kickAuditLog.User.Id).DisplayName})";
            fileOutput =
                $"Leave (Kick): {user.Username}#{user.Discriminator} ({lastNickname}) (`{user.Id}`) joined {_users[user.Id].Joins.Last():O}, \"{kickAuditLog.Reason}\" by {kickAuditLog.User.Username}#{kickAuditLog.User.Discriminator} ({guild.GetUser((ulong)kickAuditLog.User.Id).DisplayName})";
        }
        _logger.Log($"Finished constructing moderation log content", level: LogLevel.Debug);

        _logger.Log($"Fetch all scheduled jobs for this user", level: LogLevel.Debug);
        var scheduledTasks = _schedule.GetScheduledJobs(job => 
            job.Action switch
            {
                ScheduledRoleJob roleJob => roleJob.User == user.Id,
                ScheduledEchoJob echoJob => echoJob.ChannelOrUser == user.Id,
                _ => false
            }
        );
        _logger.Log($"Cancelling all scheduled jobs for this user", level: LogLevel.Debug);
        foreach (var scheduledTask in scheduledTasks.Where(scheduledTask => 
                     scheduledTask.Action is not ScheduledUnbanJob))
        {
            await _schedule.DeleteScheduledJob(scheduledTask);
        }
        _logger.Log($"Cancelled all scheduled jobs for this user", level: LogLevel.Debug);

        _logger.Log($"Sending moderation log", level: LogLevel.Debug);
        await _modLogger.CreateModLog(guild)
            .SetContent(output)
            .SetFileLogContent(fileOutput)
            .Send();
        _logger.Log($"Moderation log sent", level: LogLevel.Debug);
    }

    private async Task MemberUpdateEvent(Cacheable<SocketGuildUser,ulong> oldUser, SocketGuildUser newUser)
    {
        if (newUser.Guild.Id != DiscordHelper.DefaultGuild()) return;
        
        var changed = false;
        
        if (!_users.ContainsKey(newUser.Id))
        {
            changed = true;
            _logger.Log($"{newUser.Username}#{newUser.Discriminator} ({newUser.Id}) has no metadata, creating now...", level: LogLevel.Debug);
            var newUserData = new User();
            newUserData.Username = $"{newUser.Username}#{newUser.Discriminator}";
            newUserData.Aliases.Add(newUser.Username);
            if(newUser.JoinedAt.HasValue) newUserData.Joins.Add(newUser.JoinedAt.Value);
            _users.Add(newUser.Id, newUserData);
        }
        else
        {
            if (_users[newUser.Id].Username != $"{newUser.Username}#{newUser.Discriminator}")
            {
                _logger.Log($"User name/discriminator changed from {_users[newUser.Id].Username} to {newUser.Username}#{newUser.Discriminator}, updating...", level: LogLevel.Debug);
                _users[newUser.Id].Username =
                    $"{newUser.Username}#{newUser.Discriminator}";
                changed = true;
            }

            if (!_users[newUser.Id].Aliases.Contains(newUser.DisplayName))
            {
                _logger.Log($"{newUser.Username}#{newUser.Discriminator} ({newUser.Id}) has new displayname, updating...", level: LogLevel.Debug);
                _users[newUser.Id].Aliases.Add(newUser.DisplayName);
                changed = true;
            }
        }
        
        if (_config.MemberRole != null)
        {
            if (_users[newUser.Id].Silenced &&
                newUser.Roles.Select(role => role.Id).Contains((ulong)_config.MemberRole))
            {
                // Unsilenced, Remove the flag.
                _logger.Log(
                    $"{newUser.Username}#{newUser.Discriminator} ({newUser.Id}) unsilenced, removing silence flag...");
                _users[newUser.Id].Silenced = false;
                changed = true;
            }

            if (!_users[newUser.Id].Silenced &&
                !newUser.Roles.Select(role => role.Id).Contains((ulong)_config.MemberRole))
            {
                // Silenced, add the flag
                _logger.Log(
                    $"{newUser.Username}#{newUser.Discriminator} ({newUser.Id}) silenced, adding silence flag...");
                _users[newUser.Id].Silenced = true;
                changed = true;
            }
        }

        foreach (var roleId in _config.RolesToReapplyOnRejoin)
        {
            if (!_users[newUser.Id].RolesToReapplyOnRejoin.Contains(roleId) &&
                newUser.Roles.Select(role => role.Id).Contains(roleId))
            {
                _logger.Log(
                    $"{newUser.Username}#{newUser.Discriminator} ({newUser.Id}) gained role which will reapply on join, role {newUser.Roles.Single(role => role.Id == roleId).Name} ({roleId})");
                _users[newUser.Id].RolesToReapplyOnRejoin.Add(roleId);
                changed = true;
            }

            if (_users[newUser.Id].RolesToReapplyOnRejoin.Contains(roleId) &&
                !newUser.Roles.Select(role => role.Id).Contains(roleId))
            {
                _logger.Log(
                    $"{newUser.Username}#{newUser.Discriminator} ({newUser.Id}) lost role which would reapply on join, role {newUser.Guild.Roles.Single(role => role.Id == roleId).Name} ({roleId})");
                _users[newUser.Id].RolesToReapplyOnRejoin.Remove(roleId);
                changed = true;
            }
        }
        
        foreach (var roleId in _users[newUser.Id].RolesToReapplyOnRejoin)
        {
            if (!newUser.Guild.Roles.Select(role => role.Id).Contains(roleId))
            {
                _logger.Log(
                    $"{newUser.Username}#{newUser.Discriminator} ({newUser.Id}) had role which I would have reapplied on join but no longer exists: role id {roleId}");
                _users[newUser.Id].RolesToReapplyOnRejoin.Remove(roleId);
                _config.RolesToReapplyOnRejoin.Remove(roleId);
                await FileHelper.SaveConfigAsync(_config);
                changed = true;       
            }
            else
            {

                if (!_config.RolesToReapplyOnRejoin.Contains(roleId))
                {
                    _logger.Log(
                        $"{newUser.Username}#{newUser.Discriminator} ({newUser.Id}) has role which will no longer reapply on join, role {newUser.Guild.Roles.Single(role => role.Id == roleId).Name} ({roleId})");
                    _users[newUser.Id].RolesToReapplyOnRejoin.Remove(roleId);
                    changed = true;
                }
            }
        }

        if(changed) await FileHelper.SaveUsersAsync(_users);
    }
}