﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Cake.Core;
using Cake.Core.Annotations;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Ionic.Zip;

namespace Cake.PinNuGetDependency
{
    /// <summary>
    /// Contains functionality for pinning NuGet dependencies.
    /// </summary>
    [CakeAliasCategory("NuGet")]
    [CakeNamespaceImport("Cake.PinNuGetDependency")]
    public static class PinNuGetDependencyAliases
    {
        /// <summary>
        /// Pin NuGet Dependencies based on identifier.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="filePath">Path to the nupkg file.</param>
        /// <param name="dependancyIdentifier">Dependency identifier.</param>
        /// <exception cref="FileNotFoundException"></exception>
        [CakeMethodAlias]

        public static void PinNuGetDependency(this ICakeContext context, FilePath filePath, string dependancyIdentifier)
        {
            Ensure.ArgumentNotNull(filePath, nameof(filePath));
            Ensure.ArgumentNotNullOrWhiteSpace(dependancyIdentifier, nameof(dependancyIdentifier));

            var package = filePath.FullPath;
            if (!File.Exists(package))
            {
                throw new FileNotFoundException($"'{package}' could not be found.");
            }

            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (ZipFile zip = ZipFile.Read(package))
                {
                    var zipEntry = zip.Single(x => x.FileName.Contains(".nuspec"));

                    zip[zipEntry.FileName].Extract(memoryStream);

                    // u can't touch this
                    memoryStream.Seek(0, SeekOrigin.Begin);

                    var xmlDocument = new XmlDocument();
                    xmlDocument.Load(memoryStream);

                    XmlNamespaceManager ns = new XmlNamespaceManager(xmlDocument.NameTable);
                    ns.AddNamespace("ns1", "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd");

                    var nodes = xmlDocument.SelectNodes($"//ns1:dependency[@id=\"{dependancyIdentifier}\"]", ns);

                    foreach (XmlNode node in nodes)
                    {
                        var originalVersion = node.Attributes["version"].Value;

                        // ugh just incase someone runs this plugin twice
                        node.Attributes["version"].Value = node.Attributes["version"].Value
                            .Replace("[", "")
                            .Replace("]", "");

                        node.Attributes["version"].Value = $"[{originalVersion}]";
                    }

                    // stop hammer time
                    memoryStream.Seek(0, SeekOrigin.Begin);

                    xmlDocument.Save(memoryStream);

                    // cant touch! dis
                    memoryStream.Seek(0, SeekOrigin.Begin);

                    zip.UpdateEntry(zipEntry.FileName, memoryStream);
                    zip.Save();
                }
            }
        }
    }
}
