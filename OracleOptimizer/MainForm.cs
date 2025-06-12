using OracleOptimizer.Services;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.IO;
using Newtonsoft.Json;

namespace OracleOptimizer
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            // Pre-fill default values as per the specification
            hostTextBox.Text = "dev5-mer-db:1521/TCTN_MASTER";
            userTextBox.Text = "cisconvert";
            passwordTextBox.Text = "cisconvert";
            // Attach the event handler for the button click
            analyzeButton.Click += analyzeButton_Click;
        }

        private async void analyzeButton_Click(object? sender, EventArgs e)
        {
            // 1. Gather Inputs
            string host = hostTextBox.Text;
            string user = userTextBox.Text;
            string password = passwordTextBox.Text;
            string apiKey = geminiApiKeyTextBox.Text;
            string procedure = procedureBodyTextBox.Text;

            // Basic validation
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(procedure))
            {
                statusLabel.Text = "Error: All input fields, including API Key, are required.";
                return;
            }

            statusLabel.Text = "Analyzing with Gemini...";
            analyzeButton.Enabled = false;
            optimizedProcedureTextBox.Text = "";
            reportTextBox.Text = "";
            performanceLabel.Text = "";

            try
            {
                // 2. Instantiate Services
                var geminiService = new GeminiService(apiKey);
                var oracleService = new OracleService();
                string connectionString = $"Data Source={host};User Id={user};Password={password};";

                // Log connection attempt
                System.Diagnostics.Debug.WriteLine($"Attempting to connect to database: {host}");
                File.AppendAllText("log.txt", $"Attempting to connect to database: {host}\n");

                // 3. Call Gemini

                var geminiResponse = await geminiService.AnalyzeSqlAsync(procedure, geminiModelTextBox.Text);
                if (geminiResponse == null || string.IsNullOrWhiteSpace(geminiResponse.ValidationQueryBefore) || string.IsNullOrWhiteSpace(geminiResponse.OptimizedProcedureBody) || string.IsNullOrWhiteSpace(geminiResponse.ValidationQueryAfter))
                {
                    throw new Exception("Gemini did not return a valid or complete test plan. Check the procedure and try again.");
                }

                // Log Gemini response
                System.Diagnostics.Debug.WriteLine($"Gemini Response: {JsonConvert.SerializeObject(geminiResponse)}");
                File.AppendAllText("log.txt", $"Gemini Response: {JsonConvert.SerializeObject(geminiResponse)}\n");

                // Log API Key
                System.Diagnostics.Debug.WriteLine($"Using API Key: {apiKey}");
                File.AppendAllText("log.txt", $"Using API Key: {apiKey}\n");

                reportTextBox.Text = geminiResponse.Explanation;
                optimizedProcedureTextBox.Text = geminiResponse.OptimizedProcedureBody;
                resultsTabControl.SelectedTab = geminiReportTab;
                statusLabel.Text = "Gemini analysis complete. Getting 'before' snapshot...";

                // 4. Get "Before" Snapshot
                var sw = System.Diagnostics.Stopwatch.StartNew();
                DataTable beforeData = await oracleService.ExecuteQueryAsync(connectionString, geminiResponse.ValidationQueryBefore);
                sw.Stop();
                long beforeTime = sw.ElapsedMilliseconds;
                statusLabel.Text = $"'Before' snapshot complete ({beforeTime}ms). Executing in transaction...";

                // Log before data
                System.Diagnostics.Debug.WriteLine($"Before Data: {JsonConvert.SerializeObject(beforeData)}");
                File.AppendAllText("log.txt", $"Before Data: {JsonConvert.SerializeObject(beforeData)}\n");

                // 5. Execute in Transaction
                using (var connection = new OracleConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                    {
                        try
                        {
                            sw.Restart();
                            // Execute optimized procedure
                            await oracleService.ExecuteNonQueryAsync(connection, transaction, geminiResponse.OptimizedProcedureBody);

                            // Execute 'after' query
                            DataTable afterData = await oracleService.ExecuteQueryWithinTransactionAsync(connection, transaction, geminiResponse.ValidationQueryAfter);
                            sw.Stop();
                            long afterTime = sw.ElapsedMilliseconds;

                            statusLabel.Text = "Transaction complete. Validating data...";

                            // Log after data
                            System.Diagnostics.Debug.WriteLine($"After Data: {JsonConvert.SerializeObject(afterData)}");
                            File.AppendAllText("log.txt", $"After Data: {JsonConvert.SerializeObject(afterData)}\n");

                            // 6. Automated Validation & Decision
                            bool areIdentical = DataTableComparator.AreIdentical(beforeData, afterData, out string comparisonDetails);

                            if (areIdentical)
                            {
                                transaction.Commit();
                                statusLabel.Text = "PASS: Validation successful. Changes have been committed.";
                                performanceLabel.Text = $"Original Query Time: {beforeTime}ms\nOptimized Execution Time: {afterTime}ms\n\nValidation Details:\n{comparisonDetails}";
                            }
                            else
                            {
                                transaction.Rollback();
                                statusLabel.Text = "FAIL: Validation failed. Changes have been rolled back.";
                                performanceLabel.Text = $"Original Query Time: {beforeTime}ms\nOptimized Execution Time: {afterTime}ms\n\nValidation Details:\n{comparisonDetails}";
                            }
                            resultsTabControl.SelectedTab = performanceTab;
                        }
                        catch (Exception txEx)
                        {
                            transaction.Rollback();
                            throw new Exception("Error during transaction. Changes have been rolled back.", txEx);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"An error occurred: {ex.Message}";
                // Log the error details
                System.Diagnostics.Debug.WriteLine($"Error in analyzeButton_Click: {ex}");
                File.AppendAllText("log.txt", $"Error in analyzeButton_Click: {ex}\n");
            }
            finally
            {
                analyzeButton.Enabled = true;
            }
        }
    }
}