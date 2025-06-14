using OracleOptimizer.Services;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.IO;
using Newtonsoft.Json;
using System.Text;
using System.Collections.Generic;
using System; // For Random, Math, should be okay, or use DBMS_RANDOM if preferred by Oracle context. For this step, using simple C# random or sequential.

namespace OracleOptimizer
{
    /// <summary>
    /// Main form for the Oracle SQL Optimizer application.
    /// Provides UI for inputting database credentials, SQL procedure, API key,
    /// and displays analysis, optimized SQL, and performance results.
    /// </summary>
    public partial class MainForm : Form
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MainForm"/> class.
        /// Sets up UI components and pre-fills default values.
        /// </summary>
        public MainForm()
        {
            InitializeComponent();
            // Pre-fill default values as per the specification
            hostTextBox.Text = "dev5-mer-db:1521/TCTN_MASTER";
            userTextBox.Text = "cisconvert";
            passwordTextBox.Text = "cisconvert";
            geminiApiKeyTextBox.Text = "AIzaSyCejA2q9u3nieoSxQX9RpJsWMIJWGKDN7I";
            procedureBodyTextBox.Text = @"
PROCEDURE AR_Recapture_w_CR IS
   --  Purpose: Identify AR Recapture IDs that have a Credit (such as overpmtcr) invoice included.
   --  JME    1.11.16    Per TT134621 - Created Procedure
   vResultsRec                  daily_validation%ROWTYPE;
   CURSOR GetARRecapturewCRCur
     IS
     select a.accountno, a.ar_recapture_id from cisdata.ar_recapture_master a, cisdata.ar_recapture_invoice_master a2
       where a.status = 'ACTIVE' and a.ar_recapture_id = a2.ar_recapture_id
       and a2.balance < 0
       group by a.accountno, a.ar_recapture_id;
   BEGIN
           LogStatus('AR_Recapture_w_CR', 'Started at ' || TO_CHAR(SYSDATE, 'DD-MON-YYYY HH:MI:SS'));
           PrintOut('Start of AR_Recapture_w_CR');
     FOR GetARRecapturewCRRec IN GetARRecapturewCRCur LOOP
         vResultsRec.Validation := 'AR_Recapture_w_CR';
               vResultsRec.AccountNo  := GetARRecapturewCRRec.accountno;
         vResultsRec.Results    := 'The AR Recapture Invoice Master for Account '||GetARRecapturewCRRec.accountno|| ', AR Recapture ID '|| GetARRecapturewCRRec.ar_recapture_id || ' includes a credit invoice';
         LogResults(vResultsRec);
           END LOOP;
           PrintOut('End of AR_Recapture_w_CR');
           LogStatus('AR_Recapture_w_CR', 'Ended at ' || TO_CHAR(SYSDATE, 'DD-MON-YYYY HH:MI:SS'));
 EXCEPTION
   WHEN OTHERS THEN
            LogError('AR_Recapture_w_CR', SQLCODE, SQLERRM);
   END AR_Recapture_w_CR;
".Trim();
            // Attach the event handler for the button click
            analyzeButton.Click += analyzeButton_Click;
        }

        /// <summary>
        /// Handles the click event of the "Analyze & Optimize" button.
        /// Orchestrates the process of fetching inputs, calling services for analysis and testing,
        /// and updating the UI with results or error messages.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private async void analyzeButton_Click(object? sender, EventArgs e)
        {
            // Disable button during processing to prevent multiple clicks
            analyzeButton.Enabled = false;
            statusLabel.Text = "Processing...";
            performanceLabel.Text = ""; // Clear performance label
            // reportTextBox.Text = ""; // Decide if we want to clear this for logic tests

            try
            {
                // Gather inputs from the UI text boxes.
                string host = hostTextBox.Text;
                string user = userTextBox.Text;
                string password = passwordTextBox.Text;
                string apiKey = geminiApiKeyTextBox.Text; // Ensure this is still how API key is passed
                string originalProcedure = procedureBodyTextBox.Text;
                string modelName = geminiModelTextBox.Text;
                int testRowCount = (int)testRowCountNumericUpDown.Value;
                int commandTimeoutSeconds = (int)commandTimeoutNumericUpDown.Value;

                // Validate that all required inputs are provided.
                if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) ||
                    string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(apiKey) ||
                    string.IsNullOrWhiteSpace(originalProcedure) || string.IsNullOrWhiteSpace(modelName))
                {
                    statusLabel.Text = "Error: All input fields (Host, User, Password, API Key, Procedure, Model Name) are required.";
                    return;
                }
                if (testRowCount <= 0)
                {
                    statusLabel.Text = "Error: Test Row Count must be greater than 0.";
                    return;
                }
                if (commandTimeoutSeconds <= 0)
                {
                    statusLabel.Text = "Error: Command Timeout (seconds) must be greater than 0.";
                    return;
                }

                // Clear previous results from UI
                optimizedProcedureTextBox.Text = "";
                reportTextBox.Text = "";
                performanceLabel.Text = "";

                // Instantiate services
                var oracleService = new OracleService();
                var geminiService = new GeminiService(apiKey); // Pass API key to GeminiService
                var orchestrator = new AnalysisOrchestrator(oracleService, geminiService);

                // Call the orchestrator
                AnalysisResult result = await orchestrator.ExecuteAnalysisAndTestingAsync(
                    host, user, password, originalProcedure, modelName,
                    testRowCount, commandTimeoutSeconds);

                // Update UI with results from AnalysisResult
                if (result != null)
                {
                    reportTextBox.Text = result.GeminiExplanation;
                    optimizedProcedureTextBox.Text = result.OptimizedProcedureBody;
                    performanceLabel.Text = result.FinalReport;

                    // Select appropriate tab based on whether the logic test passed or if there's an optimized procedure
                    if (!string.IsNullOrEmpty(result.OptimizedProcedureBody))
                    {
                         resultsTabControl.SelectedTab = geminiReportTab; // Default to Gemini report if optimized proc exists
                    }
                    if (!string.IsNullOrEmpty(result.FinalReport))
                    {
                        resultsTabControl.SelectedTab = performanceTab; // Switch to performance if final report exists
                    }
                    statusLabel.Text = "Analysis and Testing Complete.";
                }
                else
                {
                    statusLabel.Text = "Error: Analysis did not return any results.";
                }
            }
            // Specific Oracle exceptions that might indicate connection or credential issues
            catch (OracleException oraEx) when (oraEx.Message.Contains("ORA-50000") || oraEx.Message.Contains("ORA-12170") || oraEx.Message.Contains("ORA-01017"))
            {
                statusLabel.Text = $"Oracle Error: {oraEx.Message}. Check connection details, credentials, and network.";
#if DEBUG
                File.AppendAllText("log.txt", $"Oracle Connection Error: {oraEx}\n");
#endif
            }
            catch (HttpRequestException httpEx)
            {
                statusLabel.Text = $"API Request Error: {httpEx.Message}. Check API key and network.";
#if DEBUG
                File.AppendAllText("log.txt", $"API Request Error: {httpEx}\n");
#endif
            }
            catch (JsonException jsonEx)
            {
                statusLabel.Text = $"JSON Parsing Error: {jsonEx.Message}. Check the API response or input data.";
#if DEBUG
                File.AppendAllText("log.txt", $"JSON Parsing Error: {jsonEx}\n");
#endif
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"An error occurred: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error in analyzeButton_Click: {ex}");
#if DEBUG
                File.AppendAllText("log.txt", $"Error in analyzeButton_Click: {ex}\n");
#endif
            }
            finally
            {
                // Re-enable button after processing finishes or if an error occurs
                analyzeButton.Enabled = true;
            }
        }

        /// <summary>
        /// Orchestrates the entire SQL analysis and testing process.
        /// This includes fetching user inputs, calling the Gemini API for SQL optimization and schema extraction,
        /// generating test data, running both original and optimized procedures against the database,
        /// and comparing their results and performance.
        /// </summary>
        // This method is now moved to AnalysisOrchestrator.cs
    }
}