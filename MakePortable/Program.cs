using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MakePortable
{
    class Program
    {
        static readonly Dictionary<string, TemplateInfo> _templates = new Dictionary<string, TemplateInfo>()
        {
            ["PCL"] = new TemplateInfo { Key = "PCL", FolderSuffix = "PORTABLE", FileName = "PCL", Description = "PCL (portable)" },
            ["NET45"] = new TemplateInfo { Key = "NET45", FolderSuffix = "NET45", FileName = "NET45", Description = ".Net 4.5" },
            ["NET46"] = new TemplateInfo { Key = "NET46", FolderSuffix = "NET46", FileName = "NET46", Description = ".Net 4.6" },
        };

        static string _currentPath;
        static string _sourceName;
        static string _sourceFolderPath;
        static string _targetName;
        static string _targetFolderPath;
        static string _targetProjectFileName;
        static TemplateInfo _selectedTemplate;

        static readonly XNamespace _xns = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003");


        static void Main(string[] args)
        {
            _currentPath = Environment.CurrentDirectory;

            if (ParseInputArgs(args))
            {
                ValidateSource();
                EnsureTargetProject();

                var links = new List<LinkItem>();
                AlignTree(
                    links,
                    new DirectoryInfo(_sourceFolderPath),
                    new DirectoryInfo(_targetFolderPath),
                    string.Empty,
                    new string[] { "properties", "bin", "obj" },
                    new string[] { ".cs" }
                    );

                PatchTargetProject(
                    links
                    );

                Console.WriteLine("Successfully aligned the target project!");
            }

            Console.WriteLine();
            Console.Write("Press any key to exit...");
            Console.ReadKey();
        }


        /// <summary>
        /// Validate and parse the application's input parameters
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        static bool ParseInputArgs(IEnumerable<string> args)
        {
            string failureReason = null;

            if (args.Any())
            {
                foreach (string a in args)
                {
                    if (a.StartsWith("--"))
                    {
                        string au = a.Substring(2).ToUpperInvariant();
                        TemplateInfo ti;
                        if (_templates.TryGetValue(au, out ti))
                        {
                            if (_selectedTemplate == null)
                            {
                                _selectedTemplate = ti;
                            }
                            else
                            {
                                failureReason = "Ambiguous profile switch: " + a;
                            }
                        }
                        else
                        {
                            failureReason = "Unsupported option: " + a;
                        }
                    }
                    else if (string.IsNullOrEmpty(_sourceFolderPath))
                    {
                        _sourceFolderPath = Path.Combine(_currentPath, a);
                    }
                    else
                    {
                        failureReason = "Bad input.";
                    }

                    if (string.IsNullOrEmpty(failureReason) == false) break;
                }
            }
            else
            {
                failureReason = "Missing arguments.";
            }

            if (string.IsNullOrEmpty(failureReason))
            {
                //success
                _selectedTemplate = _selectedTemplate ?? _templates.Values.First();
                return true;
            }
            else
            {
                //failure
                Console.WriteLine("*** ERROR!");
                Console.WriteLine(failureReason);
                Console.WriteLine();
                Console.WriteLine("Usage:");
                Console.WriteLine("  MakePortable [switches] project-path");
                Console.WriteLine();
                Console.WriteLine("Supported switches:");
                foreach (TemplateInfo ti in _templates.Values)
                {
                    Console.WriteLine($"  --{ti.Key,-8}    creates a {ti.Description} project");
                }
                return false;
            }
        }


        /// <summary>
        /// Validation of the source project location
        /// </summary>
        static void ValidateSource()
        {
            //check the source folder
            var di = new DirectoryInfo(_sourceFolderPath);
            if (di.Exists == false)
            {
                throw new ArgumentException("The source path does not exist.");
            }

            //search for suitable projects
            var srcInfos = di.GetFileSystemInfos("*.xproj", SearchOption.TopDirectoryOnly);
            switch (srcInfos.Length)
            {
                case 0:
                    throw new ArgumentException("Could not find any suitable .Net Core project.");

                case 1:
                    _sourceName = Path.GetFileNameWithoutExtension(srcInfos[0].Name);
                    Console.WriteLine("Source project found: " + srcInfos[0].Name);
                    break;

                default:
                    throw new ArgumentException($"Found {srcInfos.Length} projects: must specify the exact source.");
            }

            //define the target naming and location
            _targetName = _sourceName + "_" + _selectedTemplate.FolderSuffix;
            _targetFolderPath = Path.Combine(_currentPath, _targetName);
            _targetProjectFileName = Path.Combine(_targetFolderPath, _targetName + ".csproj");
        }


        /// <summary>
        /// Check for the target folder and project file presence.
        /// Whereas not existent, create them as brand new
        /// </summary>
        static void EnsureTargetProject()
        {
            //check the target folder
            var di = new DirectoryInfo(_targetFolderPath);
            if (di.Exists == false)
            {
                di.Create();
            }

            //check the target project
            var tfi = new FileInfo(_targetProjectFileName);
            if (tfi.Exists == false)
            {
                //take the portable project's content template
                XDocument xdoc;
                var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
                    $"MakePortable.Templates.{_selectedTemplate.FileName}ProjectTemplate.xml"
                    );

                using (var reader = new StreamReader(stream))
                {
                    xdoc = XDocument.Load(reader);
                }

                //refine the useful parameters
                var xgroup = xdoc.Root.Elements(_xns + "PropertyGroup").First();
                xgroup.Element(_xns + "ProjectGuid").Value = Guid.NewGuid().ToString("B");
                xgroup.Element(_xns + "RootNamespace").Value = _sourceName;
                xgroup.Element(_xns + "AssemblyName").Value = _targetName;

                //create the target project file
                using (var fs = tfi.OpenWrite())
                {
                    xdoc.Save(fs);
                }
            }

            //check the "properties" subfolder
            var pdi = new DirectoryInfo(Path.Combine(_targetFolderPath, "Properties"));
            if (pdi.Exists == false)
            {
                pdi.Create();
            }

            //check the "assembly-info" file
            var pfi = new FileInfo(Path.Combine(pdi.FullName, "AssemblyInfo.cs"));
            if (pfi.Exists == false)
            {
                //take the template
                string sdoc;
                var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MakePortable.Templates.AssemblyInfoTemplate.txt");
                using (var reader = new StreamReader(stream))
                {
                    sdoc = reader.ReadToEnd();
                }

                //refine the useful parameters
                sdoc = sdoc.Replace("$$TITLE", _sourceName);
                sdoc = sdoc.Replace("$$PRODUCT", _sourceName);
                sdoc = sdoc.Replace("$$YEAR", DateTime.Now.Year.ToString());

                //create the target file
                using (var fs = pfi.OpenWrite())
                using (var sw = new StreamWriter(fs))
                {
                    sw.Write(sdoc);
                }
            }
        }


        /// <summary>
        /// Scan the source project structure, then map the equivalent for the target
        /// </summary>
        /// <param name="links"></param>
        /// <param name="sourceDirectory"></param>
        /// <param name="targetDirectory"></param>
        /// <param name="foldersToIgnore"></param>
        /// <param name="extensionsToInclude"></param>
        static void AlignTree(
            List<LinkItem> links,
            DirectoryInfo sourceDirectory,
            DirectoryInfo targetDirectory,
            string linkPath,
            IEnumerable<string> foldersToIgnore,
            IEnumerable<string> extensionsToInclude
            )
        {
            //scan the current source folder content
            foreach (var fsi in sourceDirectory.EnumerateFileSystemInfos())
            {
                var fi = fsi as FileInfo;
                var di = fsi as DirectoryInfo;
                if (di != null)
                {
                    //subdirectory: skip if marked to exclude
                    if (foldersToIgnore.Contains(di.Name.ToLowerInvariant()))
                    {
                        continue;
                    }

                    //reproduce the same folders structure by the target side
                    var child = new DirectoryInfo(Path.Combine(targetDirectory.FullName, di.Name));
                    if (child.Exists == false)
                    {
                        child.Create();
                    }

                    //scan deeper
                    AlignTree(
                        links,
                        di,
                        child,
                        Path.Combine(linkPath, di.Name),
                        Enumerable.Empty<string>(),
                        extensionsToInclude
                        );
                }
                else if (fi != null)
                {
                    //file: skip unless its extension is marked for inclusion
                    var ext = Path.GetExtension(fi.Name).ToLowerInvariant();
                    if (extensionsToInclude.Contains(ext) == false)
                    {
                        continue;
                    }

                    //collect some info about the current file
                    var lnk = new LinkItem();
                    lnk.Include = MakeRelativePath(_targetFolderPath + "/", fi.FullName);
                    lnk.Link = string.IsNullOrEmpty(linkPath)
                        ? Path.GetFileName(fi.FullName)
                        : Path.Combine(linkPath, Path.GetFileName(fi.FullName));
                    links.Add(lnk);
                }
                else
                {
                    throw new NotSupportedException("FileSysItem not supported: " + fsi);
                }
            }
        }


        /// <summary>
        /// Patch the target project file so that the links are correctly aligned
        /// </summary>
        /// <param name="links"></param>
        static void PatchTargetProject(
            IEnumerable<LinkItem> links
            )
        {
            //read the target project file
            var xdoc = XDocument.Load(_targetProjectFileName);

            //find the proper element
            XElement xitemGroup;
            xitemGroup = xdoc.Root
                .Elements(_xns + "ItemGroup")
                .Where(_ => _.Elements(_xns + "Compile").Any())
                .FirstOrDefault();

            if (xitemGroup == null)
            {
                xitemGroup = xdoc.Root
                    .Elements(_xns + "ItemGroup")
                    .Where(_ => _.Elements().Any() == false)
                    .FirstOrDefault();
            }

            //clean up any child
            xitemGroup.RemoveAll();

            //add all the link items
            foreach (var lnk in links)
            {
                var xcomp = new XElement(
                    _xns + "Compile",
                    new XAttribute("Include", lnk.Include),
                    new XElement(_xns + "Link", lnk.Link)
                    );

                xitemGroup.Add(xcomp);
            }

            {
                //add the assembly-info element
                var xcomp = new XElement(
                    _xns + "Compile",
                    new XAttribute("Include", @"Properties\AssemblyInfo.cs")
                    );

                xitemGroup.Add(xcomp);
            }

            //update the project file
            xdoc.Save(_targetProjectFileName);
        }


        /// <summary>
        /// Creates a relative path from one file or folder to another.
        /// http://stackoverflow.com/questions/275689/how-to-get-relative-path-from-absolute-path
        /// </summary>
        /// <param name="fromPath">Contains the directory that defines the start of the relative path.</param>
        /// <param name="toPath">Contains the path that defines the endpoint of the relative path.</param>
        /// <returns>The relative path from the start directory to the end path or <c>toPath</c> if the paths are not related.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="UriFormatException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public static String MakeRelativePath(String fromPath, String toPath)
        {
            if (String.IsNullOrEmpty(fromPath)) throw new ArgumentNullException("fromPath");
            if (String.IsNullOrEmpty(toPath)) throw new ArgumentNullException("toPath");

            Uri fromUri = new Uri(fromPath);
            Uri toUri = new Uri(toPath);

            if (fromUri.Scheme != toUri.Scheme) { return toPath; } // path can't be made relative.

            Uri relativeUri = fromUri.MakeRelativeUri(toUri);
            String relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            if (toUri.Scheme.Equals("file", StringComparison.InvariantCultureIgnoreCase))
            {
                relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }

            return relativePath;
        }


        /// <summary>
        /// Link descriptor
        /// </summary>
        private class LinkItem
        {
            public string Include;
            public string Link;
        }


        /// <summary>
        /// Template descriptor
        /// </summary>
        private class TemplateInfo
        {
            public string Key;
            public string FolderSuffix;
            public string FileName;
            public string Description;
        }
    }
}
