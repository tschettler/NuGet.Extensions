using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NuGet.Common;

namespace NuGet.Extensions.Nuspec
{
    public class NuspecBuilder 
    {
        private readonly Manifest _manifest;
        public string FilePath { get; private set; }

        public NuspecBuilder(string assemblyName)
        {
            var file = new ManifestFile
                       {
                           Source = assemblyName + ".dll",
                           Target = "lib"
                       };

            _manifest = new Manifest
                        {
                            Metadata = new ManifestMetadata
                                       {
                                           Id = assemblyName,
                                           Title = assemblyName,
                                           Description = assemblyName
                                       },
                            Files = new List<ManifestFile> {file}
                        };

            FilePath = assemblyName + Constants.ManifestExtension;
        }

        public void SetMetadata(INuspecDataSource nuspecData, ICollection<ManifestDependency> manifestDependencies)
        {
            var metadata = _manifest.Metadata;
            metadata.Id = nuspecData.Id ?? metadata.Id;
            metadata.Title = nuspecData.Title ?? metadata.Title;
            metadata.Version = "$version$";
            metadata.Description = nuspecData.Description ?? metadata.Description;
            metadata.Authors = nuspecData.Author ?? "$author$";
            metadata.Tags = nuspecData.Tags ?? "$tags$";
            metadata.LicenseUrl = nuspecData.LicenseUrl ?? "$licenseurl$";
            metadata.RequireLicenseAcceptance = nuspecData.RequireLicenseAcceptance;
            metadata.Copyright = nuspecData.Copyright ?? "$copyright$";
            metadata.IconUrl = nuspecData.IconUrl ?? "$iconurl$";
            metadata.ProjectUrl = nuspecData.ProjectUrl ?? "$projrcturl$";
            metadata.Owners = nuspecData.Owners ?? nuspecData.Author ?? "$author$";

            //Dont add a releasenotes node if we dont have any to add...
            if (!String.IsNullOrEmpty(nuspecData.ReleaseNotes)) metadata.ReleaseNotes = nuspecData.ReleaseNotes;
        }

        public void SetDependencies(ICollection<ManifestDependency> manifestDependencies, string targetFramework = ".NET Framework, Version=4.0")
        {
            _manifest.Metadata.DependencySets =
                new List<ManifestDependencySet>
                {
                    new ManifestDependencySet {Dependencies = manifestDependencies.ToList(), TargetFramework = targetFramework}
                };
        }

        public void Save(IConsole console)
        {
            try
            {
                console.WriteLine("Saving new NuSpec: {0}", FilePath);
                var nuspecText = ToNuspecFileText();
                File.WriteAllText(FilePath, nuspecText);
            }
            catch (Exception)
            {
                console.WriteError("Could not save file: {0}", FilePath);
                throw;
            }
        }

        private string ToNuspecFileText()
        {
            using (var stream = new MemoryStream())
            {
                _manifest.Save(stream, validate: false);
                stream.Seek(0, SeekOrigin.Begin);
                var content = stream.ReadToEnd();
                return RemoveSchemaNamespace(content);
            }
        }

        private static string RemoveSchemaNamespace(string content)
        {
            // This seems to be the only way to clear out xml namespaces.
            return Regex.Replace(content, @"(xmlns:?[^=]*=[""][^""]*[""])", String.Empty, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }
    }
}