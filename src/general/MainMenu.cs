using System;
using System.Linq;
using Godot;
using Godot.Collections;
using LauncherThriveShared;
using Tutorial;
using Xoshiro.PRNG32;

/// <summary>
///   Class managing the main menu and everything in it
/// </summary>
public partial class MainMenu : NodeWithInput
{
    /// <summary>
    ///   Index of the current menu.
    /// </summary>
    [Export]
    public uint CurrentMenuIndex;

    /// <summary>
    ///   How many non-menu items there are in the menu container
    /// </summary>
    [Export]
    public int NonMenuItemsFirst = 1;

    private const string MainWebsiteLink = "https://revolutionarygamesstudio.com";

    private const double AnimationLength = 6;

    private const float AnimationLengthInverse = 1 / (float)AnimationLength;

#pragma warning disable CA2213
    private TextureRect background = null!;
    private Node3D? created3DBackground;

    [Export]
    private TextureRect thriveLogo = null!;

    private OptionsMenu options = null!;
    private NewGameSettings newGameSettings = null!;
    private AnimationPlayer guiAnimations = null!;
    private SaveManagerGUI saves = null!;
    private Thriveopedia thriveopedia = null!;

    [Export]
    private ModManager modManager = null!;

    [Export]
    private GalleryViewer galleryViewer = null!;

    [Export]
    private ThriveFeedDisplayer newsFeed = null!;

    [Export]
    private Control newsFeedDisabler = null!;

    [Export]
    private PatchNotesDisplayer patchNotes = null!;

    [Export]
    private Control patchNotesDisabler = null!;

    [Export]
    private Control feedPositioner = null!;

    [Export]
    private Control creditsContainer = null!;

    [Export]
    private CreditsScroll credits = null!;

    [Export]
    private LicensesDisplay licensesDisplay = null!;

    [Export]
    private Control MainMenuContainer = null!;

    [Export]
    private Control ToolsContainer = null!;

    [Export]
    private Control ExtrasContainer = null!;

    [Export]
    private Control BenchmarkContainer = null!;

    [Export]
    private Button freebuildButton = null!;

    [Export]
    private Button multicellularFreebuildButton = null!;

    [Export]
    private Button autoEvoExploringButton = null!;

    [Export]
    private Button microbeBenchmarkButton = null!;

    [Export]
    private Button exitToLauncherButton = null!;

    [Export]
    private Label storeLoggedInDisplay = null!;

    [Export]
    private Control socialMediaContainer = null!;

    [Export]
    private CustomWindow websiteButtonsContainer = null!;

    [Export]
    private TextureButton itchButton = null!;

    [Export]
    private TextureButton patreonButton = null!;

    [Export]
    private CustomConfirmationDialog openGlPopup = null!;

    [Export]
    private ErrorDialog modLoadFailures = null!;

    [Export]
    private CustomConfirmationDialog steamFailedPopup = null!;

    [Export]
    private CustomWindow safeModeWarning = null!;

    [Export]
    private PermanentlyDismissibleDialog modsInstalledButNotEnabledWarning = null!;

    [Export]
    private PermanentlyDismissibleDialog lowPerformanceWarning = null!;

    [Export]
    private PermanentlyDismissibleDialog thanksDialog = null!;

    [Export]
    private Control menus = null!;

    [Export]
    private Camera3D camera = null!;
#pragma warning restore CA2213

    private Array<Node>? menuArray;

    private bool introVideoPassed;

    private double timerForStartupSuccess = Constants.MAIN_MENU_TIME_BEFORE_STARTUP_SUCCESS;

    /// <summary>
    ///   True when we are able to show the thanks for buying popup due to being a store version
    /// </summary>
    private bool canShowThanks;

    /// <summary>
    ///   The store-specific page link. Defaults to the website link if we don't know a valid store name
    /// </summary>
    private string storeBuyLink = "https://revolutionarygamesstudio.com/releases/";

    private string storeDisplayName = "ERROR";

    private double averageFrameRate;

    private double animationTime = 0;

    /// <summary>
    ///   Time tracking related to performance. Note that this is reset when performance tracking is restarted.
    /// </summary>
    private double secondsInMenu;

    private bool canShowLowPerformanceWarning = true;

    public bool IsReturningToMenu { get; set; }

    public static void OnEnteringGame()
    {
        CheatManager.OnCheatsDisabled();
        SaveHelper.ClearLastSaveTime();
        LastPlayedVersion.MarkCurrentVersionAsPlayed();
    }

    public override void _Ready()
    {
        if (SceneManager.QuitOrQuitting)
        {
            GD.Print("Skipping main menu initialization due to quitting");
            return;
        }

        // Unpause the game as the MainMenu should never be paused.
        PauseManager.Instance.ForceClear();
        MouseCaptureManager.ForceDisableCapture();

        camera.GlobalPosition = new Vector3(0, 0, -10);

        RunMenuSetup();

        // Let all suppressed deletions happen (if we came back directly from the editor that was loaded from a save)
        TemporaryLoadedNodeDeleter.Instance.ReleaseAllHolds();

        CheckModFailures();

        // Start this early here to make sure this is ready as soon as possible
        // In the case where patch notes take up the news feed, this is still not a complete waste as if the player
        // exits to the menu after playing a bit they'll see the news feed
        if (Settings.Instance.ThriveNewsFeedEnabled)
        {
            ThriveNewsFeed.GetFeedContents();
        }
    }

    public override void _EnterTree()
    {
        base._EnterTree();

        ThriveopediaManager.Instance.OnPageOpenedHandler += OnThriveopediaOpened;
        Localization.Instance.OnTranslationsChanged += OnTranslationsChanged;
    }

    public override void _ExitTree()
    {
        base._ExitTree();

        ThriveopediaManager.Instance.OnPageOpenedHandler -= OnThriveopediaOpened;
        Localization.Instance.OnTranslationsChanged -= OnTranslationsChanged;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (!introVideoPassed)
        {
            animationTime += delta;

            var fraction = animationTime / AnimationLength;

            camera.GlobalPosition = new Vector3(0, 0, (float)fraction * 410f - 10f - (float)fraction * (float)fraction * 205f);

            if (animationTime >= AnimationLength)
                introVideoPassed = true;
        }

        // Do startup success only after the intro video is played or skipped (and this is the first time in this run
        // that we are in the menu)
        if (introVideoPassed && !IsReturningToMenu)
        {
            if (canShowThanks)
            {
                if (!IsReturningToMenu &&
                    !Settings.Instance.IsNoticePermanentlyDismissed(DismissibleNotice.ThanksForBuying)
                    && !SteamFailed())
                {
                    GD.Print("We are most likely a store version of Thrive, showing the thanks dialog");

                    // The text has link templates, so we need to update the right links into it
                    thanksDialog.DialogText =
                        Localization.Translate("THANKS_FOR_BUYING_THRIVE_2")
                            .FormatSafe(storeBuyLink, storeDisplayName, MainWebsiteLink);

                    thanksDialog.PopupCenteredShrink();
                }

                canShowThanks = false;
            }

            if (timerForStartupSuccess > 0)
            {
                timerForStartupSuccess -= delta;

                if (timerForStartupSuccess <= 0)
                {
                    CheckStartupSuccess();
                    WarnAboutNoEnabledMods();

                    if (SteamFailed())
                    {
                        GD.PrintErr("Steam init has failed, showing failure popup");
                        steamFailedPopup.PopupCenteredShrink();
                    }
                }
            }

            // Low menu performance will never be warned about if the popup has been dismissed,
            // if 3D backgrounds have been disabled, if the popup has been shown but not dismissed
            // on this menu session, or if the max framerate is set to 30 (or lower)
            // In addition, tracking only begins after one second in the menu
            if (!Settings.Instance.IsNoticePermanentlyDismissed(DismissibleNotice.LowPerformanceWarning)
                && Settings.Instance.Menu3DBackgroundEnabled && canShowLowPerformanceWarning
                && (Settings.Instance.MaxFramesPerSecond > 30 || Settings.Instance.MaxFramesPerSecond == 0))
            {
                secondsInMenu += delta;

                // Don't track performance when the 3D background aren't actually visible. For example when going to
                // the art gallery
                if (secondsInMenu >= 1 && created3DBackground?.Visible == true)
                {
                    averageFrameRate = TrackMenuPerformance();

                    WarnAboutLowPerformance();
                }
            }
        }

        // This makes saving seen tutorials work if a tutorial was just closed and the player exited to the main menu
        // before the delayed save was able to trigger
        AlreadySeenTutorials.Process(delta);
    }

    public override void _Notification(int notification)
    {
        base._Notification(notification);

        if (notification == NotificationWMCloseRequest)
        {
            GD.Print("Main window close signal detected");
            Invoke.Instance.Queue(QuitPressed);
        }
    }

    public void StartMusic()
    {
        Jukebox.Instance.PlayCategory("Menu");
    }

    /// <summary>
    ///   Sets the current menu index and then switches the menu
    /// </summary>
    /// <param name="index">Index of the menu. Set to <see cref="uint.MaxValue"/> to hide all menus</param>
    /// <param name="slide">If false then the menu slide animation will not be played</param>
    public void SetCurrentMenu(Control menuToShow, bool hideAll = false)
    {
        thriveLogo.Hide();
        feedPositioner.Hide();

        if (menuArray == null)
            throw new InvalidOperationException("Main menu has not been initialized");

        // Hide stuff
        websiteButtonsContainer.Visible = false;
        feedPositioner.Visible = false;

        foreach (var menu in menus.GetChildren())
        {
            if (menu is Control control)
            {
                if (control == menuToShow && !hideAll)
                {
                    control.Show();

                    if (menuToShow == MainMenuContainer)
                        thriveLogo.Show();

                    if (menuToShow == ExtrasContainer)
                        feedPositioner.Show();
                }
                else
                {
                    control.Hide();
                }
            }
        }
    }

    /// <summary>
    ///   This is when ESC is pressed. Main menu priority is lower than Options Menu
    ///   to avoid capturing ESC presses in the Options Menu.
    /// </summary>
    [RunOnKeyDown("ui_cancel", Priority = Constants.MAIN_MENU_CANCEL_PRIORITY)]
    public bool OnEscapePressed()
    {
        // In a sub menu (that doesn't have its own class)
        if (CurrentMenuIndex != 0 && CurrentMenuIndex < uint.MaxValue)
        {
            SetCurrentMenu(MainMenuContainer);

            // Handled, stop here.
            return true;
        }

        if (CurrentMenuIndex == uint.MaxValue && saves.Visible)
        {
            OnReturnFromLoadGame();
            return true;
        }

        // Not handled, pass through.
        return false;
    }

    /// <summary>
    ///   Setup the main menu.
    /// </summary>
    private void RunMenuSetup()
    {
        guiAnimations = GetNode<AnimationPlayer>("GUIAnimations");
        menuArray?.Clear();

        // Get all the menu items
        menuArray = GetTree().GetNodesInGroup("MenuItem");

        if (menuArray == null)
        {
            GD.PrintErr("Failed to find all the menu items!");
            return;
        }

        options = GetNode<OptionsMenu>("OptionsMenu");
        newGameSettings = GetNode<NewGameSettings>("NewGameSettings");
        saves = GetNode<SaveManagerGUI>("SaveManagerGUI");
        thriveopedia = GetNode<Thriveopedia>("Thriveopedia");

        // Easter egg message
        thriveLogo.RegisterToolTipForControl("thriveLogoEasterEgg", "mainMenu");

        if (FeatureInformation.GetVideoDriver() == OS.RenderingDriver.Opengl3 && !IsReturningToMenu)
            openGlPopup.PopupCenteredShrink();

        UpdateStoreVersionStatus();
        UpdateLauncherState();

        // Hide patch notes when it does not want to be shown
        if (!Settings.Instance.ShowNewPatchNotes)
        {
            patchNotesDisabler.Visible = false;
        }
        else
        {
            ShowPatchInfoIfPossible();
        }
    }
    private void UpdateStoreVersionStatus()
    {
        if (!IsReturningToMenu)
        {
            if (!string.IsNullOrEmpty(LaunchOptions.StoreVersionName))
            {
                GD.Print($"Launcher tells us that we are store version: {LaunchOptions.StoreVersionName}");
            }
        }

        canShowThanks = false;

        if (!string.IsNullOrEmpty(LaunchOptions.StoreVersionName))
        {
            GD.Print("Launcher told us store name: ", LaunchOptions.StoreVersionName);
            canShowThanks = true;

            switch (LaunchOptions.StoreVersionName)
            {
                case "steam":
                    // This is detected separately
                    break;
                case "itch":
                    storeBuyLink = "https://revolutionarygames.itch.io/thrive";
                    storeDisplayName = "itch.io";
                    break;
                default:
                    GD.PrintErr("Unknown store name for link: ", LaunchOptions.StoreVersionName);
                    break;
            }
        }

        if (!SteamHandler.Instance.IsLoaded)
        {
            storeLoggedInDisplay.Visible = false;

            itchButton.Visible = true;
            patreonButton.Visible = true;
        }
        else
        {
            storeLoggedInDisplay.Visible = true;
            UpdateSteamLoginText();

            // This is maybe unnecessary, but this wasn't too difficult to add, so this hiding logic is here
            itchButton.Visible = false;

            // There's probably no problem with showing the Patreon link in the socials section
            patreonButton.Visible = true;

            canShowThanks = true;
            storeBuyLink = "https://store.steampowered.com/app/1779200";
            storeDisplayName = "Steam";
        }
    }

    private bool SteamFailed()
    {
        return SteamHandler.IsTaggedSteamRelease() && !SteamHandler.Instance.IsLoaded;
    }

    private void UpdateSteamLoginText()
    {
        storeLoggedInDisplay.Text = Localization.Translate("STORE_LOGGED_IN_AS")
            .FormatSafe(SteamHandler.Instance.DisplayName);
    }

    private void UpdateLauncherState()
    {
        if (!LaunchOptions.LaunchedThroughLauncher)
        {
            GD.Print("We are not started through the Thrive Launcher");
            exitToLauncherButton.Visible = false;
            return;
        }

        GD.Print("Thrive Launcher started us, launcher hidden: ", LaunchOptions.LaunchingLauncherIsHidden);

        // Exit to launcher button when the user might otherwise have trouble getting back there
        exitToLauncherButton.Visible = LaunchOptions.LaunchingLauncherIsHidden;
    }

    /// <summary>
    ///   Stops any currently playing animation and plays
    ///   the given one instead
    /// </summary>
    private void PlayGUIAnimation(string animation)
    {
        if (guiAnimations.IsPlaying())
            guiAnimations.Stop();

        guiAnimations.Play(animation);
    }

    private void CheckModFailures()
    {
        var errors = ModLoader.Instance.GetAndClearModErrors();

        if (errors.Count > 0)
        {
            modLoadFailures.ExceptionInfo = string.Join("\n", errors);
            modLoadFailures.PopupCenteredShrink();
        }
    }

    private void OnIntroEnded()
    {
        // Start music after the video
        StartMusic();

        // Report to cache that we are in the main menu and that it'd be a good time to clean stuff without affecting
        // game performance
        DiskCache.Instance.InMainMenu();

        // Any lag spike here from GC should not be visible
        GC.Collect();
    }

    private void CheckStartupSuccess()
    {
        if (SafeModeStartupHandler.StartedInSafeMode())
        {
            GD.Print("We started in safe mode");
            safeModeWarning.PopupCenteredShrink();
        }

        SafeModeStartupHandler.ReportGameStartSuccessful();
    }

    /// <summary>
    ///   Updates feed visibilities if settings have been changed
    /// </summary>
    private void UpdateFeedVisibilities()
    {
        var settings = Settings.Instance;

        if (!settings.ShowNewPatchNotes && patchNotesDisabler.Visible)
        {
            patchNotesDisabler.Visible = false;
            newsFeedDisabler.Visible = true;
        }
        else if (settings.ShowNewPatchNotes && !patchNotesDisabler.Visible)
        {
            ShowPatchInfoIfPossible();
        }
    }

    private void ShowPatchInfoIfPossible()
    {
        if (patchNotes.ShowIfNewPatchNotesExist())
        {
            GD.Print("We are playing a new version of Thrive for the first time, showing patch notes");

            // Hide the news when patch notes are visible (and there's something to show there)
            newsFeedDisabler.Visible = false;

            patchNotesDisabler.Visible = true;
        }
        else
        {
            patchNotesDisabler.Visible = false;
        }
    }

    private void WarnAboutNoEnabledMods()
    {
        if (!ModLoader.Instance.HasEnabledMods() && ModLoader.Instance.HasAvailableMods())
        {
            GD.Print("Player has installed mods but no enabled ones, giving a heads up");
            modsInstalledButNotEnabledWarning.PopupIfNotDismissed();
        }
    }

    private double TrackMenuPerformance()
    {
        var currentFrameRate = Engine.GetFramesPerSecond();

        // If this is the first tracked frame, do not use the average of the frame delta and 0
        if (averageFrameRate == 0)
            return currentFrameRate;

        // Not an exact average by any means, but good enough for this purpose
        return (averageFrameRate + currentFrameRate) / 2;
    }

    private void WarnAboutLowPerformance()
    {
        if (averageFrameRate < Constants.MAIN_MENU_LOW_PERFORMANCE_FPS &&
            secondsInMenu >= Constants.MAIN_MENU_LOW_PERFORMANCE_CHECK_AFTER && !AreAnyMenuPopupsOpen() &&
            !options.Visible)
        {
            GD.Print($"Average frame rate is {averageFrameRate}, prompting to disable 3D backgrounds");
            lowPerformanceWarning.PopupIfNotDismissed();
            canShowLowPerformanceWarning = false;
        }
    }

    private void OnLowPerformanceDialogConfirmed()
    {
        Settings.Instance.Menu3DBackgroundEnabled.Value = false;
        Settings.Instance.Save();
    }

    /// <summary>
    ///   True when any popup that appears in the menu is currently displayed.
    /// </summary>
    private bool AreAnyMenuPopupsOpen()
    {
        return openGlPopup.Visible || modLoadFailures.Visible || steamFailedPopup.Visible || safeModeWarning.Visible
            || modsInstalledButNotEnabledWarning.Visible || thanksDialog.Visible || lowPerformanceWarning.Visible;
    }

    private void NewGamePressed()
    {
        GUICommon.Instance.PlayButtonPressSound();

        // Hide all the other menus
        SetCurrentMenu(MainMenuContainer, true);

        // Show the options
        newGameSettings.OpenFromMainMenu();
    }

    private void ToolsPressed()
    {
        GUICommon.Instance.PlayButtonPressSound();
        SetCurrentMenu(ToolsContainer);
    }

    private void ExtrasPressed()
    {
        GUICommon.Instance.PlayButtonPressSound();
        SetCurrentMenu(ExtrasContainer);
    }

    private void FreebuildEditorPressed()
    {
        GUICommon.Instance.PlayButtonPressSound();

        // Disable the button to prevent it being executed again.
        freebuildButton.Disabled = true;

        TransitionManager.Instance.AddSequence(ScreenFade.FadeType.FadeOut, 0.1f, () =>
        {
            OnEnteringGame();

            // Instantiate a new editor scene
            var editor = (MicrobeEditor)SceneManager.Instance.LoadScene(MainGameState.MicrobeEditor).Instantiate();

            // Start freebuild game
            editor.CurrentGame = GameProperties.StartNewMicrobeGame(new WorldGenerationSettings(), true);

            // Switch to the editor scene
            SceneManager.Instance.SwitchToScene(editor);
        }, false);
    }

    private void MulticellularFreebuildEditorPressed()
    {
        GUICommon.Instance.PlayButtonPressSound();

        // Disable the button to prevent it being executed again.
        multicellularFreebuildButton.Disabled = true;

        TransitionManager.Instance.AddSequence(ScreenFade.FadeType.FadeOut, 0.1f, () =>
        {
            OnEnteringGame();

            // Instantiate a new editor scene
            var editor = (MulticellularEditor)SceneManager.Instance
                .LoadScene(MainGameState.MulticellularEditor).Instantiate();

            // Start freebuild game
            editor.CurrentGame = GameProperties.StartNewMulticellularGame(new WorldGenerationSettings(), true);

            // Switch to the editor scene
            SceneManager.Instance.SwitchToScene(editor);
        }, false);
    }

    private void OnAutoEvoExploringPressed()
    {
        GUICommon.Instance.PlayButtonPressSound();

        autoEvoExploringButton.Disabled = true;

        TransitionManager.Instance.AddSequence(ScreenFade.FadeType.FadeOut, 0.1f,
            () => { SceneManager.Instance.SwitchToScene("res://src/auto-evo/AutoEvoExploringTool.tscn"); }, false);
    }

    // TODO: this is now used by another sub menu as well so renaming this to be more generic would be good
    private void BackFromToolsPressed()
    {
        GUICommon.Instance.PlayButtonPressSound();
        SetCurrentMenu(MainMenuContainer);
    }

    private void ViewSourceCodePressed()
    {
        GUICommon.Instance.PlayButtonPressSound();
        OS.ShellOpen("https://github.com/Revolutionary-Games/Thrive");
    }

    private void QuitPressed()
    {
        SceneManager.Instance.QuitThrive();
    }

    private void QuitToLauncherPressed()
    {
        GD.Print("Exit to launcher pressed");

        // Output a special message which the launcher should detect
        GD.Print(ThriveLauncherSharedConstants.REQUEST_LAUNCHER_OPEN);

        // To make sure this always works even with buffering, output this as an error
        GD.PrintErr("Printing request as \"error\" to ensure it isn't buffered:");
        GD.PrintErr(ThriveLauncherSharedConstants.REQUEST_LAUNCHER_OPEN);

        // Probably unnecessary, but we exit with a delay here
        Invoke.Instance.Queue(QuitPressed);
    }

    private void OptionsPressed()
    {
        GUICommon.Instance.PlayButtonPressSound();

        // Hide all the other menus
        SetCurrentMenu(MainMenuContainer, true);

        // Show the options
        options.OpenFromMainMenu();
    }

    private void OnReturnFromOptions()
    {
        options.Visible = false;
        SetCurrentMenu(MainMenuContainer);

        // In case news settings are changed, update that state
        UpdateFeedVisibilities();
        newsFeed.CheckStartFetchNews();
    }

    private void OnReturnFromNewGameSettings()
    {
        newGameSettings.Visible = false;

        SetCurrentMenu(MainMenuContainer);
    }

    private void OnRedirectedToOptionsMenuFromNewGameSettings()
    {
        OnReturnFromNewGameSettings();
        OptionsPressed();
        options.SelectOptionsTab(OptionsMenu.OptionsTab.Performance);
    }

    private void OnReturnFromThriveopedia()
    {
        thriveopedia.Visible = false;
        SetCurrentMenu(MainMenuContainer);
    }

    private void LoadGamePressed()
    {
        GUICommon.Instance.PlayButtonPressSound();

        // Hide all the other menus
        SetCurrentMenu(MainMenuContainer, true);

        // Show the options
        saves.Visible = true;
    }

    private void OnReturnFromLoadGame()
    {
        saves.Visible = false;
        SetCurrentMenu(MainMenuContainer);
    }

    private void CreditsPressed()
    {
        GUICommon.Instance.PlayButtonPressSound();

        // Hide all the other menus
        SetCurrentMenu(MainMenuContainer, true);

        // Show the credits view
        credits.Restart();
        creditsContainer.Visible = true;
    }

    private void OnReturnFromCredits()
    {
        creditsContainer.Visible = false;
        credits.Pause();

        SetCurrentMenu(MainMenuContainer);
    }

    private void ThriveopediaPressed()
    {
        GUICommon.Instance.PlayButtonPressSound();

        // Hide all the other menus
        SetCurrentMenu(MainMenuContainer, true);

        // Show the Thriveopedia
        thriveopedia.OpenFromMainMenu();
    }

    private void VisitSuggestionsSitePressed()
    {
        GUICommon.Instance.PlayButtonPressSound();
        OS.ShellOpen("https://suggestions.revolutionarygamesstudio.com/");
    }

    private void LicensesPressed()
    {
        GUICommon.Instance.PlayButtonPressSound();

        // Hide all the other menus
        SetCurrentMenu(MainMenuContainer, true);

        // Show the licenses view
        licensesDisplay.PopupCenteredShrink();
    }

    private void OnReturnFromLicenses()
    {
        SetCurrentMenu(ExtrasContainer, false);
    }

    private void ModsPressed()
    {
        GUICommon.Instance.PlayButtonPressSound();

        // Hide all the other menus
        SetCurrentMenu(MainMenuContainer, true);

        // Show the mods view
        modManager.Visible = true;
    }

    private void OnReturnFromMods()
    {
        modManager.Visible = false;
        SetCurrentMenu(MainMenuContainer);
    }

    private void ArtGalleryPressed()
    {
        GUICommon.Instance.PlayButtonPressSound();
        SetCurrentMenu(MainMenuContainer, true);
        galleryViewer.OpenFullRect();
        Jukebox.Instance.PlayCategory("ArtGallery");

        if (created3DBackground != null)
        {
            // Hide the 3D background while in the gallery as it is a fullscreen popup and rendering the expensive 3D
            // scene underneath it is not the best
            created3DBackground.Visible = false;
        }
    }

    private void OnReturnFromArtGallery()
    {
        SetCurrentMenu(ExtrasContainer);
        Jukebox.Instance.PlayCategory("Menu");

        if (created3DBackground != null)
        {
            created3DBackground.Visible = true;
        }

        ResetPerformanceTracking();
    }

    private void OnWebsitesButtonPressed()
    {
        websiteButtonsContainer.OpenModal();
    }

    private void OnSocialMediaButtonPressed(string url)
    {
        GD.Print($"Opening social link: {url}");
        OS.ShellOpen(url);
    }

    private void BenchmarksPressed()
    {
        GUICommon.Instance.PlayButtonPressSound();

        SetCurrentMenu(BenchmarkContainer, true);
    }

    private void OnReturnFromBenchmarks()
    {
        GUICommon.Instance.PlayButtonPressSound();

        SetCurrentMenu(ToolsContainer, true);
    }

    private void MicrobeBenchmarkPressed()
    {
        GUICommon.Instance.PlayButtonPressSound();

        microbeBenchmarkButton.Disabled = true;

        TransitionManager.Instance.AddSequence(ScreenFade.FadeType.FadeOut, 0.1f,
            () => { SceneManager.Instance.SwitchToScene("res://src/benchmark/microbe/MicrobeBenchmark.tscn"); }, false);
    }

    private void CloudBenchmarkPressed()
    {
        GUICommon.Instance.PlayButtonPressSound();

        microbeBenchmarkButton.Disabled = true;

        TransitionManager.Instance.AddSequence(ScreenFade.FadeType.FadeOut, 0.1f,
            () => { SceneManager.Instance.SwitchToScene("res://src/benchmark/microbe/CloudBenchmark.tscn"); }, false);
    }

    private void OnNewGameIntroVideoStarted()
    {
        if (created3DBackground != null)
        {
            // Hide the background again when playing a video as the 3D backgrounds are performance intensive
            created3DBackground.Visible = false;
        }
    }

    private void OnThriveopediaOpened(string pageName)
    {
        thriveopedia.OpenFromMainMenu();
        thriveopedia.ChangePage(pageName);
    }

    private void ResetPerformanceTracking()
    {
        secondsInMenu = 0;
        averageFrameRate = 0;
    }

    private void OnTranslationsChanged()
    {
        if (SteamHandler.Instance.IsLoaded)
        {
            UpdateSteamLoginText();
        }
    }
}
