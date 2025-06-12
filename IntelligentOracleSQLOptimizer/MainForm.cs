using System;
using System.Data;
using System.Diagnostics; // For Debug.WriteLine, if needed
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
// Assuming services are in the same namespace or a 'Services' sub-namespace.
// For this subtask, they are in the same namespace.
// using IntelligentOracleSQLOptimizer.Services;

namespace IntelligentOracleSQLOptimizer
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            // Manually wire up the event handler if not done by designer (it should be by name convention)
            // this.analyzeButton.Click += new System.EventHandler(this.analyzeButton_Click);
        }

        private async void analyzeButton_Click(object sender, EventArgs e)
        {
            string hostFull = hostTextBox.Text;
            string user = userTextBox.Text;
            string password = passwordTextBox.Text;
            string procedureBody = procedureBodyTextBox.Text;

            // Basic validation
            if (string.IsNullOrWhiteSpace(hostFull) ||
                string.IsNullOrWhiteSpace(user) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(procedureBody))
            {
                MessageBox.Show("Please fill in all connection details and the procedure body.", "Input Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Parse host, port, and service name from hostFull (e.g., "dev5-mer-db:1521/TCTN_MASTER")
            string parsedHost = "";
            string parsedPort = "1521"; // Default port
            string parsedServiceName = "";

            try
            {
                string[] hostAndService = hostFull.Split('/');
                if (hostAndService.Length == 2)
                {
                    parsedServiceName = hostAndService[1];
                    string[] hostAndPort = hostAndService[0].Split(':');
                    parsedHost = hostAndPort[0];
                    if (hostAndPort.Length == 2)
                    {
                        parsedPort = hostAndPort[1];
                    }
                }
                else // Assume it's just host or host:port, and service name might be missing or part of a different connection string schema
                {
                     string[] hostAndPort = hostFull.Split(':');
                     parsedHost = hostAndPort[0];
                     if (hostAndPort.Length == 2)
                     {
                         parsedPort = hostAndPort[1];
                     }
                     // This basic parsing assumes service name is provided after /
                     // A more robust solution might be needed for different TNS formats
                     if(string.IsNullOrWhiteSpace(parsedServiceName) && hostAndService.Length == 1 && !hostFull.Contains(":"))
                     {
                         // If only host is provided, it might be a TNS alias from tnsnames.ora
                         // For direct connection, service name is usually required.
                         // For this example, we'll assume TNS alias means service_name is not part of this string.
                         // The connection string below might need adjustment if full TNS entry is passed as host.
                     }
                }

                if (string.IsNullOrWhiteSpace(parsedHost) || string.IsNullOrWhiteSpace(parsedServiceName))
                {
                    MessageBox.Show("Invalid host format. Expected format: host:port/service_name or host/service_name. Port defaults to 1521 if not specified.", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error parsing host string: {ex.Message}", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Construct Oracle Connection String
            // Adjust if your Oracle setup uses a different format, e.g., EZCONNECT or full TNS descriptor
            string connectionString = $"Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={parsedHost})(PORT={parsedPort}))(CONNECT_DATA=(SERVICE_NAME={parsedServiceName})));User Id={user};Password={password};";

            // Update UI
            statusLabel.Text = "Analyzing...";
            analyzeButton.Enabled = false;
            optimizedProcedureTextBox.Text = ""; // Clear previous results
            reportTextBox.Text = "";
            performanceLabel.Text = "";
            // Ensure status bar updates if using StatusStrip
            // if (this.statusBar is StatusStrip statusStrip) { statusStrip.Refresh(); } // More modern approach
            // For legacy StatusBar, direct text update should be fine but might need Refresh() if inside long op on UI thread.

            var geminiService = new GeminiService();
            var oracleService = new OracleService();

            try
            {
                // 1. Call Gemini API
                statusLabel.Text = "Step 1: Calling Gemini API...";
                string geminiApiKey = "YOUR_GEMINI_API_KEY"; // Placeholder
                GeminiAnalysisResult analysisResult = null;
                try
                {
                    analysisResult = await geminiService.AnalyzeSqlAsync(procedureBody, geminiApiKey);
                }
                catch (Exception geminiEx)
                {
                    reportTextBox.Text = $"Gemini API call failed: {geminiEx.ToString()}";
                    statusLabel.Text = "Error: Gemini API call failed.";
                    return; // Exit before finally block re-enables button too soon
                }

                if (analysisResult == null || analysisResult.HasError)
                {
                    reportTextBox.Text = analysisResult?.ErrorMessage ?? "Gemini analysis returned no result or an unspecified error.";
                    statusLabel.Text = "Error: Gemini analysis failed.";
                    return;
                }

                // 2. Populate UI from Gemini
                optimizedProcedureTextBox.Text = analysisResult.OptimizedProcedureBody;
                reportTextBox.Text = analysisResult.Explanation; // Initial explanation

                // 3. Get "Before" Snapshot
                statusLabel.Text = "Step 2: Getting 'before' data snapshot...";
                DataTable beforeData;
                try
                {
                    if (string.IsNullOrWhiteSpace(analysisResult.ValidationQueryBefore))
                    {
                        reportTextBox.AppendText("\n\nWarning: Gemini did not provide a 'validation_query_before'. Skipping data validation steps.");
                        statusLabel.Text = "Warning: No 'before' query. Skipping validation.";
                        // Potentially allow user to proceed with optimization without validation or stop.
                        // For now, we'll stop if validation queries are missing.
                         performanceLabel.Text = "Validation skipped: Missing 'before' query.";
                        return;
                    }
                    beforeData = await oracleService.ExecuteQueryAsync(connectionString, analysisResult.ValidationQueryBefore);
                }
                catch (Exception oracleEx)
                {
                    reportTextBox.AppendText($"\n\nOracle error executing 'before' query: {oracleEx.ToString()}");
                    statusLabel.Text = "Error: Oracle 'before' query failed.";
                    return;
                }

                // 4. Execute in Transaction & Get "After" Snapshot
                statusLabel.Text = "Step 3: Executing optimized procedure and getting 'after' data...";
                (bool success, DataTable? afterData, string? errorMessage) transactionResult;
                try
                {
                     if (string.IsNullOrWhiteSpace(analysisResult.OptimizedProcedureBody))
                    {
                        reportTextBox.AppendText("\n\nError: Gemini did not provide an 'optimized_procedure_body'. Cannot execute.");
                        statusLabel.Text = "Error: Missing optimized procedure.";
                        return;
                    }
                    if (string.IsNullOrWhiteSpace(analysisResult.ValidationQueryAfter))
                    {
                        reportTextBox.AppendText("\n\nWarning: Gemini did not provide a 'validation_query_after'. Cannot complete validation.");
                        statusLabel.Text = "Warning: No 'after' query. Cannot complete validation.";
                        performanceLabel.Text = "Validation incomplete: Missing 'after' query.";
                        return;
                    }
                    transactionResult = await oracleService.ExecuteInTransactionAsync(connectionString, analysisResult.OptimizedProcedureBody, analysisResult.ValidationQueryAfter);
                }
                catch (Exception transEx)
                {
                    reportTextBox.AppendText($"\n\nOracle error during transaction: {transEx.ToString()}");
                    statusLabel.Text = "Error: Oracle transaction failed.";
                    return;
                }

                // 5. Compare Data & Finalize
                if (transactionResult.success && transactionResult.afterData != null)
                {
                    statusLabel.Text = "Step 4: Comparing data...";
                    // Ensure beforeData is not null (it would have exited if query failed, but as a safeguard)
                    if (beforeData == null)
                    {
                         reportTextBox.AppendText("\n\nCritical Error: 'beforeData' is null, cannot compare.");
                         statusLabel.Text = "Error: Cannot compare, 'beforeData' is null.";
                         return;
                    }

                    bool areEqual = DataTableComparator.AreTablesEqual(beforeData, transactionResult.afterData);
                    if (areEqual)
                    {
                        statusLabel.Text = "PASS: Optimization validated and committed.";
                        performanceLabel.Text = "Data comparison: PASS";
                        reportTextBox.AppendText("\n\nValidation Result: PASS - Data before and after optimization matches. Changes committed.");
                    }
                    else
                    {
                        statusLabel.Text = "FAIL (Rolled Back): Data mismatch after optimization.";
                        performanceLabel.Text = "Data comparison: FAIL - Changes rolled back.";
                        reportTextBox.AppendText("\n\nValidation Result: FAIL - Data before and after optimization does NOT match. Changes were rolled back.");
                    }
                }
                else // Transaction failed
                {
                    statusLabel.Text = $"FAIL (Rolled Back): {transactionResult.errorMessage}";
                    performanceLabel.Text = $"Execution error: {transactionResult.errorMessage}";
                    reportTextBox.AppendText($"\n\nTransaction Execution Result: FAIL - {transactionResult.errorMessage}. Changes were rolled back.");
                }
            }
            catch (Exception ex) // Top-level catch for unexpected errors
            {
                statusLabel.Text = $"Critical Error: {ex.Message}";
                reportTextBox.AppendText($"\n\nAn unexpected error occurred: {ex.ToString()}");
                Debug.WriteLine($"Critical Error in analyzeButton_Click: {ex.ToString()}");
            }
            finally
            {
                analyzeButton.Enabled = true;
                // For legacy StatusBar, the text updates should be immediate.
                // If using StatusStrip, Refresh() might be needed if updates are not showing.
                // e.g., if (this.statusBar is StatusStrip statusStrip) { statusStrip.Refresh(); }
                // For a legacy StatusBar, if statusLabel is a panel, it would be:
                // statusBar.Panels["statusLabelPanelName"].Text = statusLabel.Text; (pseudo-code)
                // Since statusLabel is a ToolStripStatusLabel and not directly part of legacy StatusBar's panels,
                // we rely on its Text property being updated. If it's visually hosted on a StatusStrip later, this is fine.
                // For now, we assume direct update or the framework handles it.
            }
        }
    }
}
