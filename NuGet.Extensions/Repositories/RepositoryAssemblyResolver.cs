using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using NuGet.Common;

namespace NuGet.Extensions.Repositories
{
    using global::NuGet.Extensions.ExtensionMethods;
    using global::NuGet.Extensions.MSBuild;

    /// <summary>
    /// Provides the ability to search across IQueryable package sources for a set of packages that contain a particular assembly or set of assemblies.
    /// </summary>
    public class RepositoryAssemblyResolver
    {
        readonly IQueryable<IPackage> _packageSource;
        private readonly IFileSystem _fileSystem;
        private readonly IConsole _console;
        private Dictionary<string, List<IPackage>> _resolvedAssemblies;

        /// <summary>
        /// Initializes a new instance of the <see cref="RepositoryAssemblyResolver"/> class.
        /// </summary>
        /// <param name="assemblies">The assemblies to look for.</param>
        /// <param name="packageSource">The package sources to search.</param>
        /// <param name="fileSystem">The file system to output any packages.config files.</param>
        /// <param name="console">The console to output to.</param>
        public RepositoryAssemblyResolver(IQueryable<IPackage> packageSource, IFileSystem fileSystem, IConsole console)
        {
            _packageSource = packageSource;
            _fileSystem = fileSystem;
            _console = console;
        }

        /// <summary>
        /// Resolves a list of packages that contain the assemblies requested.
        /// </summary>
        /// <param name="exhaustive">if set to <c>true</c> [exhaustive].</param>
        /// <returns></returns>
        public AssemblyToPackageMapping GetAssemblyToPackageMapping(List<string> assemblies, bool exhaustive)
        {
            var assemblySet = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var assembly in assemblies.Where(assembly => !assemblySet.Add(assembly)))
            {
                _console.WriteWarning("Same assembly resolution will be used for both assembly references to {0}", assembly);
            }

            _resolvedAssemblies = assemblySet.ToDictionary(a => a, _ => new List<IPackage>()); 
            
            int max = _packageSource.Count();

            var filenamePackagePairs = this.GetFilenamePackagePairs().ToList();

            _console.WriteLine("Searching through {0} packages", max);
            foreach (var assembly in assemblySet)
            {
                _console.WriteLine("Checking packages for {0}", assembly);
                var packages = filenamePackagePairs.Where(f => f.Key == assembly).Select(p => p.Value);

                if (packages.IsEmpty())
                {
                    continue;
                }

                if (exhaustive)
                {
                    _resolvedAssemblies[assembly].AddRange(packages);
                }
                else
                {
                    _resolvedAssemblies[assembly].Add(packages.OrderByDescending(p => p.Version).FirstOrDefault());
                }
            }

            return new AssemblyToPackageMapping(_console, _fileSystem, _resolvedAssemblies);
        }

        /// <summary>
        /// Resolves a dictionary of packages that contain the assemblies requested.
        /// </summary>
        /// <param name="exhaustive">if set to <c>true</c> [exhaustive].</param>
        /// <returns></returns>
        public AssemblyToPackageMapping GetAssemblyToPackageMapping(Dictionary<IReference, string> assemblies, bool exhaustive)
        {
            _resolvedAssemblies = assemblies.ToDictionary(a => a.Key.DllName, _ => new List<IPackage>());

            int max = _packageSource.Count();

            _console.WriteLine("Searching through {0} packages", max);
            foreach (var assembly in assemblies)
            {
                _console.WriteLine("Checking packages for {0}", assembly.Key.DllName);
                var packages = _packageSource.Where(p => p.Id == assembly.Value && assembly.Key.GetSafeVersion().Satisfies(p.Version));

                if (packages.IsEmpty())
                {
                    continue;
                }

                if (exhaustive)
                {
                    _resolvedAssemblies[assembly.Key.DllName].AddRange(packages);
                }
                else
                {
                    _resolvedAssemblies[assembly.Key.DllName].Add(packages.OrderByDescending(p => p.Version).FirstOrDefault());
                }
            }

            return new AssemblyToPackageMapping(_console, _fileSystem, _resolvedAssemblies);
        }

        private void ConsoleOverwrite(string formatMessage, params object[] paramObjects)
        {
            _console.Write("\r" + formatMessage, paramObjects);
        }

        private IEnumerable<KeyValuePair<string, IPackage>> GetFilenamePackagePairs()
        {
            foreach (var package in _packageSource)
            {
                var files = package.GetFiles();
                foreach (var file in files) yield return new KeyValuePair<string, IPackage>(new FileInfo(file.Path).Name, package);
            }
        }
    }
}
