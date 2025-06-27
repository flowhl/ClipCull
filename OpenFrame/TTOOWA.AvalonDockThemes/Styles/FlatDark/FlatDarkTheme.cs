using System;
using AvalonDock.Themes;

namespace TTOOWA.AvalonDockTheme;

public class FlatDarkTheme : Theme {
    public override Uri GetResourceUri() {
        return ResourceUtility.GetUri("TTOOWA.AvalonDockThemes", "Styles/FlatDark/FlatDark.xaml");
    }
}