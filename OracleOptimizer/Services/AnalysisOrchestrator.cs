using OracleOptimizer.Services; // For OracleService, GeminiService if they are in this namespace
using Oracle.ManagedDataAccess.Client;
using System.Data;
using Newtonsoft.Json;
using System.Text;
using System.Collections.Generic;
using System; // For Random, Math
using System.Threading.Tasks; // Required for async methods
using System.Linq; // Required for Linq operations like .Select, .ToList, .ToDictionary, .Except

namespace OracleOptimizer.Services
{
    public class AnalysisResult
    {
        public string? GeminiExplanation { get; set; }
        public string? OptimizedProcedureBody { get; set; }
        public string? FinalReport { get; set; }
        public bool LogicTestPassed { get; set; }
        public long OriginalTimeMs { get; set; }
        public long OptimizedTimeMs { get; set; }
        // Add any other key data MainForm might need
        public List<string>? TableNamesToTruncate { get; internal set; } // Example, if MainForm needs this
        public string? OriginalExecutableBlock { get; internal set; } // Example
        public string? OptimizedExecutableBlock { get; internal set; } // Example
        public DataTable? OriginalData { get; internal set; } // Example
        public DataTable? OptimizedData { get; internal set; } // Example
        public string? GeminiSchemaJson { get; internal set; } // Example
        public string? TestDataPlSql { get; internal set; } // Example
    }

    public class AnalysisOrchestrator
    {
        private readonly OracleService _oracleService;
        private readonly GeminiService _geminiService;

        public AnalysisOrchestrator(OracleService oracleService, GeminiService geminiService)
        {
            _oracleService = oracleService ?? throw new ArgumentNullException(nameof(oracleService));
            _geminiService = geminiService ?? throw new ArgumentNullException(nameof(geminiService));
        }

        // Placeholder for methods to be moved

        // Helper classes for deserializing Gemini's schema JSON (used by GenerateInsertStatements and GenerateFakeDataCte)
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
                System.Diagnostics.Debug.WriteLine("GenerateFakeDataCte: geminiSchemaJson is null or empty.");
                return string.Empty;
            }
            if (rowCount <= 0)
            {
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
                    return string.Empty;
                }
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"GenerateFakeDataCte: Error parsing JSON schema: {ex.Message}");
                return $"-- Error parsing JSON schema: {ex.Message}\n";
            }

            var cteParts = new List<string>();
            foreach (var table in tableSchemas)
            {
                if (table.Columns == null || table.Columns.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"GenerateFakeDataCte: Table {table.TableName} has no columns, skipping.");
                    continue;
                }

                var sbCte = new StringBuilder();
                sbCte.AppendLine($"  {SanitizeForPlSqlIdentifier(table.TableName)}_fake AS (");

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
                            string tableNamePart = table.TableName != null ? SanitizeForPlSqlIdentifier(table.TableName).Substring(0, Math.Min(SanitizeForPlSqlIdentifier(table.TableName).Length, 3)) : "TAB";
                            string colNamePart = column.ColumnName != null ? SanitizeForPlSqlIdentifier(column.ColumnName).Substring(0, Math.Min(SanitizeForPlSqlIdentifier(column.ColumnName).Length, 3)) : "COL";
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
                return string.Empty;
            }
            return "WITH\n" + string.Join(",\n", cteParts) + "\n";
        }

        private string SanitizeForPlSqlIdentifier(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "default_identifier";
            string sanitized = System.Text.RegularExpressions.Regex.Replace(name, @"[^a-zA-Z0-9_]", "_");
            if (sanitized.Length > 0 && char.IsDigit(sanitized[0]))
            {
                sanitized = "_" + sanitized;
            }
            return sanitized.Length > 30 ? sanitized.Substring(0, 30) : sanitized;
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
                tableNames.Add(table.TableName);
                sb.AppendLine($"  TYPE T_Fake_{sanitizedTableName}_Rows IS TABLE OF {table.TableName}%ROWTYPE INDEX BY PLS_INTEGER;");
                sb.AppendLine($"  V_Fake_{sanitizedTableName}_Data T_Fake_{sanitizedTableName}_Rows;");
            }
            sb.AppendLine("BEGIN");

            foreach (TableSchema table in tableSchemas)
            {
                if (table.Columns == null || table.Columns.Count == 0 || string.IsNullOrWhiteSpace(table.TableName))
                {
                    continue;
                }
                string sanitizedTableName = SanitizeForPlSqlIdentifier(table.TableName);
                sb.AppendLine($"  -- Populating data for table: {table.TableName}");
                sb.AppendLine($"  FOR i IN 1..{rowCount} LOOP");

                foreach (ColumnSchema column in table.Columns)
                {
                    string generatedValue = "NULL";
                    string? columnDataTypeUpper = column.DataType?.ToUpperInvariant();

                    if (columnDataTypeUpper == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"GenerateInsertStatements: Null data type for column {column.ColumnName} in table {table.TableName}. Using NULL.");
                    }
                    else if (columnDataTypeUpper.StartsWith("VARCHAR2") || columnDataTypeUpper.StartsWith("VARCHAR") || columnDataTypeUpper.StartsWith("CHAR") || columnDataTypeUpper.StartsWith("NVARCHAR2"))
                    {
                        int declaredLength = 30;
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
                            catch { /* Parsing error, use default length. */ }
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
                                if ((errPrefixForCalc.Length + maxILength + 2) > declaredLength)
                                {
                                    if (declaredLength >= 2) generatedValue = "''";
                                    else generatedValue = "NULL";
                                }
                            }
                            else if (declaredLength >= 2)
                            {
                                generatedValue = "''";
                            }
                            else
                            {
                                generatedValue = "NULL";
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
                        generatedValue = $"TO_DATE('2000-01-01', 'YYYY-MM-DD') + MOD(i-1, 365*50)";
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"GenerateInsertStatements: Unsupported data type {column.DataType} for column {column.ColumnName} in table {table.TableName}. Using NULL.");
                    }
                    sb.AppendLine($"    V_Fake_{sanitizedTableName}_Data(i).\"{column.ColumnName}\" := {generatedValue};");
                }
                sb.AppendLine("  END LOOP;");
                sb.AppendLine();
            }

            foreach (TableSchema table in tableSchemas)
            {
                if (table.Columns == null || table.Columns.Count == 0 || string.IsNullOrWhiteSpace(table.TableName))
                {
                    continue;
                }
                string sanitizedTableName = SanitizeForPlSqlIdentifier(table.TableName);
                sb.AppendLine($"  -- Inserting data into table: {table.TableName}");
                sb.AppendLine($"  FORALL i IN V_Fake_{sanitizedTableName}_Data.FIRST..V_Fake_{sanitizedTableName}_Data.LAST");
                sb.AppendLine($"    INSERT INTO {table.TableName} VALUES V_Fake_{sanitizedTableName}_Data(i);");
                sb.AppendLine();
            }

            sb.AppendLine("END;");
            return sb.ToString();
        }

        private async Task ClearTablesAsync(OracleConnection connection, OracleTransaction transaction, List<string> tableNames)
        {
            // 1. Normalize table names (to uppercase) and identify the current user for unqualified table names.
            var uniqueTableNames = new HashSet<string>(tableNames.Select(t => t.ToUpperInvariant()));
            var connectionStringBuilder = new OracleConnectionStringBuilder(connection.ConnectionString);
            string currentUser = connectionStringBuilder.UserID.ToUpper();

            // Parse table names into (owner, tableName, fullName) tuples.
            var parsedTables = uniqueTableNames.Select(fullTableName =>
            {
                string[] parts = fullTableName.Split('.');
                string owner = parts.Length > 1 ? parts[0] : currentUser;
                string tableName = parts.Length > 1 ? parts[1] : parts[0];
                return (owner, tableName, fullTableName);
            }).ToList();

            var tableLookup = parsedTables.ToDictionary(t => (t.owner, t.tableName), t => t.fullTableName);
            var dependencies = new Dictionary<string, HashSet<string>>();
            var whereClauses = new List<string>();
            var parameters = new List<OracleParameter>();
            int paramIndex = 0;

            foreach (var (owner, tableName, _) in parsedTables)
            {
                whereClauses.Add($"(a.owner = :p_owner{paramIndex} AND a.table_name = :p_table_name{paramIndex})");
                parameters.Add(new OracleParameter($"p_owner{paramIndex}", owner));
                parameters.Add(new OracleParameter($"p_table_name{paramIndex}", tableName));
                paramIndex++;
            }

            if (whereClauses.Count > 0)
            {
                string sql = $@"
                    SELECT a.owner AS child_owner, a.table_name AS child_table_name,
                           r.owner AS parent_owner, r.table_name AS parent_table_name
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

            var sortedList = new List<string>();
            var graphBuild = uniqueTableNames.ToDictionary(t => t, t => new HashSet<string>());
            var inDegreeBuild = uniqueTableNames.ToDictionary(t => t, t => 0);

            foreach (var parentFullName in dependencies.Keys)
            {
                foreach (var childFullName in dependencies[parentFullName])
                {
                    if (uniqueTableNames.Contains(childFullName) && uniqueTableNames.Contains(parentFullName))
                    {
                        if (graphBuild[childFullName].Add(parentFullName))
                        {
                            inDegreeBuild[parentFullName]++;
                        }
                    }
                }
            }

            var queue = new Queue<string>(inDegreeBuild.Where(kv => kv.Value == 0).Select(kv => kv.Key));
            while (queue.Count > 0)
            {
                var tableToProcess = queue.Dequeue();
                sortedList.Add(tableToProcess);
                foreach (var parentTable in graphBuild[tableToProcess])
                {
                    inDegreeBuild[parentTable]--;
                    if (inDegreeBuild[parentTable] == 0)
                    {
                        queue.Enqueue(parentTable);
                    }
                }
            }

            if (sortedList.Count < uniqueTableNames.Count)
            {
                var missing = string.Join(", ", uniqueTableNames.Except(sortedList));
                throw new Exception($"Cyclic dependency detected among tables, cannot determine deletion order. Problematic tables might include: {missing}. This can also happen if a table in the list has an FK to a table not in the list, and that external table also has FKs back into the list, forming a cycle not entirely within the provided table list.");
            }

            foreach (var table in sortedList)
            {
                // Use _oracleService field here
                await _oracleService.ExecuteNonQueryAsync(connection, transaction, $"DELETE FROM {table}");
            }
        }

        private async Task<(DataTable results, long time)> RunProcedureAndGetDataAsync(
            string connectionString, string testDataPlSql,
            List<string> tablesToTruncate, string procedureExecutableBlock, string validationQuery, int commandTimeoutSeconds)
        {
            using var connection = new OracleConnection(connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

            try
            {
                // Call the ClearTablesAsync method of this class
                await ClearTablesAsync(connection, transaction, tablesToTruncate);

                if (!string.IsNullOrWhiteSpace(testDataPlSql))
                {
                    // Use _oracleService field here
                    await _oracleService.ExecuteNonQueryAsync(connection, transaction, testDataPlSql, commandTimeoutSeconds);
                }

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                // Use _oracleService field here
                await _oracleService.ExecuteNonQueryAsync(connection, transaction, procedureExecutableBlock, commandTimeoutSeconds);
                stopwatch.Stop();

                // Use _oracleService field here
                var dataTable = await _oracleService.ExecuteQueryWithinTransactionAsync(connection, transaction, validationQuery, commandTimeoutSeconds);

                await transaction.RollbackAsync();
                return (dataTable, stopwatch.ElapsedMilliseconds);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private static Func<string, string> ConvertProcedureToExecutableBlock = (procedureBody) =>
        {
            string executableBlock = procedureBody.Trim();
            System.Text.RegularExpressions.Regex procHeaderRegex = new System.Text.RegularExpressions.Regex(
                @"^\s*PROCEDURE\s+([a-zA-Z0-9_$#""\.]+)(\s*\(.*?\))?(\s+IS|\s+AS)\s+",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
            executableBlock = procHeaderRegex.Replace(executableBlock, "DECLARE\n", 1);
            System.Text.RegularExpressions.Regex endProcRegex = new System.Text.RegularExpressions.Regex(
                @"END\s*([a-zA-Z0-9_$#""\.]+)?\s*;",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.RightToLeft | System.Text.RegularExpressions.RegexOptions.Singleline);
            executableBlock = endProcRegex.Replace(executableBlock, "END;", 1);
            return executableBlock;
        };

        public async Task<AnalysisResult> ExecuteAnalysisAndTestingAsync(
            string host, string user, string password, string originalProcedure, string modelName,
            int testRowCount, int commandTimeoutSeconds)
        {
            var analysisResult = new AnalysisResult();

            // Step 1: Analyze with Gemini for optimization and validation plan
            // GeminiService is already initialized via constructor and stored in _geminiService
            var geminiResponse = await _geminiService.AnalyzeSqlAsync(originalProcedure, modelName);
            if (geminiResponse == null || string.IsNullOrWhiteSpace(geminiResponse.OptimizedProcedureBody) || string.IsNullOrWhiteSpace(geminiResponse.ValidationQueryAfter))
            {
                throw new Exception("Gemini did not return a valid or complete test plan (missing optimized procedure or validation query).");
            }

            analysisResult.GeminiExplanation = geminiResponse.Explanation;
            analysisResult.OptimizedProcedureBody = geminiResponse.OptimizedProcedureBody;

            // Step 2: Get table schema from Gemini for test data generation
            string geminiSchemaJson = await _geminiService.GetTableSchemaFromGemini(originalProcedure, modelName);
            if (string.IsNullOrWhiteSpace(geminiSchemaJson) || geminiSchemaJson.StartsWith("-- Error"))
            {
                throw new Exception($"Failed to get table schema from Gemini. Response: {geminiSchemaJson}");
            }
            analysisResult.GeminiSchemaJson = geminiSchemaJson;

            // Step 3: Prepare for database operations
            // OracleService is already initialized via constructor and stored in _oracleService
            string connectionString = $"Data Source={host};User Id={user};Password={password};Connection Timeout=120;";

            // Generate PL/SQL block for inserting test data
            // GenerateInsertStatements is now part of this class
            string testDataPlSql = GenerateInsertStatements(geminiSchemaJson, testRowCount, out List<string> tableNamesToTruncate);
            analysisResult.TestDataPlSql = testDataPlSql;
            analysisResult.TableNamesToTruncate = tableNamesToTruncate;

            // Convert original procedure to executable block
            string originalExecutableBlock = ConvertProcedureToExecutableBlock(originalProcedure);
            analysisResult.OriginalExecutableBlock = originalExecutableBlock;

            // Run original procedure
            // RunProcedureAndGetDataAsync is now part of this class
            (DataTable originalData, long originalTime) = await RunProcedureAndGetDataAsync(
                connectionString, testDataPlSql, tableNamesToTruncate,
                originalExecutableBlock, geminiResponse.ValidationQueryAfter, commandTimeoutSeconds);
            analysisResult.OriginalData = originalData;
            analysisResult.OriginalTimeMs = originalTime;

            // Step 4: Run the optimized procedure
            // Convert optimized procedure to executable block
            string optimizedExecutableBlock = ConvertProcedureToExecutableBlock(geminiResponse.OptimizedProcedureBody);
            analysisResult.OptimizedExecutableBlock = optimizedExecutableBlock;

            // Run optimized procedure
            (DataTable optimizedData, long optimizedTime) = await RunProcedureAndGetDataAsync(
                connectionString, testDataPlSql, tableNamesToTruncate,
                optimizedExecutableBlock, geminiResponse.ValidationQueryAfter, commandTimeoutSeconds);
            analysisResult.OptimizedData = optimizedData;
            analysisResult.OptimizedTimeMs = optimizedTime;

            // Step 5: Compare results and performance
            // DataTableComparator is a static class, so it can be called directly.
            // Ensure OracleOptimizer.Services.DataTableComparator is accessible or move it.
            // For now, assuming it's accessible.
            bool areIdentical = DataTableComparator.AreIdentical(originalData, optimizedData, out string comparisonDetails);
            analysisResult.LogicTestPassed = areIdentical;

            analysisResult.FinalReport = (areIdentical ? "✅ LOGIC TEST PASSED" : "❌ LOGIC TEST FAILED") + "\n\n" +
                                         $"PERFORMANCE:\n" +
                                         $"- Original:    {originalTime}ms\n" +
                                         $"- Optimized:   {optimizedTime}ms\n" +
                                         $"- Improvement: {originalTime - optimizedTime}ms ({Math.Round((double)(originalTime - optimizedTime) * 100 / Math.Max(originalTime, 1), 2)}%)\n\n" +
                                         $"VALIDATION DETAILS:\n{comparisonDetails}";

            return analysisResult;
        }
    }
}
