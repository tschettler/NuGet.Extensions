using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Build.Evaluation;

namespace NuGet.Extensions.MSBuild
{
    using System.Reflection;
    using System.Security.Policy;

    [DebuggerDisplay("{AssemblyName}")]
    public class BinaryReferenceAdapter : IReference
    {
        private readonly ProjectItem _reference;

        private string fullName;

        private bool hasHintPath;

        private string hintPath;

        private bool isInGac;

        private bool initialized;

        public BinaryReferenceAdapter(ProjectItem reference, bool conditionTrue)
        {
            _reference = reference;
            Condition = conditionTrue;
            fullName = reference.EvaluatedInclude;
        }

        public bool IsFullyQualifiedAssemblyName
        {
            get
            {
                return this.fullName.Contains(",");
            }
        }

        public string AssemblyVersion { get; set; }

        public string AssemblyName { get; set; }

        public string DllName { get; set; }

        public bool TryGetHintPath(out string hintPath)
        {
            hintPath = null;
            var hasHintPath = _reference.HasMetadata("HintPath");
            if (hasHintPath)
            {
                hintPath = _reference.GetMetadataValue("HintPath");
            }
            
            return hasHintPath;
        }

        public void Initialize()
        {
            if (this.initialized)
            {
                return;
            }

            this.hasHintPath = this.TryGetHintPath(out this.hintPath);

            this.InitializeAssembly();

            this.DllName = Path.GetFileName(this.hintPath ?? this.AssemblyName + ".dll");

            this.initialized = true;
        }

        private void InitializeAssembly()
        {
            if (this.IsFullyQualifiedAssemblyName)
            {
                var fullNameParts = this.fullName.Split(',');
                this.AssemblyName = fullNameParts[0];
                this.AssemblyVersion = fullNameParts[1].Split('=')[1];
                this.isInGac = this.isInGac || this.CheckIfInGac();
            }
            else
            {
                var assembly = this.GetAssembly();
                if (assembly == null)
                {
                    this.AssemblyName = this.fullName;
                    this.AssemblyVersion = null;
                }
                else
                {
                    this.fullName = assembly.FullName;
                    this.isInGac = assembly.GlobalAssemblyCache;
                    this.InitializeAssembly();
                }
            }
        }

        public void ConvertToNugetReferenceWithHintPath(string hintPath)
        {
            _reference.SetMetadataValue("HintPath", hintPath);
        }


        public bool IsForAssembly(string assemblyFilename)
        {
            return this.DllName == assemblyFilename;
        }

        private Assembly GetAssembly()
        {
            try
            {
                if (this.hasHintPath)
                {
                    return Assembly.ReflectionOnlyLoadFrom(this.hintPath);
                }

                return this.IsFullyQualifiedAssemblyName ? Assembly.ReflectionOnlyLoad(this.fullName) : null;
            }
            catch
            {
                return null;
            }
        }

        public bool CheckIfInGac()
        {
            try
            {
                var assemblyName = _reference.EvaluatedInclude;
                var isFullName = assemblyName.Contains(",");

                return isFullName && Assembly.ReflectionOnlyLoad(assemblyName).GlobalAssemblyCache;
            }
            catch
            {
                return false;
            }
        }

        public bool CanConvert()
        {
            return this.hasHintPath || this.isInGac;
        }

        public bool Condition { get; private set; }
    }
}