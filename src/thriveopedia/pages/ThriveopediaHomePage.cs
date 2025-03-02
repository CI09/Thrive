﻿using Godot;

/// <summary>
///   Thriveopedia page displaying welcome information and links to websites.
/// </summary>
public class ThriveopediaHomePage : ThriveopediaPage
{
    public override string PageName => "Home";
    public override string TranslatedPageName => TranslationServer.Translate("THRIVEOPEDIA_HOME_PAGE_TITLE");

    public override string? ParentPageName => null;
}
