using System;
using System.Data;
using System.Diagnostics; // For Stopwatch
using System.Text; // For StringBuilder
using System.Windows.Forms;
using Oracle.ManagedDataAccess.Client; // Required for OracleConnection, OracleTransaction

namespace OracleOptimizer
{
    public partial class MainForm : Form
    {
        private readonly OracleService _oracleService;
        private readonly GeminiService _geminiService;

        public MainForm()
        {
            InitializeComponent();
            // Pre-fill text boxes
            hostTextBox.Text = "dev5-mer-db:1521/TCTN_MASTER";
            userTextBox.Text = "cisconvert";
            passwordTextBox.Text = "cisconvert";

            _oracleService = new OracleService();
            _geminiService = new GeminiService();

            // Set initial text for performance label if not set in designer
            if (performanceLabel != null)
            {
                performanceLabel.Text = "Performance data will appear here.";
            }
            // Set initial text for status label if not set in designer
            if (statusLabel != null)
            {
                statusLabel.Text = "Ready.";
            }

            // Wire up the event handler for the analyzeButton
            // This is typically done in MainForm.Designer.cs, but can be done here if missed.
            if (this.analyzeButton != null)
            {
                this.analyzeButton.Click += new System.EventHandler(this.analyzeButton_Click);
            }
        }

        private async void analyzeButton_Click(object sender, EventArgs e)
        {
            if (statusLabel == null) // Defensive check
            {
                MessageBox.Show("StatusLabel is not initialized.", "UI Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (analyzeButton == null) // Defensive check
            {
                 MessageBox.Show("AnalyzeButton is not initialized.", "UI Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
             if (procedureBodyTextBox == null || hostTextBox == null || userTextBox == null || passwordTextBox == null)
            {
                MessageBox.Show("One or more input TextBoxes are not initialized.", "UI Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (optimizedProcedureTextBox == null || reportTextBox == null || resultsTabControl == null || performanceLabel == null)
            {
                MessageBox.Show("One or more output controls are not initialized.", "UI Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }


            statusLabel.Text = "Analyzing...";
            analyzeButton.Enabled = false;
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                // 1. Gather Inputs
                string host = hostTextBox.Text;
                string user = userTextBox.Text;
                string password = passwordTextBox.Text;
                string procedureBody = procedureBodyTextBox.Text;

                if (string.IsNullOrWhiteSpace(host) ||
                    string.IsNullOrWhiteSpace(user) ||
                    // Password can be empty for some Oracle setups, but procedure body cannot
                    string.IsNullOrWhiteSpace(procedureBody))
                {
                    MessageBox.Show("Host, User, and Procedure Body cannot be empty.", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    statusLabel.Text = "Input error.";
                    return;
                }

                string connectionString = $"Data Source={host};User ID={user};Password={password};";

                // 2. Call Gemini (Simulated)
                statusLabel.Text = "Asking AI for test plan...";
                GeminiAnalysisResult analysisResult = await _geminiService.AnalyzeSqlAsync(procedureBody);

                optimizedProcedureTextBox.Text = analysisResult.OptimizedProcedureBody;
                reportTextBox.Text = analysisResult.Explanation;

                // Switch to the "Optimized Procedure" tab
                if (resultsTabControl.TabPages.ContainsKey("optimizedProcedureTabPage"))
                {
                    resultsTabControl.SelectedTab = resultsTabControl.TabPages["optimizedProcedureTabPage"];
                }


                // 3. Get "Before" Snapshot
                statusLabel.Text = "Getting 'before' snapshot...";
                DataTable beforeTable = await _oracleService.ExecuteQueryAsync(connectionString, analysisResult.ValidationQueryBefore);

                // 4. Execute in Transaction
                statusLabel.Text = "Executing optimized procedure in transaction...";
                DataTable afterTable = null;
                bool changesCommitted = false;

                using (var connection = new OracleConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                    {
                        try
                        {
                            // Execute the optimized procedure body
                            await _oracleService.ExecuteProcedureAsync(connection, transaction, analysisResult.OptimizedProcedureBody);

                            // Get "After" Snapshot (within the same transaction)
                            statusLabel.Text = "Getting 'after' snapshot...";
                            // Create a new command for the 'after' query using the existing transaction
                            using (var cmd = new OracleCommand(analysisResult.ValidationQueryAfter, connection))
                            {
                                cmd.Transaction = transaction;
                                afterTable = new DataTable();
                                using (var reader = await cmd.ExecuteReaderAsync())
                                {
                                    afterTable.Load(reader);
                                }
                            }

                            // 5. Automated Validation & Decision
                            statusLabel.Text = "Validating results...";
                            bool areEqual = DataTableComparator.AreTablesEqual(beforeTable, afterTable);

                            if (areEqual)
                            {
                                transaction.Commit();
                                changesCommitted = true;
                                statusLabel.Text = "PASS: Changes committed.";
                                MessageBox.Show("Validation PASS. Changes have been committed.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            else
                            {
                                transaction.Rollback();
                                statusLabel.Text = "FAIL: Changes rolled back due to data mismatch.";
                                MessageBox.Show("Validation FAIL. Data mismatch. Changes have been rolled back.", "Validation Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }
                        }
                        catch (OracleException ox)
                        {
                            transaction.Rollback();
                            statusLabel.Text = $"Oracle Error: Rolled back. {ox.Message.Split('\n')[0]}"; // Show first line of error
                            MessageBox.Show($"An Oracle error occurred: {ox.Message}\n\nChanges have been rolled back.", "Oracle Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            reportTextBox.Text += $"\n\nORACLE ERROR: {ox.Message}\n{ox.StackTrace}";
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            statusLabel.Text = $"Error: Rolled back. {ex.Message.Split('\n')[0]}"; // Show first line of error
                            MessageBox.Show($"An unexpected error occurred: {ex.Message}\n\nChanges have been rolled back.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            reportTextBox.Text += $"\n\nSYSTEM ERROR: {ex.Message}\n{ex.StackTrace}";
                        }
                    }
                }

                stopwatch.Stop();
                performanceLabel.Text = $"Analysis and execution took: {stopwatch.ElapsedMilliseconds} ms. Status: {(changesCommitted ? "Committed" : "Rolled Back")}";

            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                statusLabel.Text = $"Critical Error: {ex.Message.Split('\n')[0]}";  // Show first line of error
                MessageBox.Show($"A critical error occurred: {ex.Message}", "Critical Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                if(reportTextBox != null) // Defensive check
                {
                    reportTextBox.Text += $"\n\nCRITICAL ERROR:\n{ex.Message}\n{ex.StackTrace}";
                }
                if(performanceLabel != null) // Defensive check
                {
                   performanceLabel.Text = $"Operation failed after {stopwatch.ElapsedMilliseconds} ms.";
                }
            }
            finally
            {
                analyzeButton.Enabled = true;
                if (statusLabel.Text.Contains("..."))
                {
                    statusLabel.Text = "Operation failed or was interrupted.";
                }
            }
        }
    }
}
