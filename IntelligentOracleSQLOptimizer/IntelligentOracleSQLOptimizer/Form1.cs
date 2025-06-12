using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace IntelligentOracleSQLOptimizer
{
    public partial class Form1 : Form
    {
        #region InitializeIcon

        public void InitializeIcon()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "IntelligentOracleSQLOptimizer.Resources.application.ico";
            if (assembly != null)
            {
                using Stream? stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    this.Icon = new Icon(stream);
                }
            }
        }

        #endregion

        public readonly IConfiguration Configuration;

        public Form1()
        {
            InitializeIcon();
            InitializeComponent();
            this.CreateHandle();
            Configuration = Program.Services!.GetRequiredService<IConfiguration>();
            ChangeToken.OnChange(() => Configuration.GetReloadToken(), OnChange);
            OnChange();
        }

        private void OnChange()
        {
            this.Invoke((MethodInvoker)delegate { this.Text = Configuration.GetSection("Settings:Subkey1:Value1").Get<string>(); });
        }
    }
}