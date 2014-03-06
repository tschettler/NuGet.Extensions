using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Evaluation;

namespace NuGet.Extensions.Commands
{
    public class ProjectAdapter : IProjectAdapter
    {
        private readonly Project _project;
        private readonly string _packagesConfigFilename;

        public ProjectAdapter(Project project, string packagesConfigFilename)
        {
            _project = project;
            _packagesConfigFilename = packagesConfigFilename;
        }

        public ICollection<ProjectItem> GetBinaryReferences()
        {
            return _project.GetItems("Reference");
        }

        public string GetAssemblyName()
        {
            return _project.GetPropertyValue("AssemblyName");
        }

        public void Save()
        {
            _project.Save();
        }

        public void AddPackagesConfig()
        { //Add the packages.config to the project content, otherwise later versions of the VSIX fail...
            if (!HasPackagesConfig())
            {
                _project.Xml.AddItemGroup().AddItem("None", _packagesConfigFilename);
                Save();
            }
        }

        private bool HasPackagesConfig()
        {
            return _project.GetItems("None").Any(i => i.UnevaluatedInclude.Equals(_packagesConfigFilename));
        }
    }
}