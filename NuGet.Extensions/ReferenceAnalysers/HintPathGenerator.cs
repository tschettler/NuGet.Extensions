using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;

namespace NuGet.Extensions.ReferenceAnalysers
{
    public class HintPathGenerator : IHintPathGenerator
    {
        public HintPathGenerator()
        {}

        public string PackagesDirectory { get; set; }

        public string ForAssembly(DirectoryInfo solutionDir, DirectoryInfo projectDir, IPackage package, string assemblyFilename)
        {
            var fileLocation = GetFileLocationFromPackage(package, assemblyFilename);
            //TODO make version available, currently only works for non versioned package directories...
            var packageIdentifier = string.Format("{0}.{1}", package.Id, package.Version);
            var packagesDir = this.PackagesDirectory ?? Path.Combine(projectDir.FullName, "packages");
            var newHintPathFull = Path.Combine(packagesDir, packageIdentifier, fileLocation);
            var newHintPathRelative = GetRelativePath(projectDir.FullName + Path.DirectorySeparatorChar, newHintPathFull);
            return newHintPathRelative;
        }

        private static String GetRelativePath(string rootWithTrailingSlash, string childWithTrailingSlash)
        {
            // Validate paths.
            Contract.Assert(!String.IsNullOrEmpty(rootWithTrailingSlash));
            Contract.Assert(!String.IsNullOrEmpty(childWithTrailingSlash));

            // Create Uris
            var rootUri = new Uri(rootWithTrailingSlash);
            var childUri = new Uri(childWithTrailingSlash);

            // Get relative path.
            var relativeUri = rootUri.MakeRelativeUri(childUri);

            // Clean path and return.
            return Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        private string GetFileLocationFromPackage(IPackage package, string key)
        {
            return (from fileLocation in package.GetFiles()
                where fileLocation.Path.ToLowerInvariant().EndsWith(key, StringComparison.OrdinalIgnoreCase)
                select fileLocation.Path).FirstOrDefault();
        }
    }
}