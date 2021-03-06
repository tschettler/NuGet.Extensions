using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;

namespace NuGet.Extensions.MSBuild
{
    [DebuggerDisplay("{ProjectName} {AssemblyName}")]
    public class ProjectAdapter : IVsProject
    {
        private readonly Project _project;
        private readonly IProjectLoader _projectLoader;

        public ProjectAdapter(Project project, IProjectLoader projectLoader)
        {
            _project = project;
            _projectLoader = projectLoader;
        }

        public IEnumerable<IReference> GetBinaryReferences()
        {
            const string itemType = "Reference";
            var conditionTrueReferences = new HashSet<ProjectItem>(_project.GetItems(itemType));
            return conditionTrueReferences.Select(
                r =>
                    {
                        var adapter = new BinaryReferenceAdapter(r, true);
                        adapter.Initialize();
                        return adapter;
                    });
        }

        public string AssemblyName
        {
            get { return _project.GetPropertyValue("AssemblyName"); }
        }

        public string ProjectName
        {
            get { return _project.GetPropertyValue("ProjectName"); }
        }

        public DirectoryInfo ProjectDirectory
        {
            get { return new DirectoryInfo(_project.DirectoryPath); }
        }

        public void Save()
        {
            _project.Save();
        }

        public void AddFile(string filename)
        { //Add the packages.config to the project content, otherwise later versions of the VSIX fail...
            if (!FileAlreadyReferenced(filename))
            {
                _project.Xml.AddItemGroup().AddItem("None", filename);
            }
        }

        private bool FileAlreadyReferenced(string filename)
        {
            return _project.GetItems("None").Any(i => i.UnevaluatedInclude.Equals(filename));
        }

        public IEnumerable<IReference> GetProjectReferences()
        {
            const string itemType = "ProjectReference";
            var conditionTrueProjectReferences = new HashSet<ProjectItem>(_project.GetItems(itemType));
            return conditionTrueProjectReferences.Select(r => GetProjectReferenceAdapter(r, true));
        }

        private ProjectReferenceAdapter GetProjectReferenceAdapter(ProjectItem r, bool conditionTrue)
        {
            var projectGuid = r.GetMetadataValue("Project");
            var csprojRelativePath = r.EvaluatedInclude;
            var absoluteProjectPath = Path.Combine(ProjectDirectory.FullName, csprojRelativePath);
            var referencedProjectAdapter = _projectLoader.GetProject(Guid.Parse(projectGuid), absoluteProjectPath);
            return new ProjectReferenceAdapter(referencedProjectAdapter, () => _project.RemoveItem(r), AddBinaryReferenceIfNotExists, conditionTrue);
        }

        private void AddBinaryReferenceIfNotExists(string includePath, KeyValuePair<string, string> metadata)
        {
            var existingReferences = GetBinaryReferences().Where(r => string.Equals(r.AssemblyName, includePath, StringComparison.InvariantCultureIgnoreCase));
            if (!existingReferences.Any()) _project.AddItem("Reference", includePath, new[]{metadata});
        }
    }
}