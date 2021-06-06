﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Decoherence.CommandLineParsing
{
    public class CommandLineSerializer
    {
        public string LongOptionPrefix => "--";
        
        public string ShortOptionPrefix => "-";
        
        public string EndOfOptionArg => "--";
        
        private readonly SerializationStrategy mSerializationStrategy;

        public CommandLineSerializer(SerializationStrategy? serializationStrategy = null)
        {
            mSerializationStrategy = serializationStrategy ?? new SerializationStrategy();
        }

        public Values DeserializeValues(IEnumerable<string> args, Specs specs)
        {
            return DeserializeValues(args, specs, out _);
        }
        
        public Values DeserializeValues(IEnumerable<string> args, Specs specs, out LinkedList<string> remainArgs)
        {
            if (args == null)
                throw new ArgumentNullException(nameof(args));
            if (specs == null)
                throw new ArgumentNullException(nameof(specs));

            var values = new Values();
            remainArgs = new LinkedList<string>(args);
            List<string> valueArgHelpList = new();
            LinkedListNode<string>? endOfOptionNode = null;

            // --- 先解析Option ---
            List<Option> options = new(specs.Options);
            
            // 按switch、scalar、sequence排序，这样可以保证在处理ShortName时先消耗掉Switch，这样合法的Scalar和Sequence就在最后了
            options.Sort((a, b) => (int)a.Type - (int)b.Type);
            
            // 逐个解析每个option
            foreach (var option in options)
            {
                IEnumerable<string>? matchedValueArgs = null;
                object? value = null;

                if (remainArgs.First == null || !_TryMatchOption(option, remainArgs.First, ref endOfOptionNode, out var optionMatch))
                {
                    value = _HandleNoArg(option);
                }
                else
                {
                    if (option.Type == OptionType.Switch)
                    {
                        optionMatch!.ConsumeArgs(remainArgs);
                    }
                    else if (option.Type == OptionType.Scalar)
                    {
                        valueArgHelpList.Clear();
                        matchedValueArgs = valueArgHelpList;

                        if (optionMatch!.ValueArg == null)
                        {
                            throw new InvalidArgsException($"Lacking value of scalar option '{optionMatch.OptionNameWithPrefix}'");
                        }
                        
                        valueArgHelpList.Add(optionMatch!.ValueArg);
                        optionMatch!.ConsumeArgs(remainArgs);
                    }
                    else if (option.Type == OptionType.Sequence)
                    {
                        valueArgHelpList.Clear();
                        matchedValueArgs = valueArgHelpList;

                        while (optionMatch != null)
                        {
                            if (optionMatch!.ValueArg != null)
                            {
                                valueArgHelpList.Add(optionMatch!.ValueArg);
                            }
                            var nextNode = optionMatch!.ConsumeArgs();

                            optionMatch = null;
                            while (nextNode != null && !_TryMatchOption(option, nextNode, ref endOfOptionNode, out optionMatch))
                            {
                                if (nextNode.Value == EndOfOptionArg)
                                {
                                    endOfOptionNode = nextNode;
                                    break;
                                }
                                
                                valueArgHelpList.Add(nextNode.Value);
                                nextNode = nextNode.Next;
                            }
                        }
                    }

                    value = _DeserializeValue(option.ValueType, matchedValueArgs);
                }

                values.AddValue(option, value);
            }

            // 如果有option结束符，则从结束符下一个开始解析argument
            endOfOptionNode ??= remainArgs.Find(EndOfOptionArg);
            var firstArgNode = endOfOptionNode ?? remainArgs.First; 

            // 再逐个解析argument
            foreach (var argument in specs.Arguments)
            {
                object? value;

                if (firstArgNode == null)
                {
                    value = _HandleNoArg(argument);
                }
                else
                {
                    valueArgHelpList.Clear();
                    
                    if (argument.Type == ArgumentType.Scalar)
                    {
                        valueArgHelpList.Add(firstArgNode.Value);

                        // 消耗arg
                        var tmpNode = firstArgNode.Next;
                        remainArgs.Remove(firstArgNode);
                        firstArgNode = tmpNode;
                    }
                    else if (argument.Type == ArgumentType.Sequence)
                    {
                        var argNode = firstArgNode;
                        while (argNode != null)
                        {
                            valueArgHelpList.Add(argNode.Value);
                            
                            // 消耗arg
                            var tmpNode = argNode.Next;
                            remainArgs.Remove(argNode);
                            argNode = tmpNode;
                        }
                    }
                    
                    value = _DeserializeValue(argument.ValueType, valueArgHelpList);
                }
                
                values.AddValue(argument, value);
            }

            return values;
        }

        public string SerializeValue(Type valueType, object? value)
        {
            throw new NotImplementedException();
        }

        private object? _DeserializeValue(Type valueType, IEnumerable<string>? args)
        {
            var func = mSerializationStrategy.GetDeserializeFunc(valueType);
            return func(this, valueType, args);
        }

        private bool _TryMatchOption(Option option, LinkedListNode<string> argNode, ref LinkedListNode<string>? endOfOptionNode, out OptionMatch? optionMatch)
        {
            optionMatch = null;
            
            // LongName比ShortName更具体，所以先匹配LongName
            // 格式：
            //  --arg 100
            //  --arg=100
            if (option.LongName != null)
            {
                for (var node = argNode; node != null; node = node.Next)
                {
                    if (node.Value == EndOfOptionArg)
                    {
                        endOfOptionNode = node;
                        break;
                    }

                    var optionArg = $"{LongOptionPrefix}{option.LongName}";
                    var match = Regex.Match(node.Value, $"{optionArg}(=.*)?");
                    if (match.Success)
                    {
                        optionMatch = new OptionMatch(node, LongOptionPrefix, match, option.LongName);
                        return true;
                    }
                }
            }

            // 格式：
            //  -a 100
            //  -a100
            if (option.ShortName != null)
            {
                for (var node = argNode; node != null; node = node.Next)
                {
                    if (node.Value == EndOfOptionArg)
                    {
                        endOfOptionNode = node;
                        break;
                    }
                    
                    var match = Regex.Match(node.Value, $"{ShortOptionPrefix}.*{option.ShortName.Value}(.*)?");
                    if (match.Success)
                    {
                        optionMatch = new OptionMatch(node, LongOptionPrefix, match, option.ShortName.Value);
                        return true;
                    }
                }
            }

            return false;
        }

        private object? _HandleNoArg(Spec spec)
        {
            throw new NotImplementedException();
        }
    }
}