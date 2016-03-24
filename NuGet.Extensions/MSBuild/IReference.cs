namespace NuGet.Extensions.MSBuild
{
    public interface IReference {
        string AssemblyVersion { get; set; }
        string AssemblyName { get; set; }
        string DllName { get; set; }
        
        bool Condition { get; }

        bool IsForAssembly(string assemblyFilename);

        bool TryGetHintPath(out string hintPath);

        bool CanConvert();

        /// <summary>
        /// Note: The parent project must be saved in order for this change to persist
        /// </summary>
        void ConvertToNugetReferenceWithHintPath(string hintPath);
    }
}