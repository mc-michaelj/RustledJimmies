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
            hostLabel = new Label();
            hostTextBox = new TextBox();
            userLabel = new Label();
            userTextBox = new TextBox();
            passwordLabel = new Label();
            passwordTextBox = new TextBox();
            procedureLabel = new Label();
            procedureBodyTextBox = new TextBox();
            analyzeButton = new Button();
            resultsTabControl = new TabControl();
            optimizedProcedureTab = new TabPage();
            optimizedProcedureTextBox = new TextBox();
            geminiReportTab = new TabPage();
            reportTextBox = new TextBox();
            performanceTab = new TabPage();
            performanceLabel = new Label();
            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel();
            geminiApiKeyLabel = new Label();
            geminiApiKeyTextBox = new TextBox();
            geminiModelLabel = new Label();
            geminiModelTextBox = new TextBox();
            warningLabel = new Label();
            resultsTabControl.SuspendLayout();
            optimizedProcedureTab.SuspendLayout();
            geminiReportTab.SuspendLayout();
            performanceTab.SuspendLayout();
            statusStrip.SuspendLayout();
            SuspendLayout();
            // 
            // hostLabel
            // 
            hostLabel.AutoSize = true;
            hostLabel.Location = new Point(12, 15);
            hostLabel.Name = "hostLabel";
            hostLabel.Size = new Size(35, 15);
            hostLabel.TabIndex = 0;
            hostLabel.Text = "Host:";
            // 
            // hostTextBox
            // 
            hostTextBox.Location = new Point(53, 12);
            hostTextBox.Name = "hostTextBox";
            hostTextBox.Size = new Size(250, 23);
            hostTextBox.TabIndex = 1;
            // 
            // userLabel
            // 
            userLabel.AutoSize = true;
            userLabel.Location = new Point(320, 15);
            userLabel.Name = "userLabel";
            userLabel.Size = new Size(33, 15);
            userLabel.TabIndex = 2;
            userLabel.Text = "User:";
            // 
            // userTextBox
            // 
            userTextBox.Location = new Point(359, 12);
            userTextBox.Name = "userTextBox";
            userTextBox.Size = new Size(150, 23);
            userTextBox.TabIndex = 3;
            // 
            // passwordLabel
            // 
            passwordLabel.AutoSize = true;
            passwordLabel.Location = new Point(526, 15);
            passwordLabel.Name = "passwordLabel";
            passwordLabel.Size = new Size(60, 15);
            passwordLabel.TabIndex = 4;
            passwordLabel.Text = "Password:";
            // 
            // passwordTextBox
            // 
            passwordTextBox.Location = new Point(592, 12);
            passwordTextBox.Name = "passwordTextBox";
            passwordTextBox.PasswordChar = '*';
            passwordTextBox.Size = new Size(150, 23);
            passwordTextBox.TabIndex = 5;
            // 
            // procedureLabel
            // 
            procedureLabel.AutoSize = true;
            procedureLabel.Location = new Point(12, 140);
            procedureLabel.Name = "procedureLabel";
            procedureLabel.Size = new Size(98, 15);
            procedureLabel.TabIndex = 18; // Shifted
            procedureLabel.Text = "Procedure Body:";
            // 
            // procedureBodyTextBox
            // 
            procedureBodyTextBox.AcceptsReturn = true;
            procedureBodyTextBox.AcceptsTab = true;
            procedureBodyTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            procedureBodyTextBox.Font = new Font("Consolas", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            procedureBodyTextBox.Location = new Point(12, 158);
            procedureBodyTextBox.Multiline = true;
            procedureBodyTextBox.Name = "procedureBodyTextBox";
            procedureBodyTextBox.ScrollBars = ScrollBars.Both;
            procedureBodyTextBox.Size = new Size(760, 215);
            procedureBodyTextBox.TabIndex = 19; // Shifted
            // 
            // analyzeButton
            // 
            analyzeButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            analyzeButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            analyzeButton.Location = new Point(647, 404);
            analyzeButton.Name = "analyzeButton";
            analyzeButton.Size = new Size(125, 30);
            analyzeButton.TabIndex = 20; // Shifted
            analyzeButton.Text = "Analyze & Optimize";
            analyzeButton.UseVisualStyleBackColor = true;
            // 
            // resultsTabControl
            // 
            resultsTabControl.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            resultsTabControl.Controls.Add(optimizedProcedureTab);
            resultsTabControl.Controls.Add(geminiReportTab);
            resultsTabControl.Controls.Add(performanceTab);
            resultsTabControl.Location = new Point(12, 440);
            resultsTabControl.Name = "resultsTabControl";
            resultsTabControl.SelectedIndex = 0;
            resultsTabControl.Size = new Size(760, 150);
            resultsTabControl.TabIndex = 21; // Shifted
            // 
            // optimizedProcedureTab
            // 
            optimizedProcedureTab.Controls.Add(optimizedProcedureTextBox);
            optimizedProcedureTab.Location = new Point(4, 24);
            optimizedProcedureTab.Name = "optimizedProcedureTab";
            optimizedProcedureTab.Padding = new Padding(3);
            optimizedProcedureTab.Size = new Size(752, 172);
            optimizedProcedureTab.TabIndex = 0;
            optimizedProcedureTab.Text = "Optimized Procedure";
            optimizedProcedureTab.UseVisualStyleBackColor = true;
            // 
            // optimizedProcedureTextBox
            // 
            optimizedProcedureTextBox.Dock = DockStyle.Fill;
            optimizedProcedureTextBox.Font = new Font("Consolas", 9.75F);
            optimizedProcedureTextBox.Location = new Point(3, 3);
            optimizedProcedureTextBox.Multiline = true;
            optimizedProcedureTextBox.Name = "optimizedProcedureTextBox";
            optimizedProcedureTextBox.ReadOnly = true;
            optimizedProcedureTextBox.ScrollBars = ScrollBars.Both;
            optimizedProcedureTextBox.Size = new Size(746, 166);
            optimizedProcedureTextBox.TabIndex = 0;
            // 
            // geminiReportTab
            // 
            geminiReportTab.Controls.Add(reportTextBox);
            geminiReportTab.Location = new Point(4, 24);
            geminiReportTab.Name = "geminiReportTab";
            geminiReportTab.Padding = new Padding(3);
            geminiReportTab.Size = new Size(752, 172);
            geminiReportTab.TabIndex = 1;
            geminiReportTab.Text = "Gemini Report";
            geminiReportTab.UseVisualStyleBackColor = true;
            // 
            // reportTextBox
            // 
            reportTextBox.Dock = DockStyle.Fill;
            reportTextBox.Location = new Point(3, 3);
            reportTextBox.Multiline = true;
            reportTextBox.Name = "reportTextBox";
            reportTextBox.ReadOnly = true;
            reportTextBox.ScrollBars = ScrollBars.Vertical;
            reportTextBox.Size = new Size(746, 166);
            reportTextBox.TabIndex = 0;
            // 
            // performanceTab
            // 
            performanceTab.Controls.Add(performanceLabel);
            performanceTab.Location = new Point(4, 24);
            performanceTab.Name = "performanceTab";
            performanceTab.Size = new Size(752, 172);
            performanceTab.TabIndex = 2;
            performanceTab.Text = "Performance & Validation";
            performanceTab.UseVisualStyleBackColor = true;
            // 
            // performanceLabel
            // 
            performanceLabel.Dock = DockStyle.Fill;
            performanceLabel.Font = new Font("Consolas", 9.75F);
            performanceLabel.Location = new Point(0, 0);
            performanceLabel.Name = "performanceLabel";
            performanceLabel.Padding = new Padding(5);
            performanceLabel.Size = new Size(752, 172);
            performanceLabel.TabIndex = 0;
            performanceLabel.Text = "Performance metrics will be shown here.";
            // 
            // statusStrip
            // 
            statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel });
            statusStrip.Location = new Point(0, 593);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new Size(784, 22);
            statusStrip.TabIndex = 10;
            statusStrip.Text = "statusStrip1";
            // 
            // statusLabel
            // 
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new Size(39, 17);
            statusLabel.Text = "Ready";
            // 
            // geminiApiKeyLabel
            // 
            geminiApiKeyLabel.AutoSize = true;
            geminiApiKeyLabel.Location = new Point(12, 45);
            geminiApiKeyLabel.Name = "geminiApiKeyLabel";
            geminiApiKeyLabel.Size = new Size(93, 15);
            geminiApiKeyLabel.TabIndex = 11;
            geminiApiKeyLabel.Text = "Gemini API Key:";
            // 
            // geminiApiKeyTextBox
            // 
            geminiApiKeyTextBox.Location = new Point(111, 42);
            geminiApiKeyTextBox.Name = "geminiApiKeyTextBox";
            geminiApiKeyTextBox.PasswordChar = '*';
            geminiApiKeyTextBox.Size = new Size(400, 23);
            geminiApiKeyTextBox.TabIndex = 6;
            // 
            // geminiModelLabel
            // 
            geminiModelLabel.AutoSize = true;
            geminiModelLabel.Location = new Point(12, 75);
            geminiModelLabel.Name = "geminiModelLabel";
            geminiModelLabel.Size = new Size(90, 15);
            geminiModelLabel.TabIndex = 12;
            geminiModelLabel.Text = "Gemini Model:";
            // 
            // geminiModelTextBox
            // 
            geminiModelTextBox.Location = new Point(111, 72);
            geminiModelTextBox.Name = "geminiModelTextBox";
            geminiModelTextBox.Size = new Size(400, 23);
            geminiModelTextBox.TabIndex = 13;
            geminiModelTextBox.Text = "gemini-2.5-flash-preview-05-20";
            // 
            // testRowCountLabel
            //
            testRowCountLabel.AutoSize = true;
            testRowCountLabel.Location = new Point(12, 105);
            testRowCountLabel.Name = "testRowCountLabel";
            testRowCountLabel.Size = new Size(95, 15);
            testRowCountLabel.TabIndex = 14;
            testRowCountLabel.Text = "Test Row Count:";
            //
            // testRowCountNumericUpDown
            //
            testRowCountNumericUpDown.Location = new Point(111, 102);
            testRowCountNumericUpDown.Maximum = new decimal(new int[] { 1000000, 0, 0, 0 });
            testRowCountNumericUpDown.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            testRowCountNumericUpDown.Name = "testRowCountNumericUpDown";
            testRowCountNumericUpDown.Size = new Size(120, 23);
            testRowCountNumericUpDown.TabIndex = 15; // Original TabIndex
            testRowCountNumericUpDown.Value = new decimal(new int[] { 1000, 0, 0, 0 });
            //
            // commandTimeoutLabel
            //
            commandTimeoutLabel.AutoSize = true;
            commandTimeoutLabel.Location = new Point(243, 105);
            commandTimeoutLabel.Name = "commandTimeoutLabel";
            // Let AutoSize handle the size. Calculated/checked size is approx 110px for "Cmd Timeout (sec):"
            commandTimeoutLabel.TabIndex = 16; // Will be adjusted later if this works
            commandTimeoutLabel.Text = "Cmd Timeout (sec):";
            //
            // commandTimeoutNumericUpDown
            //
            commandTimeoutNumericUpDown.Location = new Point(365, 102); // Approx: 243 + 110 + 12
            commandTimeoutNumericUpDown.Maximum = new decimal(new int[] { 600, 0, 0, 0 });
            commandTimeoutNumericUpDown.Minimum = new decimal(new int[] { 15, 0, 0, 0 });
            commandTimeoutNumericUpDown.Name = "commandTimeoutNumericUpDown";
            commandTimeoutNumericUpDown.Size = new Size(120, 23);
            commandTimeoutNumericUpDown.TabIndex = 17; // Will be adjusted later
            commandTimeoutNumericUpDown.Value = new decimal(new int[] { 60, 0, 0, 0 });
            //
            // warningLabel
            // 
            warningLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            warningLabel.AutoSize = true;
            warningLabel.ForeColor = System.Drawing.Color.Red;
            warningLabel.Location = new Point(12, 415);
            warningLabel.Name = "warningLabel";
            warningLabel.Size = new Size(310, 15);
            warningLabel.TabIndex = 22; // Shifted
            warningLabel.Text = "WARNING: Inserts and rolls back data in the live database.";
            //
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(784, 615);
            Controls.Add(commandTimeoutNumericUpDown);
            Controls.Add(commandTimeoutLabel);
            Controls.Add(testRowCountNumericUpDown);
            Controls.Add(testRowCountLabel);
            Controls.Add(warningLabel);
            Controls.Add(geminiApiKeyTextBox);
            Controls.Add(geminiApiKeyLabel);
            Controls.Add(geminiModelTextBox);
            Controls.Add(geminiModelLabel);
            Controls.Add(statusStrip);
            Controls.Add(resultsTabControl);
            Controls.Add(analyzeButton);
            Controls.Add(procedureBodyTextBox);
            Controls.Add(procedureLabel);
            Controls.Add(passwordTextBox);
            Controls.Add(passwordLabel);
            Controls.Add(userTextBox);
            Controls.Add(userLabel);
            Controls.Add(hostTextBox);
            Controls.Add(hostLabel);
            MinimumSize = new Size(800, 600);
            Name = "MainForm";
            Text = "Intelligent Oracle SQL Optimizer";
            resultsTabControl.ResumeLayout(false);
            optimizedProcedureTab.ResumeLayout(false);
            optimizedProcedureTab.PerformLayout();
            geminiReportTab.ResumeLayout(false);
            geminiReportTab.PerformLayout();
            performanceTab.ResumeLayout(false);
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)testRowCountNumericUpDown).EndInit();
            ((System.ComponentModel.ISupportInitialize)commandTimeoutNumericUpDown).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label hostLabel;
        private TextBox hostTextBox;
        private Label userLabel;
        private TextBox userTextBox;
        private Label passwordLabel;
        private TextBox passwordTextBox;
        private Label procedureLabel;
        private TextBox procedureBodyTextBox;
        private Button analyzeButton;
        private TabControl resultsTabControl;
        private TabPage optimizedProcedureTab;
        private TextBox optimizedProcedureTextBox;
        private TabPage geminiReportTab;
        private TextBox reportTextBox;
        private TabPage performanceTab;
        private Label performanceLabel;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;
        private Label geminiApiKeyLabel;
        private TextBox geminiApiKeyTextBox;
        private Label geminiModelLabel;
        private TextBox geminiModelTextBox;
        private Label warningLabel;
        private Label testRowCountLabel;
        private System.Windows.Forms.NumericUpDown testRowCountNumericUpDown;
        private Label commandTimeoutLabel;
        private System.Windows.Forms.NumericUpDown commandTimeoutNumericUpDown;
    }
}