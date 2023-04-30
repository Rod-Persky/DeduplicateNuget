using System.Text;
using System.Xml;
using System.Xml.Linq;
using DeduplicateNugetLib;
using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Moq;

namespace DuplicateNugetLib.UnitTest;

public class UnitTest1
{
    [Fact]
    public void Test_GetPackageReferences()
    {
        XDocument doc = new XDocument(
            new XElement("Project",
                new XElement("ItemGroup",
                    new XElement("PackageReference", new XAttribute("Include", "...")),
                    new XElement("PackageReference", new XAttribute("Include", "..."))
                )
            )
        );

        var elements = Deduplicator.GetPackageReferences(doc.ToXmlDocument().DocumentElement!)!;
        elements.Count.Should().Be(2);
    }

    [Fact]
    public void Test_GetProjectReferences()
    {
        XDocument doc = new XDocument(
            new XElement("Project",
                new XElement("ItemGroup",
                    new XElement("ProjectReference", new XAttribute("Include", "A")),
                    new XElement("ProjectReference", new XAttribute("Include", "B"))
                )
            )
        );

        var elements = Deduplicator.GetProjectReferences(doc.ToXmlDocument().DocumentElement!)!;
        elements.Count.Should().Be(2);
    }


    [Fact]
    public void Test_TestIsSameReference()
    {
        var a = new XElement("PackageReference", new XAttribute("Include", "A"));
        var a_1 = new XElement("PackageReference", new XAttribute("Include", "A"));
        var b = new XElement("PackageReference", new XAttribute("Include", "B"));

        Deduplicator.TestIsSameReference(a.ToXmlElement(), a_1.ToXmlElement()).Should().BeTrue();
        Deduplicator.TestIsSameReference(a.ToXmlElement(), b.ToXmlElement()).Should().BeFalse();
    }


    [Fact]
    public void Test_TestProjectContainsReference()
    {
        XDocument doc = new XDocument(
            new XElement("Project",
                new XElement("ItemGroup",
                    new XElement("PackageReference", new XAttribute("Include", "A")),
                    new XElement("PackageReference", new XAttribute("Include", "B"))
                )
            )
        );


        var referenceToFind = new XElement("PackageReference", new XAttribute("Include", "A"));
        var referenceToNotFind = new XElement("PackageReference", new XAttribute("Include", "C"));

        Deduplicator.TestProjectContainsReference(
            doc.ToXmlDocument().DocumentElement!,
            referenceToFind.ToXmlElement()).Should().BeTrue();

        Deduplicator.TestProjectContainsReference(
            doc.ToXmlDocument().DocumentElement!,
            referenceToNotFind.ToXmlElement()).Should().BeFalse();
    }

    [Fact]
    public void Test_TestHasChildNodes()
    {
        var hasChildNodes = new XmlDocument();
        hasChildNodes.LoadXml("<a><b/></a>");

        var hasNoChildNodes = new XmlDocument();
        hasNoChildNodes.PreserveWhitespace = true;
        hasNoChildNodes.LoadXml("<a>\n\n</a>");


        Deduplicator.TestHasChildNodes(hasChildNodes.DocumentElement!).Should().BeTrue();
        Deduplicator.TestHasChildNodes(hasNoChildNodes.DocumentElement!).Should().BeFalse();
    }

    private IFileProvider SetupMock_IFileProvider(Dictionary<string, XDocument> projects)
    {
        var mockFs = new Mock<IFileProvider>();
        mockFs.Setup(_ => _.GetFileInfo(It.IsAny<string>())).Returns((string s) => {
            var fi = new Mock<IFileInfo>();
            fi.SetupGet(_ => _.Name).Returns(s);
            fi.SetupGet(_ => _.Exists).Returns(projects.ContainsKey(s));
            fi.SetupGet(_ => _.PhysicalPath).Returns(s);
            fi.Setup(_ => _.CreateReadStream()).Returns(() =>
            {
                var stream = new MemoryStream();
                projects[s].Save(stream);
                stream.Seek(0, SeekOrigin.Begin);
                return stream;
            });
            return fi.Object;
        });
        return mockFs.Object;
    }

    [Fact]
    public void Test_ResolveRelativePath()
    {
        var fi = new Mock<IFileInfo>();
        fi.SetupGet(_ => _.Name).Returns("a.csproj");
        fi.SetupGet(_ => _.Exists).Returns(true);
        fi.SetupGet(_ => _.PhysicalPath).Returns("a/a.csproj");

        Deduplicator.ResolveRelativePath(fi.Object, "b.csproj").Should().Be("a/b.csproj");
        Deduplicator.ResolveRelativePath(fi.Object, "../b/b.csproj").Should().Be("a/../b/b.csproj");
    }

    [Fact]
    public void Test_TestReferenceInParentProject_Single()
    {
        // Find Package A
        var package_a = new XmlDocument();
        package_a.LoadXml("<PackageReference Include=\"A\" />");

        // In CSPROJ that has Package A
        var projects = new Dictionary<string, XDocument>();
        projects["main.csproj"] = new XDocument(
            new XElement("Project",
                new XElement("ItemGroup",
                    new XElement("PackageReference", new XAttribute("Include", "A")),
                    new XElement("PackageReference", new XAttribute("Include", "B"))
                )
            )
        );

        var a = new Deduplicator(SetupMock_IFileProvider(projects));

        a.TestReferenceInParentProject("main.csproj", package_a.DocumentElement!, "").Should().BeTrue();
    }

    [Fact]
    public void Test_TestReferenceInParentProject_MultiLevel()
    {
        // Find Package A
        var package_a = new XmlDocument();
        package_a.LoadXml("<PackageReference Include=\"A\" />");

        // In Project main.csproj which references child.csproj
        var projects = new Dictionary<string, XDocument>();
        projects["main.csproj"] = new XDocument(
            new XElement("Project",
                new XElement("ItemGroup",
                    new XElement("PackageReference", new XAttribute("Include", "C")),
                    new XElement("ProjectReference", new XAttribute("Include", "child.csproj"))
                )
            )
        );
        projects["child.csproj"] = new XDocument(
            new XElement("Project",
                new XElement("ItemGroup",
                    new XElement("PackageReference", new XAttribute("Include", "A")),
                    new XElement("PackageReference", new XAttribute("Include", "B"))
                )
            )
        );

        var a = new Deduplicator(SetupMock_IFileProvider(projects));

        a.TestReferenceInParentProject("main.csproj", package_a.DocumentElement!, "").Should().BeTrue();
    }
}
