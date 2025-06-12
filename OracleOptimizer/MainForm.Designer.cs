namespace OracleOptimizer
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.hostLabel = new System.Windows.Forms.Label();
            this.hostTextBox = new System.Windows.Forms.TextBox();
            this.userLabel = new System.Windows.Forms.Label();
            this.userTextBox = new System.Windows.Forms.TextBox();
            this.passwordLabel = new System.Windows.Forms.Label();
            this.passwordTextBox = new System.Windows.Forms.TextBox();
            this.procedureBodyLabel = new System.Windows.Forms.Label();
            this.procedureBodyTextBox = new System.Windows.Forms.TextBox();
            this.analyzeButton = new System.Windows.Forms.Button();
            this.resultsTabControl = new System.Windows.Forms.TabControl();
            this.optimizedProcedureTabPage = new System.Windows.Forms.TabPage();
            this.optimizedProcedureTextBox = new System.Windows.Forms.TextBox();
            this.geminiReportTabPage = new System.Windows.Forms.TabPage();
            this.reportTextBox = new System.Windows.Forms.TextBox();
            this.performanceTabPage = new System.Windows.Forms.TabPage();
            this.performanceLabel = new System.Windows.Forms.Label();
            this.statusStrip = new System.Windows.Forms.StatusStrip(); // Changed from StatusBar
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();

            this.resultsTabControl.SuspendLayout();
            this.optimizedProcedureTabPage.SuspendLayout();
            this.geminiReportTabPage.SuspendLayout();
            this.performanceTabPage.SuspendLayout();
            this.statusStrip.SuspendLayout(); // Added for StatusStrip
            this.SuspendLayout();
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 561);
            this.Text = "Intelligent Oracle SQL Optimizer";
            this.Controls.Add(this.statusStrip); // Changed from statusBar
            this.Controls.Add(this.resultsTabControl);
            this.Controls.Add(this.analyzeButton);
            this.Controls.Add(this.procedureBodyTextBox);
            this.Controls.Add(this.procedureBodyLabel);
            this.Controls.Add(this.passwordTextBox);
            this.Controls.Add(this.passwordLabel);
            this.Controls.Add(this.userTextBox);
            this.Controls.Add(this.userLabel);
            this.Controls.Add(this.hostTextBox);
            this.Controls.Add(this.hostLabel);
            this.Name = "MainForm";
            //
            // hostLabel
            //
            this.hostLabel.AutoSize = true;
            this.hostLabel.Location = new System.Drawing.Point(12, 15);
            this.hostLabel.Name = "hostLabel";
            this.hostLabel.Size = new System.Drawing.Size(32, 13);
            this.hostLabel.Text = "Host:";
            //
            // hostTextBox
            //
            this.hostTextBox.Location = new System.Drawing.Point(70, 12);
            this.hostTextBox.Name = "hostTextBox";
            this.hostTextBox.Size = new System.Drawing.Size(200, 20);
            this.hostTextBox.TabIndex = 1;
            //this.hostTextBox.Text = "dev5-mer-db:1521/TCTN_MASTER"; // Default text set in MainForm.cs
            //
            // userLabel
            //
            this.userLabel.AutoSize = true;
            this.userLabel.Location = new System.Drawing.Point(12, 41);
            this.userLabel.Name = "userLabel";
            this.userLabel.Size = new System.Drawing.Size(32, 13);
            this.userLabel.Text = "User:";
            //
            // userTextBox
            //
            this.userTextBox.Location = new System.Drawing.Point(70, 38);
            this.userTextBox.Name = "userTextBox";
            this.userTextBox.Size = new System.Drawing.Size(200, 20);
            this.userTextBox.TabIndex = 2;
            //this.userTextBox.Text = "cisconvert"; // Default text set in MainForm.cs
            //
            // passwordLabel
            //
            this.passwordLabel.AutoSize = true;
            this.passwordLabel.Location = new System.Drawing.Point(12, 67);
            this.passwordLabel.Name = "passwordLabel";
            this.passwordLabel.Size = new System.Drawing.Size(56, 13);
            this.passwordLabel.Text = "Password:";
            //
            // passwordTextBox
            //
            this.passwordTextBox.Location = new System.Drawing.Point(70, 64);
            this.passwordTextBox.Name = "passwordTextBox";
            this.passwordTextBox.Size = new System.Drawing.Size(200, 20);
            this.passwordTextBox.TabIndex = 3;
            //this.passwordTextBox.Text = "cisconvert"; // Default text set in MainForm.cs
            this.passwordTextBox.UseSystemPasswordChar = true;
            //
            // procedureBodyLabel
            //
            this.procedureBodyLabel.AutoSize = true;
            this.procedureBodyLabel.Location = new System.Drawing.Point(12, 93);
            this.procedureBodyLabel.Name = "procedureBodyLabel";
            this.procedureBodyLabel.Size = new System.Drawing.Size(87, 13);
            this.procedureBodyLabel.Text = "Procedure Body:";
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
            this.procedureBodyTextBox.Size = new System.Drawing.Size(757, 150);
            this.procedureBodyTextBox.TabIndex = 4;
            //
            // analyzeButton
            //
            this.analyzeButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.analyzeButton.Location = new System.Drawing.Point(15, 265);
            this.analyzeButton.Name = "analyzeButton";
            this.analyzeButton.Size = new System.Drawing.Size(150, 23);
            this.analyzeButton.TabIndex = 5;
            this.analyzeButton.Text = "Analyze & Optimize";
            this.analyzeButton.UseVisualStyleBackColor = true;
            //
            // resultsTabControl
            //
            this.resultsTabControl.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.resultsTabControl.Controls.Add(this.optimizedProcedureTabPage);
            this.resultsTabControl.Controls.Add(this.geminiReportTabPage);
            this.resultsTabControl.Controls.Add(this.performanceTabPage);
            this.resultsTabControl.Location = new System.Drawing.Point(15, 294);
            this.resultsTabControl.Name = "resultsTabControl";
            this.resultsTabControl.SelectedIndex = 0;
            this.resultsTabControl.Size = new System.Drawing.Size(757, 236);
            this.resultsTabControl.TabIndex = 6;
            //
            // optimizedProcedureTabPage
            //
            this.optimizedProcedureTabPage.Controls.Add(this.optimizedProcedureTextBox);
            this.optimizedProcedureTabPage.Location = new System.Drawing.Point(4, 22);
            this.optimizedProcedureTabPage.Name = "optimizedProcedureTabPage";
            this.optimizedProcedureTabPage.Padding = new System.Windows.Forms.Padding(3);
            this.optimizedProcedureTabPage.Size = new System.Drawing.Size(749, 210);
            this.optimizedProcedureTabPage.TabIndex = 0;
            this.optimizedProcedureTabPage.Text = "Optimized Procedure";
            this.optimizedProcedureTabPage.UseVisualStyleBackColor = true;
            //
            // optimizedProcedureTextBox
            //
            this.optimizedProcedureTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.optimizedProcedureTextBox.Location = new System.Drawing.Point(3, 3);
            this.optimizedProcedureTextBox.Multiline = true;
            this.optimizedProcedureTextBox.Name = "optimizedProcedureTextBox";
            this.optimizedProcedureTextBox.ReadOnly = true;
            this.optimizedProcedureTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.optimizedProcedureTextBox.Size = new System.Drawing.Size(743, 204);
            this.optimizedProcedureTextBox.TabIndex = 0;
            //
            // geminiReportTabPage
            //
            this.geminiReportTabPage.Controls.Add(this.reportTextBox);
            this.geminiReportTabPage.Location = new System.Drawing.Point(4, 22);
            this.geminiReportTabPage.Name = "geminiReportTabPage";
            this.geminiReportTabPage.Padding = new System.Windows.Forms.Padding(3);
            this.geminiReportTabPage.Size = new System.Drawing.Size(749, 210);
            this.geminiReportTabPage.TabIndex = 1;
            this.geminiReportTabPage.Text = "Gemini Report";
            this.geminiReportTabPage.UseVisualStyleBackColor = true;
            //
            // reportTextBox
            //
            this.reportTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.reportTextBox.Location = new System.Drawing.Point(3, 3);
            this.reportTextBox.Multiline = true;
            this.reportTextBox.Name = "reportTextBox";
            this.reportTextBox.ReadOnly = true;
            this.reportTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.reportTextBox.Size = new System.Drawing.Size(743, 204);
            this.reportTextBox.TabIndex = 0;
            //
            // performanceTabPage
            //
            this.performanceTabPage.Controls.Add(this.performanceLabel);
            this.performanceTabPage.Location = new System.Drawing.Point(4, 22);
            this.performanceTabPage.Name = "performanceTabPage";
            this.performanceTabPage.Size = new System.Drawing.Size(749, 210);
            this.performanceTabPage.TabIndex = 2;
            this.performanceTabPage.Text = "Performance";
            this.performanceTabPage.UseVisualStyleBackColor = true;
            //
            // performanceLabel
            //
            this.performanceLabel.AutoSize = true;
            this.performanceLabel.Location = new System.Drawing.Point(10, 10);
            this.performanceLabel.Name = "performanceLabel";
            this.performanceLabel.Size = new System.Drawing.Size(194, 13);
            this.performanceLabel.TabIndex = 0;
            this.performanceLabel.Text = "Performance data will appear here.";
            //
            // statusStrip
            //
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusLabel});
            this.statusStrip.Location = new System.Drawing.Point(0, 539);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(784, 22);
            this.statusStrip.TabIndex = 7;
            this.statusStrip.Text = "statusStrip1";
            //
            // statusLabel
            //
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(39, 17);
            this.statusLabel.Text = "Ready";

            this.resultsTabControl.ResumeLayout(false);
            this.optimizedProcedureTabPage.ResumeLayout(false);
            this.optimizedProcedureTabPage.PerformLayout();
            this.geminiReportTabPage.ResumeLayout(false);
            this.geminiReportTabPage.PerformLayout();
            this.performanceTabPage.ResumeLayout(false);
            this.performanceTabPage.PerformLayout();
            this.statusStrip.ResumeLayout(false); // Added
            this.statusStrip.PerformLayout(); // Added
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
        private System.Windows.Forms.Label procedureBodyLabel;
        private System.Windows.Forms.TextBox procedureBodyTextBox;
        private System.Windows.Forms.Button analyzeButton;
        private System.Windows.Forms.TabControl resultsTabControl;
        private System.Windows.Forms.TabPage optimizedProcedureTabPage;
        private System.Windows.Forms.TextBox optimizedProcedureTextBox;
        private System.Windows.Forms.TabPage geminiReportTabPage;
        private System.Windows.Forms.TextBox reportTextBox;
        private System.Windows.Forms.TabPage performanceTabPage;
        private System.Windows.Forms.Label performanceLabel;
        private System.Windows.Forms.StatusStrip statusStrip; // Changed
        private System.Windows.Forms.ToolStripStatusLabel statusLabel;

    }
}
