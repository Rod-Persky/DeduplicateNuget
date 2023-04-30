using System;
using System.ComponentModel;
using System.IO;
using System.Reflection.Emit;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.FileProviders;
using static System.Net.Mime.MediaTypeNames;

namespace DeduplicateNugetLib
{
    public class Deduplicator
    {


        private IFileProvider FileProvider;



        public Deduplicator(
            IFileProvider fileProvider)
        {
            FileProvider = fileProvider;
        }

        #region Static Methods
        public static XmlNodeList? GetPackageReferences(System.Xml.XmlElement csprojXml)
        {
            var packageReferences = csprojXml.GetElementsByTagName("PackageReference");
            return packageReferences;
        }


        public static XmlNodeList? GetProjectReferences(XmlElement csprojXml)
        {
            var projectReferences = csprojXml.GetElementsByTagName("ProjectReference");
            return projectReferences;
        }

        public static bool TestIsSameReference(XmlElement packageA, XmlElement packageB)
        {
            if (packageA.GetAttribute("Include") == packageB.GetAttribute("Include"))
            {
                return true;
            }

            return false;
        }

        public static bool TestProjectContainsReference(XmlElement projectXml, XmlElement reference)
        {
            var elementsOfReferenceType = projectXml.GetElementsByTagName(reference.Name);
            foreach (XmlElement element in elementsOfReferenceType)
            {
                var isSame = TestIsSameReference(reference, element);
                if (isSame)
                {
                    return true;
                }
            }

            return false;
        }


        public static bool TestHasChildNodes(XmlElement xmlElement)
        {
            if (!xmlElement.HasChildNodes)
            {
                return false;
            }

            // Enumerate all children to determine any non whitespace item return true
            foreach (XmlLinkedNode childNode in xmlElement.ChildNodes)
            {
                if (childNode.LocalName == "#whitespace")
                {
                    continue;
                }
                return true;
            }

            // has nodes, but all are whitespace, so no real child nodes
            return false;
        }

        #endregion


        public static string ResolveRelativePath(IFileInfo currentFile, string relativeFile)
        {
            string currentFullFilePath = currentFile.PhysicalPath ?? currentFile.Name;
            var currentFolder = currentFullFilePath.Substring(0, currentFullFilePath.Length - currentFile.Name.Length);
            return Path.Join(currentFolder, relativeFile);
        }

        public bool TestReferenceInParentProject(string projectFileName, XmlElement reference, string level)
        {
            var projectFile = FileProvider.GetFileInfo(projectFileName);

            Console.WriteLine($"{level} -> Checking for {reference["Include"]} in {projectFile.Name}");

            // Load file
            XmlDocument projectXml = new XmlDocument();
            projectXml.PreserveWhitespace = true;
            projectXml.Load(projectFile.CreateReadStream());

            var parentProjectReferences = GetProjectReferences(projectXml.DocumentElement);

            var parentContainsReference = TestProjectContainsReference(projectXml.DocumentElement, reference);
            if (parentContainsReference)
            {
                Console.WriteLine($"Duplicate found: {reference["Include"]} in {projectFile}");
                return true;
            }


            //Push - Location $projectFile.Directory
            foreach (XmlElement parentProjectReference in parentProjectReferences!)
            {
                // Get the relative parent project path and resolve it against the current file
                var relativeParentProjectPath = parentProjectReference.GetAttribute("Include");
                var resolvedPath = ResolveRelativePath(projectFile, relativeParentProjectPath);

                parentContainsReference = TestReferenceInParentProject(resolvedPath, reference, $"{level}-");
                if (parentContainsReference)
                {
                    Console.WriteLine($"Duplicate found: {reference["Include"]} in {parentProjectReference["Include"]}");
                    break;
                }
            }
            //Pop - Location | Out - Null

            return parentContainsReference;
        }

        public bool DeduplicatePackageReferences(XmlElement projectXml)
        {
            var projectReferences = GetProjectReferences(projectXml);
            var packageReferences = GetPackageReferences(projectXml);
            bool projectNeedsUpdate = false;

            if (packageReferences == null || projectReferences == null)
            {
                return projectNeedsUpdate;
            }

            foreach (XmlElement packageReference in packageReferences) {
                foreach (XmlElement projectReference in projectReferences)
                {
                    var foundDuplicate = TestReferenceInParentProject(projectReference.GetAttribute("Include"), packageReference, "");
                    if (foundDuplicate)
                    {
                        Console.WriteLine($"Duplicate found: {packageReference.GetAttribute("Include")}");
                        var parentNode = packageReference.ParentNode;
                        parentNode.RemoveChild(packageReference);

                        XmlElement? pel = parentNode as XmlElement;
                        if (TestHasChildNodes(pel!))
                        {
                            parentNode.ParentNode.RemoveChild(parentNode.PreviousSibling);
                            parentNode.ParentNode.RemoveChild(parentNode);
                        }

                        projectNeedsUpdate = true;
                        break;
                    }
                }
            }

            return projectNeedsUpdate;
        }
    }
}

