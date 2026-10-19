using System;
using SourceGit.ViewModels;
using Xunit;

namespace SourceGit.Tests
{
    public class PreferencesTests
    {
        [Fact]
        public void TestPreferencesInstanceNotNull()
        {
            var instance = Preferences.Instance;
            Assert.NotNull(instance);
        }

        [Theory]
        [InlineData("Default")]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TestThemeSettingValues(string theme)
        {
            var prefs = new Preferences();
            prefs.Theme = theme;
            Assert.Equal(theme, prefs.Theme);
        }

        [Fact]
        public void TestDefaultFontSettings()
        {
            var prefs = new Preferences();
            Assert.True(prefs.DefaultFontSize >= 10);
            Assert.True(prefs.EditorFontSize >= 10);
            Assert.True(prefs.EditorTabWidth >= 1);
        }
    }
}
