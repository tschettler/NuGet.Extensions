﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NuGet.Extensions.ReferenceAnalysers;
using NUnit.Framework;
using NuGet.Extensions.FeedAudit;

namespace NuGet.Extensions.Tests.FeedAudit
{
    [TestFixture]
    public class GacResolverTests
    {
        [TestCase("System", Result = true, Description = "Can resolve System")]
        [TestCase("Giberishshsidfasdfasdfasdf.asdfasdfas.dasdfasdf", Result = false, Description = "Cant resolve gibberish")]
        [TestCase("System.Xml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", Result = true, Description = "Using full name")]
        //TODO Fix the mismatch between assertion and test name - also note the below test fails when run after the IPackageManagerExtensionsTests in the same appdomain
        [TestCase("System.Xml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=MSIL", Result = true, Description = "With a null PublicKeyToken, we return false rather than throw exception")]
        public bool CanResolveSystem(string assemblyName)
        {
            string test;
            return GacResolver.AssemblyExist(assemblyName, out test);
        }
    }
}
