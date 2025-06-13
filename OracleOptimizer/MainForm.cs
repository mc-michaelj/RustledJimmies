using OracleOptimizer.Services;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.IO;
using Newtonsoft.Json;
using System.Text;
using OracleOptimizer; // Added for Logger
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
            // The Gemini API key should be stored securely, not hardcoded.
            // For example, use environment variables, a secure configuration file, or a secrets manager.
            // Consider using PlaceholderText for initial guidance in the UI if needed.
            geminiApiKeyTextBox.Text = "";
            geminiApiKeyTextBox.PlaceholderText = "Enter API Key (Store Securely - See App Docs)";
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

        private async void analyzeButton_Click(object? sender, EventArgs e)
        {
            analyzeButton.Enabled = false;
            statusLabel.Text = "Processing...";
            performanceLabel.Text = ""; // Clear performance label

            try
            {
                Logger.LogInfo("Analysis process started by user.");
                await ExecuteAnalysisAndTestingAsync();
                Logger.LogInfo("Analysis process completed successfully.");
            }
            catch (OracleException oraEx)
            {
                string userMessage = "An Oracle database error occurred. Please check your connection details and credentials. Contact support if the issue persists.";
                if (oraEx.Number == 1017) // ORA-01017: invalid username/password; logon denied
                {
                    userMessage = "Oracle Error: Invalid username or password. Please verify your credentials.";
                }
                else if (oraEx.Number == 12170 || oraEx.Number == 12541 || oraEx.Number == 12514) // TNS errors
                {
                    userMessage = "Oracle Error: Could not connect to the database. Verify host, port, and service name. Ensure the listener is running.";
                }
                else if (oraEx.Message.Contains("ORA-50000")) // Custom application error
                {
                    userMessage = "An application-specific Oracle error occurred. Details have been logged.";
                }
                statusLabel.Text = userMessage;
                Logger.LogError("OracleException in analyzeButton_Click", oraEx);
            }
            catch (HttpRequestException httpEx)
            {
                statusLabel.Text = "API Request Error: Could not connect to the analysis service. Check your network connection and API key.";
                Logger.LogError("HttpRequestException in analyzeButton_Click", httpEx);
            }
            catch (JsonException jsonEx)
            {
                statusLabel.Text = "Data Error: Could not process the data received from the analysis service. Details have been logged.";
                Logger.LogError("JsonException in analyzeButton_Click", jsonEx);
            }
            catch (InvalidOperationException ioEx) // Can be thrown by various operations
            {
                statusLabel.Text = "An internal operation error occurred. Details have been logged.";
                Logger.LogError("InvalidOperationException in analyzeButton_Click", ioEx);
            }
            catch (ArgumentNullException argNullEx)
            {
                statusLabel.Text = $"Input Error: {argNullEx.Message}"; // Display specific message from ArgumentNullException
                Logger.LogError("ArgumentNullException in analyzeButton_Click", argNullEx);
            }
            catch (ArgumentException argEx) // Catch new validation errors
            {
                statusLabel.Text = $"Input Error: {argEx.Message}"; // Display specific message from ArgumentException
                Logger.LogError("ArgumentException in analyzeButton_Click", argEx);
            }
            catch (Exception ex) // General fallback
            {
                statusLabel.Text = "An unexpected error occurred. Details have been logged.";
                Logger.LogError("Generic Exception in analyzeButton_Click", ex);
            }
            finally
            {
                analyzeButton.Enabled = true;
            }
        }

        private async Task ExecuteAnalysisAndTestingAsync()
        {
            // Read TEST_ROW_COUNT from the NumericUpDown control
            int testRowCount = (int)testRowCountNumericUpDown.Value;
            Logger.LogInfo($"Using Test Row Count: {testRowCount}");

            Func<string, string> convertProcedureToExecutableBlock = (procedureBody) =>
            {
                string executableBlock = procedureBody.Trim();
                var regex = new System.Text.RegularExpressions.Regex(@"\A\s*PROCEDURE\s+.*?(\s+IS|\s+AS)\s+", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
                executableBlock = regex.Replace(executableBlock, "DECLARE\n", 1);
                regex = new System.Text.RegularExpressions.Regex(@"END\s+.*?;", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.RightToLeft);
                executableBlock = regex.Replace(executableBlock, "END;", 1);
                return executableBlock;
            };

            string host = hostTextBox.Text;
            string user = userTextBox.Text;
            string password = passwordTextBox.Text;
            string apiKey = geminiApiKeyTextBox.Text;
            string originalProcedure = procedureBodyTextBox.Text;
            string modelName = geminiModelTextBox.Text;

            // Input Validation
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentNullException(nameof(host), "Host (Data Source) cannot be empty.");
            // Basic check for Oracle easy connect string format (e.g., hostname:port/service_name)
            if (!host.Contains(':') || !host.Contains('/'))
            {
                throw new ArgumentException("Invalid Host (Data Source) format. Expected format like 'hostname:port/service_name'.", nameof(host));
            }
            if (string.IsNullOrWhiteSpace(user)) throw new ArgumentNullException(nameof(user), "User ID cannot be empty.");
            if (string.IsNullOrWhiteSpace(password)) throw new ArgumentNullException(nameof(password), "Password cannot be empty.");
            if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentNullException(nameof(apiKey), "API Key cannot be empty.");
            if (string.IsNullOrWhiteSpace(modelName)) throw new ArgumentNullException(nameof(modelName), "Gemini Model Name cannot be empty.");

            if (string.IsNullOrWhiteSpace(originalProcedure)) throw new ArgumentNullException(nameof(originalProcedure), "Procedure body cannot be empty.");
            const int MAX_PROCEDURE_LENGTH = 50000; // Max 50k chars
            if (originalProcedure.Length > MAX_PROCEDURE_LENGTH)
            {
                throw new ArgumentException($"Procedure body is too long. Maximum length is {MAX_PROCEDURE_LENGTH} characters.", nameof(originalProcedure));
            }
            // Basic check for non-ASCII characters or suspicious patterns if desired - keeping it simple for now.

            Logger.LogInfo($"Starting analysis for host: {host}, user: {user}, model: {modelName}");

            // Step 1 & 2: Analyze with Gemini and get schema
            statusLabel.Text = "1/5: Analyzing with Gemini for optimization...";
            (var geminiResponse, var geminiSchemaJson) = await GetGeminiAnalysisAndSchemaAsync(apiKey, originalProcedure, modelName);

            reportTextBox.Text = geminiResponse.Explanation;
            optimizedProcedureTextBox.Text = geminiResponse.OptimizedProcedureBody;
            resultsTabControl.SelectedTab = geminiReportTab;

            // Step 3, 4, 5: Run procedures and compare results
            string connectionString = $"Data Source={host};User Id={user};Password={password};Connection Timeout=120;";
            string finalReportText = await ExecuteAndCompareOracleProceduresAsync(
                new OracleService(), connectionString, originalProcedure, geminiResponse.OptimizedProcedureBody,
                geminiSchemaJson, geminiResponse.ValidationQueryAfter, convertProcedureToExecutableBlock, testRowCount // Use the variable here
            );

            performanceLabel.Text = finalReportText;
            resultsTabControl.SelectedTab = performanceTab;
            statusLabel.Text = "Analysis and Testing Complete.";
        }

        private async Task<string> ExecuteAndCompareOracleProceduresAsync(
            OracleService oracleService, string connectionString, string originalProcedure, string optimizedProcedureBody,
            string geminiSchemaJson, string validationQuery, Func<string, string> convertProcedureToExecutableBlock, int testRowCount)
        {
            statusLabel.Text = "3/5: Preparing test data..."; // Changed
            Logger.LogInfo("Generating test data for procedure runs.");
            string testDataPlSql = GenerateInsertStatements(geminiSchemaJson, testRowCount, out List<string> tableNamesToTruncate);
            if (testDataPlSql.StartsWith("-- Error"))
            {
                Logger.LogError($"Failed to generate test data PL/SQL: {testDataPlSql}");
                throw new InvalidOperationException("Failed to generate test data for database operations. Check logs for schema parsing issues.");
            }

            // Run Original Procedure
            statusLabel.Text = "3/5: Running Original Procedure...";
            string originalExecutableBlock = convertProcedureToExecutableBlock(originalProcedure);
            Logger.LogInfo("Executing original procedure.");
            (DataTable originalData, long originalTime) = await RunProcedureAndGetDataAsync(oracleService, connectionString, testDataPlSql, tableNamesToTruncate, originalExecutableBlock, validationQuery, "Original");

            // Run Optimized Procedure
            statusLabel.Text = "4/5: Running Optimized Procedure...";
            string optimizedExecutableBlock = convertProcedureToExecutableBlock(optimizedProcedureBody);
            Logger.LogInfo("Executing optimized procedure.");
            (DataTable optimizedData, long optimizedTime) = await RunProcedureAndGetDataAsync(oracleService, connectionString, testDataPlSql, tableNamesToTruncate, optimizedExecutableBlock, validationQuery, "Optimized");

            // Compare Results
            statusLabel.Text = "5/5: Comparing results...";
            Logger.LogInfo("Comparing results from original and optimized procedure runs.");
            bool areIdentical = DataTableComparator.AreIdentical(originalData, optimizedData, out string comparisonDetails);

            return (areIdentical ? "✅ LOGIC TEST PASSED" : "❌ LOGIC TEST FAILED") + "\n\n" +
                   $"PERFORMANCE:\n" +
                   $"- Original:    {originalTime}ms\n" +
                   $"- Optimized:   {optimizedTime}ms\n" +
                   $"- Improvement: {originalTime - optimizedTime}ms\n\n" +
                   $"VALIDATION DETAILS:\n{comparisonDetails}";
        }

        private async Task<(GeminiSqlAnalysisResponse geminiResponse, string geminiSchemaJson)> GetGeminiAnalysisAndSchemaAsync(string apiKey, string originalProcedure, string modelName)
        {
            optimizedProcedureTextBox.Text = ""; // Clear previous results
            reportTextBox.Text = "";             // Clear previous results

            var geminiService = new GeminiService(apiKey);

            Logger.LogInfo("Requesting SQL analysis from Gemini.");
            statusLabel.Text = "1/5: Analyzing SQL with Gemini..."; // More specific status
            var geminiResponse = await geminiService.AnalyzeSqlAsync(originalProcedure, modelName);
            if (geminiResponse == null || string.IsNullOrWhiteSpace(geminiResponse.OptimizedProcedureBody) || string.IsNullOrWhiteSpace(geminiResponse.ValidationQueryAfter))
            {
                Logger.LogError("Gemini service returned invalid or incomplete data from AnalyzeSqlAsync.", new InvalidDataException("Gemini response (AnalyzeSqlAsync) was null or missing critical fields."));
                throw new InvalidOperationException("The analysis service (Gemini) did not return a valid or complete SQL analysis. Please check the logs.");
            }
            Logger.LogInfo("Successfully received SQL analysis from Gemini.");

            statusLabel.Text = "2/5: Getting table schema from Gemini..."; // More specific status
            Logger.LogInfo("Requesting table schema from Gemini.");
            string geminiSchemaJson = await geminiService.GetTableSchemaFromGemini(originalProcedure, modelName);
            if (string.IsNullOrWhiteSpace(geminiSchemaJson) || geminiSchemaJson.StartsWith("-- Error"))
            {
                Logger.LogError($"Failed to get table schema from Gemini. Response: {geminiSchemaJson}", new InvalidDataException("Gemini schema response was invalid."));
                throw new InvalidOperationException($"Failed to get table schema from the analysis service. Response: {geminiSchemaJson}");
            }
            Logger.LogInfo("Successfully received table schema from Gemini.");

            return (geminiResponse, geminiSchemaJson);
        }

        private async Task ClearTablesAsync(OracleService oracleService, OracleConnection connection, OracleTransaction transaction, List<string> tableNames)
        {
            var uniqueTableNames = new HashSet<string>(tableNames.Select(t => t.ToUpperInvariant()));
            if (!uniqueTableNames.Any()) return;

            statusLabel.Text = "Analyzing table dependencies for cleanup...";
            var connectionStringBuilder = new OracleConnectionStringBuilder(connection.ConnectionString);
            string currentUser = connectionStringBuilder.UserID.ToUpper();

            var parsedTablesInfo = uniqueTableNames.Select(fullTableName =>
            {
                string[] parts = fullTableName.Split('.');
                return (Owner: parts.Length > 1 ? parts[0] : currentUser, TableName: parts.Length > 1 ? parts[1] : parts[0], FullName: fullTableName);
            }).ToList();

            var tableLookupByOwnerTable = parsedTablesInfo.ToDictionary(t => (t.Owner, t.TableName), t => t.FullName);

            var dependencies = await GetTableDependenciesAsync(oracleService, connection, transaction, parsedTablesInfo, tableLookupByOwnerTable);
            var sortedTablesToClear = SortTablesForDeletion(uniqueTableNames, dependencies);

            if (sortedTablesToClear.Any()) statusLabel.Text = "Clearing data from identified tables...";
            foreach (var table in sortedTablesToClear)
            {
                Logger.LogInfo($"Executing DELETE FROM {table}");
                await oracleService.ExecuteNonQueryAsync(connection, transaction, $"DELETE FROM {table}");
            }
        }

        private async Task<Dictionary<string, HashSet<string>>> GetTableDependenciesAsync(
            OracleService oracleService, OracleConnection connection, OracleTransaction transaction,
            List<(string Owner, string TableName, string FullName)> parsedTablesInfo,
            Dictionary<(string Owner, string TableName), string> tableLookupByOwnerTable)
        {
            var dependencies = new Dictionary<string, HashSet<string>>();
            var whereClauses = new List<string>();
            var parameters = new List<OracleParameter>();
            int i = 0;

            foreach (var (owner, tableName, _) in parsedTablesInfo)
            {
                whereClauses.Add($"(a.owner = :p_owner{i} AND a.table_name = :p_table_name{i})");
                parameters.Add(new OracleParameter($"p_owner{i}", owner));
                parameters.Add(new OracleParameter($"p_table_name{i}", tableName));
                i++;
            }

            if (!whereClauses.Any()) return dependencies;

            string sql = $@"
                SELECT a.owner, a.table_name, r.owner AS r_owner, r.table_name AS r_table_name
                FROM all_constraints a
                JOIN all_constraints r ON a.r_constraint_name = r.constraint_name AND a.r_owner = r.owner
                WHERE a.constraint_type = 'R'
                AND ({string.Join(" OR ", whereClauses)})";

            using (var cmd = new OracleCommand(sql, connection))
            {
                cmd.Transaction = transaction;
                cmd.Parameters.AddRange(parameters.ToArray());
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var childOwner = reader.GetString(0).ToUpper();
                        var childTable = reader.GetString(1).ToUpper();
                        var parentOwner = reader.GetString(2).ToUpper();
                        var parentTable = reader.GetString(3).ToUpper();

                        if (tableLookupByOwnerTable.TryGetValue((childOwner, childTable), out var childFullName) &&
                            tableLookupByOwnerTable.TryGetValue((parentOwner, parentTable), out var parentFullName))
                        {
                            if (!dependencies.ContainsKey(parentFullName))
                            {
                                dependencies[parentFullName] = new HashSet<string>();
                            }
                            dependencies[parentFullName].Add(childFullName);
                        }
                    }
                }
            }
            return dependencies;
        }

        private List<string> SortTablesForDeletion(HashSet<string> uniqueTableNames, Dictionary<string, HashSet<string>> dependencies)
        {
            var sortedList = new List<string>();
            var inDegree = uniqueTableNames.ToDictionary(t => t, t => 0);
            // Correctly build the graph: if B depends on A (A is parent of B), edge is A -> B
            // For deletion, we want to delete children first. So if A is parent of B, B should come before A.
            // Kahn's algorithm gives a topological sort. If we build graph as Parent -> Child,
            // then process nodes with in-degree 0. These are tables not depended upon by other tables in the set.
            // This is the reverse of what we want for deletion (delete children first).
            // So, we should build the graph as Child -> Parent for Kahn's.
            // Or, build Parent -> Child and then reverse the sorted list. Let's try Child -> Parent.

            // Child -> Set of Parents that are within uniqueTableNames
            var graph = uniqueTableNames.ToDictionary(t => t, t => new HashSet<string>());
            // In-degree: count of tables (within uniqueTableNames) that this table depends on.
            // So, if B depends on A (A is parent), B has an in-degree related to A.

            // Let's re-think: We want to delete tables that nothing else (in our list) depends on first.
            // These are the "leaf" nodes in a dependency graph where edge A -> B means A must exist before B (B depends on A).
            // So, we want to delete B before A. This means we process nodes with no outgoing edges to other nodes in the list first.
            // Kahn's algorithm sorts by processing nodes with in-degree 0.
            // If A->B means B depends on A:
            // Parent A, Child B. To delete B then A, A must have an in-degree of 0 from B's perspective in a reversed graph.

            // Let's stick to standard Kahn's: A is prerequisite for B (B depends on A). Edge A -> B.
            // In-degree of B is 1 (from A). In-degree of A is 0. Kahn's will output A, then B.
            // This is the order of CREATION. For deletion, we need the REVERSE order: B, then A.

            // Graph: Key = Table, Value = Set of tables that depend on Key (Children of Key)
            var adjacencyList = uniqueTableNames.ToDictionary(t => t, t => new HashSet<string>());
            var currentInDegree = uniqueTableNames.ToDictionary(t => t, t => 0);

            foreach (var parentTable in dependencies.Keys) // parentTable is a key in dependencies
            {
                foreach (var childTable in dependencies[parentTable]) // childTable depends on parentTable
                {
                    if (uniqueTableNames.Contains(parentTable) && uniqueTableNames.Contains(childTable))
                    {
                        if (adjacencyList[parentTable].Add(childTable)) // Edge: parentTable -> childTable
                        {
                            currentInDegree[childTable]++;
                        }
                    }
                }
            }

            var queue = new Queue<string>(currentInDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
            while (queue.Count > 0)
            {
                var table = queue.Dequeue();
                sortedList.Add(table);

                foreach (var dependentTable in adjacencyList[table]) // For each table that depends on `table`
                {
                    currentInDegree[dependentTable]--;
                    if (currentInDegree[dependentTable] == 0)
                    {
                        queue.Enqueue(dependentTable);
                    }
                }
            }

            if (sortedList.Count < uniqueTableNames.Count)
            {
                var missing = string.Join(", ", uniqueTableNames.Except(sortedList));
                Logger.LogError($"Cyclic dependency detected among tables. Problematic tables might include: {missing}. Full list: {string.Join(", ", uniqueTableNames)}", new InvalidOperationException("Cyclic dependency in tables."));
                throw new InvalidOperationException($"Cyclic dependency detected among tables, cannot determine deletion order. Problematic tables might include: {missing}");
            }

            // For deletion, we need to delete in reverse topological order
            sortedList.Reverse();
            return sortedList;
        }

        private async Task<(DataTable results, long time)> RunProcedureAndGetDataAsync(
            OracleService oracleService, string connectionString, string testDataPlSql,
            List<string> tablesToTruncate, string procedureExecutableBlock, string validationQuery, string procedureType /* "Original" or "Optimized" */)
        {
            Logger.LogInfo($"Starting RunProcedureAndGetDataAsync for {procedureType} procedure.");
            using var connection = new OracleConnection(connectionString);
            await connection.OpenAsync();
            Logger.LogInfo($"Database connection opened for {procedureType}.");
            using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
            Logger.LogInfo($"Transaction started for {procedureType}.");

            try
            {
                statusLabel.Text = $"Clearing test tables for {procedureType} run...";
                Logger.LogInfo($"Clearing tables for {procedureType}: {string.Join(", ", tablesToTruncate)}");
                await ClearTablesAsync(oracleService, connection, transaction, tablesToTruncate);
                Logger.LogInfo($"Tables cleared for {procedureType}.");

                if (!string.IsNullOrWhiteSpace(testDataPlSql))
                {
                    statusLabel.Text = $"Inserting test data for {procedureType} run...";
                    Logger.LogInfo($"Inserting test data for {procedureType}.");
                    await oracleService.ExecuteNonQueryAsync(connection, transaction, testDataPlSql);
                    Logger.LogInfo($"Test data inserted for {procedureType}.");
                }

                statusLabel.Text = $"Executing {procedureType} procedure...";
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                Logger.LogInfo($"Executing {procedureType} procedure block.");
                await oracleService.ExecuteNonQueryAsync(connection, transaction, procedureExecutableBlock);
                stopwatch.Stop();
                Logger.LogInfo($"{procedureType} procedure executed in {stopwatch.ElapsedMilliseconds}ms.");

                statusLabel.Text = $"Validating results for {procedureType} procedure...";
                Logger.LogInfo($"Executing validation query for {procedureType}.");
                var dataTable = await oracleService.ExecuteQueryWithinTransactionAsync(connection, transaction, validationQuery);
                Logger.LogInfo($"Validation query completed for {procedureType}.");

                await transaction.RollbackAsync();
                Logger.LogInfo($"Transaction rolled back for {procedureType}.");
                return (dataTable, stopwatch.ElapsedMilliseconds);
            }
            catch (OracleException oraEx)
            {
                Logger.LogError($"OracleException during {procedureType} procedure execution or data retrieval.", oraEx);
                await transaction.RollbackAsync(); // Ensure rollback on error
                Logger.LogInfo($"Transaction rolled back for {procedureType} due to OracleException.");
                // Re-throw to be caught by the main handler in analyzeButton_Click
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Generic Exception during {procedureType} procedure execution or data retrieval.", ex);
                await transaction.RollbackAsync(); // Ensure rollback on error
                Logger.LogInfo($"Transaction rolled back for {procedureType} due to Exception.");
                // Re-throw to be caught by the main handler in analyzeButton_Click
                throw;
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
                Logger.LogInfo("GenerateFakeDataCte: geminiSchemaJson is null or empty.");
                return string.Empty;
            }
            if (rowCount <= 0)
            {
                Logger.LogInfo("GenerateFakeDataCte: rowCount must be positive.");
                return string.Empty;
            }

            List<TableSchema>? tableSchemas;
            try
            {
                tableSchemas = JsonConvert.DeserializeObject<List<TableSchema>>(geminiSchemaJson);
                if (tableSchemas == null || tableSchemas.Count == 0)
                {
                    Logger.LogInfo("GenerateFakeDataCte: Deserialized schema is null or empty.");
                    return string.Empty; // No tables to process
                }
            }
            catch (JsonException ex)
            {
                Logger.LogError("GenerateFakeDataCte: Error parsing JSON schema", ex);
                return $"-- Error parsing JSON schema: {ex.Message}\n";
            }

            var cteParts = new List<string>();
            foreach (var table in tableSchemas)
            {
                if (table.Columns == null || table.Columns.Count == 0)
                {
                    Logger.LogInfo($"GenerateFakeDataCte: Table {table.TableName} has no columns, skipping.");
                    continue; // Skip this table if it has no columns
                }

                var sbCte = new StringBuilder();
                sbCte.AppendLine($"  {table.TableName}_fake AS (");

                for (int i = 1; i <= rowCount; i++)
                {
                    sbCte.Append("    SELECT ");
                    for (int colIdx = 0; colIdx < table.Columns.Count; colIdx++)
                    {
                        ColumnSchema column = table.Columns[colIdx];
                        string generatedValue;
                        string? colDataTypeUpper = column.DataType?.ToUpperInvariant();

                        if (colDataTypeUpper == null)
                        {
                            generatedValue = "NULL";
                        }
                        else if (colDataTypeUpper.StartsWith("VARCHAR2") || colDataTypeUpper.StartsWith("VARCHAR") || colDataTypeUpper.StartsWith("CHAR") || colDataTypeUpper.StartsWith("NVARCHAR2"))
                        {
                            string tableNamePart = table.TableName != null ? table.TableName.Substring(0, Math.Min(table.TableName.Length, 3)) : "TAB";
                            string colNamePart = column.ColumnName != null ? column.ColumnName.Substring(0, Math.Min(column.ColumnName.Length, 3)) : "COL";
                            generatedValue = $"'Val_{tableNamePart}_{colNamePart}_{i}'";
                        }
                        else if (colDataTypeUpper.StartsWith("NUMBER") || colDataTypeUpper.StartsWith("INTEGER") || colDataTypeUpper.StartsWith("INT") || colDataTypeUpper.StartsWith("DECIMAL") || colDataTypeUpper.StartsWith("FLOAT"))
                        {
                            generatedValue = $"{i}";
                        }
                        else if (colDataTypeUpper.StartsWith("DATE"))
                        {
                            generatedValue = $"TO_DATE('2000-01-01', 'YYYY-MM-DD') + {i - 1}";
                        }
                        else
                        {
                            generatedValue = "NULL";
                            Logger.LogInfo($"GenerateFakeDataCte: Unsupported data type {column.DataType} for column {column.ColumnName} in table {table.TableName}. Using NULL.");
                        }

                        sbCte.Append($"{generatedValue} AS \"{column.ColumnName}\"");

                        if (colIdx < table.Columns.Count - 1)
                        {
                            sbCte.Append(", ");
                        }
                    }
                    sbCte.AppendLine(" FROM DUAL");
                    if (i < rowCount)
                    {
                        sbCte.AppendLine("  UNION ALL");
                    }
                }
                sbCte.Append("  )");
                cteParts.Add(sbCte.ToString());
            }

            if (cteParts.Count == 0)
            {
                return string.Empty; // No CTEs were generated
            }

            return "WITH\n" + string.Join(",\n", cteParts) + "\n";
        }

        private string SanitizeForPlSqlIdentifier(string? name) // CS8604: Make 'name' parameter nullable
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                Logger.LogInfo("SanitizeForPlSqlIdentifier: Input name is null or whitespace, returning 'default_identifier'.");
                return "default_identifier";
            }
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

        private List<TableSchema>? DeserializeTableSchemas(string geminiSchemaJson, out string? errorResult)
        {
            errorResult = null;
            if (string.IsNullOrWhiteSpace(geminiSchemaJson))
            {
                Logger.LogError("DeserializeTableSchemas: geminiSchemaJson is null or empty.");
                errorResult = "-- Error: Gemini schema JSON is null or empty.\n";
                return null;
            }

            try
            {
                var tableSchemas = JsonConvert.DeserializeObject<List<TableSchema>>(geminiSchemaJson);
                if (tableSchemas == null || tableSchemas.Count == 0)
                {
                    Logger.LogError("DeserializeTableSchemas: Deserialized schema is null or empty.");
                    errorResult = "-- Error: Deserialized schema is null or contains no tables.\n";
                    return null;
                }
                return tableSchemas;
            }
            catch (JsonException ex)
            {
                Logger.LogError("DeserializeTableSchemas: Error parsing JSON schema", ex);
                errorResult = $"-- Error parsing JSON schema: {ex.Message}\n";
                return null;
            }
        }

        private string GeneratePlSqlDeclarations(List<TableSchema> tableSchemas, List<string> outTableNames)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("DECLARE");
            foreach (TableSchema table in tableSchemas)
            {
                if (table.Columns == null || table.Columns.Count == 0 || string.IsNullOrWhiteSpace(table.TableName))
                {
                    Logger.LogInfo($"GeneratePlSqlDeclarations: Table {(table.TableName ?? "[NULL TABLE NAME]")} has no columns or is invalid, skipping for PL/SQL type declaration.");
                    continue;
                }
                string sanitizedTableName = SanitizeForPlSqlIdentifier(table.TableName);
                outTableNames.Add(table.TableName); // Add original table name for TRUNCATE list

                sb.AppendLine($"  TYPE T_Fake_{sanitizedTableName}_Rows IS TABLE OF {table.TableName}%ROWTYPE INDEX BY PLS_INTEGER;");
                sb.AppendLine($"  V_Fake_{sanitizedTableName}_Data T_Fake_{sanitizedTableName}_Rows;");
            }
            return sb.ToString();
        }

        private string GenerateInsertStatements(string geminiSchemaJson, int rowCount, out List<string> tableNames)
        {
            tableNames = new List<string>(); // Initialize out parameter
            var tableSchemas = DeserializeTableSchemas(geminiSchemaJson, out string? errorResult);
            if (errorResult != null)
            {
                return errorResult;
            }
            if (tableSchemas == null) // Should be covered by errorResult but as a safeguard
            {
                return "-- Error: Unknown error during schema deserialization.\n";
            }

            if (rowCount <= 0)
            {
                Logger.LogError("GenerateInsertStatements: rowCount must be positive.");
                return "-- Error: Row count must be positive.\n";
            }

            StringBuilder sb = new StringBuilder();
            sb.Append(GeneratePlSqlDeclarations(tableSchemas, tableNames)); // tableNames is populated here
            sb.AppendLine("BEGIN");
            sb.Append(GeneratePlSqlDataPopulationLogic(tableSchemas, rowCount));
            sb.Append(GeneratePlSqlInsertAllLoop(tableSchemas));
            sb.AppendLine("END;");
            return sb.ToString();
        }

        private string GeneratePlSqlDataPopulationLogic(List<TableSchema> tableSchemas, int rowCount)
        {
            StringBuilder sb = new StringBuilder();
            foreach (TableSchema table in tableSchemas)
            {
                if (table.Columns == null || table.Columns.Count == 0 || string.IsNullOrWhiteSpace(table.TableName))
                {
                    Logger.LogInfo($"GeneratePlSqlDataPopulationLogic: Skipping table {(table.TableName ?? "[NULL TABLE NAME]")} due to no columns or invalid name.");
                    continue;
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
                        Logger.LogInfo($"GeneratePlSqlDataPopulationLogic: Null data type for column {column.ColumnName} in table {table.TableName}. Using NULL.");
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
                                        declaredLength = Math.Max(1, Math.Min(parsedLength, 4000));
                                    }
                                }
                            }
                            catch { /* Use default if parsing fails */ }
                        }
                        int maxILength = rowCount.ToString().Length;
                        string prefixForCalc = "Val_";
                        string suffixTemplateForCalc = "_";
                        int fixedPartsLength = prefixForCalc.Length + suffixTemplateForCalc.Length + maxILength;
                        int availableLength = declaredLength - fixedPartsLength - 2;

                        if (availableLength >= 1)
                        {
                            int randomPartLength = Math.Min(availableLength, 20);
                            generatedValue = $"'{prefixForCalc}' || DBMS_RANDOM.STRING('A', {randomPartLength}) || '{suffixTemplateForCalc}' || i";
                        }
                        else
                        {
                            string errPrefixForCalc = "Err_";
                            fixedPartsLength = errPrefixForCalc.Length + maxILength;
                            availableLength = declaredLength - fixedPartsLength - 2;
                            if (availableLength >= 0)
                            {
                                generatedValue = $"'E_' || TO_CHAR(i)";
                                if (("E_".Length + maxILength + 2) > declaredLength)
                                {
                                    if (declaredLength >= 2) generatedValue = "''"; else generatedValue = "NULL";
                                }
                            }
                            else if (declaredLength >= 2) { generatedValue = "''"; }
                            else { generatedValue = "NULL"; }
                            Logger.LogInfo($"GeneratePlSqlDataPopulationLogic: VARCHAR2 column {column.ColumnName} in table {table.TableName} has declared length {declaredLength} too small. Using fallback: {generatedValue}");
                        }
                    }
                    else if (columnDataTypeUpper.StartsWith("NUMBER") || columnDataTypeUpper.StartsWith("INTEGER") || columnDataTypeUpper.StartsWith("INT") || columnDataTypeUpper.StartsWith("DECIMAL") || columnDataTypeUpper.StartsWith("FLOAT"))
                    {
                        generatedValue = $"TRUNC(DBMS_RANDOM.VALUE(1, 100000)) + MOD(i, 100000)";
                    }
                    else if (columnDataTypeUpper.StartsWith("DATE"))
                    {
                        generatedValue = $"TO_DATE('2000-01-01', 'YYYY-MM-DD') + MOD(i-1, 365*50)";
                    }
                    else
                    {
                        Logger.LogInfo($"GeneratePlSqlDataPopulationLogic: Unsupported data type {column.DataType} for column {column.ColumnName} in table {table.TableName}. Using NULL.");
                    }
                    sb.AppendLine($"    V_Fake_{sanitizedTableName}_Data(i).\"{column.ColumnName}\" := {generatedValue};");
                }
                sb.AppendLine("  END LOOP;");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private string GeneratePlSqlInsertAllLoop(List<TableSchema> tableSchemas)
        {
            StringBuilder sb = new StringBuilder();
            // After populating all collections, do the inserts
            foreach (TableSchema table in tableSchemas)
            {
                if (table.Columns == null || table.Columns.Count == 0 || string.IsNullOrWhiteSpace(table.TableName))
                {
                    Logger.LogInfo($"GeneratePlSqlInsertAllLoop: Skipping table {(table.TableName ?? "[NULL TABLE NAME]")} due to no columns or invalid name.");
                    continue;
                }
                string sanitizedTableName = SanitizeForPlSqlIdentifier(table.TableName);
                sb.AppendLine($"  -- Inserting data into table: {table.TableName}");
                sb.AppendLine($"  FORALL i IN V_Fake_{sanitizedTableName}_Data.FIRST..V_Fake_{sanitizedTableName}_Data.LAST");
                sb.AppendLine($"    INSERT INTO {table.TableName} VALUES V_Fake_{sanitizedTableName}_Data(i);");
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}