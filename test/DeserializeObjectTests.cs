﻿using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Decoherence.CommandLineSerialization.Test
{
    public class DeserializeObjectTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void TestFields()
        {
            var obj = _Deserialize<TestingClass1>("--FieldA 1 --FieldB 2 3", out var remainArgs);
            
            Assert.True(obj.FieldA == 1 && obj.FieldB == 2);
            Assert.True(string.Join(' ', remainArgs) == "3");
        }
        
        [Test]
        public void TestConstructorFields()
        {
            var obj = _Deserialize<TestingClass2>("1 --FieldB 2 3", out var remainArgs);
            
            Assert.True(obj.FieldA == 1 && obj.FieldB == 2);
            Assert.True(string.Join(' ', remainArgs) == "3");
        }
        
        [Test]
        public void TestArgumentFields()
        {
            var obj = _Deserialize<TestingClass3>("--FieldC 3 2 1 3", out var remainArgs);
            
            Assert.True(obj.FieldA == 1 && obj.FieldB == 2 && obj.FieldC == 3);
            Assert.True(string.Join(' ', remainArgs) == "3");
        }

        [Test]
        public void TestComplexList()
        {
            var obj = _Deserialize<List<TestingClass1>>("-- \"--FieldA 1 --FieldB 1\" \"--FieldA 2 --FieldB 2\"", out var remainArgs);
            
            Assert.True(obj[0].FieldA == 1 && obj[0].FieldB == 1 && obj[1].FieldA == 2 && obj[1].FieldB == 2);
            Assert.True(string.Join(' ', remainArgs) == "");
        }
        
        [Test]
        public void TestLimitEnumFail()
        {
            Assert.Catch<InvalidOperationException>(() => _Deserialize<TestingClass7>("--FieldA AAA", out _));
        }
        
        [Test]
        public void TestLimitEnumSuccess()
        {
            var obj = _Deserialize<TestingClass7>("--FieldA Two", out var remainArgs);
            
            Assert.True(obj.FieldA == TestingEnum.Two);
            Assert.True(string.Join(' ', remainArgs) == "");
        }

        private T _Deserialize<T>(string commandLine, out LinkedList<string> remainArgs)
        {
            CommandLineSerializer serializer = new();
            return (T)serializer.DeserializeObject(typeof(T), ImplUtil.SplitCommandLine(commandLine), out remainArgs)!;
        }
    }
}