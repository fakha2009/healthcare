using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace HealthcareSanatoriumInterface
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string dbPath = ResolveDatabasePath(baseDir);
            string assemblyPath = Path.Combine(baseDir, "HealthcareModules.dll");

            if (args.Length > 0)
            {
                if (args[0] == "--self-test")
                {
                    RunWithExitCode(() => RunSelfTest(assemblyPath, dbPath));
                    return;
                }

                if (args[0] == "--pdf-self-test")
                {
                    RunWithExitCode(() => RunPdfSelfTest(assemblyPath, dbPath));
                    return;
                }

                if (args[0] == "--generate-manual-pdf")
                {
                    string target = args.Length > 1 ? args[1] : Path.Combine(baseDir, "UserGuide.pdf");
                    RunWithExitCode(() => GenerateManualPdf(assemblyPath, target));
                    return;
                }

                if (args[0].StartsWith("--generate-manual-pdf=", StringComparison.OrdinalIgnoreCase))
                {
                    string target = args[0].Substring("--generate-manual-pdf=".Length);
                    RunWithExitCode(() => GenerateManualPdf(assemblyPath, target));
                    return;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Show splash immediately — before the slow DLL load + form construction
            Form splash = CreateHostSplash();
            splash.Show();
            Application.DoEvents();

            Form form = null;
            try
            {
                form = CreateMainForm(assemblyPath, dbPath);
            }
            catch
            {
                splash.Close();
                splash.Dispose();
                throw;
            }

            // Close splash as soon as the main form finishes loading
            form.Load += delegate { splash.Close(); splash.Dispose(); };
            Application.Run(form);
            form.Dispose();
        }

        private static int RunSelfTest(string assemblyPath, string dbPath)
        {
            try
            {
                int result = InvokeIntMethod(assemblyPath, "PluginBootstrap", "RunSelfTest", dbPath);
                TryDeleteFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "self-test-error.txt"));
                return result;
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "self-test-error.txt"), ex.ToString());
                return 1;
            }
        }

        private static int RunPdfSelfTest(string assemblyPath, string dbPath)
        {
            try
            {
                int result = InvokeIntMethod(assemblyPath, "PluginBootstrap", "RunPdfSelfTest", dbPath);
                TryDeleteFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pdf-self-test-error.txt"));
                return result;
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pdf-self-test-error.txt"), ex.ToString());
                return 1;
            }
        }

        private static void GenerateManualPdf(string assemblyPath, string targetPath)
        {
            InvokeVoidMethod(assemblyPath, "PluginBootstrap", "GenerateManualPdf", targetPath);
        }

        private static Form CreateMainForm(string assemblyPath, string dbPath)
        {
            return (Form)InvokeMethod(assemblyPath, "PluginBootstrap", "CreateMainForm", dbPath);
        }

        // Lightweight splash that needs NO DLL — shows before Assembly.LoadFrom
        private static Form CreateHostSplash()
        {
            Form f = new Form();
            f.FormBorderStyle = FormBorderStyle.None;
            f.StartPosition = FormStartPosition.CenterScreen;
            f.Size = new System.Drawing.Size(420, 160);
            f.BackColor = System.Drawing.Color.FromArgb(16, 22, 36);
            f.ShowInTaskbar = false;
            f.TopMost = true;

            Label title = new Label();
            title.Text = "Система здравоохранения";
            title.ForeColor = System.Drawing.Color.FromArgb(220, 232, 250);
            title.Font = new System.Drawing.Font("Segoe UI", 16f, System.Drawing.FontStyle.Bold);
            title.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            title.Dock = DockStyle.Top;
            title.Height = 70;

            Label status = new Label();
            status.Text = "Запуск приложения...";
            status.ForeColor = System.Drawing.Color.FromArgb(90, 120, 160);
            status.Font = new System.Drawing.Font("Segoe UI", 9f);
            status.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            status.Dock = DockStyle.Top;
            status.Height = 28;

            Panel bar = new Panel();
            bar.Dock = DockStyle.Top;
            bar.Height = 4;
            bar.BackColor = System.Drawing.Color.FromArgb(35, 52, 78);

            Panel fill = new Panel();
            fill.BackColor = System.Drawing.Color.FromArgb(74, 144, 255);
            fill.Width = 0;
            fill.Height = 4;
            bar.Controls.Add(fill);

            // Animate the progress bar
            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
            t.Interval = 30;
            t.Tick += delegate
            {
                if (fill.Width < bar.Width)
                    fill.Width = Math.Min(fill.Width + 6, bar.Width);
            };
            f.Shown += delegate { t.Start(); };
            f.FormClosed += delegate { t.Stop(); t.Dispose(); };

            f.Controls.Add(bar);
            f.Controls.Add(status);
            f.Controls.Add(title);
            return f;
        }

        private static string ResolveDatabasePath(string baseDir)
        {
            string[] candidates = new[]
            {
                Path.Combine(baseDir, "HealthcareSanatoriumSystem.accdb"),
                Path.Combine(baseDir, "HealthcareSanatoriumSystem_FIXED.accdb"),
                Path.Combine(baseDir, "HealthcareSanatoriumSystem.mdb"),
                Path.Combine(baseDir, "db.accdb"),
                Path.Combine(baseDir, "assest", "db.accdb")
            };

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return Path.Combine(baseDir, "HealthcareSanatoriumSystem.accdb");
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private static void RunWithExitCode(Func<int> action)
        {
            Environment.Exit(action());
        }

        private static void RunWithExitCode(Action action)
        {
            action();
            Environment.Exit(0);
        }

        private static int InvokeIntMethod(string assemblyPath, string typeName, string methodName, params object[] args)
        {
            return (int)InvokeMethod(assemblyPath, typeName, methodName, args);
        }

        private static void InvokeVoidMethod(string assemblyPath, string typeName, string methodName, params object[] args)
        {
            InvokeMethod(assemblyPath, typeName, methodName, args);
        }

        private static object InvokeMethod(string assemblyPath, string typeName, string methodName, params object[] args)
        {
            Assembly assembly = Assembly.LoadFrom(assemblyPath);
            Type pluginType = assembly.GetType("HealthcareSanatoriumInterface." + typeName, throwOnError: true);
            MethodInfo method = pluginType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                throw new MissingMethodException(pluginType.FullName, methodName);
            }

            return method.Invoke(null, args);
        }
    }
}
