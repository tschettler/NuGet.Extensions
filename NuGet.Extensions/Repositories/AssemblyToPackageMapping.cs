using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Common;

namespace NuGet.Extensions.Repositories
{
    public class AssemblyToPackageMapping 
    {
        private readonly Dictionary<string, List<IPackage>> _assemblyToPackageMapping;
        private readonly IConsole _console;
        private readonly IFileSystem _fileSystem;
        private readonly Lazy<IDictionary<string,List<IPackage>>> _resolvedMappings;
        private readonly Lazy<IList<string>> _failedMappings;

        public AssemblyToPackageMapping(IConsole console, IFileSystem fileSystem, Dictionary<string, List<IPackage>> assemblyToPackageMapping)
        {
            _console = console;
            _fileSystem = fileSystem;
            _assemblyToPackageMapping = assemblyToPackageMapping;
            _resolvedMappings = new Lazy<IDictionary<string, List<IPackage>>>(GetResolvedMappings);
            _failedMappings = new Lazy<IList<string>>(GetFailedMappings);
        }

        public IList<string> FailedMappings { get { return _failedMappings.Value; } }
        public IDictionary<string, List<IPackage>> ResolvedMappings { get { return _resolvedMappings.Value; } }


        /// <summary>
        /// Outputs a package.config file reflecting the set of packages that provides the requested set of assemblies.
        /// </summary>
        public void OutputPackageConfigFile()
        {
            var packagesConfig = Constants.PackageReferenceFile;
            //if (_fileSystem.FileExists(packagesConfig))
            //    _fileSystem.DeleteFile(packagesConfig);

            var prf = new PackageReferenceFile(_fileSystem, string.Format(".\\{0}", packagesConfig));
            foreach (var assemblyToPackageMapping in ResolvedMappings)
            {
                IPackage chosenPackage;
                if (assemblyToPackageMapping.Value.Count > 1)
                {
                    chosenPackage = assemblyToPackageMapping.Value.OrderByDescending(l => l.Version).FirstOrDefault();
                    _console.WriteLine();
                    _console.WriteLine(
                        String.Format(
                            "{0} : Choosing {1} ({2}) from {3} choices.",
                            assemblyToPackageMapping.Key,
                            chosenPackage.Id,
                            chosenPackage.Version,
                            assemblyToPackageMapping.Value.Count()));
                }
                else
                {
                    chosenPackage = assemblyToPackageMapping.Value.First();
                }

                //Only add if we do not have another instance of the ID, not the id/version combo....
                if (prf.GetPackageReferences().All(p => p.Id != chosenPackage.Id))
                {
                    prf.AddEntry(chosenPackage.Id, chosenPackage.Version);
                }
            }
        }

        private Dictionary<string, List<IPackage>> GetResolvedMappings()
        {
            return _assemblyToPackageMapping.Where(assemblyToPackageMapping => assemblyToPackageMapping.Value.Any()).ToDictionary(m => m.Key, m=> m.Value);
        }

        private IList<string> GetFailedMappings()
        {
            return _assemblyToPackageMapping.Where(assemblyToPackageMapping => !assemblyToPackageMapping.Value.Any()).Select(mapping => mapping.Key).ToList();
        }
    }
}