
using Discord;
using Izzy_Moonbot.Adapters;
using Izzy_Moonbot.Describers;
using Izzy_Moonbot.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Izzy_Moonbot_Tests;

// This file is for any test-related code that we want all the
// other test files to share. Usually test double factories.

public static class TestUtils
{
    public static DateTimeOffset FiMEpoch = new DateTimeOffset(2010, 10, 10, 0, 0, 0, TimeSpan.Zero);

    public static (Config, ConfigDescriber, (StubGuildUser, StubGuildUser), List<TestRole>, (StubChannel, StubChannel, StubChannel), StubGuild, StubClient) DefaultStubs()
    {
        var izzyHerself = new StubGuildUser("Izzy Moonbot", 1);
        var sunny = new StubGuildUser("Sunny", 2);
        var zipp = new StubGuildUser("Zipp", 3);
        var pipp = new StubGuildUser("Pipp", 4);
        var hitch = new StubGuildUser("Hitch", 5);
        var users = new List<StubGuildUser> { izzyHerself, sunny, zipp, pipp, hitch };

        var alicorn = new TestRole("Alicorn", 1);
        var roles = new List<TestRole> { alicorn, new TestRole("Pegasus", 2) };

        // because user ids are also Direct Message channel ids, regular channels must have different ids
        var generalChannel = new StubChannel(1001, "general");
        var modChat = new StubChannel(1002, "modchat");
        var logChat = new StubChannel(1003, "botlogs");
        var channels = new List<StubChannel> { generalChannel, modChat, logChat };

        var guild = new StubGuild(1, "Maretime Bay", roles, users, channels);
        guild.UserRoles.Add(sunny.Id, new List<ulong> { alicorn.Id });
        guild.UserRoles.Add(izzyHerself.Id, new List<ulong> { alicorn.Id }); // bots are honorary alicorns
        guild.ChannelAccessRole.Add(modChat.Id, alicorn.Id);

        var client = new StubClient(izzyHerself, new List<StubGuild> { guild });

        var cfg = new Config();
        var cd = new ConfigDescriber();

        return (cfg, cd, (izzyHerself, sunny), roles, (generalChannel, modChat, logChat), guild, client);
    }

    // The built-in Assert.AreEqual and CollectionsAssert.AreEqual have error messages so bad it was worth writing my own asserts
    public static void AssertListsAreEqual<T>(IList<T>? expected, IList<T>? actual, string message = "")
    {
        if (expected is null || actual is null)
        {
            Assert.AreEqual(expected, actual);
            return;
        }
        if (expected.Count() != actual.Count())
            Assert.AreEqual(expected, actual, $"\nCount() mismatch: {expected.Count()} != {actual.Count()}");
        foreach (var i in Enumerable.Range(0, expected.Count()))
            Assert.AreEqual(expected[i], actual[i], $"\nItem {i}" + message);
    }

    public static void AssertSetsAreEqual<T>(ISet<T>? expected, ISet<T>? actual, string message = "")
    {
        if (expected is null || actual is null)
        {
            Assert.AreEqual(expected, actual);
            return;
        }
        if (expected.Count() != actual.Count())
            Assert.AreEqual(expected, actual, $"\nCount() mismatch: {expected.Count()} != {actual.Count()}");
        foreach (var value in expected)
            Assert.IsTrue(actual.Contains(value), $"\nValue {value}" + message);
    }

    // The built-in Assert.AreEqual and CollectionsAssert.AreEqual don't even work on Dictionaries, so everyone has to write their own
    public static void AssertDictionariesAreEqual<K, V>(IDictionary<K, V>? expected, IDictionary<K, V>? actual, string message = "")
    {
        if (expected is null || actual is null)
        {
            Assert.AreEqual(expected, actual);
            return;
        }
        AssertListsAreEqual(
            expected.OrderBy(kv => kv.Key).ToList(),
            actual.OrderBy(kv => kv.Key).ToList()
        );
    }

    // even my AssertDictionariesAreEqual helper falls apart on Set values
    public static void AssertDictsOfSetsAreEqual<K, V>(IDictionary<K, HashSet<V>>? expected, IDictionary<K, HashSet<V>>? actual, string message = "")
    {
        if (expected is null || actual is null)
        {
            Assert.AreEqual(expected, actual);
            return;
        }
        if (expected.Count() != actual.Count())
            Assert.AreEqual(expected, actual, $"\nCount() mismatch: {expected.Count()} != {actual.Count()}");
        foreach (var kv in expected)
        {
            AssertSetsAreEqual(expected[kv.Key], actual[kv.Key], $"\nKey {kv.Key}" + message);
        }
    }

    public static void AssertEmbedFieldsAre(IList<EmbedField> actual, IList<(string, string)> expected)
    {
        if (expected.Count() != actual.Count())
            Assert.IsTrue(false, $"\nCount() mismatch: {expected.Count()} != {actual.Count()}");

        foreach (var ((name, value), embedField) in expected.Zip(actual, Tuple.Create))
        {
            Assert.AreEqual(name, embedField.Name);
            Assert.AreEqual(value, embedField.Value);
        }
    }
}
