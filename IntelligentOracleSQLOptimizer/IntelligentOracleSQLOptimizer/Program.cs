using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime;
using System.Threading;
using System.Windows.Forms;

namespace IntelligentOracleSQLOptimizer
{
    internal static class Program
    {
        private static IServiceProvider? services;

        public static IServiceProvider? Services { get => services; set => services = value; }

        public static IHost? host;

        private static void ConfigureAppConfiguration(HostBuilderContext context, IConfigurationBuilder builder)
        {
            var config = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: true, reloadOnChange: true).Build();
            builder.AddConfiguration(config);
        }

        private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            services.AddSingleton<Form1>();
        }

        private static void ConfigureLogging(HostBuilderContext context, ILoggingBuilder builder)
        {
            builder.ClearProviders();
            builder.AddConsole();
        }

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static async Task Main()
        {
            System.Runtime.ProfileOptimization.SetProfileRoot(AppDomain.CurrentDomain.BaseDirectory);
            System.Runtime.ProfileOptimization.StartProfile($@"{System.Reflection.Assembly.GetAssembly(typeof(Program))!.GetName().Name}.profile");

            bool createdNew;
            var appName = System.Reflection.Assembly.GetAssembly(typeof(Program))!.GetName().Name;
            Mutex m = new Mutex(true, appName, out createdNew);
            if (!createdNew)
            {
                MessageBox.Show($"{appName} is already running!", "Multiple Instances not supported.", MessageBoxButtons.OK ,  MessageBoxIcon.Error);
                return;
            }

            host = new HostBuilder()
                .ConfigureServices(ConfigureServices)
                .ConfigureAppConfiguration(ConfigureAppConfiguration)
                .ConfigureLogging(ConfigureLogging).Build();
            Services = host.Services;
            await host.StartAsync();

            Application.ApplicationExit += Application_ApplicationExit;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(Services!.GetRequiredService<Form1>());
        }

        private static async void Application_ApplicationExit(object? sender, EventArgs e)
        {
            await host!.StopAsync();
        }
    }
}