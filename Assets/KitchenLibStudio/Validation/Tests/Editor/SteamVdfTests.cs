using NUnit.Framework;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class SteamVdfTests
    {
        [Test]
        public void ParsesCurrentNestedObjectsAndEscapedWindowsPath()
        {
            SteamVdfNode root = SteamVdf.Parse(
                "\"libraryfolders\"\n{\n  \"1\" { \"path\" \"D:\\\\SteamLibrary\" \"apps\" { \"1599600\" \"1\" } }\n}");

            SteamVdfNode library = root.GetObject("libraryfolders").GetObject("1");
            Assert.That(library.GetString("path"), Is.EqualTo("D:\\SteamLibrary"));
            Assert.That(library.GetObject("apps").GetString("1599600"), Is.EqualTo("1"));
        }

        [Test]
        public void ParsesLegacyLibraryValueAndComments()
        {
            SteamVdfNode root = SteamVdf.Parse(
                "// legacy\n\"LibraryFolders\" { \"TimeNextStatsReport\" \"0\" \"1\" \"E:\\\\Games\" }");

            Assert.That(root.GetObject("LibraryFolders").GetString("1"), Is.EqualTo("E:\\Games"));
        }

        [Test]
        public void MalformedVdfReportsLineAndColumn()
        {
            SteamVdfParseException exception = Assert.Throws<SteamVdfParseException>(() =>
                SteamVdf.Parse("\"libraryfolders\"\n{\n  \"1\" \"unterminated"));

            Assert.That(exception.Line, Is.EqualTo(3));
            Assert.That(exception.Column, Is.GreaterThan(3));
            StringAssert.Contains("line 3", exception.Message);
        }
    }
}
