using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Izzy_Moonbot.Adapters;
using Izzy_Moonbot.Describers;
using Izzy_Moonbot.Helpers;
using Izzy_Moonbot.Settings;

namespace Izzy_Moonbot.Modules;

public class ConfigCommand
{
    public static async Task TestableConfigCommandAsync(
        IIzzyContext context,
        Config config,
        ConfigDescriber configDescriber,
        string configItemKey = "",
        string? value = "")
    {
        if (configItemKey == "")
        {
            await context.Channel.SendMessageAsync(
                $"Hii!! Here's now to use the config command!\n" +
                $"Run `{config.Prefix}config <category>` to list the config items in a category.\n" +
                $"Run `{config.Prefix}config <item>` to view information about an item.\n" +
                $"\n" +
                $"Here are all the config categories I have: `setup`, `misc`, `banner`, `managedroles`, `filter`, `spam`, `raid`, `bored`.\n" +
                $"\n" +
                $"ℹ Use `{config.Prefix}help` to learn about Izzy's other commands.");

            return;
        }

        var configItem = configDescriber.GetItem(configItemKey);

        var configCategory = configDescriber.StringToCategory(configItemKey);

        if (configItem == null)
        {
            if (configCategory == null)
            {
                // Invalid .config arguments
                var userInput = configItemKey;
                Func<string, bool> isSuggestable = item =>
                    DiscordHelper.WithinLevenshteinDistanceOf(userInput, item, Convert.ToUInt32(item.Length / 2));

                var itemsToSuggest = configDescriber.GetSettableConfigItems().Where(isSuggestable);
                var categoriesToSuggest = Enum.GetNames<ConfigItemCategory>().Select(c => c.ToLower()).Where(isSuggestable);

                var errorMessage = $"Sorry, I couldn't find a config value or category called `{configItemKey}`!";
                if (itemsToSuggest.Any() || categoriesToSuggest.Any())
                {
                    errorMessage += $"\nDid you mean {string.Join(" or ", itemsToSuggest.Concat(categoriesToSuggest).Select(s => $"`{s}`"))}?";
                }
                await context.Channel.SendMessageAsync(errorMessage);
                return;
            }
            else
            {
                // .config <category>
                var category = configCategory.Value;

                var itemNameList = configDescriber.GetSettableConfigItemsByCategory(category);

                var itemShortDescriptionsList = itemNameList.Select(itemName => {
                    var configItem = configDescriber.GetItem(itemName);
                    if (configItem == null)
                        throw new InvalidOperationException($"Failed to get configItem for key {itemName}");

                    switch (configItem.Type)
                    {
                        case ConfigItemType.String: case ConfigItemType.Char: case ConfigItemType.Boolean:
                        case ConfigItemType.Integer: case ConfigItemType.UnsignedInteger: case ConfigItemType.Double:
                        case ConfigItemType.Enum: case ConfigItemType.Role: case ConfigItemType.Channel:
                            return itemName + " = " + ConfigHelper.GetValue(config, itemName);
                        case ConfigItemType.StringSet: case ConfigItemType.RoleSet: case ConfigItemType.ChannelSet:
                        case ConfigItemType.StringDictionary: case ConfigItemType.StringSetDictionary:
                            return itemName;
                        default:
                            throw new InvalidOperationException($"I seem to have encountered a setting type that I do not know about.");
                    }
                }).ToList();

                PaginationHelper.PaginateIfNeededAndSendMessage(
                    context,
                    $"Hii!! Here's a list of all the config items I could find in the {configDescriber.CategoryToString(category)} category!",
                    itemShortDescriptionsList,
                    $"Run `{config.Prefix}config <item>` to view information about an item! Please note that config items are *case sensitive*."
                );
                return;
            }
        }
    
        // .config <itemKey>

        if (value == null || value == "")
        {
            // Only the configItemKey was given, we give the user what their data was
            await context.Channel.SendMessageAsync(
                ConfigItemDescription(config, configDescriber, configItemKey),
                allowedMentions: AllowedMentions.None);
        }
        else
        {
            // value provided
            if (configDescriber.TypeIsValue(configItem.Type))
            {
                switch (configItem.Type)
                {
                    case ConfigItemType.String:
                        if (configItem.Nullable && value == "<nothing>") value = null;
                        else value = DiscordHelper.StripQuotes(value);

                        var resultString =
                            await ConfigHelper.SetSimpleValue(config, configItem.Name, value);
                        await context.Channel.SendMessageAsync($"I've set `{configItem.Name}` to the following content: {resultString}",
                            allowedMentions: AllowedMentions.None);
                        break;
                    case ConfigItemType.Char:
                        if (configItem.Nullable && value == "<nothing>") value = null;

                        try
                        {
                            char? output = null;
                            if (value != null)
                            {
                                if (!char.TryParse(value, out var res))
                                    throw new FormatException(); // Trip "invalid content" catch below.
                                output = res;
                            }

                            var resultChar =
                                await ConfigHelper.SetSimpleValue(config, configItem.Name, output);
                            await context.Channel.SendMessageAsync($"I've set `{configItem.Name}` to the following content: {resultChar}",
                                allowedMentions: AllowedMentions.None);
                        }
                        catch (FormatException)
                        {
                            await context.Channel.SendMessageAsync(
                                $"I couldn't set `{configItem.Name}` to the content provided because you provided content that I couldn't turn into a character. Please try again.",
                                allowedMentions: AllowedMentions.None);
                        }

                        break;
                    case ConfigItemType.Boolean:
                        if (configItem.Nullable && value == "<nothing>") value = null;

                        try
                        {
                            var resultBoolean =
                                await ConfigHelper.SetBooleanValue(config, configItem.Name, value);
                            await context.Channel.SendMessageAsync($"I've set `{configItem.Name}` to the following content: {resultBoolean}",
                                allowedMentions: AllowedMentions.None);
                        }
                        catch (FormatException)
                        {
                            await context.Channel.SendMessageAsync(
                                $"I couldn't set `{configItem.Name}` to the content provided because you provided content that I couldn't turn into a boolean. Please try again.",
                                allowedMentions: AllowedMentions.None);
                        }

                        break;
                    case ConfigItemType.Integer:
                        if (configItem.Nullable && value == "<nothing>") value = null;

                        try
                        {
                            int? output = null;
                            if (value != null)
                            {
                                if (!int.TryParse(value, out var res))
                                    throw new FormatException(); // Trip "invalid content" catch below.
                                output = res;
                            }

                            var resultInteger =
                                await ConfigHelper.SetSimpleValue(config, configItem.Name, output);
                            await context.Channel.SendMessageAsync($"I've set `{configItem.Name}` to the following content: {resultInteger}",
                                allowedMentions: AllowedMentions.None);
                        }
                        catch (FormatException)
                        {
                            await context.Channel.SendMessageAsync(
                                $"I couldn't set `{configItem.Name}` to the content provided because you provided content that I couldn't turn into a integer. Please try again.",
                                allowedMentions: AllowedMentions.None);
                        }

                        break;
                    case ConfigItemType.UnsignedInteger:
                        if (configItem.Nullable && value == "<nothing>") value = null;

                        try
                        {
                            ulong? output = null;
                            if (value != null)
                            {
                                if (!ulong.TryParse(value, out var res))
                                    throw new FormatException(); // Trip "invalid content" catch below.
                                output = res;
                            }

                            var resultInteger =
                                await ConfigHelper.SetSimpleValue(config, configItem.Name, output);
                            await context.Channel.SendMessageAsync($"I've set `{configItem.Name}` to the following content: {resultInteger}",
                                allowedMentions: AllowedMentions.None);
                        }
                        catch (FormatException)
                        {
                            await context.Channel.SendMessageAsync(
                                $"I couldn't set `{configItem.Name}` to the content provided because you provided content that I couldn't turn into a integer. Please try again.",
                                allowedMentions: AllowedMentions.None);
                        }

                        break;
                    case ConfigItemType.Double:
                        if (configItem.Nullable && value == "<nothing>") value = null;

                        try
                        {
                            double? output = null;
                            if (value != null)
                            {
                                if (!double.TryParse(value, out var res))
                                    throw new FormatException(); // Trip "invalid content" catch below.
                                output = res;
                            }

                            var resultDouble =
                                await ConfigHelper.SetSimpleValue(config, configItem.Name, output);
                            await context.Channel.SendMessageAsync($"I've set `{configItem.Name}` to the following content: {resultDouble}",
                                allowedMentions: AllowedMentions.None);
                        }
                        catch (FormatException)
                        {
                            await context.Channel.SendMessageAsync(
                                $"I couldn't set `{configItem.Name}` to the content provided because you provided content that I couldn't turn into a double. Please try again.",
                                allowedMentions: AllowedMentions.None);
                        }

                        break;
                    case ConfigItemType.Enum:
                        if (configItem.Nullable && value == "<nothing>") value = null;

                        var rawValue = ConfigHelper.GetValue(config, configItem.Name);
                        var enumValue = rawValue as Enum;
                        if (enumValue == null) throw new InvalidCastException($"Config item {configItem} is supposed to be an enum, but its value {rawValue} failed the `as Enum` cast");

                        var enumType = enumValue.GetType();

                        try
                        {
                            Enum? output = null;
                            if (value != null)
                            {
                                if (!Enum.TryParse(enumType, value, true /* ignoreCase */, out var res))
                                    throw new FormatException(); // Trip "invalid content" catch below.
                                output = res as Enum;
                            }

                            var resultDouble =
                                await ConfigHelper.SetSimpleValue(config, configItem.Name, output);
                            await context.Channel.SendMessageAsync($"I've set `{configItem.Name}` to the following content: {resultDouble}",
                                allowedMentions: AllowedMentions.None);
                        }
                        catch (FormatException)
                        {
                            await context.Channel.SendMessageAsync(
                                $"I couldn't set `{configItem.Name}` to `{value}` because that's not a possible value for the {enumType.Name} enum type.\n" +
                                $"Possible values are: {String.Join(", ", enumType.GetEnumNames().Select(n => $"`{n}`"))}",
                                allowedMentions: AllowedMentions.None);
                        }
                        break;
                    case ConfigItemType.Role:
                        if (configItem.Nullable && value == "<nothing>") value = null;
                        try
                        {
                            var result =
                                await ConfigHelper.SetRoleValue(config, configItem.Name, value,
                                    context);
                            var response = "`null`";
                            if (result != null) response = $"<@&{result.Id}>";
                            await context.Channel.SendMessageAsync($"I've set `{configItem.Name}` to the following content: {response}",
                                allowedMentions: AllowedMentions.None);
                        }
                        catch (MemberAccessException)
                        {
                            await context.Channel.SendMessageAsync(
                                $"I couldn't set `{configItem.Name}` to the content provided because you provided content that I couldn't turn into a role. Please try again.",
                                allowedMentions: AllowedMentions.None);
                        }

                        break;
                    case ConfigItemType.Channel:
                        if (configItem.Nullable && value == "<nothing>") value = null;
                        try
                        {
                            var result =
                                await ConfigHelper.SetChannelValue(config, configItem.Name, value,
                                    context);
                            var response = "`null`";
                            if (result != null) response = $"<#{result.Id}>";
                            await context.Channel.SendMessageAsync($"I've set `{configItem.Name}` to the following content: {response}",
                                allowedMentions: AllowedMentions.None);
                        }
                        catch (MemberAccessException)
                        {
                            await context.Channel.SendMessageAsync(
                                $"I couldn't set `{configItem.Name}` to the content provided because you provided content that I couldn't turn into a channel. Please try again.",
                                allowedMentions: AllowedMentions.None);
                        }

                        break;
                    default:
                        await context.Channel.SendMessageAsync("I seem to have encountered a setting type that I do not know about.");
                        break;
                }
            }
            else if (configDescriber.TypeIsSet(configItem.Type))
            {
                var action = value.Split(' ')[0].ToLower();
                value = value.Replace(action + " ", "");

                if (action == "list")
                    switch (configItem.Type)
                    {
                        case ConfigItemType.StringSet:
                            var stringSet = ConfigHelper.GetStringSet(config, configItem.Name);

                            if (stringSet == null)
                            {
                                await context.Channel.SendMessageAsync("Somehow, the entire list is null.");
                                return;
                            }

                            PaginationHelper.PaginateIfNeededAndSendMessage(
                                context,
                                $"**{configItem.Name}** contains the following values:",
                                stringSet.OrderBy(x => x).ToList(),
                                "",
                                allowedMentions: AllowedMentions.None
                            );
                            break;
                        case ConfigItemType.RoleSet:
                            var roleSet = ConfigHelper.GetRoleSet(config, configItem.Name, context);

                            var roleMentionList = new List<string>();
                            foreach (var role in roleSet) roleMentionList.Add(role.Mention);

                            PaginationHelper.PaginateIfNeededAndSendMessage(
                                context,
                                $"**{configItem.Name}** contains the following values:",
                                roleMentionList,
                                "",
                                codeblock: false,
                                allowedMentions: AllowedMentions.None
                            );
                            break;
                        case ConfigItemType.ChannelSet:
                            var channelSet =
                                ConfigHelper.GetChannelSet(config, configItem.Name, context);

                            var channelMentionList = new List<string>();
                            foreach (var channel in channelSet) channelMentionList.Add($"<#{channel.Id}>");

                            PaginationHelper.PaginateIfNeededAndSendMessage(
                                context,
                                $"**{configItem.Name}** contains the following values:",
                                channelMentionList,
                                "",
                                codeblock: false,
                                allowedMentions: AllowedMentions.None
                            );
                            break;
                        default:
                            await context.Channel.SendMessageAsync("I seem to have encountered a setting type that I do not know about.");
                            break;
                    }
                else if (action == "add")
                    switch (configItem.Type)
                    {
                        case ConfigItemType.StringSet:
                            try
                            {
                                value = DiscordHelper.StripQuotes(value);
                                var output =
                                    await ConfigHelper.AddToStringSet(config, configItem.Name, value);

                                await context.Channel.SendMessageAsync(
                                    $"I added the following content to the `{configItem.Name}` string list:\n```\n{output}\n```",
                                    allowedMentions: AllowedMentions.None);
                            }
                            catch (ArgumentException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't add your content to the `{configItem.Name}` list because the `{configItem.Name}` config item isn't a list. There is likely a misconfiguration in the config item describer.");
                            }

                            break;
                        case ConfigItemType.RoleSet:
                            try
                            {
                                var output =
                                    await ConfigHelper.AddToRoleSet(config, configItem.Name, value,
                                        context);

                                await context.Channel.SendMessageAsync(
                                    $"I added the following content to the `{configItem.Name}` role list:\n{output}");
                            }
                            catch (MemberAccessException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't add the content you provided to the `{configItem.Name}` list because you provided content that I couldn't turn into a role I know about. Please try again.",
                                    allowedMentions: AllowedMentions.None);
                            }
                            catch (ArgumentOutOfRangeException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't add the role you provided to the `{configItem.Name}` list because the role is already in that list.");
                            }
                            catch (ArgumentException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't add the content you provided to the `{configItem.Name}` list because the `{configItem.Name}` config item isn't a list. There is likely a misconfiguration in the config item describer.");
                            }

                            break;
                        case ConfigItemType.ChannelSet:
                            try
                            {
                                var output =
                                    await ConfigHelper.AddToChannelSet(config, configItem.Name, value,
                                        context);

                                await context.Channel.SendMessageAsync(
                                    $"I added the following content to the `{configItem.Name}` channel list:\n{output}");
                            }
                            catch (MemberAccessException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't add the content you provided to the `{configItem.Name}` list because you provided content that I couldn't turn into a channel I know about. Please try again.",
                                    allowedMentions: AllowedMentions.None);
                            }
                            catch (ArgumentOutOfRangeException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't add the channel you provided to the `{configItem.Name}` list because the channel is already in that list.");
                            }
                            catch (ArgumentException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't add the content you provided to the `{configItem.Name}` list because the `{configItem.Name}` config item isn't a list. There is likely a misconfiguration in the config item describer.");
                            }

                            break;
                        default:
                            await context.Channel.SendMessageAsync("I seem to have encountered a setting type that I do not know about.");
                            break;
                    }
                else if (action == "remove")
                    switch (configItem.Type)
                    {
                        case ConfigItemType.StringSet:
                            try
                            {
                                value = DiscordHelper.StripQuotes(value);
                                var output =
                                    await ConfigHelper.RemoveFromStringSet(config, configItem.Name,
                                        value);

                                await context.Channel.SendMessageAsync(
                                    $"I removed the following content from the `{configItem.Name}` string list:\n```\n{output}\n```",
                                    allowedMentions: AllowedMentions.None);
                            }
                            catch (ArgumentOutOfRangeException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't remove the content you provided from the `{configItem.Name}` list because the content isn't in that list to begin with.");
                            }
                            catch (ArgumentException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't remove the content you provided from the `{configItem.Name}` list because the `{configItem.Name}` config item isn't a list. There is likely a misconfiguration in the config item describer.");
                            }

                            break;
                        case ConfigItemType.RoleSet:
                            try
                            {
                                var output =
                                    await ConfigHelper.RemoveFromRoleSet(config, configItem.Name,
                                        value, context);

                                await context.Channel.SendMessageAsync(
                                    $"I removed the following content from the `{configItem.Name}` role list:\n{output}");
                            }
                            catch (MemberAccessException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't remove the content you provided from the `{configItem.Name}` list because you provided content that I couldn't turn into a role I know about. Please try again.",
                                    allowedMentions: AllowedMentions.None);
                            }
                            catch (ArgumentOutOfRangeException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't remove the role you provided from the `{configItem.Name}` list because the role isn't in that list to begin with.");
                            }
                            catch (ArgumentException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't remove the content you provided from the `{configItem.Name}` list because the `{configItem.Name}` config item isn't a list. There is likely a misconfiguration in the config item describer.");
                            }

                            break;
                        case ConfigItemType.ChannelSet:
                            try
                            {
                                var output =
                                    await ConfigHelper.RemoveFromChannelSet(config, configItem.Name,
                                        value, context);

                                await context.Channel.SendMessageAsync(
                                    $"I removed the following content from the `{configItem.Name}` channel list:\n{output}");
                            }
                            catch (MemberAccessException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't remove the content you provided from the `{configItem.Name}` list because you provided content that I couldn't turn into a channel I know about. Please try again.",
                                    allowedMentions: AllowedMentions.None);
                            }
                            catch (ArgumentOutOfRangeException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't remove the channel you provided from the `{configItem.Name}` list because the channel isn't in that list to begin with.");
                            }
                            catch (ArgumentException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't remove the content you provided from the `{configItem.Name}` list because the `{configItem.Name}` config item isn't a list. There is likely a misconfiguration in the config item describer.");
                            }

                            break;
                        default:
                            await context.Channel.SendMessageAsync("I seem to have encountered a setting type that I do not know about.");
                            break;
                    }
                else if (action == "clear")
                    switch (configItem.Type)
                    {
                        case ConfigItemType.StringSet:
                            try
                            {
                                var output = await ConfigHelper.ClearStringSet(config, configItem.Name);

                                await context.Channel.SendMessageAsync(
                                    $"I've cleared the `{configItem.Name}` string list, so it's now empty.\n\nIt used to contain:```\n{string.Join('\n', output)}\n```",
                                    allowedMentions: AllowedMentions.None);
                            }
                            catch (ArgumentException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't clear the `{configItem.Name}` list because the `{configItem.Name}` config item isn't a list. There is likely a misconfiguration in the config item describer.");
                            }

                            break;
                        case ConfigItemType.RoleSet:
                            try
                            {
                                var output = await ConfigHelper.ClearRoleSet(config, configItem.Name);

                                await context.Channel.SendMessageAsync(
                                    $"I've cleared the `{configItem.Name}` role list, so it's now empty.\n\nIt used to contain:\n{string.Join('\n', output.Select(r => $"<@&{r}>"))}",
                                    allowedMentions: AllowedMentions.None);
                            }
                            catch (ArgumentException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't clear the `{configItem.Name}` list because the `{configItem.Name}` config item isn't a list. There is likely a misconfiguration in the config item describer.");
                            }

                            break;
                        case ConfigItemType.ChannelSet:
                            try
                            {
                                var output = await ConfigHelper.ClearChannelSet(config, configItem.Name);

                                await context.Channel.SendMessageAsync(
                                    $"I've cleared the `{configItem.Name}` channel list, so it's now empty.\n\nIt used to contain:\n{string.Join('\n', output.Select(c => $"<#{c}>"))}");
                            }
                            catch (ArgumentException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't clear the `{configItem.Name}` list because the `{configItem.Name}` config item isn't a list. There is likely a misconfiguration in the config item describer.");
                            }

                            break;
                        default:
                            await context.Channel.SendMessageAsync("I seem to have encountered a setting type that I do not know about.");
                            break;
                    }
            }
            else if (configDescriber.TypeIsDictionaryValue(configItem.Type))
            {
                var action = value.Split(' ')[0].ToLower();
                value = value.Replace(action + " ", "");

                if (action == "list")
                {
                    switch (configItem.Type)
                    {
                        case ConfigItemType.StringDictionary:
                            try
                            {
                                IEnumerable<string> items = (configItem.Nullable) ?
                                    ConfigHelper.GetDictionary<string?>(config, configItem.Name).Select(kv => $"{kv.Key} = {kv.Value}") :
                                    ConfigHelper.GetDictionary<string>(config, configItem.Name).Select(kv => $"{kv.Key} = {kv.Value}");

                                PaginationHelper.PaginateIfNeededAndSendMessage(
                                    context,
                                    $"**{configItem.Name}** contains the following keys:",
                                    items.ToList(),
                                    $""
                                );
                            }
                            catch (ArgumentOutOfRangeException ex)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't list the keys in the `{configItem.Name}` map? {ex.Message}");
                            }
                            catch (ArgumentException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't list the keys in the `{configItem.Name}` map because the `{configItem.Name}` config item isn't a map. There is likely a misconfiguration in the config item describer.");
                            }

                            break;
                        default:
                            await context.Channel.SendMessageAsync("I seem to have encountered a setting type that I do not know about.");
                            break;
                    }
                }
                else if (action == "get")
                {
                    switch (configItem.Type)
                    {
                        case ConfigItemType.StringDictionary:
                            try
                            {
                                value = DiscordHelper.StripQuotes(value);
                                var contents = "";

                                if (configItem.Nullable)
                                    contents = ConfigHelper.GetDictionaryValue<string?>(
                                        config,
                                        configItem.Name, value);
                                else
                                    contents = (string?)ConfigHelper.GetDictionaryValue<string>(
                                        config,
                                        configItem.Name, value);

                                await context.Channel.SendMessageAsync(
                                    $"**{value}** contains the following value: `{contents?.Replace("`", "\\`")}`");
                            }
                            catch (ArgumentOutOfRangeException ex)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't get the value in the `{value}` key from the `{configItem.Name}` map? {ex.Message}");
                            }
                            catch (KeyNotFoundException ex)
                            {
                                if (ex.Message.Contains(value))
                                {
                                    await context.Channel.SendMessageAsync("The key you provided does not exist within the map.");
                                }
                            }
                            catch (ArgumentException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't get the value in the `{value}` key from the `{configItem.Name}` map because the `{configItem.Name}` config item isn't a map. There is likely a misconfiguration in the config item describer.");
                            }

                            break;
                        default:
                            await context.Channel.SendMessageAsync("I seem to have encountered a setting type that I do not know about.");
                            break;
                    }
                }
                else if (action == "set")
                {
                    var (key, setValue) = DiscordHelper.GetArgument(value);
                    key = key ?? "";
                    value = setValue ?? "";
                    
                    switch (configItem.Type)
                    {
                        case ConfigItemType.StringDictionary:
                            try
                            {
                                value = DiscordHelper.StripQuotes(value);

                                (string, string?, string?) result = ("", null, null);

                                if (configItem.Nullable)
                                {
                                    if (value == "<nothing>") value = null;
                                    
                                    if (ConfigHelper.DoesDictionaryKeyExist<string?>(config,
                                            configItem.Name, key))
                                        result = await ConfigHelper.SetNullableStringDictionaryValue(config,
                                            configItem.Name, key, value);
                                    else
                                        result = await ConfigHelper.CreateDictionaryKey<string?>(config,
                                            configItem.Name, key, value);
                                }
                                else
                                {
                                    if (ConfigHelper.DoesDictionaryKeyExist<string>(config,
                                            configItem.Name, key))
                                        result = await ConfigHelper.SetStringDictionaryValue(config,
                                            configItem.Name, key, value);
                                    else
                                        result = await ConfigHelper.CreateDictionaryKey<string>(config,
                                            configItem.Name, key, value);
                                }

                                if (result.Item2 == null)
                                {
                                    await context.Channel.SendMessageAsync(
                                        $"I added the following string to the `{result.Item1}` map key in the `{configItem.Name}` map: `{result.Item3?.Replace("`", "\\`")}`");
                                }
                                else
                                {
                                    await context.Channel.SendMessageAsync(
                                        $"I changed the string in the `{result.Item1}` map key in the `{configItem.Name}` map from `{result.Item2.Replace("`", "\\`")}` to `{result.Item3?.Replace("`", "\\`")}`");
                                }
                            }
                            catch (ArgumentOutOfRangeException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't create the string you wanted in the `{configItem.Name}` map because the `{key}` key already exists.");
                            }
                            catch (ArgumentException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't create the string you wanted in the `{configItem.Name}` map because the `{configItem.Name}` config item isn't a map. There is likely a misconfiguration in the config item describer.");
                            }

                            break;
                        default:
                            await context.Channel.SendMessageAsync("I seem to have encountered a setting type that I do not know about.");
                            break;
                    }
                }
                else if (action == "delete" || action == "remove")
                {
                    switch (configItem.Type)
                    {
                        case ConfigItemType.StringDictionary:
                            try
                            {
                                value = DiscordHelper.StripQuotes(value);
                                if (configItem.Nullable)
                                    await ConfigHelper.RemoveDictionaryKey<string?>(config,
                                        configItem.Name, value);
                                else
                                    await ConfigHelper.RemoveDictionaryKey<string>(config,
                                        configItem.Name, value);

                                await context.Channel.SendMessageAsync(
                                    $"I removed the string with the following key from the `{configItem.Name}` map: `{value}`");
                            }
                            catch (ArgumentOutOfRangeException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't remove the string you wanted from the `{configItem.Name}` map because the `{value}` key already doesn't exist.");
                            }
                            catch (ArgumentException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't remove the string you wanted from the `{configItem.Name}` map because the `{configItem.Name}` config item isn't a map. There is likely a misconfiguration in the config item describer.");
                            }

                            break;
                        default:
                            await context.Channel.SendMessageAsync("I seem to have encountered a setting type that I do not know about.");
                            break;
                    }
                }
                else if (action == "clear")
                {
                    switch (configItem.Type)
                    {
                        case ConfigItemType.StringDictionary:
                            try
                            {
                                var output = await ConfigHelper.ClearDictionary<string>(config, configItem.Name);

                                await context.Channel.SendMessageAsync(
                                    $"I've cleared the `{configItem.Name}` string map, so it's now empty.\n\nIt used to contain:```\n{string.Join('\n', output)}\n```",
                                    allowedMentions: AllowedMentions.None);
                            }
                            catch (ArgumentException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't clear the `{configItem.Name}` map because the `{configItem.Name}` config item isn't a map. There is likely a misconfiguration in the config item describer.");
                            }

                            break;
                        case ConfigItemType.StringSetDictionary:
                            try
                            {
                                var output = await ConfigHelper.ClearDictionary<HashSet<string>>(config, configItem.Name);

                                await context.Channel.SendMessageAsync(
                                    $"I've cleared the `{configItem.Name}` map, so it's now empty.\n\nIt used to contain:\n{string.Join('\n', output.Select(r => $"<@&{r}>"))}",
                                    allowedMentions: AllowedMentions.None);
                            }
                            catch (ArgumentException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't clear the `{configItem.Name}` map because the `{configItem.Name}` config item isn't a map. There is likely a misconfiguration in the config item describer.");
                            }

                            break;
                        default:
                            await context.Channel.SendMessageAsync("I seem to have encountered a setting type that I do not know about.");
                            break;
                    }
                }
                else
                {
                    await context.Channel.SendMessageAsync(
                        "The action you wanted to take isn't supported for this type of config item, the available actions are `list`, `get`, `set`, `delete` and `clear`.");
                    return;
                }
            }
            else if (configDescriber.TypeIsDictionarySet(configItem.Type))
            {
                var action = value.Split(' ')[0].ToLower();
                value = value.Replace(action + " ", "");

                if (action == "list")
                {
                    switch (configItem.Type)
                    {
                        case ConfigItemType.StringSetDictionary:
                            try
                            {
                                var dict = ConfigHelper.GetDictionary<HashSet<string>>(config, configItem.Name);

                                PaginationHelper.PaginateIfNeededAndSendMessage(
                                    context,
                                    $"**{configItem.Name}** contains the following keys:",
                                    dict.Select(kv => $"{kv.Key} ({kv.Value.Count} entries)\n").ToList(),
                                    $""
                                );
                            }
                            catch (ArgumentOutOfRangeException ex)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't list the keys in the `{configItem.Name}` map? {ex.Message}");
                            }
                            catch (ArgumentException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't list the keys in the `{configItem.Name}` map because the `{configItem.Name}` config item isn't a map. There is likely a misconfiguration in the config item describer.");
                            }

                            break;
                        default:
                            await context.Channel.SendMessageAsync("I seem to have encountered a setting type that I do not know about.");
                            break;
                    }
                }
                else if (action == "get")
                {
                    if (value == "")
                    {
                        await context.Channel.SendMessageAsync("Please provide a key to get.");
                        return;
                    }
                    
                    switch (configItem.Type)
                    {
                        case ConfigItemType.StringSetDictionary:
                            try
                            {
                                value = DiscordHelper.StripQuotes(value);
                                var stringSet =
                                    ConfigHelper.GetDictionaryValue<HashSet<string>>(config, configItem.Name,
                                        value);

                                PaginationHelper.PaginateIfNeededAndSendMessage(
                                    context,
                                    $"**{value}** contains the following values:",
                                    stringSet.OrderBy(x => x).ToList(),
                                    $"",
                                    allowedMentions: AllowedMentions.None
                                );
                            }
                            catch (KeyNotFoundException ex)
                            {
                                if (ex.Message.Contains(value))
                                {
                                    await context.Channel.SendMessageAsync("The key you provided does not exist within the map.");
                                }
                            }

                            break;
                        // Other types, I don't see any reason to add them until I need them.
                        default:
                            await context.Channel.SendMessageAsync("I seem to have encountered a setting type that I do not know about.");
                            break;
                    }
                }
                else if (action == "add")
                {
                    var key = value.Split(' ')[0].ToLower();
                    value = value.Replace(key + " ", "");
                    
                    switch (configItem.Type)
                    {
                        case ConfigItemType.StringSetDictionary:
                            try
                            {
                                key = DiscordHelper.StripQuotes(key);
                                value = DiscordHelper.StripQuotes(value);
                                var output =
                                    ConfigHelper.DoesDictionaryKeyExist<HashSet<string>>(config, configItem.Name, key)
                                    ? await ConfigHelper.AddToStringSetDictionaryValue(config,
                                        configItem.Name, key, value)
                                    : await ConfigHelper.CreateStringSetDictionaryKey(config, configItem.Name, key, value);

                                await context.Channel.SendMessageAsync(
                                    $"I added the following string to the `{output.Item1}` string list in the `{configItem.Name}` map: `{output.Item2}`",
                                    allowedMentions: AllowedMentions.None);
                            }
                            catch (ArgumentException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't add your content to the `{key}` string list in the `{configItem.Name}` map because the `{configItem.Name}` config item isn't a map. There is likely a misconfiguration in the config item describer.");
                            }

                            break;
                        // Other types, I don't see any reason to add them until I need them.
                        default:
                            await context.Channel.SendMessageAsync("I seem to have encountered a setting type that I do not know about.");
                            break;
                    }                    
                }
                else if (action == "deleteitem")
                {
                    var key = value.Split(' ')[0].ToLower();
                    value = value.Replace(key + " ", "");
                    switch (configItem.Type)
                    {
                        case ConfigItemType.StringSetDictionary:
                            try
                            {
                                key = DiscordHelper.StripQuotes(key);
                                value = DiscordHelper.StripQuotes(value);
                                var output =
                                    await ConfigHelper.RemoveFromStringSetDictionaryValue(
                                        config, configItem.Name, key, value);

                                await context.Channel.SendMessageAsync(
                                    $"I removed the following content from the `{output.Item1}` string list in the `{configItem.Name}` map: `{output.Item2}`",
                                    allowedMentions: AllowedMentions.None);
                            }
                            catch (ArgumentException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't remove your content from the `{key}` string list in the `{configItem.Name}` map because the `{configItem.Name}` config item isn't a map. There is likely a misconfiguration in the config item describer.");
                            }

                            break;
                        // Other types, I don't see any reason to add them until I need them.
                        default:
                            await context.Channel.SendMessageAsync("I seem to have encountered a setting type that I do not know about.");
                            break;
                    }
                }
                else if (action == "deletelist")
                {
                    switch (configItem.Type)
                    {
                        case ConfigItemType.StringSetDictionary:
                            try
                            {
                                value = DiscordHelper.StripQuotes(value);
                                await ConfigHelper.RemoveDictionaryKey<HashSet<string>>(config,
                                    configItem.Name, value);

                                await context.Channel.SendMessageAsync(
                                    $"I deleted the string list with the following key from the `{configItem.Name}` map: {value}");
                            }
                            catch (ArgumentOutOfRangeException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't delete the string list you wanted from the `{configItem.Name}` map because the `{value}` key already doesn't exist.");
                            }
                            catch (ArgumentException)
                            {
                                await context.Channel.SendMessageAsync(
                                    $"I couldn't delete the string list you wanted from the `{configItem.Name}` map because the `{configItem.Name}` config item isn't a map. There is likely a misconfiguration in the config item describer.");
                            }

                            break;
                        // Other types, I don't see any reason to add them until I need them.
                        default:
                            await context.Channel.SendMessageAsync("I seem to have encountered a setting type that I do not know about.");
                            break;
                    }
                }
                else
                {
                    
                }
            }
            else
            {
                await context.Channel.SendMessageAsync($"I couldn't determine what type {configItem.Type} is.");
            }
        }
    }

    public static string ConfigItemDescription(Config config, ConfigDescriber configDescriber, string configItemKey)
    {
        var configItem = configDescriber.GetItem(configItemKey);
        if (configItem == null)
            throw new InvalidOperationException($"Failed to get configItem for key {configItemKey}");

        var nullableString = " (Pass `<nothing>` as the value when setting to set to nothing/null)";
        if (!configItem.Nullable) nullableString = "";

        switch (configItem.Type)
        {
            case ConfigItemType.String:
            case ConfigItemType.Char:
            case ConfigItemType.Boolean:
            case ConfigItemType.Integer:
            case ConfigItemType.UnsignedInteger:
            case ConfigItemType.Double:
                return $"**{configItem.Name}** - {configDescriber.TypeToString(configItem.Type)} - {configDescriber.CategoryToString(configItem.Category)} category\n" +
                       $"*{configItem.Description}*\n" +
                       $"Current value: `{ConfigHelper.GetValue(config, configItem.Name)}`\n" +
                       $"Run `{config.Prefix}config {configItem.Name} <value>` to set this value.{nullableString}";
            case ConfigItemType.Enum:
                // Figure out what its values are.
                var rawValue = ConfigHelper.GetValue(config, configItem.Name);
                var enumValue = rawValue as Enum;
                if (enumValue == null) throw new InvalidCastException($"Config item {configItem} is supposed to be an enum, but its value {rawValue} failed the `as Enum` cast");

                var enumType = enumValue.GetType();
                var possibleEnumNames = enumType.GetEnumNames().Select(s => $"`{s}`").ToArray();

                return $"**{configItem.Name}** - {configDescriber.TypeToString(configItem.Type)} - {configDescriber.CategoryToString(configItem.Category)} category\n" +
                       $"*{configItem.Description}*\n" +
                       $"Possible values are: {string.Join(", ", possibleEnumNames)}\n" +
                       $"Current value: `{enumType.GetEnumName(enumValue)}`\n" +
                       $"Run `{config.Prefix}config {configItem.Name} <value>` to set this value.{nullableString}";
            case ConfigItemType.Role:
                return
                    $"**{configItem.Name}** - {configDescriber.TypeToString(configItem.Type)} - {configDescriber.CategoryToString(configItem.Category)} category\n" +
                    $"*{configItem.Description}*\n" +
                    $"Current value: <@&{ConfigHelper.GetValue(config, configItem.Name)}>\n" +
                    $"Run `{config.Prefix}config {configItem.Name} <value>` to set this value.{nullableString}";
            case ConfigItemType.Channel:
                return $"**{configItem.Name}** - {configDescriber.TypeToString(configItem.Type)} - {configDescriber.CategoryToString(configItem.Category)} category\n" +
                       $"*{configItem.Description}*\n" +
                       $"Current value: <#{ConfigHelper.GetValue(config, configItem.Name)}>\n" +
                       $"Run `{config.Prefix}config {configItem.Name} <value>` to set this value.{nullableString}";
            case ConfigItemType.StringSet:
            case ConfigItemType.RoleSet:
            case ConfigItemType.ChannelSet:
                return $"**{configItem.Name}** - {configDescriber.TypeToString(configItem.Type)} - {configDescriber.CategoryToString(configItem.Category)} category\n" +
                       $"*{configItem.Description}*\n" +
                       $"Run `{config.Prefix}config {configItem.Name} list` to view the contents of this list.\n" +
                       $"Run `{config.Prefix}config {configItem.Name} add <value>` to add a value to this list.{nullableString}\n" +
                       $"Run `{config.Prefix}config {configItem.Name} remove <value>` to remove a value from this list.{nullableString}\n" +
                       $"Run `{config.Prefix}config {configItem.Name} clear` to clear this list of all values.{nullableString}";
            case ConfigItemType.StringDictionary:
                return $"**{configItem.Name}** - {configDescriber.TypeToString(configItem.Type)} - {configDescriber.CategoryToString(configItem.Category)} category\n" +
                       $"*{configItem.Description}*\n" +
                       $"Run `{config.Prefix}config {configItem.Name} list` to view a list of keys in this map.\n" +
                       $"Run `{config.Prefix}config {configItem.Name} get <key>` to get the current value of a key in this map.\n" +
                       $"Run `{config.Prefix}config {configItem.Name} set <key> <value>` to set a key to a value in this map, creating the key if need be.\n" +
                       $"Run `{config.Prefix}config {configItem.Name} delete <key>` to delete a key from this map.\n" +
                       $"Run `{config.Prefix}config {configItem.Name} clear` to clear this map of all values.";
            case ConfigItemType.StringSetDictionary:
                return $"**{configItem.Name}** - {configDescriber.TypeToString(configItem.Type)} - {configDescriber.CategoryToString(configItem.Category)} category\n" +
                       $"*{configItem.Description}*\n" +
                       $"Run `{config.Prefix}config {configItem.Name} list` to view a list of keys in this map.\n" +
                       $"Run `{config.Prefix}config {configItem.Name} get <key>` to get the values of a key in this map.\n" +
                       $"Run `{config.Prefix}config {configItem.Name} add <key> <value>` to add a value to a key in this map, creating the key if need be.\n" +
                       $"Run `{config.Prefix}config {configItem.Name} deleteitem <key> <value>` to remove a value from a key from this map.\n" +
                       $"Run `{config.Prefix}config {configItem.Name} deletelist <key>` to delete a key from this map.\n" +
                       $"Run `{config.Prefix}config {configItem.Name} clear` to clear this map of all values.";
            default:
                return "I seem to have encountered a setting type that I do not know about.";
        }
    }
}
