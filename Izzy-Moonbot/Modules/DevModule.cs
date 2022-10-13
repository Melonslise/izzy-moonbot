using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Izzy_Moonbot.Attributes;
using Izzy_Moonbot.Helpers;
using Izzy_Moonbot.Service;
using Izzy_Moonbot.Settings;
using Microsoft.Extensions.Logging;

namespace Izzy_Moonbot.Modules;

[Summary("Development commands.")]
public class DevModule : ModuleBase<SocketCommandContext>
{
    private readonly FilterService _filterService;
    private readonly LoggingService _loggingService;
    private readonly ModLoggingService _modLoggingService;
    private readonly ModService _modService;
    private readonly SpamService _pressureService;
    private readonly RaidService _raidService;
    private readonly ScheduleService _scheduleService;
    private readonly Config _config;
    private readonly State _state;
    private readonly Dictionary<ulong, User> _users;

    public DevModule(Config config, Dictionary<ulong, User> users, FilterService filterService,
        LoggingService loggingService, ModLoggingService modLoggingService, ModService modService,
        SpamService pressureService, RaidService raidService, ScheduleService scheduleService, State state)
    {
        _config = config;
        _users = users;
        _filterService = filterService;
        _loggingService = loggingService;
        _modLoggingService = modLoggingService;
        _modService = modService;
        _pressureService = pressureService;
        _raidService = raidService;
        _scheduleService = scheduleService;
        _state = state;
    }

    [Command("test")]
    [Summary("Unit tests for Izzy Moonbow")]
    [DevCommand]
    public async Task TestCommandAsync([Summary("Test Identifier")] string testId = "",
        [Remainder] [Summary("Test arguments")]
        string argString = "")
    {
        var args = argString.Split(" ");
        switch (testId)
        {
            case "pagination":
                var pages =
                    "Hello!||This is a test of pagination!||If this works, you're able to see this.||The paginated message will expire in 5 minutes.||Hopefully my code isn't broken..."
                        .Split("||");
                var staticParts =
                    $"**Test utility** - Pagination test{Environment.NewLine}*This is a simple test for the pagination utility!*{Environment.NewLine}*This is a header which will remain regardless of the current page.*{Environment.NewLine}Below is the paginated content.||This is the footer of the pagination message which will remain regardless of the current page{Environment.NewLine}There is a countdown below as well as buttons to change the page."
                        .Split("||");

                var paginationHelper = new PaginationHelper(Context, pages, staticParts);
                break;
            case "pressure-hook":
                Context.Message.ReplyAsync(
                    $"**Test utility** - Pressure hookin test.{Environment.NewLine}*Other services or modules can hook into the pressure service to do specific things.*{Environment.NewLine}*An example of this is getting pressure for a user.*{Environment.NewLine}*Like, your current pressure is `{_pressureService.GetPressure(Context.User.Id)}`*");
                break;
            case "dump-users-size":
                Context.Message.ReplyAsync($"UserStore size: {_users.Count}");
                break;
            case "create-echo-task":
                var action = _scheduleService.stringToAction(
                    $"echo in {Context.Channel.Id} content Hello! Exactly 1 minute should have passed between the test command and this message!");
                var task = new ScheduledTask(DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow + TimeSpan.FromMinutes(1), action);
                await _scheduleService.CreateScheduledTask(task, Context.Guild);
                await Context.Message.ReplyAsync("Created scheduled task.");
                break;
            case "test-twilight":
                await Context.Channel.SendMessageAsync(
                    $"Dear Princess Twilight,{Environment.NewLine}```{Environment.NewLine}" +
                    $"[2022-07-30 00:19:07 ERR] Izzy Moonbot has encountered an error. Logging information...{Environment.NewLine}" +
                    $"[2022-07-30 00:19:07 ERR] Message: Server requested a reconnect{Environment.NewLine}" +
                    $"[2022-07-30 00:19:07 ERR] Source: System.Private.CoreLib{Environment.NewLine}" +
                    $"[2022-07-30 00:19:07 ERR] HResult: -2146233088{Environment.NewLine}" +
                    "[2022-07-30 00:19:07 ERR] Stack trace:    at Discord.ConnectionManager.<>c__DisplayClass29_0.<<StartAsync>b__0>d.MoveNext()" +
                    $"{Environment.NewLine}```Your faithful Bot,{Environment.NewLine}Izzy Moonbot");
                break;
            case "twilight":
                await Context.Guild.GetTextChannel(1002687344199094292).SendMessageAsync(
                    $"Dear Princess Twilight,{Environment.NewLine}```{Environment.NewLine}" +
                    $"[2022-07-30 00:19:07 ERR] Izzy Moonbot has encountered an error. Logging information...{Environment.NewLine}" +
                    $"[2022-07-30 00:19:07 ERR] Message: Server requested a reconnect{Environment.NewLine}" +
                    $"[2022-07-30 00:19:07 ERR] Source: System.Private.CoreLib{Environment.NewLine}" +
                    $"[2022-07-30 00:19:07 ERR] HResult: -2146233088{Environment.NewLine}" +
                    "[2022-07-30 00:19:07 ERR] Stack trace:    at Discord.ConnectionManager.<>c__DisplayClass29_0.<<StartAsync>b__0>d.MoveNext()" +
                    $"{Environment.NewLine}```Your faithful Bot,{Environment.NewLine}Izzy Moonbot");

                break;
            case "immediate-log":
                await _modLoggingService.CreateActionLog(Context.Guild)
                    .SetActionType(LogType.Notice)
                    .SetReason(
                        $"This is a test of the new ModLoggingService.{Environment.NewLine}This should log immediatly.{Environment.NewLine}Run the `batch-log` test to test batch logging.")
                    .Send();
                break;
            case "batch-log":
                _config.BatchSendLogs = true;
                await FileHelper.SaveConfigAsync(_config);
                await _modLoggingService.CreateActionLog(Context.Guild)
                    .SetActionType(LogType.Notice)
                    .SetReason(
                        $"This is a test of the new ModLoggingService.{Environment.NewLine}This should log in batch with several Mod log types.{Environment.NewLine}Run the `immediate-log` test to test immediate logging.")
                    .Send();
                await _modLoggingService.CreateModLog(Context.Guild)
                    .SetContent("Mod log #1")
                    .Send();
                await _modLoggingService.CreateModLog(Context.Guild)
                    .SetContent("Mod log #2")
                    .Send();
                await _modLoggingService.CreateModLog(Context.Guild)
                    .SetContent("Mod log #3")
                    .Send();
                await _modLoggingService.CreateModLog(Context.Guild)
                    .SetContent("Mod log #4")
                    .Send();
                await _modLoggingService.CreateModLog(Context.Guild)
                    .SetContent("Mod log #5")
                    .Send();
                break;
            case "import-filter":
                var toFilter = Context.Message.ReferencedMessage.CleanContent.Split(Environment.NewLine).AsEnumerable();
                if (args[1] == "no") toFilter = toFilter.Skip(1);
                else toFilter = toFilter.Skip(2);

                var msg = await ReplyAsync(
                    $"Confirm: Import the list of words you replied to into the `{args[0]}` list? Checking reactions in 10 seconds.");
                await msg.AddReactionAsync(Emoji.Parse("✅"));
                Task.Factory.StartNew(async () =>
                {
                    await Task.Delay(Convert.ToInt32(10000));
                    var users = msg.GetReactionUsersAsync(Emoji.Parse("✅"), 2);
                    if (users.AnyAsync(users =>
                        {
                            return users.Any(user => user.Id == Context.User.Id) ? true : false;
                        }).Result)
                    {
                        await msg.RemoveAllReactionsAsync();
                        await msg.ModifyAsync(message => message.Content = "⚠  **Importing. Please wait...**");

                        _config.FilteredWords[args[1]].AddRange(toFilter);

                        await FileHelper.SaveConfigAsync(_config);
                        await msg.ModifyAsync(message => message.Content = "⚠  **Done!**");
                    }
                });

                break;
            case "raid":
                // Simulates a raid.
                // args[0] is time in seconds between joins
                // rest is user ids.
                var timePeriod = Convert.ToInt32(args[0]) * 1000;
                var users = args.Skip(1).Select(user =>
                {
                    if (ulong.TryParse(user, out var id)) return Context.Guild.GetUser(id);
                    return null;
                }).Where(user =>
                {
                    if (user == null) return false;
                    return true;
                });

                var raidMsg = await ReplyAsync(
                    $"Confirm: Simulate {users.Count()} users joining {timePeriod} milliseconds apart? Checking reactions in 10 seconds.");
                await raidMsg.AddReactionAsync(Emoji.Parse("✅"));
                Task.Factory.StartNew(async () =>
                {
                    await Task.Delay(Convert.ToInt32(10000));
                    var raidMsgUsers = raidMsg.GetReactionUsersAsync(Emoji.Parse("✅"), 2);
                    if (raidMsgUsers.AnyAsync(raidMsgUsersActual =>
                        {
                            return raidMsgUsersActual.Any(user => user.Id == Context.User.Id) ? true : false;
                        }).Result)
                    {
                        await raidMsg.RemoveAllReactionsAsync();
                        await raidMsg.ModifyAsync(message => message.Content = "⚠  **Executing...**");

                        Task.Factory.StartNew(async () =>
                        {
                            foreach (var user in users)
                            {
                                await Task.Delay(timePeriod);
                                await _raidService.ProcessMemberJoin(user);
                            }
                        });

                        await raidMsg.ModifyAsync(message =>
                            message.Content = "⚠  **Executed. Expect raid alarms if hit.**");
                    }
                    else
                    {
                        await raidMsg.RemoveAllReactionsAsync();
                        await raidMsg.ModifyAsync(message => message.Content = "⚠  **Cancelled.**");
                    }
                });
                break;
            case "state":
                _state.CurrentSmallJoinCount++;
                await ReplyAsync($"At {_state.CurrentSmallJoinCount}.");
                break;
            case "kicklog":
                await _modLoggingService.CreateActionLog(Context.Guild)
                    .SetActionType(LogType.Kick)
                    .AddTarget(Context.User as SocketGuildUser)
                    .SetTime(DateTimeOffset.Now)
                    .SetReason(
                        "This is a test of the kick log type.")
                    .Send();
                break;
            case "multiuser":
                await _modLoggingService.CreateActionLog(Context.Guild)
                    .SetActionType(LogType.Notice)
                    .AddTarget(Context.User as SocketGuildUser)
                    .AddTarget(Context.User as SocketGuildUser)
                    .AddTarget(Context.User as SocketGuildUser)
                    .AddTarget(Context.User as SocketGuildUser)
                    .AddTarget(Context.User as SocketGuildUser)
                    .SetTime(DateTimeOffset.Now)
                    .SetReason(
                        "This is a test of multiple users in 1 log.")
                    .Send();
                break;
            case "asyncSyncTesk":
                Console.WriteLine("Application executing on thread {0}",
                    Thread.CurrentThread.ManagedThreadId);
                var asyncTask = Task.Run( () => {  Console.WriteLine("Task {0} (asyncTask) executing on Thread {1}",
                        Task.CurrentId,
                        Thread.CurrentThread.ManagedThreadId);
                    long sum = 0;
                    for (int ctr = 1; ctr <= 1000000; ctr++ )
                        sum += ctr;
                    return sum;
                });
                var syncTask = new Task<long>( () =>  { Console.WriteLine("Task {0} (syncTask) executing on Thread {1}",
                        Task.CurrentId,
                        Thread.CurrentThread.ManagedThreadId);
                    long sum = 0;
                    for (int ctr = 1; ctr <= 1000000; ctr++ )
                        sum += ctr;
                    return sum;
                });
                syncTask.RunSynchronously();
                Console.WriteLine();
                Console.WriteLine("Task {0} returned {1:N0}", syncTask.Id, syncTask.Result);
                Console.WriteLine("Task {0} returned {1:N0}", asyncTask.Id, asyncTask.Result);
                break;
            case "overloadFilter":
                var message = Context.Message as SocketMessage;
                for (var i = 0; i < 10; i++)
                {
                    Task.Run(async () => await _filterService.ProcessMessage(message, Context.Client));
                }
                break;
            case "logTest":
                var pressureTracer = new Dictionary<string, double>{ {"Base", _config.SpamBasePressure} };
                await _loggingService.Log($"Pressure increase by 0 to 0/{_config.SpamMaxPressure}.{Environment.NewLine}                          Pressure trace: {string.Join(", ", pressureTracer)}", Context, level: LogLevel.Debug);
                break;
            case "invitesDisabled":
                await ReplyAsync("Invites disabled: " + Context.Guild.Features.HasFeature("INVITES_DISABLED"));
                break;
            case "timehelper":
                var timestring = string.Join(" ", args);
                try
                {
                    var time = TimeHelper.Convert(timestring);

                    await ReplyAsync($"Successfully converted {timestring} to a DateTimeOffset.{Environment.NewLine}" +
                                     $"DateTime: <t:{time.Time.ToUnixTimeSeconds()}:F> (<t:{time.Time.ToUnixTimeSeconds()}:R>){Environment.NewLine}" +
                                     $"Repeats: {(time.Repeats ? "yes" : "no")}{Environment.NewLine}" +
                                     $"Repeats every: {time.RepeatType ?? "Doesn't repeat"}");
                }
                catch (FormatException exception)
                {
                    await ReplyAsync($"Exception: {exception.Message}");
                }
                break;
            case "repeat-scheduled":
                var timeinput = string.Join(" ", args);
                try
                {
                    var time = TimeHelper.Convert(timeinput);

                    var repeatType = time.RepeatType switch
                    {
                        "relative" => ScheduledTaskRepeatType.Relative,
                        "daily" => ScheduledTaskRepeatType.Daily,
                        "weekly" => ScheduledTaskRepeatType.Weekly,
                        "yearly" => ScheduledTaskRepeatType.Yearly,
                        _ => ScheduledTaskRepeatType.None
                    };

                    var repeataction = _scheduleService.stringToAction(
                        $"echo in {Context.Channel.Id} content Hello! I'm a repeating task occuring `{timeinput}`!");
                    var repeattask = new ScheduledTask(DateTimeOffset.UtcNow,
                        time.Time, repeataction, repeatType);
                    await _scheduleService.CreateScheduledTask(repeattask, Context.Guild);
                    await Context.Message.ReplyAsync("Created repeating scheduled task.");
                    break;
                }
                catch (FormatException exception)
                {
                    await ReplyAsync($"Exception: {exception.Message}");
                }
                break;
            default:
                Context.Message.ReplyAsync("Unknown test.");
                break;
        }
    }

    [Summary("Submodule for viewing and modifying the realtime state of Izzy Moonbot")]
    public class StateSubmodule : ModuleBase<SocketCommandContext>
    {
        private State _state;

        public StateSubmodule(State state)
        {
            _state = state;
        }

        [Command("state")]
        [Summary("State values")]
        public async Task StateCommandAsync([Summary("State name")] string stateKey = "")
        {
            if (stateKey == "")
            {
                var stateKeys = typeof(State).GetProperties().Select(info => info.Name);
                await ReplyAsync(
                    $"Please provide a state to view the value of (`.state <state>`):{Environment.NewLine}```{Environment.NewLine}" +
                    string.Join(", ", stateKeys) +
                    $"{Environment.NewLine}```");
            }
        }

        public static bool DoesStateExist<T>(string key) where T : State
        {
            var t = typeof(T);

            if (t.GetProperty(key) == null) return false;
            return true;
        }
    }
    
    [Summary("Submodule for viewing and modifying the internal scheduler of Izzy Moonbot")]
    public class SchedulerSubmodule : ModuleBase<SocketCommandContext>
    {
        private readonly Config _config;
        private readonly ScheduleService _schedule;

        public SchedulerSubmodule(List<ScheduledTask> scheduledTasks, Config config, ScheduleService schedule)
        {
            _config = config;
            _schedule = schedule;
        }

        [Command("schedule")]
        [Summary("Manage schedule")]
        [DevCommand]
        public async Task ScheduleCommandAsync([Summary("Action")] string action = "", [Summary("[...]")][Remainder] string argsString = "")
        {
            // TODO: Reprogram this command to be much more user-friendly. [REQUIRES: TimeHelper]
            if (action == "")
            {
                await ReplyAsync($"Invalid usage, please refer to proper usage below:{Environment.NewLine}" +
                                 $"`{_config.Prefix}schedule info` - List general information regarding scheduled tasks.{Environment.NewLine}" +
                                 $"`{_config.Prefix}schedule list` - List all scheduled tasks.{Environment.NewLine}" +
                                 $"`{_config.Prefix}schedule get <id>` - Get scheduled task by ID.{Environment.NewLine}" +
                                 $"`{_config.Prefix}schedule modify <id> <schedule task string>` - Modify scheduled task to new data.{Environment.NewLine}" +
                                 $"`{_config.Prefix}schedule reschedule <id> <timestamp>` - Change next execution time.{Environment.NewLine}" +
                                 $"`{_config.Prefix}schedule repeat <id> <true/false>` - Change whether this task repeats or not.{Environment.NewLine}" +
                                 $"`{_config.Prefix}schedule delete <id>` - Delete scheduled task.{Environment.NewLine}" +
                                 $"`{_config.Prefix}schedule create <timestamp> <action string>` - Create scheduled task.{Environment.NewLine}{Environment.NewLine}"+
                                 $"*Please note that IDs are not persistent and will change as scheduled tasks are processed.*");
            } else if (action.ToLower() == "info")
            {
                var removeRoles = _schedule.GetScheduledTasks().Where(task => task.Action.Type == ScheduledTaskActionType.RemoveRole);
                var addRoles = _schedule.GetScheduledTasks().Where(task => task.Action.Type == ScheduledTaskActionType.AddRole);
                var echo = _schedule.GetScheduledTasks().Where(task => task.Action.Type == ScheduledTaskActionType.Echo);
                var unban = _schedule.GetScheduledTasks().Where(task => task.Action.Type == ScheduledTaskActionType.Unban);

                await ReplyAsync(
                    $"There are {_schedule.GetScheduledTasks().Count} scheduled tasks awaiting execution, of which:{Environment.NewLine}" +
                    $"{addRoles.Count()} are adding roles,{Environment.NewLine}" +
                    $"{removeRoles.Count()} are removing roles,{Environment.NewLine}" +
                    $"{echo.Count()} are echoing messages, and{Environment.NewLine}" +
                    $"{unban.Count()} are unbanning a user.");
            } else if (action.ToLower() == "list")
            {
                var list = _schedule.GetScheduledTasks().Select((task, i) => $"{i}: ``{_schedule.actionToString(task.Action)}`` at <t:{task.ExecuteAt.ToUnixTimeSeconds()}:F>");

                await ReplyAsync(
                    $"List of scheduled tasks awaiting execution:{Environment.NewLine}{string.Join(Environment.NewLine, list)}");
            } else if (action.ToLower() == "get")
            {
                if (!int.TryParse(argsString, out int scheduleId))
                {
                    await ReplyAsync(
                        $"I was unable to process the provided id into an integer. Please provide an integer.");
                    return;
                }

                if (_schedule.GetScheduledTasks().Count <= scheduleId)
                {
                    await ReplyAsync("ID not found.");
                    return;
                }

                var scheduledTask = _schedule.GetScheduledTasks()[scheduleId];

                await ReplyAsync($"Information about schedule task id {scheduleId}{Environment.NewLine}" +
                                 $"Created at: <t:{scheduledTask.CreatedAt.ToUnixTimeSeconds()}:F>{Environment.NewLine}" +
                                 $"Executes at: <t:{scheduledTask.ExecuteAt.ToUnixTimeSeconds()}:F>{Environment.NewLine}" +
                                 $"Action: ``{_schedule.actionToString(scheduledTask.Action)}``");
            } else if (action.ToLower() == "modify")
            {
                var args = argsString.Split(" ");

                if (args.Length < 2)
                {
                    await ReplyAsync($"Invalid usage, please refer to proper usage below:{Environment.NewLine}" +
                                     $"`{_config.Prefix}schedule modify <id> <schedule task string>` where...{Environment.NewLine}" +
                                     $"`<id>` is the id of the scheduled task to edit, and{Environment.NewLine}" +
                                     $"`<schedule task string>` is a scheduled action in string form.");
                    return;
                }
                
                if (!int.TryParse(args[0], out int scheduleId))
                {
                    await ReplyAsync(
                        $"I was unable to process the provided id into an integer. Please provide an integer.");
                    return;
                }

                if (_schedule.GetScheduledTasks().Count <= scheduleId)
                {
                    await ReplyAsync("ID not found.");
                    return;
                }

                var scheduledTask = _schedule.GetScheduledTasks()[scheduleId];

                try
                {
                    var scheduledAction = _schedule.stringToAction(string.Join(" ", args.Skip(1)));

                    var newScheduledTask = _schedule.GetScheduledTasks()[scheduleId];
                    newScheduledTask.Action = scheduledAction;

                    await _schedule.ModifyScheduledTask(scheduledTask, newScheduledTask);
                    await ReplyAsync("Operation complete.");
                }
                catch (FormatException)
                {
                    await ReplyAsync("That scheduled action string was malformed or invalid. Please try again.");
                }
            } else if (action.ToLower() == "reschedule")
            {
                var args = argsString.Split(" ");

                if (args.Length < 2)
                {
                    await ReplyAsync($"Invalid usage, please refer to proper usage below:{Environment.NewLine}" +
                                     $"`{_config.Prefix}schedule reschedule <id> <timestamp>` where...{Environment.NewLine}" +
                                     $"`<id>` is the id of the scheduled task to reschedule, and{Environment.NewLine}" +
                                     $"`<timestamp>` is a timestamp of when it should execute **in seconds**.");
                    return;
                }
                
                if (!int.TryParse(args[0], out int scheduleId))
                {
                    await ReplyAsync(
                        $"I was unable to process the provided id into an integer. Please provide an integer.");
                    return;
                }

                if (_schedule.GetScheduledTasks().Count <= scheduleId)
                {
                    await ReplyAsync("ID not found.");
                    return;
                }
                
                if (!long.TryParse(args[1], out long timestamp))
                {
                    await ReplyAsync(
                        $"I was unable to process the provided timestamp into a datetime. Please provide a valid timestamp.");
                    return;
                }

                var scheduledTask = _schedule.GetScheduledTasks()[scheduleId];

                var scheduledExecute = DateTimeOffset.FromUnixTimeSeconds(timestamp);

                var newScheduledTask = _schedule.GetScheduledTasks()[scheduleId];
                newScheduledTask.ExecuteAt = scheduledExecute;

                await _schedule.ModifyScheduledTask(scheduledTask, newScheduledTask);
                await ReplyAsync("Operation complete.");
            } else if (action.ToLower() == "delete")
            {
                var args = argsString.Split(" ");

                if (args.Length < 1)
                {
                    await ReplyAsync($"Invalid usage, please refer to proper usage below:{Environment.NewLine}" +
                                     $"`{_config.Prefix}schedule delete <id>` where...{Environment.NewLine}" +
                                     $"`<id>` is the id of the scheduled task to delete");
                    return;
                }
                
                if (!int.TryParse(args[0], out int scheduleId))
                {
                    await ReplyAsync(
                        $"I was unable to process the provided id into an integer. Please provide an integer.");
                    return;
                }

                if (_schedule.GetScheduledTasks().Count <= scheduleId)
                {
                    await ReplyAsync("ID not found.");
                    return;
                }

                var scheduledTask = _schedule.GetScheduledTasks()[scheduleId];

                await _schedule.DeleteScheduledTask(scheduledTask);
                
                await ReplyAsync("Operation complete.");
            } else if (action.ToLower() == "create")
            {
                var args = argsString.Split(" ");

                if (args.Length < 2)
                {
                    await ReplyAsync($"Invalid usage, please refer to proper usage below:{Environment.NewLine}" +
                                     $"`{_config.Prefix}schedule create <timestamp> <schedule task string>` where...{Environment.NewLine}" +
                                     $"`<timestamp>` is the timestamp when this task should execute **in seconds**, and{Environment.NewLine}" +
                                     $"`<schedule task string>` is a scheduled action in string form.");
                    return;
                }
                
                if (!long.TryParse(args[0], out long timestamp))
                {
                    await ReplyAsync(
                        $"I was unable to process the provided timestamp into a datetime. Please provide a valid timestamp.");
                    return;
                }

                try
                {
                    var scheduledAction = _schedule.stringToAction(string.Join(" ", args.Skip(1)));
                    var scheduledExecute = DateTimeOffset.FromUnixTimeSeconds(timestamp);
                    
                    var scheduledTask = new ScheduledTask(DateTimeOffset.UtcNow, scheduledExecute, scheduledAction);

                    await _schedule.CreateScheduledTask(scheduledTask, Context.Guild);
                    await ReplyAsync("Operation complete.");
                }
                catch (FormatException)
                {
                    await ReplyAsync("That scheduled action string was malformed or invalid. Please try again.");
                }
            }
        }

        public static bool DoesStateExist<T>(string key) where T : State
        {
            var t = typeof(T);

            if (t.GetProperty(key) == null) return false;
            return true;
        }
    }
}