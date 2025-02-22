﻿using System;
using System.Collections.Generic;
using System.Text;
using Decoherence.CommandLineSerialization.Attributes;

namespace Decoherence.CommandLineSerialization;

public static class ImplUtil
{
    public static ISpec GenerateSpecByAttribute(
        SpecAttribute attr, 
        Type objType, 
        string defaultOptionName,
        Func<bool, ValueType> GetDefaultValueType,
        IValueSerializer? defaultValueSerializer)
    {
        ThrowUtil.ThrowIfArgumentNullOrWhiteSpace(defaultOptionName);
            
        if (attr is ArgumentAttribute argumentAttr)
        {
            return new Argument(argumentAttr.ValueType != ValueType.Default ? argumentAttr.ValueType : GetDefaultValueType(false), 
                objType, 
                argumentAttr.ValueName,
                argumentAttr.Desc,
                argumentAttr.Priority, argumentAttr.Serializer ?? defaultValueSerializer);
        }

        var optionAttr = (OptionAttribute)attr;
        char? shortName = optionAttr.ShortName != null ? optionAttr.ShortName[0] : (defaultOptionName.Length == 1 ? defaultOptionName[0] : null);
        string? longName = optionAttr.LongName ?? (shortName == null && defaultOptionName.Length > 1 ? defaultOptionName : null);
            
        return new Option(
            shortName,
            longName,
            optionAttr.ValueType != ValueType.Default ? optionAttr.ValueType : GetDefaultValueType(true), 
            objType, 
            optionAttr.ValueName,
            optionAttr.Desc,
            optionAttr.Serializer ?? defaultValueSerializer);
    }

    public static string MergeCommandLine(IEnumerable<string> argList)
    {
        StringBuilder sb = new("");
        foreach (var arg in argList)
        {
            if (arg.Contains(" ") || arg.Contains("\t"))
            {
                sb.Append('"');
                sb.Append(arg.Replace("\"", "\\\""));
                sb.Append('"');
            }
            else
            {
                sb.Append(arg);
            }

            sb.Append(' ');
        }

        if (sb.Length > 0)
        {
            sb.Remove(sb.Length - 1, 1);
        }

        return sb.ToString();
    }

    public static LinkedList<string> SplitCommandLine(string? commandLine)
    {
        LinkedList<string> argList = new();
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return argList;
        }

        string state;
        bool unescapeAny;
        HashSet<char> unescapeChs = new();
        Dictionary<char, string> ch2SpecialCh = new();

        void enterNormalState()
        {
            state = "normal";
                
            ch2SpecialCh.Clear();
            ch2SpecialCh['\"'] = "\"";
            ch2SpecialCh['\''] = "'";
            ch2SpecialCh[' '] = " ";
            ch2SpecialCh['\t'] = "\t";
                
            unescapeAny = true;
        }
            
        void enterDoubleQuote()
        {
            state = "double_quote";
                
            ch2SpecialCh.Clear();
            ch2SpecialCh['\"'] = "\"";
                
            unescapeAny = false;
            unescapeChs.Clear();
            unescapeChs.Add('\\');
            unescapeChs.Add('"');
        }
            
        void enterSingleQuote()
        {
            state = "single_quote";
                
            ch2SpecialCh.Clear();
            ch2SpecialCh['\''] = "'";
                
            unescapeAny = false;
            unescapeChs.Clear();
            unescapeChs.Add('\\');
        }

        enterNormalState();
        StringBuilder sb = new();
        bool lastSplitCh = true;

        for (var i = 0; i < commandLine.Length; ++i)
        {
            string? specialCh = null;
            var ch = commandLine[i];
            if (ch == '\\' && (unescapeAny || unescapeChs.Count > 0))
            {
                if (i == commandLine.Length - 1)
                {
                    specialCh = "null";
                }
                else if (unescapeAny || unescapeChs.Contains(commandLine[i + 1]))
                {
                    ch = commandLine[++i];
                }
            }
            else
            {
                ch2SpecialCh.TryGetValue(ch, out specialCh);
            }

            if (specialCh == "null")
                continue;
                
            if (specialCh == "\"")
            {
                if (state == "double_quote")
                {
                    enterNormalState();
                }
                else
                {
                    enterDoubleQuote();
                }
            }
            else if (specialCh == "'")
            {
                if (state == "single_quote")
                {
                    enterNormalState();
                }
                else
                {
                    enterSingleQuote();
                }
            }
            else if (!lastSplitCh && (specialCh is " " or "\t"))
            {
                argList.AddLast(sb.ToString());
                sb.Clear();
            }
            else if (specialCh == null)
            {
                sb.Append(ch);
            }

            lastSplitCh = specialCh is " " or "\t";
        }

        if (state is "double_quote" or "single_quote")
        {
            throw new InvalidOperationException($"Split '{commandLine}' error: lack of '\"'.");
        }

        if (!lastSplitCh)
        {
            argList.AddLast(sb.ToString());
        }

        return argList;
    }

    public static ValueType GetDefaultValueType(bool isOption, Type objType)
    {
        if (isOption)
        {
            return objType == typeof(bool) ? ValueType.Non : ValueType.Single;
        }
        else
        {
            return ValueType.Single;
        }
    }
}