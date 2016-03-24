using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGet.Extensions.ExtensionMethods
{
    using global::NuGet.Extensions.MSBuild;

    public static class IReferenceExtensions
    {
        public static SemanticVersion GetSemanticVersion(this IReference reference)
        {
            return new SemanticVersion(reference.AssemblyVersion ?? "0.0.0.0");
        }

        public static VersionSpec GetSafeVersion(this IReference reference)
        {
            var minVersion = new SemanticVersion(reference.AssemblyVersion ?? "0.0.0.0");

            var maxVer = new Version(reference.AssemblyVersion ?? "9999.9998.9999.9999");
            var maxVersion = new SemanticVersion(new Version(maxVer.Major, maxVer.Minor + 1));

            return new VersionSpec()
            {
                IsMinInclusive = true,
                IsMaxInclusive = true,
                MinVersion = minVersion,
                MaxVersion = maxVersion
            };
        }
    }
}
