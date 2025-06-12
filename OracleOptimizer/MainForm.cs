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
            analyzeButton.Enabled = false;
            statusLabel.Text = "Processing...";
            performanceLabel.Text = ""; // Clear performance label
            // reportTextBox.Text = ""; // Decide if we want to clear this for logic tests

            try
            {
                if (useLogicTestCheckBox.Checked)
                {
                    await ExecuteLogicTestAsync();
                }
                else if (usePerfTestCheckBox.Checked)
                {
                    await ExecutePerformanceTestAsync();
                }
                else
                {
                    // Helper function to reliably extract the first executable SQL statement from a string
                    // that might contain comments and multiple queries.
                    Func<string, string> extractFirstQuery = (rawSql) =>
                    {
                        if (string.IsNullOrWhiteSpace(rawSql)) return string.Empty;
                        var noComments = System.Text.RegularExpressions.Regex.Replace(rawSql, @"--.*", "");
                        var statements = noComments.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var stmt in statements)
                        {
                            if (!string.IsNullOrWhiteSpace(stmt))
                            {
                                return stmt.Trim();
                            }
                        }
                        return string.Empty;
                    };

                    string host = hostTextBox.Text;
                    string user = userTextBox.Text;
                    string password = passwordTextBox.Text;
                    string apiKey = geminiApiKeyTextBox.Text;
                    string procedure = procedureBodyTextBox.Text;
                    string modelName = geminiModelTextBox.Text;

                    if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(procedure))
                    {
                        statusLabel.Text = "Error: All input fields, including API Key and Procedure, are required for optimization.";
                        return;
                    }

                    statusLabel.Text = "Analyzing with Gemini for optimization...";
                    optimizedProcedureTextBox.Text = ""; // Clear previous optimized procedure
                    reportTextBox.Text = ""; // Clear previous report

                    var geminiService = new GeminiService(apiKey);
                    var oracleService = new OracleService();
                    string connectionString = $"Data Source={host};User Id={user};Password={password};Connection Timeout=60;";

                    System.Diagnostics.Debug.WriteLine($"Attempting to connect to database: {host}");
                    File.AppendAllText("log.txt", $"Attempting to connect to database: {host}\n");

                    var geminiResponse = await geminiService.AnalyzeSqlAsync(procedure, modelName);
                    if (geminiResponse == null || string.IsNullOrWhiteSpace(geminiResponse.ValidationQueryBefore) || string.IsNullOrWhiteSpace(geminiResponse.OptimizedProcedureBody) || string.IsNullOrWhiteSpace(geminiResponse.ValidationQueryAfter))
                    {
                        throw new Exception("Gemini did not return a valid or complete test plan. Check the procedure and try again.");
                    }

                    System.Diagnostics.Debug.WriteLine($"Gemini Response: {JsonConvert.SerializeObject(geminiResponse)}");
                    File.AppendAllText("log.txt", $"Gemini Response: {JsonConvert.SerializeObject(geminiResponse)}\n");
                    System.Diagnostics.Debug.WriteLine($"Using API Key: {apiKey}");
                    File.AppendAllText("log.txt", $"Using API Key: {apiKey}\n");

                    reportTextBox.Text = geminiResponse.Explanation;
                    optimizedProcedureTextBox.Text = geminiResponse.OptimizedProcedureBody;
                    resultsTabControl.SelectedTab = geminiReportTab;
                    statusLabel.Text = "Gemini analysis complete. Getting 'before' snapshot...";

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
                    System.Diagnostics.Debug.WriteLine($"Before Data: {JsonConvert.SerializeObject(beforeData)}");
                    File.AppendAllText("log.txt", $"Before Data: {JsonConvert.SerializeObject(beforeData)}\n");

                    using (var connection = new OracleConnection(connectionString))
                    {
                        await connection.OpenAsync();
                        using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                        {
                            try
                            {
                                var sw = System.Diagnostics.Stopwatch.StartNew();
                                string procedureBodyToExecute = geminiResponse.OptimizedProcedureBody.Trim();
                                var regex = new System.Text.RegularExpressions.Regex(@"\A\s*PROCEDURE\s+.*?(\s+IS|\s+AS)\s+", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
                                string executableBlock = regex.Replace(procedureBodyToExecute, "DECLARE\n", 1);
                                regex = new System.Text.RegularExpressions.Regex(@"END\s+.*?;", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.RightToLeft);
                                executableBlock = regex.Replace(executableBlock, "END;", 1);
                                executableBlock = System.Text.RegularExpressions.Regex.Replace(executableBlock, @"LogError\(.*?\);", "RAISE;", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
                                executableBlock = System.Text.RegularExpressions.Regex.Replace(executableBlock, @"^\s*(LogStatus|PrintOut)\(.*\);", "NULL;", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);

                                await oracleService.ExecuteNonQueryAsync(connection, transaction, executableBlock);

                                string afterQuery = extractFirstQuery(geminiResponse.ValidationQueryAfter);
                                if (string.IsNullOrEmpty(afterQuery))
                                {
                                    throw new Exception("Could not find a valid 'after' validation query in the Gemini response.");
                                }
                                afterQuery = System.Text.RegularExpressions.Regex.Replace(afterQuery, @"\s+AND\s+.*?log_timestamp.*?(?=\s+ORDER BY|\s*$)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

                                DataTable afterData = await oracleService.ExecuteQueryWithinTransactionAsync(connection, transaction, afterQuery);
                                sw.Stop();
                                long afterTime = sw.ElapsedMilliseconds;

                                statusLabel.Text = "Transaction complete. Validating data...";
                                System.Diagnostics.Debug.WriteLine($"After Data: {JsonConvert.SerializeObject(afterData)}");
                                File.AppendAllText("log.txt", $"After Data: {JsonConvert.SerializeObject(afterData)}\n");

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
            }
            catch (OracleException oraEx) when (oraEx.Message.Contains("ORA-50000") || oraEx.Message.Contains("ORA-12170") || oraEx.Message.Contains("ORA-01017"))
            {
                statusLabel.Text = $"Oracle Error: {oraEx.Message}. Check connection details, credentials, and network.";
                File.AppendAllText("log.txt", $"Oracle Connection Error: {oraEx}\n");
            }
            catch (HttpRequestException httpEx)
            {
                statusLabel.Text = $"API Request Error: {httpEx.Message}. Check API key and network.";
                File.AppendAllText("log.txt", $"API Request Error: {httpEx}\n");
            }
            catch (JsonException jsonEx)
            {
                statusLabel.Text = $"JSON Parsing Error: {jsonEx.Message}. Check the API response or input data.";
                File.AppendAllText("log.txt", $"JSON Parsing Error: {jsonEx}\n");
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"An error occurred: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error in analyzeButton_Click: {ex}");
                File.AppendAllText("log.txt", $"Error in analyzeButton_Click: {ex}\n");
            }
            finally
            {
                analyzeButton.Enabled = true;
            }
        }

        private async Task ExecutePerformanceTestAsync()
        {
            statusLabel.Text = "Starting Performance Test...";
            performanceLabel.Text = "Measuring...";
            reportTextBox.Text = ""; // Clear report text box for new results
            resultsTabControl.SelectedTab = performanceTab; // Switch to performance tab

            string originalSqlScript = procedureBodyTextBox.Text;
            string optimizedSqlScript = optimizedProcedureTextBox.Text; // This is the "after" script
            string apiKey = geminiApiKeyTextBox.Text;
            string modelName = geminiModelTextBox.Text;
            string host = hostTextBox.Text;
            string user = userTextBox.Text;
            string password = passwordTextBox.Text;
            int currentPerfTestRowCount = (int)perfTestRowCount.Value;

            if (string.IsNullOrWhiteSpace(originalSqlScript))
            {
                statusLabel.Text = "Performance Test Aborted: Original SQL script is empty.";
                return;
            }
            if (string.IsNullOrWhiteSpace(optimizedSqlScript))
            {
                statusLabel.Text = "Performance Test Aborted: Optimized SQL script is empty.";
                return;
            }
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                statusLabel.Text = "Performance Test Aborted: Gemini API Key is empty.";
                return;
            }
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
            {
                statusLabel.Text = "Performance Test Aborted: Database connection details are incomplete.";
                return;
            }

            var geminiService = new GeminiService(apiKey); // Instantiate outside using block for broader scope if needed for schema
            var oracleService = new OracleService();
            string connectionString = $"Data Source={host};User Id={user};Password={password};Connection Timeout=60;"; // Longer timeout for potentially long operations

            long originalTimeMs = -1;
            long optimizedTimeMs = -1;

            using OracleConnection connection = new OracleConnection(connectionString);
            try
            {
                await connection.OpenAsync();
                using OracleTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
                try
                {
                    statusLabel.Text = "Perf Test: Getting table schema...";
                    string geminiSchemaJson = await geminiService.GetTableSchemaFromGemini(originalSqlScript, modelName);
                    if (string.IsNullOrWhiteSpace(geminiSchemaJson) || geminiSchemaJson.StartsWith("-- Error"))
                    {
                        throw new Exception($"Failed to get table schema from Gemini. Response: {geminiSchemaJson}");
                    }

                    statusLabel.Text = "Perf Test: Generating insert statements...";
                    string plSqlInsertBlock = GenerateInsertStatements(geminiSchemaJson, currentPerfTestRowCount, out _); // tableNames out param not strictly needed here
                    if (string.IsNullOrWhiteSpace(plSqlInsertBlock) || plSqlInsertBlock.StartsWith("-- Error"))
                    {
                        throw new Exception($"Failed to generate insert statements. Response: {plSqlInsertBlock}");
                    }

                    statusLabel.Text = "Perf Test: Inserting temporary data...";
                    System.Diagnostics.Debug.WriteLine($"Executing Insert PL/SQL Block:\n{plSqlInsertBlock}");
                    await oracleService.ExecuteNonQueryAsync(connection, transaction, plSqlInsertBlock);
                    statusLabel.Text = "Perf Test: Temporary data inserted.";

                    // Execute Original Script
                    statusLabel.Text = "Perf Test: Executing original script...";
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    System.Diagnostics.Debug.WriteLine($"Executing Original Script (Perf Test):\n{originalSqlScript}");
                    await oracleService.ExecuteNonQueryAsync(connection, transaction, originalSqlScript);
                    stopwatch.Stop();
                    originalTimeMs = stopwatch.ElapsedMilliseconds;
                    statusLabel.Text = "Perf Test: Original script executed.";

                    // Execute Optimized Script
                    statusLabel.Text = "Perf Test: Executing optimized script...";
                    stopwatch.Restart();
                    System.Diagnostics.Debug.WriteLine($"Executing Optimized Script (Perf Test):\n{optimizedSqlScript}");
                    await oracleService.ExecuteNonQueryAsync(connection, transaction, optimizedSqlScript);
                    stopwatch.Stop();
                    optimizedTimeMs = stopwatch.ElapsedMilliseconds;
                    statusLabel.Text = "Perf Test: Optimized script executed.";

                    statusLabel.Text = "Perf Test: Rolling back temporary data...";
                    await transaction.RollbackAsync(); // Use RollbackAsync
                    statusLabel.Text = "Performance Test: Completed. All temporary data rolled back.";

                    performanceLabel.Text = $"Original: {originalTimeMs / 1000.0:F2}s, Optimized: {optimizedTimeMs / 1000.0:F2}s";
                    reportTextBox.Text = $"Performance Test Results (with {currentPerfTestRowCount} rows):\n" +
                                         $"- Original Script Execution Time: {originalTimeMs} ms ({originalTimeMs / 1000.0:F2}s)\n" +
                                         $"- Optimized Script Execution Time: {optimizedTimeMs} ms ({optimizedTimeMs / 1000.0:F2}s)\n\n" +
                                         "All changes involving temporary data have been rolled back.";
                    resultsTabControl.SelectedTab = performanceTab;

                }
                catch (Exception dbEx) // Catches errors during DB operations within transaction
                {
                    if (transaction?.Connection != null) // Check if transaction is still valid before trying to rollback
                    {
                        try { await transaction.RollbackAsync(); } // Use RollbackAsync
                        catch (Exception rbEx) { System.Diagnostics.Debug.WriteLine($"Rollback failed: {rbEx.Message}"); }
                    }
                    statusLabel.Text = $"Performance Test Failed (DB Operation): {dbEx.Message}";
                    reportTextBox.Text = $"Performance Test Database Operation Error:\n{dbEx.ToString()}";
                    resultsTabControl.SelectedTab = geminiReportTab; // Show error in report tab
                    System.Diagnostics.Debug.WriteLine($"Error during ExecutePerformanceTestAsync DB ops: {dbEx}");
                    File.AppendAllText("log.txt", $"Error during ExecutePerformanceTestAsync DB ops: {dbEx}\n");
                }
            }
            catch (Exception ex) // Catches errors in setup (connection, API calls before transaction)
            {
                statusLabel.Text = $"Performance Test Failed (Setup): {ex.Message}";
                reportTextBox.Text = $"Performance Test Setup Error:\n{ex.ToString()}";
                resultsTabControl.SelectedTab = geminiReportTab; // Show error in report tab
                System.Diagnostics.Debug.WriteLine($"Error in ExecutePerformanceTestAsync setup: {ex}");
                File.AppendAllText("log.txt", $"Error in ExecutePerformanceTestAsync setup: {ex}\n");
            }
            finally
            {
                 if (connection?.State == ConnectionState.Open)
                 {
                    await connection.CloseAsync();
                 }
            }
        }

        private async Task ExecuteLogicTestAsync()
        {
            statusLabel.Text = "Starting Logic Test...";
            performanceLabel.Text = "N/A (Logic Test)"; // Performance metrics are not applicable
            // reportTextBox.Text = ""; // Clear report text box for new comparison details

            string originalSqlScript = procedureBodyTextBox.Text;
            string optimizedSqlScript = optimizedProcedureTextBox.Text; // This is the "after" script
            string apiKey = geminiApiKeyTextBox.Text;
            string modelName = geminiModelTextBox.Text;
            string host = hostTextBox.Text;
            string user = userTextBox.Text;
            string password = passwordTextBox.Text;
            int currentLogicTestRowCount = (int)logicTestRowCount.Value;

            if (string.IsNullOrWhiteSpace(originalSqlScript))
            {
                statusLabel.Text = "Logic Test Aborted: Original SQL script is empty.";
                return;
            }
            if (string.IsNullOrWhiteSpace(optimizedSqlScript))
            {
                statusLabel.Text = "Logic Test Aborted: Optimized SQL script (for 'after' state) is empty.";
                return;
            }
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                statusLabel.Text = "Logic Test Aborted: Gemini API Key is empty.";
                return;
            }
             if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
            {
                statusLabel.Text = "Logic Test Aborted: Database connection details are incomplete.";
                return;
            }

            try
            {
                var geminiService = new GeminiService(apiKey);
                var oracleService = new OracleService();
                // Using a shorter timeout for CTE queries as they should be local once the CTE is built.
                string connectionString = $"Data Source={host};User Id={user};Password={password};Connection Timeout=30;";


                statusLabel.Text = "Logic Test: Getting table schema from Gemini...";
                string geminiSchemaJson = await geminiService.GetTableSchemaFromGemini(originalSqlScript, modelName);

                if (string.IsNullOrWhiteSpace(geminiSchemaJson) || geminiSchemaJson.StartsWith("-- Error"))
                {
                    statusLabel.Text = "Logic Test Failed: Could not get table schema. Check Gemini response.";
                    reportTextBox.Text = geminiSchemaJson; // Show error from Gemini if any
                    resultsTabControl.SelectedTab = geminiReportTab;
                    return;
                }

                statusLabel.Text = "Logic Test: Generating fake data CTE...";
                string fakeDataCte = GenerateFakeDataCte(geminiSchemaJson, currentLogicTestRowCount);

                if (string.IsNullOrWhiteSpace(fakeDataCte) || fakeDataCte.StartsWith("-- Error"))
                {
                    statusLabel.Text = "Logic Test Failed: Could not generate fake data CTE.";
                    reportTextBox.Text = fakeDataCte; // Show error from CTE generation if any
                    resultsTabControl.SelectedTab = geminiReportTab;
                    return;
                }

                string validationQueryBeforeWithCte = fakeDataCte + "\n" + originalSqlScript;
                string validationQueryAfterWithCte = fakeDataCte + "\n" + optimizedSqlScript;

                // For debugging:
                System.Diagnostics.Debug.WriteLine("--- Logic Test: Before Query with CTE ---");
                System.Diagnostics.Debug.WriteLine(validationQueryBeforeWithCte);
                System.Diagnostics.Debug.WriteLine("--- Logic Test: After Query with CTE ---");
                System.Diagnostics.Debug.WriteLine(validationQueryAfterWithCte);

                DataTable dataTableBefore;
                DataTable dataTableAfter;

                using (OracleConnection connection = new OracleConnection(connectionString))
                {
                    await connection.OpenAsync();
                    statusLabel.Text = "Logic Test: Executing 'before' script with fake data...";
                    dataTableBefore = await oracleService.ExecuteQueryWithinTransactionAsync(connection, null, validationQueryBeforeWithCte);

                    statusLabel.Text = "Logic Test: Executing 'after' script with fake data...";
                    dataTableAfter = await oracleService.ExecuteQueryWithinTransactionAsync(connection, null, validationQueryAfterWithCte);
                    // Connection will be closed by the using statement
                }

                statusLabel.Text = "Logic Test: Comparing results...";
                bool areIdentical = DataTableComparator.AreIdentical(dataTableBefore, dataTableAfter, out string comparisonDetails);

                reportTextBox.Text = $"Logic Test Comparison Details:\n{comparisonDetails}";
                resultsTabControl.SelectedTab = geminiReportTab; // Show comparison details in the report tab

                if (areIdentical)
                {
                    statusLabel.Text = "Logic Test: Passed. Results are identical.";
                }
                else
                {
                    statusLabel.Text = "Logic Test: Failed. Results differ.";
                }
            }
            catch (OracleException oraEx)
            {
                statusLabel.Text = $"Logic Test Oracle Error: {oraEx.Message}.";
                reportTextBox.Text = $"Logic Test Oracle Error:\n{oraEx.ToString()}";
                resultsTabControl.SelectedTab = geminiReportTab;
                File.AppendAllText("log.txt", $"Logic Test Oracle Error: {oraEx}\n");
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Logic Test Failed: {ex.Message}";
                reportTextBox.Text = $"Logic Test Exception:\n{ex.ToString()}";
                resultsTabControl.SelectedTab = geminiReportTab;
                System.Diagnostics.Debug.WriteLine($"Error in ExecuteLogicTestAsync: {ex}");
                File.AppendAllText("log.txt", $"Error in ExecuteLogicTestAsync: {ex}\n");
            }
        }

        // Helper classes for deserializing Gemini's schema JSON
        private class ColumnSchema
        {
            [JsonProperty("columnName")]
            public string? ColumnName { get; set; }

            [JsonProperty("dataType")]
            public string? DataType { get; set; }
        }

        private class TableSchema
        {
            [JsonProperty("tableName")]
            public string? TableName { get; set; }

            [JsonProperty("columns")]
            public List<ColumnSchema>? Columns { get; set; }
        }

        private string GenerateFakeDataCte(string geminiSchemaJson, int rowCount)
        {
            if (string.IsNullOrWhiteSpace(geminiSchemaJson))
            {
                // Or throw new ArgumentNullException(nameof(geminiSchemaJson));
                System.Diagnostics.Debug.WriteLine("GenerateFakeDataCte: geminiSchemaJson is null or empty.");
                return string.Empty;
            }
            if (rowCount <= 0)
            {
                // Or throw new ArgumentOutOfRangeException(nameof(rowCount));
                System.Diagnostics.Debug.WriteLine("GenerateFakeDataCte: rowCount must be positive.");
                return string.Empty;
            }

            List<TableSchema>? tableSchemas;
            try
            {
                tableSchemas = JsonConvert.DeserializeObject<List<TableSchema>>(geminiSchemaJson);
                if (tableSchemas == null || tableSchemas.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("GenerateFakeDataCte: Deserialized schema is null or empty.");
                    return string.Empty; // No tables to process
                }
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"GenerateFakeDataCte: Error parsing JSON schema: {ex.Message}");
                // Consider re-throwing or returning an error indicator if appropriate for the caller
                return $"-- Error parsing JSON schema: {ex.Message}\n";
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("WITH");

            for (int tableIdx = 0; tableIdx < tableSchemas.Count; tableIdx++)
            {
                TableSchema table = tableSchemas[tableIdx];
                if (table.Columns == null || table.Columns.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"GenerateFakeDataCte: Table {table.TableName} has no columns, skipping.");
                    continue; // Skip this table if it has no columns
                }

                sb.AppendLine($"  {table.TableName}_fake AS (");

                for (int i = 1; i <= rowCount; i++)
                {
                    sb.Append("    SELECT ");
                    for (int colIdx = 0; colIdx < table.Columns.Count; colIdx++)
                    {
                        ColumnSchema column = table.Columns[colIdx];
                        string generatedValue;
                        string? colDataTypeUpper = column.DataType?.ToUpperInvariant();

                        if (colDataTypeUpper == null) {
                            generatedValue = "NULL";
                        }
                        else if (colDataTypeUpper.StartsWith("VARCHAR2") || colDataTypeUpper.StartsWith("VARCHAR") || colDataTypeUpper.StartsWith("CHAR") || colDataTypeUpper.StartsWith("NVARCHAR2"))
                        {
                            // Using a simple concatenation with table, column, and row index for uniqueness and traceability
                            // Ensure column and table names are not null before trying to Substring
                            string tableNamePart = table.TableName != null ? table.TableName.Substring(0, Math.Min(table.TableName.Length, 3)) : "TAB";
                            string colNamePart = column.ColumnName != null ? column.ColumnName.Substring(0, Math.Min(column.ColumnName.Length, 3)) : "COL";
                            generatedValue = $"'Val_{tableNamePart}_{colNamePart}_{i}'";
                        }
                        else if (colDataTypeUpper.StartsWith("NUMBER") || colDataTypeUpper.StartsWith("INTEGER") || colDataTypeUpper.StartsWith("INT") || colDataTypeUpper.StartsWith("DECIMAL") || colDataTypeUpper.StartsWith("FLOAT"))
                        {
                            // Using a simple row index, easily predictable for testing
                            generatedValue = $"{i}";
                        }
                        else if (colDataTypeUpper.StartsWith("DATE"))
                        {
                            // Generating a sequence of dates starting from SYSDATE - rowCount days ago up to SYSDATE - 1 day ago
                            generatedValue = $"TO_DATE('2000-01-01', 'YYYY-MM-DD') + {i - 1}";
                        }
                        else
                        {
                            generatedValue = "NULL"; // Default for unknown types
                            System.Diagnostics.Debug.WriteLine($"GenerateFakeDataCte: Unsupported data type {column.DataType} for column {column.ColumnName} in table {table.TableName}. Using NULL.");
                        }

                        sb.Append($"{generatedValue} AS \"{column.ColumnName}\""); // Enclose column name in quotes

                        if (colIdx < table.Columns.Count - 1)
                        {
                            sb.Append(", ");
                        }
                    }
                    sb.AppendLine(" FROM DUAL");
                    if (i < rowCount)
                    {
                        sb.AppendLine("  UNION ALL");
                    }
                }
                sb.Append("  )"); // End of current fake table CTE definition

                if (tableIdx < tableSchemas.Count - 1)
                {
                    // Check if there's actually a next table to prevent trailing comma after last valid table.
                    // This check is a bit simplistic if some tables might be skipped.
                    // A better way would be to build a list of valid CTEs and then join them.
                    // For now, this assumes all tables in schema are processed.
                    sb.AppendLine(",");
                }
                else
                {
                    sb.AppendLine(); // Newline after the last CTE block
                }
            }

            // A more robust way to handle trailing commas if tables could be skipped:
            // Remove last comma if string ends with ",\n" or ",\r\n"
            string result = sb.ToString();
            if (result.EndsWith(",\n"))
            {
                result = result.Substring(0, result.Length - 2) + "\n";
            }
            else if (result.EndsWith($",{Environment.NewLine}"))
            {
                 result = result.Substring(0, result.Length - (Environment.NewLine.Length + 1)) + Environment.NewLine;
            }


            return result;
        }

        private string SanitizeForPlSqlIdentifier(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "default_identifier";

            // Replace non-alphanumeric characters (except underscore) with underscore
            string sanitized = System.Text.RegularExpressions.Regex.Replace(name, @"[^a-zA-Z0-9_]", "_");

            // Ensure it doesn't start with a number (PL/SQL identifiers cannot)
            if (char.IsDigit(sanitized[0]))
            {
                sanitized = "_" + sanitized;
            }
            // Max length for PL/SQL identifiers is typically 30 characters in older Oracle versions, 128c in newer.
            // Be mindful if very long table/column names are possible. Truncating might be needed.
            // For now, assume names are reasonably sized or rely on Oracle to handle longer names if supported.
            return sanitized.Length > 30 ? sanitized.Substring(0, 30) : sanitized; // Basic truncation for safety
        }

        private string SanitizeForPlSqlIdentifier(string? name) // CS8604: Make 'name' parameter nullable
        {
            if (string.IsNullOrWhiteSpace(name)) return "default_identifier";

            // Replace non-alphanumeric characters (except underscore) with underscore
            string sanitized = System.Text.RegularExpressions.Regex.Replace(name, @"[^a-zA-Z0-9_]", "_");

            // Ensure it doesn't start with a number (PL/SQL identifiers cannot)
            if (char.IsDigit(sanitized[0]))
            {
                sanitized = "_" + sanitized;
            }
            // Max length for PL/SQL identifiers is typically 30 characters in older Oracle versions, 128c in newer.
            // Be mindful if very long table/column names are possible. Truncating might be needed.
            // For now, assume names are reasonably sized or rely on Oracle to handle longer names if supported.
            return sanitized.Length > 30 ? sanitized.Substring(0, 30) : sanitized; // Basic truncation for safety
        }

        private string GenerateInsertStatements(string geminiSchemaJson, int rowCount, out List<string> tableNames)
        {
            tableNames = new List<string>();
            if (string.IsNullOrWhiteSpace(geminiSchemaJson))
            {
                System.Diagnostics.Debug.WriteLine("GenerateInsertStatements: geminiSchemaJson is null or empty.");
                return "-- Error: Gemini schema JSON is null or empty.\n";
            }
            if (rowCount <= 0)
            {
                System.Diagnostics.Debug.WriteLine("GenerateInsertStatements: rowCount must be positive.");
                return "-- Error: Row count must be positive.\n";
            }

            List<TableSchema>? tableSchemas;
            try
            {
                tableSchemas = JsonConvert.DeserializeObject<List<TableSchema>>(geminiSchemaJson);
                if (tableSchemas == null || tableSchemas.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("GenerateInsertStatements: Deserialized schema is null or empty.");
                    return "-- Error: Deserialized schema is null or contains no tables.\n";
                }
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"GenerateInsertStatements: Error parsing JSON schema: {ex.Message}");
                return $"-- Error parsing JSON schema: {ex.Message}\n";
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("DECLARE");

            foreach (TableSchema table in tableSchemas)
            {
                if (table.Columns == null || table.Columns.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"GenerateInsertStatements: Table {table.TableName} has no columns, skipping for PL/SQL generation.");
                    continue;
                }
                string sanitizedTableName = SanitizeForPlSqlIdentifier(table.TableName);
                // It's important to add the original table name to the list for later use (e.g. TRUNCATE)
                tableNames.Add($"\"{table.TableName}\"");

                sb.AppendLine($"  TYPE T_Fake_{sanitizedTableName}_Rows IS TABLE OF \"{table.TableName}\"%ROWTYPE INDEX BY PLS_INTEGER;");
                sb.AppendLine($"  V_Fake_{sanitizedTableName}_Data T_Fake_{sanitizedTableName}_Rows;");
            }
            sb.AppendLine("BEGIN");

            foreach (TableSchema table in tableSchemas)
            {
                 if (table.Columns == null || table.Columns.Count == 0)
                {
                    continue; // Already logged, just skip here
                }
                string sanitizedTableName = SanitizeForPlSqlIdentifier(table.TableName);
                sb.AppendLine($"  -- Populating data for table: {table.TableName}");
                sb.AppendLine($"  FOR i IN 1..{rowCount} LOOP");

                foreach (ColumnSchema column in table.Columns)
                {
                    string generatedValue = "NULL"; // Ensure generatedValue is always initialized
                    string? columnDataTypeUpper = column.DataType?.ToUpperInvariant();

                    if (columnDataTypeUpper == null)
                    {
                        // generatedValue remains "NULL"
                        System.Diagnostics.Debug.WriteLine($"GenerateInsertStatements: Null data type for column {column.ColumnName} in table {table.TableName}. Using NULL.");
                    }
                    else if (columnDataTypeUpper.StartsWith("VARCHAR2") || columnDataTypeUpper.StartsWith("VARCHAR") || columnDataTypeUpper.StartsWith("CHAR") || columnDataTypeUpper.StartsWith("NVARCHAR2"))
                    {
                        int declaredLength = 30; // Default
                        if (column.DataType != null && column.DataType.Contains("("))
                        {
                            try
                            {
                                int startIndex = column.DataType.IndexOf("(") + 1;
                                int endIndex = column.DataType.IndexOf(")");
                                if (endIndex > startIndex)
                                {
                                    string lenStr = column.DataType.Substring(startIndex, endIndex - startIndex);
                                    if (int.TryParse(lenStr, out int parsedLength))
                                    {
                                        declaredLength = Math.Max(1, Math.Min(parsedLength, 4000)); // Clamp to reasonable Oracle limits
                                    }
                                }
                            }
                            catch { /* Use default if parsing fails */ }
                        }

                        int declaredLength = 30; // Default
                        if (column.DataType != null && column.DataType.Contains("("))
                        {
                            try
                            {
                                int startIndex = column.DataType.IndexOf("(") + 1;
                                int endIndex = column.DataType.IndexOf(")");
                                if (endIndex > startIndex)
                                {
                                    string lenStr = column.DataType.Substring(startIndex, endIndex - startIndex);
                                    if (int.TryParse(lenStr, out int parsedLength))
                                    {
                                        declaredLength = Math.Max(1, Math.Min(parsedLength, 4000)); // Clamp to reasonable Oracle limits
                                    }
                                }
                            }
                            catch { /* Use default if parsing fails */ }
                        }

                        // CS0122 Fix: Use maxILength for C# calculations regarding the PL/SQL 'i' variable's string representation.
                        int maxILength = rowCount.ToString().Length;
                        string prefixForCalc = "Val_";
                        string suffixTemplateForCalc = "_"; // Represents the underscore before 'i' in generated PL/SQL

                        // Calculate length needed for prefix, the underscore, and the max possible length of 'i' as a string.
                        int fixedPartsLength = prefixForCalc.Length + suffixTemplateForCalc.Length + maxILength;
                        int availableLength = declaredLength - fixedPartsLength - 2; // -2 for the quotes ''

                        if (availableLength >= 1)
                        {
                            int randomPartLength = Math.Min(availableLength, 20); // Max 20 chars for random part
                            // Generated PL/SQL will use the actual PL/SQL loop variable 'i'
                            generatedValue = $"'{prefixForCalc}' || DBMS_RANDOM.STRING('A', {randomPartLength}) || '{suffixTemplateForCalc}' || i";
                        }
                        else
                        {
                            // Fallback: Try to generate 'Err_' || i, ensuring it fits.
                            string errPrefixForCalc = "Err_";
                            fixedPartsLength = errPrefixForCalc.Length + maxILength;
                            availableLength = declaredLength - fixedPartsLength - 2; // -2 for quotes

                            if (availableLength >= 0) // Need at least 0 for the errPrefix to concatenate with i
                            {
                                 // Ensure the 'Err_' and 'i' concatenation doesn't exceed declaredLength.
                                 // This is tricky because 'i' in PL/SQL varies. The check is against maxILength.
                                 // A simpler robust fallback is to use a very short string or i truncated if possible.
                                generatedValue = $"'E_' || TO_CHAR(i)"; // Example: E_1, E_100
                                // Check if this simple fallback itself is too long
                                // Max length of 'E_' || i is 'E_'.Length + maxILength
                                if (("E_".Length + maxILength + 2) > declaredLength)
                                {
                                    if (declaredLength >= 2) generatedValue = "''"; // Empty string if 'E_i' is too long
                                    else generatedValue = "NULL"; // If even '' is too long
                                }
                            }
                            else if (declaredLength >= 2)
                            {
                                generatedValue = "''"; // Empty Oracle string if nothing else fits
                            }
                            else
                            {
                                generatedValue = "NULL"; // Cannot fit even ''
                            }
                            System.Diagnostics.Debug.WriteLine($"GenerateInsertStatements: VARCHAR2 column {column.ColumnName} in table {table.TableName} has declared length {declaredLength} too small for full pattern 'Val_RANDOM_i'. Using fallback: {generatedValue}");
                        }
                    }
                    else if (columnDataTypeUpper.StartsWith("NUMBER") || columnDataTypeUpper.StartsWith("INTEGER") || columnDataTypeUpper.StartsWith("INT") || columnDataTypeUpper.StartsWith("DECIMAL") || columnDataTypeUpper.StartsWith("FLOAT"))
                    {
                        generatedValue = $"TRUNC(DBMS_RANDOM.VALUE(1, 100000)) + MOD(i, 100000)";
                    }
                    else if (columnDataTypeUpper.StartsWith("DATE"))
                    {
                        generatedValue = $"TO_DATE('2000-01-01', 'YYYY-MM-DD') + MOD(i-1, 365*50)"; // spread over 50 years
                    }
                    else
                    {
                        // generatedValue remains "NULL"
                        System.Diagnostics.Debug.WriteLine($"GenerateInsertStatements: Unsupported data type {column.DataType} for column {column.ColumnName} in table {table.TableName}. Using NULL.");
                    }

                    sb.AppendLine($"    V_Fake_{sanitizedTableName}_Data(i).\"{column.ColumnName}\" := {generatedValue};");
                }
                sb.AppendLine("  END LOOP;");
                sb.AppendLine();
            }

            // After populating all collections, do the inserts
            foreach (TableSchema table in tableSchemas)
            {
                if (table.Columns == null || table.Columns.Count == 0)
                {
                    continue; // Skip if no columns
                }
                string sanitizedTableName = SanitizeForPlSqlIdentifier(table.TableName);
                sb.AppendLine($"  -- Inserting data into table: {table.TableName}");
                sb.AppendLine($"  FORALL i IN V_Fake_{sanitizedTableName}_Data.FIRST..V_Fake_{sanitizedTableName}_Data.LAST");
                // Table name in INSERT INTO should be quoted if it can contain special characters or is case-sensitive.
                sb.AppendLine($"    INSERT INTO \"{table.TableName}\" VALUES V_Fake_{sanitizedTableName}_Data(i);");
                sb.AppendLine();
            }

            sb.AppendLine("END;");
            return sb.ToString();
        }
    }
}