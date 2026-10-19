using System;
using System.IO;
using System.Xml.Linq;
using Xunit;

namespace SourceGit.Tests
{
    public class ThemeResourcesTests
    {
        [Theory]
        [InlineData("Light")]
        [InlineData("Dark")]
        public void TestThemeFileContainsRequiredResources(string themeKey)
        {
            var baseDir = AppContext.BaseDirectory;
            var projectDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
            var themesFilePath = Path.Combine(projectDir, "src", "Resources", "Themes.axaml");

            Assert.True(File.Exists(themesFilePath), $"Themes.axaml not found: {themesFilePath}");

            var content = File.ReadAllText(themesFilePath);

            Assert.Contains($"<ResourceDictionary x:Key=\"{themeKey}\">", content);

            var requiredColorKeys = new[]
            {
                "Color.Window",
                "Color.Contents",
                "Color.SubPanel",
                "Color.ToolBar",
                "Color.Border0",
                "Color.Border1",
                "Color.Border2",
                "Color.Badge",
                "Color.BadgeFG",
                "Color.FG1",
                "Color.FG2"
            };

            foreach (var key in requiredColorKeys)
            {
                Assert.Contains($"x:Key=\"{key}\"", content);
            }

            var requiredBrushKeys = new[]
            {
                "Brush.Window",
                "Brush.Contents",
                "Brush.SubPanel",
                "Brush.ToolBar",
                "Brush.Border0",
                "Brush.Border1",
                "Brush.Border2",
                "Brush.Badge",
                "Brush.BadgeFG",
                "Brush.FG1",
                "Brush.FG2"
            };

            foreach (var key in requiredBrushKeys)
            {
                Assert.Contains($"x:Key=\"{key}\"", content);
            }
        }
    }
}
