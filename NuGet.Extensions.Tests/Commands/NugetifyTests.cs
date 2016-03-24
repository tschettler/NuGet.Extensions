using System.IO;
using Moq;
using NuGet.Extensions.Commands;
using NuGet.Extensions.Tests.MSBuild;
using NuGet.Extensions.Tests.TestData;
using NUnit.Framework;

namespace NuGet.Extensions.Tests.Commands
{
    using System;
    using System.Collections.Generic;

    public class NugetifyTests
    {
        private DirectoryInfo _solutionDir;
        private string _solutionFile;
        private DirectoryInfo _packageSource;

        [SetUp]
        public void SetupIsolatedSolutionAndUnrelatedPackages()
        {
            _solutionDir = Isolation.GetIsolatedTestSolutionDir();
            _solutionFile = Path.Combine(_solutionDir.FullName, Paths.AdapterTestsSolutionFile.Name);
            _packageSource = Isolation.GetIsolatedEmptyPackageSource();
        }

        [TearDown]
        public void DeleteIsolatedSolutionAndPackagesFolder()
        {
            _packageSource.Delete(true);
            _solutionDir.Delete(true);
        }

        [Test]
        public void NugetifyThrowsNoErrorsWhenNoPackagesFound()
        {
            var console = new ConsoleMock();

            var nugetify = GetNugetifyCommand(console, _solutionFile, _packageSource);
            nugetify.ExecuteCommand();

            console.AssertConsoleHasNoErrorsOrWarnings();
        }

        [Test]
        public void NugetifyThrowsErrorsWhenSolutionNotFound()
        {
            var console = new ConsoleMock();

            var nugetify = GetNugetifyCommand(console, "non-existent-solution.sln", _packageSource);
            nugetify.ExecuteCommand();

            console.AssertConsoleHasErrors();
        }

        [Test]
        [Ignore]
        public void TestWithProjectReferences()
        {
            var console = new ConsoleMock();

            var sln = @"C:\Path\To\My\Solution";
            var source = new DirectoryInfo(@"\\custom\repository\path");
            var nugetify = GetNugetifyCommandWithRepositories(console, sln, source);
            nugetify.Source.Add(source.FullName);
            nugetify.Source.Add("https://www.nuget.org/api/v2/");
            nugetify.ExecuteCommand();

            console.AssertConsoleHasNoErrorsOrWarnings();
        }


        private static Nugetify GetNugetifyCommand(ConsoleMock console, string solutionFile, DirectoryInfo packageSource)
        {
            var repositoryFactory = new Mock<IPackageRepositoryFactory>();
            var packageSourceProvider = new Mock<IPackageSourceProvider>();
            var nugetify = new NugetifyCommandRunner(repositoryFactory.Object, packageSourceProvider.Object)
                           {
                               Console = console.Object,
                               MsBuildProperties = "Configuration=Releasable,SomeOtherParameter=false",
                               NuSpec = true,
                           };
            nugetify.Arguments.AddRange(new[] { solutionFile, packageSource.FullName });
            return nugetify;
        }

        private static Nugetify GetNugetifyCommandWithRepositories(ConsoleMock console, string solutionFile, DirectoryInfo packageSource)
        {
            var repositoryFactory = new Mock<IPackageRepositoryFactory>();
            repositoryFactory.Setup(f => f.CreateRepository(@"\\custom\repository\path")).Returns(new LocalPackageRepository(@"\\custom\repository\path"));
            repositoryFactory.Setup(f => f.CreateRepository("https://www.nuget.org/api/v2/")).Returns(new DataServicePackageRepository(new Uri("https://www.nuget.org/api/v2/")));

            var packageSourceProvider = new Mock<IPackageSourceProvider>();
            packageSourceProvider.Setup(p => p.LoadPackageSources())
                .Returns(new List<PackageSource>()
                             {
                                 new PackageSource(@"\\custom\repository\path", "My NuGet Packages", true),
                                 new PackageSource("https://www.nuget.org/api/v2/", "nuget.org", true)
                             });

            packageSourceProvider.Setup(p => p.IsPackageSourceEnabled(It.Is<PackageSource>(s => s.IsEnabled))).Returns(true);

            var nugetify = new NugetifyCommandRunner(repositoryFactory.Object, packageSourceProvider.Object)
            {
                Console = console.Object,
                MsBuildProperties = "Configuration=Releasable,SomeOtherParameter=false",
                NuSpec = true,
            };
            nugetify.Arguments.AddRange(new[] { solutionFile, packageSource.FullName });
            return nugetify;
        }
    }

    internal class NugetifyCommandRunner : Nugetify
    {
        public NugetifyCommandRunner(IPackageRepositoryFactory factory, IPackageSourceProvider provider)
        {
            RepositoryFactory = factory;
            SourceProvider = provider;
        }
    }
}
