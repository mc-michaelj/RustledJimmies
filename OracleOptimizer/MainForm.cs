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
            // Helper function to reliably extract the first executable SQL statement from a string
            // that might contain comments and multiple queries.
            Func<string, string> extractFirstQuery = (rawSql) =>
            {
                if (string.IsNullOrWhiteSpace(rawSql)) return string.Empty;
                // Remove all line comments (--) from the script
                var noComments = System.Text.RegularExpressions.Regex.Replace(rawSql, @"--.*", "");
                // Split the remaining text into individual statements based on the semicolon
                var statements = noComments.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                // Find and return the first non-whitespace statement
                foreach (var stmt in statements)
                {
                    if (!string.IsNullOrWhiteSpace(stmt))
                    {
                        return stmt.Trim();
                    }
                }
                return string.Empty; // Return empty if no valid statement is found
            };

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
                string connectionString = $"Data Source={host};User Id={user};Password={password};Connection Timeout=60;";

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
                // Run the 'before' query multiple times to warm the database cache and get a more accurate timing.
                string beforeQuery = extractFirstQuery(geminiResponse.ValidationQueryBefore);
                if (string.IsNullOrEmpty(beforeQuery))
                {
                    throw new Exception("Could not find a valid 'before' validation query in the Gemini response.");
                }

                long beforeTime = long.MaxValue;
                DataTable beforeData = new DataTable();
                const int WARMUP_RUNS = 3;

                for (int i = 0; i < WARMUP_RUNS; i++)
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    // We only need to capture the data on the last run.
                    if (i == WARMUP_RUNS - 1)
                    {
                        beforeData = await oracleService.ExecuteQueryAsync(connectionString, beforeQuery);
                    }
                    else
                    {
                        await oracleService.ExecuteQueryAsync(connectionString, beforeQuery);
                    }
                    sw.Stop();
                    if (sw.ElapsedMilliseconds < beforeTime)
                    {
                        beforeTime = sw.ElapsedMilliseconds;
                    }
                }
                statusLabel.Text = $"'Before' snapshot complete (Best of {WARMUP_RUNS}: {beforeTime}ms). Executing in transaction...";

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
                            // For the optimized DML, we only run it once as it changes the state of the database.
                            var sw = System.Diagnostics.Stopwatch.StartNew();
                            string procedureBody = geminiResponse.OptimizedProcedureBody.Trim();

                            // Use regex to robustly convert a PROCEDURE into an anonymous DECLARE block.
                            // This handles variations in whitespace (spaces, newlines, tabs) around keywords.
                            // It replaces "PROCEDURE <name> IS/AS" with "DECLARE"
                            var regex = new System.Text.RegularExpressions.Regex(
                                @"\A\s*PROCEDURE\s+.*?(\s+IS|\s+AS)\s+",
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline
                            );

                            string executableBlock = regex.Replace(procedureBody, "DECLARE\n", 1);

                            // It also replaces the final "END <name>;" with "END;"
                            // This makes the block executable.
                            regex = new System.Text.RegularExpressions.Regex(
                                @"END\s+.*?;",
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.RightToLeft
                            );
                            executableBlock = regex.Replace(executableBlock, "END;", 1);

                            // The optimized procedure may contain calls to local logging procedures (e.g., LogStatus, PrintOut, LogError)
                            // that don't exist in our context. We need to remove them for the block to be valid.

                            // Replace LogError with RAISE; to ensure exceptions are propagated up to the C# code.
                            executableBlock = System.Text.RegularExpressions.Regex.Replace(
                                executableBlock,
                                @"LogError\(.*?\);",
                                "RAISE;",
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline
                            );

                            // Remove any calls to LogStatus or PrintOut by replacing them with a valid no-op statement.
                            executableBlock = System.Text.RegularExpressions.Regex.Replace(
                                executableBlock,
                                @"^\s*(LogStatus|PrintOut)\(.*\);",
                                "NULL;",
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline
                            );

                            await oracleService.ExecuteNonQueryAsync(connection, transaction, executableBlock);

                            // Execute 'after' query
                            string afterQuery = extractFirstQuery(geminiResponse.ValidationQueryAfter);
                            if (string.IsNullOrEmpty(afterQuery))
                            {
                                throw new Exception("Could not find a valid 'after' validation query in the Gemini response.");
                            }

                            // Gemini sometimes assumes a log_timestamp column exists for validation.
                            // We will remove this condition as our target table may not have it.
                            // This regex removes any AND condition that refers to "log_timestamp".
                            afterQuery = System.Text.RegularExpressions.Regex.Replace(
                                afterQuery,
                                @"\s+AND\s+.*?log_timestamp.*?(?=\s+ORDER BY|\s*$)",
                                "",
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline
                            );

                            DataTable afterData = await oracleService.ExecuteQueryWithinTransactionAsync(connection, transaction, afterQuery);
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
            catch (OracleException oraEx) when (oraEx.Message.Contains("ORA-50000"))
            {
                statusLabel.Text = "Error: Connection to Oracle timed out. Check firewall, VPN, and host details.";
                File.AppendAllText("log.txt", $"Oracle Connection Error: {oraEx}\n");
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