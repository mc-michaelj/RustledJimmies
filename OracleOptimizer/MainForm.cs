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
                // Core logic for analysis and testing
                await ExecuteAnalysisAndTestingAsync();
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
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <exception cref="Exception">Throws various exceptions if critical steps fail (e.g., API errors, data generation issues).</exception>
        private async Task ExecuteAnalysisAndTestingAsync()
        {
            // Read the desired number of test rows from the UI
            int testRowCount = (int)testRowCountNumericUpDown.Value;
            // Read the desired command timeout in seconds from the UI
            int commandTimeoutSeconds = (int)commandTimeoutNumericUpDown.Value;

            // Overall goal: Convert a named PL/SQL procedure string into an executable anonymous block.
            // This is necessary because Oracle often requires anonymous blocks for dynamic execution,
            // especially when the procedure isn't pre-compiled in the database or when testing variations.
            // Limitations:
            // - Assumes a relatively standard procedure format. Might struggle with highly complex package bodies or unusual syntax.
            // - Regex-based parsing can be fragile if the procedure contains string literals or comments that mimic procedure syntax.
            // - Does not handle procedures with overloaded versions directly; it will convert the first textual match.
            // - Character set for procedure names in regex `[a-zA-Z0-9_$#""\.]` might need adjustment for other special characters if used in names.
            Func<string, string> convertProcedureToExecutableBlock = (procedureBody) =>
            {
                string executableBlock = procedureBody.Trim();

                // Regex for the procedure header:
                // - `^\s*PROCEDURE`: Matches "PROCEDURE" at the beginning of the string (after optional whitespace). `^` is equivalent to `\A` when Singleline is not used, but with Singleline `\A` is stricter for start of entire string.
                // - `\s+([a-zA-Z0-9_$#""\.]+)`: Matches and captures the procedure name. Allows for alphanumeric characters, underscores, dollar signs, hash symbols, quoted identifiers (via `""`), and periods (for schema.package.procedure).
                // - `(\s*\(.*?\))?`: Optionally matches and captures parameters `(...)`. `.*?` is non-greedy. `Singleline` option allows `.` to match newlines, so parameters can span lines.
                // - `(\s+IS|\s+AS)\s+`: Matches "IS" or "AS" keyword.
                // - Options: `IgnoreCase` for case-insensitivity (e.g., procedure, PROCEDURE), `Singleline` so `.` matches `\n`.
                System.Text.RegularExpressions.Regex procHeaderRegex = new System.Text.RegularExpressions.Regex(
                    @"^\s*PROCEDURE\s+([a-zA-Z0-9_$#""\.]+)(\s*\(.*?\))?(\s+IS|\s+AS)\s+",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

                executableBlock = procHeaderRegex.Replace(executableBlock, "DECLARE\n", 1); // Replace header with "DECLARE"

                // Regex for the END statement:
                // - `END`: Matches the keyword END.
                // - `\s*([a-zA-Z0-9_$#""\.]+)?`: Optionally matches a procedure name (same character set as above). The `?` makes the name optional.
                // - `\s*;`: Matches the semicolon, preceded by optional whitespace.
                // - Options: `IgnoreCase`, `RightToLeft` (crucial for finding the *final* END statement of the main procedure, not a nested block's END), `Singleline`.
                System.Text.RegularExpressions.Regex endProcRegex = new System.Text.RegularExpressions.Regex(
                    @"END\s*([a-zA-Z0-9_$#""\.]+)?\s*;",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.RightToLeft | System.Text.RegularExpressions.RegexOptions.Singleline);

                executableBlock = endProcRegex.Replace(executableBlock, "END;", 1); // Replace with a simple "END;"
                return executableBlock;
            };

            // Gather inputs from the UI text boxes.
            string host = hostTextBox.Text;
            string user = userTextBox.Text;
            string password = passwordTextBox.Text;
            string apiKey = geminiApiKeyTextBox.Text;
            string originalProcedure = procedureBodyTextBox.Text;
            string modelName = geminiModelTextBox.Text;

            // Validate that all required inputs are provided.
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(originalProcedure))
            {
                statusLabel.Text = "Error: All input fields, including API Key and Procedure, are required.";
                return; // Exit if validation fails.
            }

            // --- Step 1: Analyze with Gemini for optimization and validation plan ---
            statusLabel.Text = "1/5: Analyzing with Gemini for optimization...";
            optimizedProcedureTextBox.Text = ""; // Clear previous results from UI
            reportTextBox.Text = "";      // Clear previous results from UI

            var geminiService = new GeminiService(apiKey); // Initialize Gemini service
            var geminiResponse = await geminiService.AnalyzeSqlAsync(originalProcedure, modelName);
            // Ensure a valid and complete response is received from Gemini.
            if (geminiResponse == null || string.IsNullOrWhiteSpace(geminiResponse.OptimizedProcedureBody) || string.IsNullOrWhiteSpace(geminiResponse.ValidationQueryAfter))
            {
                throw new Exception("Gemini did not return a valid or complete test plan (missing optimized procedure or validation query).");
            }

            // Display Gemini's explanation and optimized procedure in the UI.
            reportTextBox.Text = geminiResponse.Explanation;
            optimizedProcedureTextBox.Text = geminiResponse.OptimizedProcedureBody;
            resultsTabControl.SelectedTab = geminiReportTab; // Switch to the Gemini report tab

            // --- Step 2: Get table schema from Gemini for test data generation ---
            statusLabel.Text = "2/5: Getting table schema for testing...";
            string geminiSchemaJson = await geminiService.GetTableSchemaFromGemini(originalProcedure, modelName);
            if (string.IsNullOrWhiteSpace(geminiSchemaJson) || geminiSchemaJson.StartsWith("-- Error")) // Check for errors or empty schema
            {
                throw new Exception($"Failed to get table schema from Gemini. Response: {geminiSchemaJson}");
            }

            // --- Step 3: Prepare for database operations - generate test data and run original procedure ---
            statusLabel.Text = "3/5: Running Original Procedure...";
            var oracleService = new OracleService(); // Initialize Oracle service
            // Construct connection string from UI inputs.
            string connectionString = $"Data Source={host};User Id={user};Password={password};Connection Timeout=120;";
            // Generate PL/SQL block for inserting test data based on the schema from Gemini.
            string testDataPlSql = GenerateInsertStatements(geminiSchemaJson, testRowCount, out List<string> tableNamesToTruncate);

            // Convert the original procedure to an executable anonymous block.
            string originalExecutableBlock = convertProcedureToExecutableBlock(originalProcedure);
            // Run the original procedure and get its results and execution time.
            (DataTable originalData, long originalTime) = await RunProcedureAndGetDataAsync(oracleService, connectionString, testDataPlSql, tableNamesToTruncate, originalExecutableBlock, geminiResponse.ValidationQueryAfter, commandTimeoutSeconds);

            // --- Step 4: Run the optimized procedure ---
            statusLabel.Text = "4/5: Running Optimized Procedure...";
            // Convert the optimized procedure (from Gemini response) to an executable anonymous block.
            string optimizedExecutableBlock = convertProcedureToExecutableBlock(geminiResponse.OptimizedProcedureBody);
            // Run the optimized procedure using the same test data and get its results and execution time.
            (DataTable optimizedData, long optimizedTime) = await RunProcedureAndGetDataAsync(oracleService, connectionString, testDataPlSql, tableNamesToTruncate, optimizedExecutableBlock, geminiResponse.ValidationQueryAfter, commandTimeoutSeconds);

            // --- Step 5: Compare results and performance ---
            statusLabel.Text = "5/5: Comparing results...";
            // Use DataTableComparator to check if the data from original and optimized runs are identical.
            bool areIdentical = DataTableComparator.AreIdentical(originalData, optimizedData, out string comparisonDetails);

            // Construct the final report string.
            string finalReport = (areIdentical ? "✅ LOGIC TEST PASSED" : "❌ LOGIC TEST FAILED") + "\n\n" +
                                 $"PERFORMANCE:\n" +
                                 $"- Original:    {originalTime}ms\n" +
                                 $"- Optimized:   {optimizedTime}ms\n" +
                                 $"- Improvement: {originalTime - optimizedTime}ms ({Math.Round((double)(originalTime - optimizedTime) * 100 / Math.Max(originalTime, 1), 2)}%)\n\n" +
                                 $"VALIDATION DETAILS:\n{comparisonDetails}";

            // Display the final report in the UI and switch to the performance tab.
            performanceLabel.Text = finalReport;
            resultsTabControl.SelectedTab = performanceTab;
            statusLabel.Text = "Analysis and Testing Complete.";
        }

        /// <summary>
        /// Clears specified tables in an Oracle database in an order that respects foreign key constraints.
        /// This method first queries `all_constraints` to build a dependency graph for the given tables,
        /// then performs a topological sort to determine the correct deletion order.
        /// Limitation: it only considers foreign key (FK) dependencies among the tables explicitly
        /// listed for clearing and does not account for FKs from external tables not in the clear list.
        /// </summary>
        /// <param name="oracleService">An instance of <see cref="OracleService"/> to execute database commands.</param>
        /// <param name="connection">An open <see cref="OracleConnection"/> to the database.</param>
        /// <param name="transaction">An active <see cref="OracleTransaction"/> for the operations.</param>
        /// <param name="tableNames">A list of table names (potentially schema-qualified) to be cleared.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <exception cref="Exception">Thrown if a cyclic dependency is detected among the tables, preventing a valid deletion order.</exception>
        private async Task ClearTablesAsync(OracleService oracleService, OracleConnection connection, OracleTransaction transaction, List<string> tableNames)
        {
            // 1. Normalize table names (to uppercase) and identify the current user for unqualified table names.
            var uniqueTableNames = new HashSet<string>(tableNames.Select(t => t.ToUpperInvariant()));
            var connectionStringBuilder = new OracleConnectionStringBuilder(connection.ConnectionString);
            string currentUser = connectionStringBuilder.UserID.ToUpper();

            // Parse table names into (owner, tableName, fullName) tuples.
            // If a table name is not schema-qualified, assume it belongs to the current user.
            var parsedTables = uniqueTableNames.Select(fullTableName =>
            {
                string[] parts = fullTableName.Split('.');
                string owner = parts.Length > 1 ? parts[0] : currentUser; // Schema (owner) is part[0] if present
                string tableName = parts.Length > 1 ? parts[1] : parts[0]; // Table name is part[1] or part[0]
                return (owner, tableName, fullTableName);
            }).ToList();

            // Create a lookup for quick access to full table names from owner/tableName pairs.
            var tableLookup = parsedTables.ToDictionary(t => (t.owner, t.tableName), t => t.fullTableName);

            // 2. Query `all_constraints` to discover foreign key dependencies *among the specified tables*.
            // `dependencies` will store ParentTable -> Set of ChildTables that have FK to ParentTable.
            // Limitation: This logic only considers FK dependencies among the tables explicitly listed in `tableNames`.
            // It does not account for FKs from tables *not* in this list that might point to tables *in* this list,
            // or FKs from tables *in* this list pointing to tables *not* in this list (which would typically not prevent deletion of the table in the list, but its parent might be external).
            var dependencies = new Dictionary<string, HashSet<string>>();

            var whereClauses = new List<string>();
            var parameters = new List<OracleParameter>();
            int paramIndex = 0;
            foreach (var (owner, tableName, _) in parsedTables)
            {
                // Build WHERE clauses for the SQL query to find constraints related to the parsed tables.
                // This looks for constraints where either the constraint owner/table or the referenced table owner/table match.
                // The query below actually focuses on `a` being the FK constraint, and `r` being the PK/UK constraint it refers to.
                // So, `a.owner` and `a.table_name` are for the child table with the FK.
                // And `r.owner` and `r.table_name` are for the parent table with the PK/UK.
                // The `whereClauses` here are used to find all FK constraints *defined on* the tables in `parsedTables`.
                whereClauses.Add($"(a.owner = :p_owner{paramIndex} AND a.table_name = :p_table_name{paramIndex})");
                parameters.Add(new OracleParameter($"p_owner{paramIndex}", owner));
                parameters.Add(new OracleParameter($"p_table_name{paramIndex}", tableName));
                paramIndex++;
            }

            if (whereClauses.Count > 0)
            {
                // SQL to find (ChildTable, ParentTable) relationships for FKs.
                // `a` is the constraint (FK), `r` is the referenced constraint (PK/UK).
                // So, `a.owner, a.table_name` is the Child table.
                // `r.owner, r.table_name` is the Parent table.
                string sql = $@"
                    SELECT a.owner AS child_owner, a.table_name AS child_table_name,
                           r.owner AS parent_owner, r.table_name AS parent_table_name
                    FROM all_constraints a
                    JOIN all_constraints r ON a.r_constraint_name = r.constraint_name AND a.r_owner = r.owner
                    WHERE a.constraint_type = 'R' -- 'R' denotes a Referential integrity (foreign key) constraint
                    AND ({string.Join(" OR ", whereClauses)})"; // Filter for constraints on tables in our list.

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

                            // Only consider dependencies where both child and parent are in the `uniqueTableNames` list.
                            if (tableLookup.TryGetValue((childOwner, childTable), out var childFullName) &&
                                tableLookup.TryGetValue((parentOwner, parentTable), out var parentFullName))
                            {
                                // Record that `parentFullName` is a parent of `childFullName`.
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

            // 3. Topological Sort (Kahn's algorithm) to determine deletion order.
            // The goal is to delete children before their parents.
            var sortedList = new List<string>(); // This will store the tables in the order they should be deleted.

            // `inDegree` stores how many tables (in the list) a given table depends on (i.e., has an FK to).
            // So, `inDegree[ChildTable]` = number of ParentTables (in the list) it has FKs to.
            // This seems incorrect for the standard Kahn's. Let's re-verify `dependencies` and `graph` construction.

            // `dependencies`: ParentTable -> Set of ChildTables (that have FK to ParentTable)
            // We need to build a graph where an edge Child -> Parent means Child must be deleted before Parent.
            // `adjGraph[Child]` = list of Parents it depends on.
            // `currentInDegree[Parent]` = number of Children that depend on it.

            // Let's use the variable names as in the existing, verified logic from previous step:
            // `graph`: Child -> Set of Parents it refers to (within the list).
            // `inDegree`: Parent -> Count of children that refer to it (within the list).
            // Queue starts with tables that are not parents to any other table in the list.
            // This means `sortedList` will process children first, then parents. This is the correct deletion order.

            var currentInDegree = uniqueTableNames.ToDictionary(t => t, t => 0); // Table -> number of tables that have an FK to it (its children in the list)
            var adjacencyList = uniqueTableNames.ToDictionary(t => t, t => new HashSet<string>()); // Parent -> Set of its children (in the list)

            foreach(var parentTableFullName in dependencies.Keys)
            {
                foreach(var childTableFullName in dependencies[parentTableFullName])
                {
                    if (adjacencyList[parentTableFullName].Add(childTableFullName))
                    {
                        currentInDegree[childTableFullName]++; // childTableFullName has an FK to parentTableFullName
                    }
                }
            }

            // Initialize queue with tables that have an in-degree of 0 (i.e., no *other tables in the list* have FKs to them).
            // These are the "parent-most" tables or isolated tables.
            // For deletion, we want to start with tables that do not constrain others, i.e., children.
            // So, queue should start with tables that have no outgoing FKs to other tables *in the list*, or tables that are "leaf" nodes.

            // Re-using the correctly reasoned logic from the previous turn's trace:
            // `dependencies` (Parent -> Children) is what we get from DB.
            // `graphBuild` (Child -> Parents it refers to).
            // `inDegreeBuild` (Parent -> Count of its children).
            // Queue starts with tables with `inDegreeBuild == 0` (tables that are not parents to any other table in the list).
            // This means these tables are children or isolated. These should be deleted first.
            var graphBuild = uniqueTableNames.ToDictionary(t => t, t => new HashSet<string>());
            var inDegreeBuild = uniqueTableNames.ToDictionary(t => t, t => 0);

            foreach (var parentFullName in dependencies.Keys) // parentFullName is a table with a PK/UK
            {
                foreach (var childFullName in dependencies[parentFullName]) // childFullName has an FK to parentFullName
                {
                    if (uniqueTableNames.Contains(childFullName) && uniqueTableNames.Contains(parentFullName))
                    {
                        if (graphBuild[childFullName].Add(parentFullName)) // childFullName depends on parentFullName
                        {
                            inDegreeBuild[parentFullName]++; // parentFullName is depended upon by childFullName
                        }
                    }
                }
            }

            var queue = new Queue<string>(inDegreeBuild.Where(kv => kv.Value == 0).Select(kv => kv.Key));

            while (queue.Count > 0)
            {
                var tableToProcess = queue.Dequeue(); // This is a "child" or isolated table, ready for deletion.
                sortedList.Add(tableToProcess);

                // For each parent that `tableToProcess` depended on:
                foreach (var parentTable in graphBuild[tableToProcess]) // These are actual parents of tableToProcess
                {
                    inDegreeBuild[parentTable]--; // Decrement the count of children for this parent.
                    if (inDegreeBuild[parentTable] == 0) // If all children (in the list) of this parent are processed
                    {
                        queue.Enqueue(parentTable); // This parent can now be added to the queue for deletion.
                    }
                }
            }

            // If not all tables are in sortedList, there's a cycle or an unhandled dependency.
            if (sortedList.Count < uniqueTableNames.Count)
            {
                var missing = string.Join(", ", uniqueTableNames.Except(sortedList));
                // Added more detail to exception as per previous reasoning.
                throw new Exception($"Cyclic dependency detected among tables, cannot determine deletion order. Problematic tables might include: {missing}. This can also happen if a table in the list has an FK to a table not in the list, and that external table also has FKs back into the list, forming a cycle not entirely within the provided table list.");
            }

            // 4. Clear table data in the topologically sorted order (children first, then parents).
            foreach (var table in sortedList)
            {
                await oracleService.ExecuteNonQueryAsync(connection, transaction, $"DELETE FROM {table}");
            }
        }

        /// <summary>
        /// Runs a given PL/SQL executable block and a validation query within a single Oracle transaction.
        /// It first clears specified tables, then inserts test data (if provided), executes the main PL/SQL block,
        /// measures its execution time, runs the validation query, and finally rolls back the transaction.
        /// </summary>
        /// <param name="oracleService">An instance of <see cref="OracleService"/>.</param>
        /// <param name="connectionString">The Oracle database connection string.</param>
        /// <param name="testDataPlSql">A PL/SQL block for inserting test data. Can be null or empty if no data insertion is needed.</param>
        /// <param name="tablesToTruncate">A list of table names to be cleared before running the test.</param>
        /// <param name="procedureExecutableBlock">The main PL/SQL block (original or optimized procedure) to execute and measure.</param>
        /// <param name="validationQuery">The SQL query to run after the procedure execution to fetch results for comparison.</param>
        /// <param name="commandTimeoutSeconds">The timeout in seconds for OracleCommand execution.</param>
        /// <returns>A tuple containing the <see cref="DataTable"/> with results from the validation query and a <see cref="long"/> representing the execution time of the procedure block in milliseconds.</returns>
        /// <remarks>All database operations (clearing, data insertion, procedure execution) are rolled back at the end.</remarks>
        private async Task<(DataTable results, long time)> RunProcedureAndGetDataAsync(
            OracleService oracleService, string connectionString, string testDataPlSql,
            List<string> tablesToTruncate, string procedureExecutableBlock, string validationQuery, int commandTimeoutSeconds)
        {
            using var connection = new OracleConnection(connectionString);
            await connection.OpenAsync();
            // Start a transaction with ReadCommitted isolation level.
            using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

            try
            {
                // Clear tables in the correct order respecting FKs.
                // Note: ClearTablesAsync itself uses ExecuteNonQueryAsync, but it's for DDL-like operations (DELETE)
                // and might not need the configurable timeout, or could be enhanced separately if needed.
                // For now, it uses the default timeout of OracleService.ExecuteNonQueryAsync if not specified.
                await ClearTablesAsync(oracleService, connection, transaction, tablesToTruncate);

                // Insert fresh test data if provided.
                if (!string.IsNullOrWhiteSpace(testDataPlSql))
                {
                    await oracleService.ExecuteNonQueryAsync(connection, transaction, testDataPlSql, commandTimeoutSeconds);
                }

                // Execute the main PL/SQL procedure block and measure its time.
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                await oracleService.ExecuteNonQueryAsync(connection, transaction, procedureExecutableBlock, commandTimeoutSeconds);
                stopwatch.Stop();

                // Execute the validation query to get the state of data after procedure execution.
                var dataTable = await oracleService.ExecuteQueryWithinTransactionAsync(connection, transaction, validationQuery, commandTimeoutSeconds);

                // IMPORTANT: Rollback all changes made during this test run.
                // This includes table clearing, data insertion, and any effects of the executed procedure.
                await transaction.RollbackAsync();
                return (dataTable, stopwatch.ElapsedMilliseconds);
            }
            catch
            {
                // Ensure rollback happens even if an error occurs during the try block.
                await transaction.RollbackAsync();
                throw; // Re-throw the original exception.
            }
        }

        // Helper classes for deserializing Gemini's schema JSON (used by GenerateInsertStatements and GenerateFakeDataCte)
        /// <summary>
        /// Represents the schema of a single column, used for deserializing schema information from JSON.
        /// </summary>
        private class ColumnSchema
        {
            /// <summary>
            /// Gets or sets the name of the column.
            /// </summary>
            [JsonProperty("columnName")]
            public string? ColumnName { get; set; }

            /// <summary>
            /// Gets or sets the Oracle data type of the column (e.g., VARCHAR2, NUMBER, DATE).
            /// </summary>
            [JsonProperty("dataType")]
            public string? DataType { get; set; }
        }

        /// <summary>
        /// Represents the schema of a single table, including its name and a list of its columns.
        /// Used for deserializing schema information from JSON.
        /// </summary>
        private class TableSchema
        {
            /// <summary>
            /// Gets or sets the name of the table, potentially schema-qualified.
            /// </summary>
            [JsonProperty("tableName")]
            public string? TableName { get; set; }

            /// <summary>
            /// Gets or sets the list of column schemas for this table.
            /// </summary>
            [JsonProperty("columns")]
            public List<ColumnSchema>? Columns { get; set; }
        }

        /// <summary>
        /// Generates a PL/SQL CTE (Common Table Expression) block for creating fake data. (Currently Unused)
        /// This method was likely intended for an alternative way to generate test data using CTEs,
        /// which can be useful for direct SELECT statements or more complex data setup scenarios
        /// not involving persistent DML in the same way `GenerateInsertStatements` does.
        /// </summary>
        /// <param name="geminiSchemaJson">JSON string describing table schemas, obtained from Gemini.</param>
        /// <param name="rowCount">The number of fake data rows to generate for each table.</param>
        /// <returns>A string containing a PL/SQL WITH clause defining CTEs for fake data generation.
        /// Returns an empty string if no data can be generated, or an SQL comment error string if parsing fails.</returns>
        private string GenerateFakeDataCte(string geminiSchemaJson, int rowCount)
        {
            // Validate inputs: schema JSON must not be empty, and rowCount must be positive.
            if (string.IsNullOrWhiteSpace(geminiSchemaJson))
            {
                System.Diagnostics.Debug.WriteLine("GenerateFakeDataCte: geminiSchemaJson is null or empty.");
                return string.Empty; // Return empty if no schema.
            }
            if (rowCount <= 0)
            {
                System.Diagnostics.Debug.WriteLine("GenerateFakeDataCte: rowCount must be positive.");
                return string.Empty; // Return empty if row count is not positive.
            }

            List<TableSchema>? tableSchemas;
            try
            {
                // Deserialize the provided JSON into a list of TableSchema objects.
                tableSchemas = JsonConvert.DeserializeObject<List<TableSchema>>(geminiSchemaJson);
                if (tableSchemas == null || tableSchemas.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("GenerateFakeDataCte: Deserialized schema is null or empty.");
                    return string.Empty; // No tables to process from the schema.
                }
            }
            catch (JsonException ex)
            {
                // Log and return an error comment if JSON parsing fails.
                System.Diagnostics.Debug.WriteLine($"GenerateFakeDataCte: Error parsing JSON schema: {ex.Message}");
                return $"-- Error parsing JSON schema: {ex.Message}\n";
            }

            var cteParts = new List<string>(); // To hold individual CTE definitions.
            // Iterate over each table defined in the schema.
            foreach (var table in tableSchemas)
            {
                // Skip tables that have no columns defined.
                if (table.Columns == null || table.Columns.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"GenerateFakeDataCte: Table {table.TableName} has no columns, skipping.");
                    continue;
                }

                var sbCte = new StringBuilder();
                // Start CTE definition (e.g., "TableName_fake AS ( ... )").
                sbCte.AppendLine($"  {SanitizeForPlSqlIdentifier(table.TableName)}_fake AS (");

                // Generate `rowCount` rows for the current table's CTE.
                for (int i = 1; i <= rowCount; i++)
                {
                    sbCte.Append("    SELECT ");
                    // For each column, generate a value based on its data type.
                    for (int colIdx = 0; colIdx < table.Columns.Count; colIdx++)
                    {
                        ColumnSchema column = table.Columns[colIdx];
                        string generatedValue;
                        string? colDataTypeUpper = column.DataType?.ToUpperInvariant();

                        // Simple data generation based on common Oracle types.
                        if (colDataTypeUpper == null)
                        {
                            generatedValue = "NULL";
                        }
                        else if (colDataTypeUpper.StartsWith("VARCHAR2") || colDataTypeUpper.StartsWith("VARCHAR") || colDataTypeUpper.StartsWith("CHAR") || colDataTypeUpper.StartsWith("NVARCHAR2"))
                        {
                            // Generate a unique string like 'Val_TAB_COL_1'.
                            string tableNamePart = table.TableName != null ? SanitizeForPlSqlIdentifier(table.TableName).Substring(0, Math.Min(SanitizeForPlSqlIdentifier(table.TableName).Length, 3)) : "TAB";
                            string colNamePart = column.ColumnName != null ? SanitizeForPlSqlIdentifier(column.ColumnName).Substring(0, Math.Min(SanitizeForPlSqlIdentifier(column.ColumnName).Length, 3)) : "COL";
                            generatedValue = $"'Val_{tableNamePart}_{colNamePart}_{i}'";
                        }
                        else if (colDataTypeUpper.StartsWith("NUMBER") || colDataTypeUpper.StartsWith("INTEGER") || colDataTypeUpper.StartsWith("INT") || colDataTypeUpper.StartsWith("DECIMAL") || colDataTypeUpper.StartsWith("FLOAT"))
                        {
                            // Generate a simple number based on the loop index.
                            generatedValue = $"{i}";
                        }
                        else if (colDataTypeUpper.StartsWith("DATE"))
                        {
                            // Generate a date by adding days to a base date.
                            generatedValue = $"TO_DATE('2000-01-01', 'YYYY-MM-DD') + {i - 1}";
                        }
                        else
                        {
                            // Default to NULL for unsupported data types.
                            generatedValue = "NULL";
                            System.Diagnostics.Debug.WriteLine($"GenerateFakeDataCte: Unsupported data type {column.DataType} for column {column.ColumnName} in table {table.TableName}. Using NULL.");
                        }

                        // Append the generated value aliased with the original column name (quoted).
                        sbCte.Append($"{generatedValue} AS \"{column.ColumnName}\"");

                        if (colIdx < table.Columns.Count - 1)
                        {
                            sbCte.Append(", "); // Add comma between column definitions.
                        }
                    }
                    sbCte.AppendLine(" FROM DUAL"); // In Oracle, SELECT without FROM needs DUAL.
                    if (i < rowCount)
                    {
                        sbCte.AppendLine("  UNION ALL"); // Combine rows using UNION ALL.
                    }
                }
                sbCte.Append("  )"); // End of CTE definition.
                cteParts.Add(sbCte.ToString());
            }

            // If no CTEs were generated (e.g., all tables had no columns), return empty.
            if (cteParts.Count == 0)
            {
                return string.Empty;
            }

            // Combine all CTE definitions into a single WITH clause.
            return "WITH\n" + string.Join(",\n", cteParts) + "\n";
        }

        /// <summary>
        /// Sanitizes a string to be a valid PL/SQL identifier.
        /// Replaces non-alphanumeric characters (except underscore) with underscores.
        /// Ensures the identifier does not start with a number by prepending an underscore if necessary.
        /// Truncates the identifier to 30 characters for compatibility with older Oracle versions.
        /// </summary>
        /// <param name="name">The string to sanitize.</param>
        /// <returns>A sanitized string suitable for use as a PL/SQL identifier.</returns>
        /// <remarks>
        /// The 30-character truncation is based on older Oracle versions (e.g., prior to 12cR2).
        /// Newer Oracle versions support longer identifiers (up to 128 bytes).
        /// This truncation ensures broader compatibility but might not be strictly necessary for modern Oracle databases if the target environment supports longer names.
        /// </remarks>
        private string SanitizeForPlSqlIdentifier(string? name) // CS8604: Make 'name' parameter nullable
        {
            if (string.IsNullOrWhiteSpace(name)) return "default_identifier";

            // Replace non-alphanumeric characters (excluding underscore) with underscore.
            string sanitized = System.Text.RegularExpressions.Regex.Replace(name, @"[^a-zA-Z0-9_]", "_");

            // If the first character is a digit, prepend an underscore as PL/SQL identifiers cannot start with a number.
            if (sanitized.Length > 0 && char.IsDigit(sanitized[0])) // Added length check for safety, though Regex.Replace likely ensures non-empty if input is not just symbols
            {
                sanitized = "_" + sanitized;
            }
            // Truncate to 30 characters.
            // This is a conservative limit based on older Oracle versions (e.g., pre-12.2).
            // Oracle 12.2 and later support identifiers up to 128 bytes.
            // Consider adjusting this limit if targeting only newer Oracle versions.
            return sanitized.Length > 30 ? sanitized.Substring(0, 30) : sanitized;
        }

        /// <summary>
        /// Generates a PL/SQL anonymous block containing INSERT statements for populating tables with test data.
        /// The data generation logic attempts to create plausible values based on Oracle data types
        /// (VARCHAR2, NUMBER, DATE) and uses PL/SQL collections for bulk insertion via FORALL.
        /// </summary>
        /// <param name="geminiSchemaJson">A JSON string describing the table schemas (name, columns, data types),
        /// typically obtained from Gemini or a similar schema extraction process.</param>
        /// <param name="rowCount">The number of rows to generate and insert for each table.</param>
        /// <param name="tableNames">An output list that will be populated with the original, potentially schema-qualified,
        /// names of the tables for which data generation code was created. This is used by other parts of the system
        /// (e.g., for table clearing).</param>
        /// <returns>A string containing the generated PL/SQL anonymous block for data insertion.
        /// Returns an SQL comment error string if schema parsing or data generation fails for any reason.</returns>
        private string GenerateInsertStatements(string geminiSchemaJson, int rowCount, out List<string> tableNames)
        {
            tableNames = new List<string>(); // Initialize output list of table names.
            // Validate inputs: schema JSON must be provided and rowCount must be positive.
            if (string.IsNullOrWhiteSpace(geminiSchemaJson))
            {
                System.Diagnostics.Debug.WriteLine("GenerateInsertStatements: geminiSchemaJson is null or empty.");
                return "-- Error: Gemini schema JSON is null or empty.\n"; // Return SQL comment error.
            }
            if (rowCount <= 0)
            {
                System.Diagnostics.Debug.WriteLine("GenerateInsertStatements: rowCount must be positive.");
                return "-- Error: Row count must be positive.\n"; // Return SQL comment error.
            }

            List<TableSchema>? tableSchemas;
            try
            {
                // Deserialize the JSON schema string into a list of TableSchema objects.
                tableSchemas = JsonConvert.DeserializeObject<List<TableSchema>>(geminiSchemaJson);
                if (tableSchemas == null || tableSchemas.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("GenerateInsertStatements: Deserialized schema is null or empty.");
                    return "-- Error: Deserialized schema is null or contains no tables.\n";
                }
            }
            catch (JsonException ex)
            {
                // Log and return an error if JSON parsing fails.
                System.Diagnostics.Debug.WriteLine($"GenerateInsertStatements: Error parsing JSON schema: {ex.Message}");
                return $"-- Error parsing JSON schema: {ex.Message}\n";
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("DECLARE"); // Start of the PL/SQL anonymous block.

            // --- Declaration Section ---
            // For each table in the schema, declare a PL/SQL collection type (table of %ROWTYPE)
            // and a variable of that collection type to temporarily store generated rows.
            foreach (TableSchema table in tableSchemas)
            {
                // Skip tables with no columns or no name, as they cannot be processed.
                if (table.Columns == null || table.Columns.Count == 0 || string.IsNullOrWhiteSpace(table.TableName))
                {
                    System.Diagnostics.Debug.WriteLine($"GenerateInsertStatements: Table {(table.TableName ?? "[NULL] ")} has no columns or is invalid, skipping for PL/SQL generation.");
                    continue;
                }
                // Sanitize the table name for use in PL/SQL type and variable names (e.g., replace special chars).
                string sanitizedTableName = SanitizeForPlSqlIdentifier(table.TableName);
                // Add the original table name (which might be schema-qualified) to the output list for later use (e.g., TRUNCATE).
                tableNames.Add(table.TableName);

                // Define a PL/SQL collection type: e.g., TYPE T_Fake_MYTABLE_Rows IS TABLE OF MYSCHEMA.MYTABLE%ROWTYPE INDEX BY PLS_INTEGER;
                sb.AppendLine($"  TYPE T_Fake_{sanitizedTableName}_Rows IS TABLE OF {table.TableName}%ROWTYPE INDEX BY PLS_INTEGER;");
                // Declare a variable of this collection type: e.g., V_Fake_MYTABLE_Data T_Fake_MYTABLE_Rows;
                sb.AppendLine($"  V_Fake_{sanitizedTableName}_Data T_Fake_{sanitizedTableName}_Rows;");
            }
            sb.AppendLine("BEGIN"); // Start of the executable section of the PL/SQL block.

            // --- Data Population Section ---
            // Loop through each table schema again to generate data and populate the PL/SQL collections.
            foreach (TableSchema table in tableSchemas)
            {
                if (table.Columns == null || table.Columns.Count == 0 || string.IsNullOrWhiteSpace(table.TableName))
                {
                    continue; // Skip invalid tables (already logged).
                }
                string sanitizedTableName = SanitizeForPlSqlIdentifier(table.TableName);
                sb.AppendLine($"  -- Populating data for table: {table.TableName}");
                // Loop `rowCount` times to generate that many rows for the current table.
                sb.AppendLine($"  FOR i IN 1..{rowCount} LOOP");

                // For each column in the table, generate an appropriate fake value.
                foreach (ColumnSchema column in table.Columns)
                {
                    string generatedValue = "NULL"; // Default to NULL if type is unknown or generation fails.
                    string? columnDataTypeUpper = column.DataType?.ToUpperInvariant();

                    if (columnDataTypeUpper == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"GenerateInsertStatements: Null data type for column {column.ColumnName} in table {table.TableName}. Using NULL.");
                    }
                    // Handle VARCHAR2, CHAR, NVARCHAR2 types:
                    else if (columnDataTypeUpper.StartsWith("VARCHAR2") || columnDataTypeUpper.StartsWith("VARCHAR") || columnDataTypeUpper.StartsWith("CHAR") || columnDataTypeUpper.StartsWith("NVARCHAR2"))
                    {
                        int declaredLength = 30; // Default assumed length for strings.
                        // Attempt to parse actual declared length from the data type string (e.g., "VARCHAR2(100)").
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
                                        // Clamp length to avoid excessively large strings and Oracle limits.
                                        declaredLength = Math.Max(1, Math.Min(parsedLength, 4000));
                                    }
                                }
                            }
                            catch { /* Parsing error, use default length. Already logged by omission. */ }
                        }

                        // Dynamically construct a string value using DBMS_RANDOM.STRING for variability,
                        // ensuring it fits within the `declaredLength`.
                        int maxILength = rowCount.ToString().Length; // Max length of loop counter `i` when converted to string.
                        string prefixForCalc = "Val_";
                        string suffixTemplateForCalc = "_";
                        int fixedPartsLength = prefixForCalc.Length + suffixTemplateForCalc.Length + maxILength;
                        int availableLength = declaredLength - fixedPartsLength - 2; // -2 for the surrounding quotes ''.

                        if (availableLength >= 1) // Check if there's enough space for at least 1 random character.
                        {
                            int randomPartLength = Math.Min(availableLength, 20); // Limit random part to 20 chars or available.
                            generatedValue = $"'{prefixForCalc}' || DBMS_RANDOM.STRING('A', {randomPartLength}) || '{suffixTemplateForCalc}' || i";
                        }
                        else // Fallback if the full pattern is too long for the declared column length.
                        {
                            string errPrefixForCalc = "Err_";
                            fixedPartsLength = errPrefixForCalc.Length + maxILength;
                            availableLength = declaredLength - fixedPartsLength - 2;

                            if (availableLength >= 0) // Check if 'Err_' + i can fit.
                            {
                                generatedValue = $"'E_' || TO_CHAR(i)"; // Example: 'E_1', 'E_100'
                                if ((errPrefixForCalc.Length + maxILength + 2) > declaredLength) // Check if this fallback itself is too long
                                {
                                    if (declaredLength >= 2) generatedValue = "''"; // Empty Oracle string ' '
                                    else generatedValue = "NULL"; // Not even empty string fits
                                }
                            }
                            else if (declaredLength >= 2) // Can only fit empty string.
                            {
                                generatedValue = "''";
                            }
                            else // Cannot fit anything.
                            {
                                generatedValue = "NULL";
                            }
                            System.Diagnostics.Debug.WriteLine($"GenerateInsertStatements: VARCHAR2 column {column.ColumnName} in table {table.TableName} has declared length {declaredLength} too small for full pattern 'Val_RANDOM_i'. Using fallback: {generatedValue}");
                        }
                    }
                    // Handle NUMBER, INTEGER, INT, DECIMAL, FLOAT types:
                    else if (columnDataTypeUpper.StartsWith("NUMBER") || columnDataTypeUpper.StartsWith("INTEGER") || columnDataTypeUpper.StartsWith("INT") || columnDataTypeUpper.StartsWith("DECIMAL") || columnDataTypeUpper.StartsWith("FLOAT"))
                    {
                        // Generate a number combining a random value and the loop counter for variability.
                        generatedValue = $"TRUNC(DBMS_RANDOM.VALUE(1, 100000)) + MOD(i, 100000)";
                    }
                    // Handle DATE type:
                    else if (columnDataTypeUpper.StartsWith("DATE"))
                    {
                        // Generate a date by adding days (modulated over 50 years) to a base date.
                        generatedValue = $"TO_DATE('2000-01-01', 'YYYY-MM-DD') + MOD(i-1, 365*50)";
                    }
                    else
                    {
                        // For any other unsupported data types, log and use NULL.
                        System.Diagnostics.Debug.WriteLine($"GenerateInsertStatements: Unsupported data type {column.DataType} for column {column.ColumnName} in table {table.TableName}. Using NULL.");
                        // generatedValue remains "NULL"
                    }

                    // Assign the generated PL/SQL expression string to the corresponding field in the current row of the PL/SQL collection.
                    // Column names are quoted to handle potential reserved words or special characters in original column names.
                    sb.AppendLine($"    V_Fake_{sanitizedTableName}_Data(i).\"{column.ColumnName}\" := {generatedValue};");
                }
                sb.AppendLine("  END LOOP;"); // End of FOR i IN 1..rowCount LOOP for current table.
                sb.AppendLine();
            }

            // --- Bulk Insertion Section ---
            // After all PL/SQL collections are populated, use FORALL to perform bulk inserts into the actual database tables.
            // This is much more efficient than row-by-row inserts.
            foreach (TableSchema table in tableSchemas)
            {
                if (table.Columns == null || table.Columns.Count == 0 || string.IsNullOrWhiteSpace(table.TableName))
                {
                    continue; // Skip invalid tables.
                }
                string sanitizedTableName = SanitizeForPlSqlIdentifier(table.TableName);
                sb.AppendLine($"  -- Inserting data into table: {table.TableName}");
                // FORALL statement to bulk insert all rows from the collection into the table.
                sb.AppendLine($"  FORALL i IN V_Fake_{sanitizedTableName}_Data.FIRST..V_Fake_{sanitizedTableName}_Data.LAST");
                // The INSERT statement uses the original table name (which can be schema-qualified)
                // and refers to the collection record for values.
                sb.AppendLine($"    INSERT INTO {table.TableName} VALUES V_Fake_{sanitizedTableName}_Data(i);");
                sb.AppendLine();
            }

            sb.AppendLine("END;"); // End of the PL/SQL anonymous block.
            return sb.ToString();
        }
    }
}