using System;
using System.Net;
using Gtk;
using TraceLog;
using Translations;
using XDM.Core;
using XDM.Core.DataAccess;
using XDMApp = XDM.Core.Application;
using System.Linq;
using XDM.Core.BrowserMonitoring;
using XDM.Core.Util;

namespace XDM.GtkUI
{
    class Program
    {
        private const string DisableCachingName = @"TestSwitch.LocalAppContext.DisableCaching";
        private const string DontEnableSchUseStrongCryptoName = @"Switch.System.Net.DontEnableSchUseStrongCrypto";

        static void Main(string[] args)
        {
            Config.LoadConfig();
            var debugMode = Environment.GetEnvironmentVariable("XDM_DEBUG_MODE");
            if (!string.IsNullOrEmpty(debugMode) && debugMode == "1")
            {
                var logFile = System.IO.Path.Combine(Config.AppDir, "log.txt");
                Log.InitFileBasedTrace(System.IO.Path.Combine(Config.AppDir, "log.txt"));
            }
            Log.Debug("Application_Startup");
            Environment.SetEnvironmentVariable("GTK_USE_PORTAL", "1");
            Gtk.Application.Init("xdm-app", ref args);
            GLib.ExceptionManager.UnhandledException += ExceptionManager_UnhandledException;
            var globalStyleSheet = @"
                                    .large-font{ font-size: 16px; }
                                    .medium-font{ font-size: 14px; }
                                    ";

            var screen = Gdk.Screen.Default;
            var provider = new CssProvider();
            provider.LoadFromData(globalStyleSheet);
            Gtk.StyleContext.AddProviderForScreen(screen, provider, 800);
            //var screen = Gdk.Screen.Default;
            //var provider = new CssProvider();
            //provider.LoadFromData(@".dark 
            //                                    {
            //                                        color: gray;
            //                                        background: rgb(36,41,46);
            //                                    }

            //                                    treeview.view :selected 
            //                                    {
            //                                        background-color: rgb(10,106,182);
            //                                        color: white;
            //                                    }
            //.listt
            //{
            //font-family: Segoe UI;
            //}
            //                                    .dark2
            //                                    {
            //                                        color: gray;
            //                                        background: rgb(35,35,35);
            //                                        /*background: rgb(36,41,46);*/
            //                                    }
            //                                    .toolbar-border-dark
            //                                    {  
            //                                        border-bottom: 1px solid rgb(20,20,20);
            //                                    }
            //                                    .toolbar-border-light
            //                                    {  
            //                                        border-bottom: 2px solid rgb(240,240,240);
            //                                    }
            //                                  ");
            //Gtk.StyleContext.AddProviderForScreen(screen, provider, 800);

            // Certificates are validated by default. Only bypass validation when the user
            // explicitly opts in via XDM_ALLOW_INSECURE_SSL=1 (e.g. TLS-intercepting proxy).
            if (PlatformHelper.AllowInsecureSsl)
            {
                ServicePointManager.ServerCertificateValidationCallback += (a, b, c, d) => true;
            }
            ServicePointManager.DefaultConnectionLimit = 100;

            ServicePointManager.SecurityProtocol = SecurityProtocolType.SystemDefault;

            AppContext.SetSwitch(DisableCachingName, true);
            AppContext.SetSwitch(DontEnableSchUseStrongCryptoName, true);

            Log.Debug("Loading languages...");

            LoadLanguageTexts();

            if (Config.Instance.AllowSystemDarkTheme)
            {
                Gtk.Settings.Default.ThemeName = "Adwaita";
                Gtk.Settings.Default.ApplicationPreferDarkTheme = true;
            }

            var core = new ApplicationCore();
            var app = new XDMApp();
            var win = new MainWindow();

            Log.Debug("Configuring app context...");

            ApplicationContext.FirstRunCallback += ApplicationContext_FirstRunCallback;
            ApplicationContext.Configurer()
                .RegisterApplicationWindow(win)
                .RegisterApplication(app)
                .RegisterApplicationCore(core)
                .RegisterCapturedVideoTracker(new VideoTracker())
                .RegisterClipboardMonitor(new ClipboardMonitor())
                .RegisterLinkRefresher(new LinkRefresher())
                .RegisterPlatformUIService(new GtkPlatformUIService())
                .Configure();

            Log.Debug("Processing arguments...");

            ArgsProcessor.Process(args);

            // Register a real Gtk.Application (GApplication) instead of relying on
            // the legacy static Gtk.Application.Init/Run pair alone. Startup crash
            // reports on GTK 3.24.51+ implicate GTK window/application plumbing
            // running without a registered application; giving GTK a registered
            // application with the main window added is the documented GtkSharp
            // pattern and removes that failure mode.
            // This runs AFTER the SingleInstance handshake above on purpose: a
            // second instance forwards its args over HTTP and exits before ever
            // touching D-Bus (contacting the bus as a remote instance and then
            // tearing down segfaults). The main loop intentionally stays on the
            // static Gtk.Application.Run() (gtk_main): switching to
            // g_application_run would change quit lifecycle semantics.
            // NOTE: the ID must be valid D-Bus reverse-DNS ("xdm-app" is rejected
            // by gtk_application_new); the bus name is cosmetic here since we never
            // enter g_application_run.
            var gtkApp = new Gtk.Application("com.xtremedownloadmanager.xdm", GLib.ApplicationFlags.None);
            try
            {
                gtkApp.Register(null);
                gtkApp.AddWindow(win);
            }
            catch (Exception ex)
            {
                // No session bus (containers, minimal envs): fall back to the
                // legacy path rather than failing startup.
                Log.Debug(ex, "Gtk.Application registration failed, continuing without it");
            }

            Log.Debug("Gtk Run...");

            Gtk.Application.Run();
        }

        private static void ApplicationContext_FirstRunCallback(object? sender, EventArgs e)
        {
            PlatformHelper.EnableAutoStart(true);
        }

        private static void ExceptionManager_UnhandledException(GLib.UnhandledExceptionArgs args)
        {
            Log.Debug("GLib ExceptionManager_UnhandledException: " + args.ExceptionObject);
            args.ExitApplication = false;
        }

        private static void LoadLanguageTexts()
        {
            Log.Debug("Language loading ...");
            try
            {
                var indexFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Lang\index.txt");
                if (System.IO.File.Exists(indexFile))
                {
                    var lines = System.IO.File.ReadAllLines(indexFile);
                    foreach (var line in lines)
                    {
                        var index = line.IndexOf("=");
                        if (index > 0)
                        {
                            var name = line.Substring(0, index);
                            var value = line.Substring(index + 1);
                            if (name == Config.Instance.Language)
                            {
                                TextResource.Load(value);
                                break;
                            }
                        }
                    }
                }
                Log.Debug("Language loaded.");
            }
            catch (Exception ex)
            {
                Log.Debug(ex, ex.Message);
            }
        }
    }
}
