namespace IntelligentOracleSQLOptimizer
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.hostLabel = new System.Windows.Forms.Label();
            this.hostTextBox = new System.Windows.Forms.TextBox();
            this.userLabel = new System.Windows.Forms.Label();
            this.userTextBox = new System.Windows.Forms.TextBox();
            this.passwordLabel = new System.Windows.Forms.Label();
            this.passwordTextBox = new System.Windows.Forms.TextBox();
            this.procedureLabel = new System.Windows.Forms.Label();
            this.procedureBodyTextBox = new System.Windows.Forms.TextBox();
            this.analyzeButton = new System.Windows.Forms.Button();
            this.resultsTabControl = new System.Windows.Forms.TabControl();
            this.optimizedProcedureTab = new System.Windows.Forms.TabPage();
            this.optimizedProcedureTextBox = new System.Windows.Forms.TextBox();
            this.reportTab = new System.Windows.Forms.TabPage();
            this.reportTextBox = new System.Windows.Forms.TextBox();
            this.performanceTab = new System.Windows.Forms.TabPage();
            this.performanceLabel = new System.Windows.Forms.Label();
            this.statusBar = new System.Windows.Forms.StatusBar();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel(); // This will be added to statusBar later

            this.resultsTabControl.SuspendLayout();
            this.optimizedProcedureTab.SuspendLayout();
            this.reportTab.SuspendLayout();
            this.performanceTab.SuspendLayout();
            this.SuspendLayout();
            //
            // hostLabel
            //
            this.hostLabel.AutoSize = true;
            this.hostLabel.Location = new System.Drawing.Point(12, 15);
            this.hostLabel.Name = "hostLabel";
            this.hostLabel.Size = new System.Drawing.Size(32, 13);
            this.hostLabel.TabIndex = 0;
            this.hostLabel.Text = "Host:";
            //
            // hostTextBox
            //
            this.hostTextBox.Location = new System.Drawing.Point(80, 12);
            this.hostTextBox.Name = "hostTextBox";
            this.hostTextBox.Size = new System.Drawing.Size(200, 20);
            this.hostTextBox.TabIndex = 1;
            this.hostTextBox.Text = "dev5-mer-db:1521/TCTN_MASTER";
            this.hostTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            //
            // userLabel
            //
            this.userLabel.AutoSize = true;
            this.userLabel.Location = new System.Drawing.Point(12, 41);
            this.userLabel.Name = "userLabel";
            this.userLabel.Size = new System.Drawing.Size(32, 13);
            this.userLabel.TabIndex = 2;
            this.userLabel.Text = "User:";
            //
            // userTextBox
            //
            this.userTextBox.Location = new System.Drawing.Point(80, 38);
            this.userTextBox.Name = "userTextBox";
            this.userTextBox.Size = new System.Drawing.Size(200, 20);
            this.userTextBox.TabIndex = 3;
            this.userTextBox.Text = "cisconvert";
            this.userTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            //
            // passwordLabel
            //
            this.passwordLabel.AutoSize = true;
            this.passwordLabel.Location = new System.Drawing.Point(12, 67);
            this.passwordLabel.Name = "passwordLabel";
            this.passwordLabel.Size = new System.Drawing.Size(56, 13);
            this.passwordLabel.TabIndex = 4;
            this.passwordLabel.Text = "Password:";
            //
            // passwordTextBox
            //
            this.passwordTextBox.Location = new System.Drawing.Point(80, 64);
            this.passwordTextBox.Name = "passwordTextBox";
            this.passwordTextBox.PasswordChar = '*';
            this.passwordTextBox.Size = new System.Drawing.Size(200, 20);
            this.passwordTextBox.TabIndex = 5;
            this.passwordTextBox.Text = "cisconvert";
            this.passwordTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            //
            // procedureLabel
            //
            this.procedureLabel.AutoSize = true;
            this.procedureLabel.Location = new System.Drawing.Point(12, 93);
            this.procedureLabel.Name = "procedureLabel";
            this.procedureLabel.Size = new System.Drawing.Size(83, 13);
            this.procedureLabel.TabIndex = 6;
            this.procedureLabel.Text = "Procedure Body:";
            //
            // procedureBodyTextBox
            //
            this.procedureBodyTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.procedureBodyTextBox.Location = new System.Drawing.Point(15, 109);
            this.procedureBodyTextBox.Multiline = true;
            this.procedureBodyTextBox.Name = "procedureBodyTextBox";
            this.procedureBodyTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.procedureBodyTextBox.Size = new System.Drawing.Size(757, 200);
            this.procedureBodyTextBox.TabIndex = 7;
            //
            // analyzeButton
            //
            this.analyzeButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.analyzeButton.Location = new System.Drawing.Point(657, 315);
            this.analyzeButton.Name = "analyzeButton";
            this.analyzeButton.Size = new System.Drawing.Size(115, 23);
            this.analyzeButton.TabIndex = 8;
            this.analyzeButton.Text = "Analyze & Optimize";
            this.analyzeButton.UseVisualStyleBackColor = true;
            //
            // resultsTabControl
            //
            this.resultsTabControl.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.resultsTabControl.Controls.Add(this.optimizedProcedureTab);
            this.resultsTabControl.Controls.Add(this.reportTab);
            this.resultsTabControl.Controls.Add(this.performanceTab);
            this.resultsTabControl.Location = new System.Drawing.Point(15, 344);
            this.resultsTabControl.Name = "resultsTabControl";
            this.resultsTabControl.SelectedIndex = 0;
            this.resultsTabControl.Size = new System.Drawing.Size(757, 250);
            this.resultsTabControl.TabIndex = 9;
            //
            // optimizedProcedureTab
            //
            this.optimizedProcedureTab.Controls.Add(this.optimizedProcedureTextBox);
            this.optimizedProcedureTab.Location = new System.Drawing.Point(4, 22);
            this.optimizedProcedureTab.Name = "optimizedProcedureTab";
            this.optimizedProcedureTab.Padding = new System.Windows.Forms.Padding(3);
            this.optimizedProcedureTab.Size = new System.Drawing.Size(749, 224);
            this.optimizedProcedureTab.TabIndex = 0;
            this.optimizedProcedureTab.Text = "Optimized Procedure";
            this.optimizedProcedureTab.UseVisualStyleBackColor = true;
            //
            // optimizedProcedureTextBox
            //
            this.optimizedProcedureTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.optimizedProcedureTextBox.Location = new System.Drawing.Point(3, 3);
            this.optimizedProcedureTextBox.Multiline = true;
            this.optimizedProcedureTextBox.Name = "optimizedProcedureTextBox";
            this.optimizedProcedureTextBox.ReadOnly = true;
            this.optimizedProcedureTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.optimizedProcedureTextBox.Size = new System.Drawing.Size(743, 218);
            this.optimizedProcedureTextBox.TabIndex = 0;
            //
            // reportTab
            //
            this.reportTab.Controls.Add(this.reportTextBox);
            this.reportTab.Location = new System.Drawing.Point(4, 22);
            this.reportTab.Name = "reportTab";
            this.reportTab.Padding = new System.Windows.Forms.Padding(3);
            this.reportTab.Size = new System.Drawing.Size(749, 224);
            this.reportTab.TabIndex = 1;
            this.reportTab.Text = "Gemini Report";
            this.reportTab.UseVisualStyleBackColor = true;
            //
            // reportTextBox
            //
            this.reportTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.reportTextBox.Location = new System.Drawing.Point(3, 3);
            this.reportTextBox.Multiline = true;
            this.reportTextBox.Name = "reportTextBox";
            this.reportTextBox.ReadOnly = true;
            this.reportTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.reportTextBox.Size = new System.Drawing.Size(743, 218);
            this.reportTextBox.TabIndex = 0;
            //
            // performanceTab
            //
            this.performanceTab.Controls.Add(this.performanceLabel);
            this.performanceTab.Location = new System.Drawing.Point(4, 22);
            this.performanceTab.Name = "performanceTab";
            this.performanceTab.Padding = new System.Windows.Forms.Padding(3);
            this.performanceTab.Size = new System.Drawing.Size(749, 224);
            this.performanceTab.TabIndex = 2;
            this.performanceTab.Text = "Performance";
            this.performanceTab.UseVisualStyleBackColor = true;
            //
            // performanceLabel
            //
            this.performanceLabel.AutoSize = true;
            this.performanceLabel.Location = new System.Drawing.Point(6, 3);
            this.performanceLabel.Name = "performanceLabel";
            this.performanceLabel.Size = new System.Drawing.Size(182, 13);
            this.performanceLabel.TabIndex = 0;
            this.performanceLabel.Text = "Performance results will appear here.";
            //
            // statusBar
            //
            this.statusBar.Location = new System.Drawing.Point(0, 600); // Adjusted Y for screen size
            this.statusBar.Name = "statusBar";
            this.statusBar.Size = new System.Drawing.Size(784, 22);
            this.statusBar.TabIndex = 10;
            this.statusBar.Text = "statusBar";
            // For ToolStripStatusLabel, it's typically added to a StatusStrip, not a legacy StatusBar.
            // If a StatusStrip is intended:
            // this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            // this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            // this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.statusLabel });
            // this.statusStrip1.Location = new System.Drawing.Point(0, 578);
            // this.statusStrip1.Name = "statusStrip1";
            // this.statusStrip1.Size = new System.Drawing.Size(784, 22);
            // this.statusStrip1.TabIndex = 10;
            // this.statusStrip1.Text = "statusStrip1";
            // this.statusLabel.Name = "statusLabel";
            // this.statusLabel.Size = new System.Drawing.Size(39, 17);
            // this.statusLabel.Text = "Ready";
            // For legacy StatusBar, panels are used. Let's assume StatusStrip for modern ToolStripStatusLabel.
            // The subtask asks for StatusBar and ToolStripStatusLabel. This is a bit of a mix.
            // For now, I will define statusLabel, but adding it to a legacy StatusBar requires StatusBarPanel.
            // Let's assume the user means a StatusStrip or wants the ToolStripStatusLabel for later use with a StatusStrip.
            // I will proceed by initializing ToolStripStatusLabel but not explicitly adding it to the legacy StatusBar.
            // If a StatusStrip is actually required, the .csproj might need a reference to System.Design.
            this.statusLabel.Name = "statusLabel"; // Defined, but not added to legacy statusBar directly.
            this.statusLabel.Text = "Ready";

            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 622); // Adjusted for content
            this.Controls.Add(this.resultsTabControl);
            this.Controls.Add(this.analyzeButton);
            this.Controls.Add(this.procedureBodyTextBox);
            this.Controls.Add(this.procedureLabel);
            this.Controls.Add(this.passwordTextBox);
            this.Controls.Add(this.passwordLabel);
            this.Controls.Add(this.userTextBox);
            this.Controls.Add(this.userLabel);
            this.Controls.Add(this.hostTextBox);
            this.Controls.Add(this.hostLabel);
            this.Controls.Add(this.statusBar); // Add legacy StatusBar
            this.Name = "MainForm";
            this.Text = "Intelligent Oracle SQL Optimizer";
            this.resultsTabControl.ResumeLayout(false);
            this.optimizedProcedureTab.ResumeLayout(false);
            this.optimizedProcedureTab.PerformLayout();
            this.reportTab.ResumeLayout(false);
            this.reportTab.PerformLayout();
            this.performanceTab.ResumeLayout(false);
            this.performanceTab.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label hostLabel;
        private System.Windows.Forms.TextBox hostTextBox;
        private System.Windows.Forms.Label userLabel;
        private System.Windows.Forms.TextBox userTextBox;
        private System.Windows.Forms.Label passwordLabel;
        private System.Windows.Forms.TextBox passwordTextBox;
        private System.Windows.Forms.Label procedureLabel;
        private System.Windows.Forms.TextBox procedureBodyTextBox;
        private System.Windows.Forms.Button analyzeButton;
        private System.Windows.Forms.TabControl resultsTabControl;
        private System.Windows.Forms.TabPage optimizedProcedureTab;
        private System.Windows.Forms.TextBox optimizedProcedureTextBox;
        private System.Windows.Forms.TabPage reportTab;
        private System.Windows.Forms.TextBox reportTextBox;
        private System.Windows.Forms.TabPage performanceTab;
        private System.Windows.Forms.Label performanceLabel;
        private System.Windows.Forms.StatusBar statusBar; // Legacy StatusBar
        private System.Windows.Forms.ToolStripStatusLabel statusLabel; // Modern component
        // If StatusStrip was intended:
        // private System.Windows.Forms.StatusStrip statusStrip1;
    }
}
