using System;
using System.Xml;
using Microsoft.Extensions.FileProviders;

namespace DeduplicateNugetTool
{
	public class Tool
	{
        IFileProvider FileProvider = new NullFileProvider();
        DeduplicateNugetLib.Deduplicator Deduplicator;

        public Tool(IFileProvider fileProvider)
		{
            FileProvider = fileProvider;
            Deduplicator = new DeduplicateNugetLib.Deduplicator(fileProvider);
        }


        public void DoDependencyCleaning(FileInfo projectFileName)
        {
            XmlDocument projectXml = new XmlDocument();
            projectXml.PreserveWhitespace = true;
            projectXml.Load(projectFileName.FullName);

            bool packagesNeedsUpdate = Deduplicator.DeduplicatePackageReferences(projectXml.DocumentElement!);


            if (packagesNeedsUpdate)
            {
                Console.WriteLine("update required, but skipping save");
                //projectXml.Save(projectFileName.FullName);
            }
        }

        public void Run()
        {
            var projectFiles = Directory
                .GetFiles(Directory.GetCurrentDirectory(), @"*.csproj", SearchOption.AllDirectories);

            foreach (var projectFile in projectFiles)
            {
                var fileInfo = new FileInfo(projectFile);
                if (fileInfo.Exists)
                {
                    DoDependencyCleaning(fileInfo);
                }
            }
        }
    }
}

