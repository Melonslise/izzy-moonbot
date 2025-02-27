using System;

namespace Izzy_Moonbot.Types;

public class ConfigValueChangeEvent : EventArgs
{
    public string Name;
    public object? Original;
    public object? Current;

    public ConfigValueChangeEvent(string name, object? original, object? current)
    {
        Name = name;
        Original = original;
        Current = current;
    }

    public override string ToString()
    {
        return $"{Name}: {Original ?? "NULL"} => {Current ?? "NULL"}";
    }
}