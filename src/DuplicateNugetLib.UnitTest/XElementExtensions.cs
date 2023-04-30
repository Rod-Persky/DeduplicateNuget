namespace DuplicateNugetLib.UnitTest
{
    using System.Xml;
    using System.Xml.Linq;

    public static class XElementExtensions
    {
        public static XmlElement ToXmlElement(this XElement el)
        {
            var doc = new XmlDocument();
            doc.Load(el.CreateReader());
            return doc.DocumentElement!;
        }

        public static XmlDocument ToXmlDocument(this XDocument el)
        {
            var doc = new XmlDocument();
            doc.Load(el.CreateReader());
            return doc;
        }
    }
}
