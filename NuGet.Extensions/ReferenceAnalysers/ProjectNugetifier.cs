using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.Extensions.Comparers;
using NuGet.Extensions.ExtensionMethods;
using NuGet.Extensions.MSBuild;
using NuGet.Extensions.Repositories;

namespace NuGet.Extensions.ReferenceAnalysers
{
    using Newtonsoft.Json;

    public class ProjectNugetifier
    {
        private readonly IConsole _console;
        private readonly IFileSystem _projectFileSystem;
        private readonly IVsProject _vsProject;
        private readonly IPackageRepository _packageRepository;
        private static readonly string PackageReferenceFilename = Constants.PackageReferenceFile;
        private readonly IHintPathGenerator _hintPathGenerator;

        private static Dictionary<string, string> referenceMap;

        static ProjectNugetifier()
        {
            var path = Environment.ExpandEnvironmentVariables("%LOCALAPPDATA%\\NuGet\\referencemap.json");
            var json = File.ReadAllText(path);
            referenceMap = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
        }

        public ProjectNugetifier(IVsProject vsProject, IPackageRepository packageRepository, IFileSystem projectFileSystem, IConsole console, IHintPathGenerator hintPathGenerator)
        {
            _console = console;
            _projectFileSystem = projectFileSystem;
            _vsProject = vsProject;
            _packageRepository = packageRepository;
            _hintPathGenerator = hintPathGenerator;
        }

        public ICollection<IPackage> NugetifyReferences(DirectoryInfo solutionDir)
        {
            var projectReferences = _vsProject.GetProjectReferences().ToList();
            var binaryReferences = _vsProject.GetBinaryReferences().ToList();

            return this.NugetifyProjectReferences(solutionDir, projectReferences.Union(binaryReferences).ToList());
        }

        public ICollection<IPackage> NugetifyProjectReferences(DirectoryInfo solutionDir, List<IReference> references)
        {
            var resolvedMappings = this.ResolveReferenceMappings(references).ToList();
            var packageReferencesAdded = new HashSet<IPackage>(new LambdaComparer<IPackage>(IPackageExtensions.Equals, IPackageExtensions.GetHashCode));

            foreach (var mapping in resolvedMappings)
            {
                var referenceMatch = references.FirstOrDefault(r => r.IsForAssembly(mapping.Key));
                if (referenceMatch != null)
                {
                    var includeName = referenceMatch.AssemblyName;
                    var includeVersion = referenceMatch.AssemblyVersion;
                    var package = mapping.Value.OrderByDescending(p => p.Version).First();
                    packageReferencesAdded.Add(package);
                    LogHintPathRewriteMessage(package, includeName, package.Version.ToString());

                    var newHintPath = _hintPathGenerator.ForAssembly(solutionDir, _vsProject.ProjectDirectory, package, mapping.Key);
                    referenceMatch.ConvertToNugetReferenceWithHintPath(newHintPath);
                }
            }

            return packageReferencesAdded;
        }

        private void LogHintPathRewriteMessage(IPackage package, string includeName, string includeVersion)
        {
            var message = string.Format(
                    "Attempting to update hintpaths for \"{0}\" {1}using package \"{2}\" version \"{3}\"",
                    includeName,
                    string.IsNullOrEmpty(includeVersion) ? string.Empty : "version \"" + includeVersion + "\" ",
                package.Id,
                package.Version);
            if (package.Id.Equals(includeName, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(includeVersion)
                    && package.Version.Version != SemanticVersion.Parse(includeVersion).Version)
                {
                    _console.WriteWarning(message);
                }
                else
                {
                    _console.WriteLine(message);
                }
            }
            else
            {
                _console.WriteWarning(message);
            }
        }

        public void AddNugetReferenceMetadata(ISharedPackageRepository sharedPackagesRepository, ICollection<IPackage> packagesToAdd)
        {
            _console.WriteLine("Checking for any project references for {0}...", PackageReferenceFilename);
            if (!packagesToAdd.Any())
            {
                return;
            }
            this.CreatePackagesConfig(packagesToAdd);
            this.RegisterPackagesConfig(sharedPackagesRepository);
        }

        private void CreatePackagesConfig(ICollection<IPackage> packagesToAdd)
        {
            this._console.WriteLine("Creating {0}", PackageReferenceFilename);
            var packagesConfig = new PackageReferenceFile(this._projectFileSystem, PackageReferenceFilename);
            foreach (var package in packagesToAdd)
            {
                if (!packagesConfig.EntryExists(package.Id, package.Version))
                {
                    packagesConfig.AddEntry(package.Id, package.Version);
                }
            }
        }

        private void RegisterPackagesConfig(ISharedPackageRepository sharedPackagesRepository)
        {
            var packagesConfigFilePath = Path.Combine(_vsProject.ProjectDirectory.FullName + "\\", PackageReferenceFilename);
            sharedPackagesRepository.RegisterRepository(packagesConfigFilePath);
            _vsProject.AddFile(PackageReferenceFilename);
        }

        //private IEnumerable<KeyValuePair<string, List<IPackage>>> ResolveReferenceMappings(IEnumerable<IReference> references)
        //{
        //    var referenceList = GetReferencedAssemblies(references);
        //    if (referenceList.Any())
        //    {
        //        _console.WriteLine("Checking feed for {0} references...", referenceList.Count);

        //        //IQueryable<IPackage> packageSource = _packageRepository.GetPackages();

        //        var ids = referenceList.Distinct().ToDictionary(r => r, this.GetPackageId);

        //        IQueryable<IPackage> packageSource = _packageRepository.FindPackages(ids.Values).Where(p => p.IsReleaseVersion() && p.IsListed()).AsQueryable();
        //        var assemblyResolver = new RepositoryAssemblyResolver(packageSource, _projectFileSystem, _console);
        //        var referenceMappings = assemblyResolver.GetAssemblyToPackageMapping(ids, false);
        //        referenceMappings.OutputPackageConfigFile();

        //        // next, lets rewrite the project file with the mappings to the new location...
        //        // Going to have to use the mapping to assembly name that we get back from the resolve above
        //        _console.WriteLine();
        //        _console.WriteLine("Found {0} package to assembly mappings on feed...", referenceMappings.ResolvedMappings.Count());
        //        referenceMappings.FailedMappings.ToList().ForEach(f => _console.WriteLine("Could not match: {0}", f));
        //        return referenceMappings.ResolvedMappings;
        //    }

        //    _console.WriteLine("No references found to resolve (all GAC?)");
        //    return Enumerable.Empty<KeyValuePair<string, List<IPackage>>>();
        //}

        private IEnumerable<KeyValuePair<string, List<IPackage>>> ResolveReferenceMappings(IEnumerable<IReference> references)
        {
            var resolveReferenceMappings = Enumerable.Empty<KeyValuePair<string, List<IPackage>>>();
            
            var existingPackageReferences = this.GetExistingPackageReferences();

            // filter out the references that are already nuget package references
            var referenceList = references.Where(r => r.CanConvert()).ToList();
            var existingPackagesMapping = this.GetExistingPackagesMapping(existingPackageReferences, referenceList);

            referenceList = referenceList.Where(r => existingPackagesMapping.All(m => m.Key != r.DllName)).ToList();

            if (referenceList.Any())
            {
                _console.WriteLine("Checking feed for {0} references...", referenceList.Count);

                //IQueryable<IPackage> packageSource = _packageRepository.GetPackages();

                var ids = referenceList.Distinct().ToDictionary(r => r, this.GetPackageId);

                IQueryable<IPackage> packageSource =
                    _packageRepository.FindPackages(ids.Values)
                        .Where(p => p.IsReleaseVersion() && p.IsListed())
                        .AsQueryable();
                var assemblyResolver = new RepositoryAssemblyResolver(packageSource, _projectFileSystem, _console);
                var referenceMappings = assemblyResolver.GetAssemblyToPackageMapping(ids, false);
                referenceMappings.OutputPackageConfigFile();

                // next, lets rewrite the project file with the mappings to the new location...
                // Going to have to use the mapping to assembly name that we get back from the resolve above
                _console.WriteLine();
                _console.WriteLine(
                    "Found {0} package to assembly mappings on feed...",
                    referenceMappings.ResolvedMappings.Count());
                referenceMappings.FailedMappings.ToList().ForEach(f => _console.WriteLine("Could not match: {0}", f));
                resolveReferenceMappings = referenceMappings.ResolvedMappings;
            }
            else
            {
                _console.WriteLine("No references found to resolve");               
            }

            existingPackagesMapping.AddRange(resolveReferenceMappings);

            return existingPackagesMapping;
        }

        private Dictionary<string, List<IPackage>> GetExistingPackagesMapping(IEnumerable<PackageReference> existingPackageReferences, List<IReference> referenceList)
        {
            var existingPackagesMapping = new Dictionary<string, List<IPackage>>();
            foreach (var pr in existingPackageReferences)
            {
                var reference = referenceList.FirstOrDefault(r => this.GetPackageId(r) == pr.Id && r.GetSafeVersion().Satisfies(pr.Version));
                if (reference == null)
                {
                    continue;
                }

                var package = _packageRepository.FindPackage(pr.Id, pr.Version);

                if (package == null)
                {
                    continue;
                }

                existingPackagesMapping[reference.DllName] = new List<IPackage>() { package };
            }

            return existingPackagesMapping;
        }

        private IEnumerable<PackageReference> GetExistingPackageReferences()
        {
            var packagesConfig = Constants.PackageReferenceFile;

            //Path.Combine(_vsProject.ProjectDirectory.FullName + "\\", PackageReferenceFilename);
            var prf = new PackageReferenceFile(_projectFileSystem, string.Format(".\\{0}", packagesConfig));

            return prf.GetPackageReferences();
        } 

        private string GetPackageId(IReference reference)
        {
            var id = Path.GetFileNameWithoutExtension(reference.DllName);
            if (referenceMap.ContainsKey(id))
            {
                id = referenceMap[id];
            }

            return id;
        } 

        private static Dictionary<string, string> GetReferenceMap()
        {
            var path = Environment.ExpandEnvironmentVariables("%LOCALAPPDATA%\\NuGet\\referencemap.json");
            var json = File.ReadAllText(path);
            var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

            return dict;
        } 

        private static List<string> GetReferencedAssemblies(IEnumerable<IReference> references)
        {
            var referenceFiles = new List<string>();

            foreach (var reference in references)
            {
                string hintPath;
                if (reference is ProjectReferenceAdapter)
                {
                    referenceFiles.Add((reference as ProjectReferenceAdapter).AssemblyName + ".dll");
                }
                else if (reference.TryGetHintPath(out hintPath))
                {
                    referenceFiles.Add(Path.GetFileName(hintPath));
                }
                else if (reference.CanConvert())
                {
                    referenceFiles.Add(reference.AssemblyName + ".dll");
                }
            }

            return referenceFiles;
        }

        public ICollection<ManifestDependency> GetManifestDependencies(ICollection<IPackage> packagesAdded)
        {
            var referencedProjectAssemblyNames = _vsProject.GetProjectReferences().Select(prf => prf.AssemblyName);
            var assemblyNames = new HashSet<string>(referencedProjectAssemblyNames);
            assemblyNames.AddRange(packagesAdded.Select(brf => brf.Id));
            return assemblyNames.Select(name => new ManifestDependency{Id = name}).ToList();
        }
    }
}