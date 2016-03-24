using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using Microsoft.Build.Evaluation;

using NuGet.Commands;
using NuGet.Common;
using NuGet.Extensions.MSBuild;
using NuGet.Extensions.Nuspec;
using NuGet.Extensions.ReferenceAnalysers;

namespace NuGet.Extensions.Commands
{
    [Command("nugetify", "Given a solution, csproj, or directory, attempts to replace all file references with package references, adding all required" +
                         " packages.config files as it goes.", MaxArgs = 1)]
    public class Nugetify : Command, INuspecDataSource
    {
        private readonly List<string> _sources = new List<string>();

        [ImportingConstructor]
        public Nugetify()
        {
        }

        [Option("A list of sources to search")]
        public ICollection<string> Source
        {
            get { return _sources; }
        }

        [Option("Comma separated list of key=value pairs of parameters to be used when loading projects, note that SolutionDir is automatically set.")]
        public string MsBuildProperties { get; set; }

        [Option("NuSpec project URL")]
        public string ProjectUrl { get; set; }

        [Option("NuSpec license URL")]
        public string LicenseUrl { get; set; }

        [Option("NuSpec icon URL")]
        public string IconUrl { get; set; }

        [Option("NuSpec tags")]
        public string Tags { get; set; }

        [Option("NuSpec release notes")]
        public string ReleaseNotes { get; set; }

        [Option("NuSpec description")]
        public string Description { get; set; }

        [Option("NuSpec ID")]
        public string Id { get; set; }

        [Option("Create NuSpecs for solution")]
        public Boolean NuSpec { get; set; }

        [Option("NuSpec title")]
        public string Title { get; set; }

        [Option("NuSpec author")]
        public string Author { get; set; }

        [Option("NuSpec require license acceptance (defaults to false)")]
        public bool RequireLicenseAcceptance { get; set; }

        [Option(("NuSpec copyright"))]
        public string Copyright { get; set; }

        [Option("NuSpec owners")]
        public string Owners { get; set; }

        public override void ExecuteCommand()
        {
            var path = this.Arguments.FirstOrDefault();
            if (string.IsNullOrEmpty(path))
            {
                path = Directory.GetCurrentDirectory();
            }

            // if it's a directory, have to find sln or csproj file
            if (Directory.Exists(path))
            {
                var exts = new[] { "*.sln", "*.csproj" };
                var file = exts.SelectMany(e => Directory.GetFiles(path, e)).FirstOrDefault();

                if (file == null)
                {
                    this.Console.WriteError("Could not find a solution or project file in : {0}", path);
                    return;
                }

                path = file;
            }
            else if (!File.Exists(path))
            {
                this.Console.WriteError("Could not find file : {0}", path);
                return;
            }

            var projectFile = new FileInfo(path);
            switch (projectFile.Extension)
            {
                case ".sln":
                    this.NugetifySolution(projectFile);
                    break;
                case ".csproj":
                    this.NugetifyProject(projectFile);
                    break;
            }
        }

        private void NugetifySolution(FileInfo solutionFile)
        {
            Console.WriteLine("Loading projects from solution {0}", solutionFile.Name);

            var repository = this.GetRepository();

            this.RemoveNugetProjectsFromSln(solutionFile.FullName, repository);

            using (var solutionAdapter = new CachingSolutionLoader(solutionFile, this.GetMsBuildProperties(solutionFile), this.Console))
            {
                this.ProcessProjects(solutionFile, solutionAdapter.GetProjects(), repository);
            }

            Console.WriteLine("Complete!");
        }

        private void NugetifyProject(FileInfo solutionFile)
        {
            using (var projectLoader = new CachingProjectLoader(new Dictionary<string, string>(), this.Console))
            {
                var project = new Project(solutionFile.FullName);

                var projectAdapter = new ProjectAdapter(project, projectLoader);

                var repository = this.GetRepository();

                this.ProcessProjects(solutionFile, new List<IVsProject> { projectAdapter }, repository);
            }

            Console.WriteLine("Complete!");
        }

        private AggregateRepository GetRepository()
        {
            var repository = AggregateRepositoryHelper.CreateAggregateRepositoryFromSources(RepositoryFactory, SourceProvider, this.Source);
            repository.Logger = this.Console;
            return repository;
        }

        private void ProcessProjects(FileInfo solutionFile, List<IVsProject> projectAdapters, AggregateRepository repository)
        {
            var existingSolutionPackagesRepo = this.GetPackageRepository(solutionFile.Directory.FullName);
            
            Console.WriteLine("Processing {0} projects...", projectAdapters.Count);

            var primaryPackages = new List<string>();

            foreach (var projectAdapter in projectAdapters)
            {
                Console.WriteLine();
                Console.WriteLine("Processing project: {0}", projectAdapter.ProjectName);

                var newpackages = this.NugetifyProject(projectAdapter, solutionFile.Directory, existingSolutionPackagesRepo, repository);
                var primaryPackagesForProject = this.GetPrimaryPackages(newpackages);
                primaryPackages.AddRange(primaryPackagesForProject);

                Console.WriteLine("Project completed!");
            }

            Console.WriteLine();

            Console.WriteLine(ConsoleColor.DarkGreen, "Package reinstall commands:");

            // todo: update this to a more succinct list based on dependencies
            //var dependencies = packagesAdded.SelectMany(p => p.DependencySets).SelectMany(d => d.Dependencies).Select(d => d.Id);
            //var uniquePackageIds = packagesAdded.Select(p => p.Id).Distinct().Where(id => !dependencies.Contains(id));

            //var uniquePackageIds = packagesAdded.Where(p => p.DependencySets.Any()).Select(p => p.Id).Distinct();

            foreach (var package in primaryPackages.Distinct())
            {
                Console.WriteLine("update-package {0} -Reinstall", package);
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Gets only packages that are not dependencies of other packages
        /// </summary>
        /// <param name="packages">
        /// The packages.
        /// </param>
        /// <returns>
        /// The <see cref="IEnumerable"/>.
        /// </returns>
        private IEnumerable<string> GetPrimaryPackages(ICollection<IPackage> packages)
        {
            var dependencies = packages.SelectMany(p => p.DependencySets).SelectMany(d => d.Dependencies).Select(d => d.Id);

            return from package in packages where dependencies.All(d => d != package.Id) select package.Id;
        }

        private SharedPackageRepository GetPackageRepository(string slnPath)
        {
            // we remove the packages directory
            var packagesPath = Path.Combine(slnPath, "packages");

            var existingpath = this.GetClosestPath(Directory.GetParent(slnPath).FullName, "packages");

            if (existingpath != null && Directory.Exists(packagesPath))
            {
                Directory.Delete(packagesPath, true);
            }

            return new SharedPackageRepository(existingpath ?? packagesPath);
        }

        public string GetClosestPath(string currentPath, string folderName)
        {
            var folderPath = Path.Combine(currentPath, folderName);
            if (Directory.Exists(folderPath))
            {
                return folderPath;
            }

            var isRoot = Directory.GetDirectoryRoot(currentPath) == currentPath;

            return isRoot ? null : this.GetClosestPath(Directory.GetParent(currentPath).FullName, folderName);
        }

        private void RemoveNugetProjectsFromSln(string slnPath, AggregateRepository repository)
        {
            var slnSrc = File.ReadAllText(slnPath);
            var solution = new Solution(slnPath);

            Console.WriteLine("Checking for projects in solution that are NuGet packages...");
            var projectsToRemove = solution.Projects.Skip(1).Where(p => repository.FindPackagesById(p.ProjectName).Any()).ToList();

            if (projectsToRemove.IsEmpty())
            {
                return;
            }

            var regexFormat = @"^Project.*\""{0}\"".*$[\s\S]*?^EndProject";

            foreach (var project in projectsToRemove)
            {
                var regex = string.Format(regexFormat, Regex.Escape(project.ProjectName));
                slnSrc = Regex.Replace(slnSrc, regex, string.Empty, RegexOptions.Multiline);
            }

            File.WriteAllText(slnPath, slnSrc);
            Console.WriteLine("Removed {0} projects from the solution:{1}{2}", projectsToRemove.Count(), Environment.NewLine, string.Join(Environment.NewLine, projectsToRemove.Select(p => p.ProjectName)));
        }

        private ICollection<IPackage> NugetifyProject(IVsProject projectAdapter, DirectoryInfo solutionRoot, ISharedPackageRepository existingSolutionPackagesRepo, AggregateRepository repository)
        {
            var projectNugetifier = this.CreateProjectNugetifier(projectAdapter, repository);
            var packagesAdded = projectNugetifier.NugetifyReferences(solutionRoot);
            projectNugetifier.AddNugetReferenceMetadata(existingSolutionPackagesRepo, packagesAdded);
            projectAdapter.Save();

            if (!this.NuSpec)
            {
                return packagesAdded;
            }

            var manifestDependencies = projectNugetifier.GetManifestDependencies(packagesAdded);
            var nuspecBuilder = new NuspecBuilder(projectAdapter.AssemblyName);
            nuspecBuilder.SetMetadata(this, manifestDependencies);
            nuspecBuilder.SetDependencies(manifestDependencies);
            nuspecBuilder.Save(this.Console);
            return packagesAdded;
        }

        private ProjectNugetifier CreateProjectNugetifier(IVsProject projectAdapter, AggregateRepository repository)
        {
            var projectFileSystem = new PhysicalFileSystem(projectAdapter.ProjectDirectory.ToString());
            var hintPathGenerator = new HintPathGenerator()
                                        {
                                            PackagesDirectory =
                                                this.GetPackageRepository(projectFileSystem.Root)
                                                .Source
                                        };

            return new ProjectNugetifier(projectAdapter, repository, projectFileSystem, Console, hintPathGenerator);
        }

        private IDictionary<string, string> GetMsBuildProperties(FileInfo solutionFile)
        {
            var buildProperties = this.GetParsedBuildProperties();
            buildProperties["SolutionDir"] = solutionFile.Directory.FullName;
            return buildProperties;
        }

        private IDictionary<string, string> GetParsedBuildProperties()
        {
            if (this.MsBuildProperties == null)
            {
                return new Dictionary<string, string>();
            }

            var keyValuePairs = this.MsBuildProperties.Split(',');
            var twoElementArrays = keyValuePairs.Select(kvp => kvp.Split('=')).ToList();
            foreach (var errorKvp in twoElementArrays.Where(a => a.Length != 2))
            {
                throw new ArgumentException(string.Format("Key value pair near {0} is formatted incorrectly", string.Join(",", errorKvp[0])));
            }

            return twoElementArrays.ToDictionary(kvp => kvp[0].Trim(), kvp => kvp[1].Trim());
        }
    }
}