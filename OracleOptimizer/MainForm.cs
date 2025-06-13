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

        private async void analyzeButton_Click(object? sender, EventArgs e)
        {
            analyzeButton.Enabled = false;
            statusLabel.Text = "Processing...";
            performanceLabel.Text = ""; // Clear performance label
            // reportTextBox.Text = ""; // Decide if we want to clear this for logic tests

            try
            {
                await ExecuteAnalysisAndTestingAsync();
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

        private async Task ExecuteAnalysisAndTestingAsync()
        {
            const int TEST_ROW_COUNT = 1000;
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

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(originalProcedure))
            {
                statusLabel.Text = "Error: All input fields, including API Key and Procedure, are required.";
                return;
            }

            statusLabel.Text = "1/5: Analyzing with Gemini for optimization...";
            optimizedProcedureTextBox.Text = "";
            reportTextBox.Text = "";

            var geminiService = new GeminiService(apiKey);
            var geminiResponse = await geminiService.AnalyzeSqlAsync(originalProcedure, modelName);
            if (geminiResponse == null || string.IsNullOrWhiteSpace(geminiResponse.OptimizedProcedureBody) || string.IsNullOrWhiteSpace(geminiResponse.ValidationQueryAfter))
            {
                throw new Exception("Gemini did not return a valid or complete test plan.");
            }

            reportTextBox.Text = geminiResponse.Explanation;
            optimizedProcedureTextBox.Text = geminiResponse.OptimizedProcedureBody;
            resultsTabControl.SelectedTab = geminiReportTab;

            statusLabel.Text = "2/5: Getting table schema for testing...";
            string geminiSchemaJson = await geminiService.GetTableSchemaFromGemini(originalProcedure, modelName);
            if (string.IsNullOrWhiteSpace(geminiSchemaJson) || geminiSchemaJson.StartsWith("-- Error"))
            {
                throw new Exception($"Failed to get table schema from Gemini. Response: {geminiSchemaJson}");
            }

            statusLabel.Text = "3/5: Running Original Procedure...";
            var oracleService = new OracleService();
            string connectionString = $"Data Source={host};User Id={user};Password={password};Connection Timeout=120;";
            string testDataPlSql = GenerateInsertStatements(geminiSchemaJson, TEST_ROW_COUNT, out List<string> tableNamesToTruncate);

            string originalExecutableBlock = convertProcedureToExecutableBlock(originalProcedure);
            (DataTable originalData, long originalTime) = await RunProcedureAndGetDataAsync(oracleService, connectionString, testDataPlSql, tableNamesToTruncate, originalExecutableBlock, geminiResponse.ValidationQueryAfter);

            statusLabel.Text = "4/5: Running Optimized Procedure...";
            string optimizedExecutableBlock = convertProcedureToExecutableBlock(geminiResponse.OptimizedProcedureBody);
            (DataTable optimizedData, long optimizedTime) = await RunProcedureAndGetDataAsync(oracleService, connectionString, testDataPlSql, tableNamesToTruncate, optimizedExecutableBlock, geminiResponse.ValidationQueryAfter);

            statusLabel.Text = "5/5: Comparing results...";
            bool areIdentical = DataTableComparator.AreIdentical(originalData, optimizedData, out string comparisonDetails);

            string finalReport = (areIdentical ? "✅ LOGIC TEST PASSED" : "❌ LOGIC TEST FAILED") + "\n\n" +
                                 $"PERFORMANCE:\n" +
                                 $"- Original:    {originalTime}ms\n" +
                                 $"- Optimized:   {optimizedTime}ms\n" +
                                 $"- Improvement: {originalTime - optimizedTime}ms\n\n" +
                                 $"VALIDATION DETAILS:\n{comparisonDetails}";

            performanceLabel.Text = finalReport;
            resultsTabControl.SelectedTab = performanceTab;
            statusLabel.Text = "Analysis and Testing Complete.";
        }

        private async Task ClearTablesAsync(OracleService oracleService, OracleConnection connection, OracleTransaction transaction, List<string> tableNames)
        {
            // 1. Normalize table names and prepare for query
            var uniqueTableNames = new HashSet<string>(tableNames.Select(t => t.ToUpperInvariant()));
            var connectionStringBuilder = new OracleConnectionStringBuilder(connection.ConnectionString);
            string currentUser = connectionStringBuilder.UserID.ToUpper();

            var parsedTables = uniqueTableNames.Select(fullTableName =>
            {
                string[] parts = fullTableName.Split('.');
                string owner = parts.Length > 1 ? parts[0] : currentUser;
                string tableName = parts.Length > 1 ? parts[1] : parts[0];
                return (owner, tableName, fullTableName);
            }).ToList();

            var tableLookup = parsedTables.ToDictionary(t => (t.owner, t.tableName), t => t.fullTableName);

            // 2. Query for dependencies among the specified tables
            var dependencies = new Dictionary<string, HashSet<string>>(); // Key: Parent, Value: Set of Children

            var whereClauses = new List<string>();
            var parameters = new List<OracleParameter>();
            int i = 0;
            foreach (var (owner, tableName, _) in parsedTables)
            {
                whereClauses.Add($"(a.owner = :p_owner{i} AND a.table_name = :p_table_name{i})");
                parameters.Add(new OracleParameter($"p_owner{i}", owner));
                parameters.Add(new OracleParameter($"p_table_name{i}", tableName));
                i++;
            }

            if (whereClauses.Count > 0)
            {
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

                            if (tableLookup.TryGetValue((childOwner, childTable), out var childFullName) &&
                                tableLookup.TryGetValue((parentOwner, parentTable), out var parentFullName))
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
            }

            // 3. Topological Sort (Kahn's algorithm) to determine deletion order
            var sortedList = new List<string>();
            var inDegree = uniqueTableNames.ToDictionary(t => t, t => 0);
            var graph = uniqueTableNames.ToDictionary(t => t, t => new HashSet<string>()); // Child -> Set of Parents

            foreach (var parent in dependencies.Keys)
            {
                foreach (var child in dependencies[parent])
                {
                    if (uniqueTableNames.Contains(child) && uniqueTableNames.Contains(parent))
                    {
                        if (graph[child].Add(parent))
                        {
                            inDegree[parent]++;
                        }
                    }
                }
            }

            var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                sortedList.Add(node);

                if (graph.TryGetValue(node, out var neighbors))
                {
                    foreach (var neighbor in neighbors)
                    {
                        inDegree[neighbor]--;
                        if (inDegree[neighbor] == 0)
                        {
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }

            if (sortedList.Count < uniqueTableNames.Count)
            {
                var missing = string.Join(", ", uniqueTableNames.Except(sortedList));
                throw new Exception($"Cyclic dependency detected among tables, cannot determine deletion order. Problematic tables might include: {missing}");
            }

            // 4. Clear table data respecting foreign keys
            foreach (var table in sortedList)
            {
                await oracleService.ExecuteNonQueryAsync(connection, transaction, $"DELETE FROM {table}");
            }
        }

        private async Task<(DataTable results, long time)> RunProcedureAndGetDataAsync(
            OracleService oracleService, string connectionString, string testDataPlSql,
            List<string> tablesToTruncate, string procedureExecutableBlock, string validationQuery)
        {
            using var connection = new OracleConnection(connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

            try
            {
                // Clear tables in the correct order for deletion
                await ClearTablesAsync(oracleService, connection, transaction, tablesToTruncate);

                // Insert fresh data for the test run
                if (!string.IsNullOrWhiteSpace(testDataPlSql))
                {
                    await oracleService.ExecuteNonQueryAsync(connection, transaction, testDataPlSql);
                }

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                await oracleService.ExecuteNonQueryAsync(connection, transaction, procedureExecutableBlock);
                stopwatch.Stop();

                var dataTable = await oracleService.ExecuteQueryWithinTransactionAsync(connection, transaction, validationQuery);

                await transaction.RollbackAsync(); // Rollback all changes (deletes, inserts, procedure effects)
                return (dataTable, stopwatch.ElapsedMilliseconds);
            }
            catch
            {
                await transaction.RollbackAsync();
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

            var cteParts = new List<string>();
            foreach (var table in tableSchemas)
            {
                if (table.Columns == null || table.Columns.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"GenerateFakeDataCte: Table {table.TableName} has no columns, skipping.");
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
                            System.Diagnostics.Debug.WriteLine($"GenerateFakeDataCte: Unsupported data type {column.DataType} for column {column.ColumnName} in table {table.TableName}. Using NULL.");
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
                if (table.Columns == null || table.Columns.Count == 0 || string.IsNullOrWhiteSpace(table.TableName))
                {
                    System.Diagnostics.Debug.WriteLine($"GenerateInsertStatements: Table {(table.TableName ?? "[NULL] ")} has no columns or is invalid, skipping for PL/SQL generation.");
                    continue;
                }
                string sanitizedTableName = SanitizeForPlSqlIdentifier(table.TableName);
                // It's important to add the original table name to the list for later use (e.g. TRUNCATE)
                tableNames.Add(table.TableName);

                sb.AppendLine($"  TYPE T_Fake_{sanitizedTableName}_Rows IS TABLE OF {table.TableName}%ROWTYPE INDEX BY PLS_INTEGER;");
                sb.AppendLine($"  V_Fake_{sanitizedTableName}_Data T_Fake_{sanitizedTableName}_Rows;");
            }
            sb.AppendLine("BEGIN");

            foreach (TableSchema table in tableSchemas)
            {
                if (table.Columns == null || table.Columns.Count == 0 || string.IsNullOrWhiteSpace(table.TableName))
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
                if (table.Columns == null || table.Columns.Count == 0 || string.IsNullOrWhiteSpace(table.TableName))
                {
                    continue; // Skip if no columns
                }
                string sanitizedTableName = SanitizeForPlSqlIdentifier(table.TableName);
                sb.AppendLine($"  -- Inserting data into table: {table.TableName}");
                sb.AppendLine($"  FORALL i IN V_Fake_{sanitizedTableName}_Data.FIRST..V_Fake_{sanitizedTableName}_Data.LAST");
                // Table name in INSERT INTO should not be quoted if it's schema-qualified
                sb.AppendLine($"    INSERT INTO {table.TableName} VALUES V_Fake_{sanitizedTableName}_Data(i);");
                sb.AppendLine();
            }

            sb.AppendLine("END;");
            return sb.ToString();
        }
    }
}