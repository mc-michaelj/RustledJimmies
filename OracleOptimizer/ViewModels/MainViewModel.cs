using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OracleOptimizer.Core;
using OracleOptimizer.Models;
using OracleOptimizer.Services;
using System;
using System.Data;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows; // Required for MessageBox in case of unhandled errors, though direct UI interaction from VM is not ideal.

namespace OracleOptimizer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private IDatabaseService? _databaseService;
    private IGeminiApiService? _geminiApiService;

    // --- Observable Properties ---
    [ObservableProperty]
    private string _connectionString = "User Id=cisconvert;Password=cisconvert;Data Source=dev5-mer-db:1521/TCTN_MASTER";

    [ObservableProperty]
    private string _originalSql = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOptimizedSqlAvailable))]
    private string _optimizedSql = "";

    public bool IsOptimizedSqlAvailable => !string.IsNullOrWhiteSpace(OptimizedSql);

    [ObservableProperty]
    private string _geminiReport = "";

    [ObservableProperty]
    private string _validationDetails = "";

    [ObservableProperty]
    private string _performanceReport = "";

    [ObservableProperty]
    private string _statusBarText = "Ready";

    [ObservableProperty]
    private bool _isSaveEnabled = false;

    // Gemini API Key - In a real app, this should come from a secure config or user input
    // For this exercise, it might be hardcoded or an input field added to UI if time permits.
    // Let's assume for now it's hardcoded or will be passed if we had a settings dialog.
    // For the purpose of this task, let's add it as a property that could be bound or set.
    [ObservableProperty]
    private string _geminiApiKey = ""; // TODO: User should provide this.

    // --- Constructor ---
    public MainViewModel()
    {
        // Default constructor for XAML instantiation.
        // Services will be instantiated on demand or passed via DI if we enhance this later.
        _databaseService = null;
        _geminiApiService = null;
    }

    // Constructor for testing or if we implement DI
    public MainViewModel(IDatabaseService databaseService, IGeminiApiService geminiApiService)
    {
        _databaseService = databaseService;
        _geminiApiService = geminiApiService;
    }

    // --- Relay Commands ---
    [RelayCommand]
    private async Task AnalyzeAndOptimizeAsync()
    {
        StatusBarText = "Starting analysis...";
        IsSaveEnabled = false;
        OptimizedSql = "";
        GeminiReport = "";
        ValidationDetails = "";
        PerformanceReport = "";

        // Instantiate services here if not using DI for simplicity in this example
        _databaseService ??= new OracleDatabaseService();
        _geminiApiService ??= new GeminiApiService();

        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            StatusBarText = "Error: Oracle Connection String is required.";
            // MessageBox.Show("Oracle Connection String is required.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        if (string.IsNullOrWhiteSpace(OriginalSql))
        {
            StatusBarText = "Error: Original SQL script is required.";
            // MessageBox.Show("Original SQL script is required.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        if (string.IsNullOrWhiteSpace(GeminiApiKey))
        {
            StatusBarText = "Error: Gemini API Key is required.";
            // MessageBox.Show("Gemini API Key is required. Please enter it to proceed.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Error);
            // Potentially, add a UI element for this or guide the user.
            // For now, we'll just update status bar.
            return;
        }


        Stopwatch stopwatch = new Stopwatch();
        DataTable? beforeData = null;
        DataTable? afterData = null;
        TimeSpan originalSqlExecutionTime;
        TimeSpan optimizedSqlExecutionTime = TimeSpan.Zero; // Initialize

        try
        {
            // BEFORE RUN
            StatusBarText = "Executing original SQL script...";
            stopwatch.Start();
            beforeData = _databaseService.ExecuteQuery(ConnectionString, OriginalSql);
            stopwatch.Stop();
            originalSqlExecutionTime = stopwatch.Elapsed;
            StatusBarText = $"Original SQL executed in {originalSqlExecutionTime.TotalMilliseconds} ms. Rows: {(beforeData != null ? beforeData.Rows.Count : 0)}.";

            // GEMINI CALL
            StatusBarText = "Calling Gemini API for optimization...";
            GeminiApiResponse geminiResponse = await _geminiApiService.AnalyzeSqlScriptAsync(OriginalSql, GeminiApiKey); // Use the API key property

            if (geminiResponse == null || string.IsNullOrWhiteSpace(geminiResponse.OptimizedSql))
            {
                StatusBarText = "Gemini API did not return an optimized script.";
                GeminiReport = geminiResponse?.Explanation ?? "No explanation provided.";
                return;
            }

            GeminiReport = geminiResponse.Explanation; // Display explanation regardless of validation outcome

            // AFTER RUN (Optimized Script)
            // The spec says "Execute the optimized_sql from the Gemini response using ExecuteNonQuery."
            // This seems like a potential issue if the optimized script is a SELECT query.
            // However, if Gemini is instructed to provide "validation_queries", these should be SELECTs.
            // Let's assume "optimized_sql" could be DML and "validation_queries" are for data retrieval.
            // If "optimized_sql" is a SELECT, ExecuteNonQuery would fail or do nothing.
            // For now, following the spec. If it's meant to be a SELECT, it should be ExecuteQuery.
            // Let's assume the "optimized_sql" is a DML or a script that doesn't return a data table directly for comparison.
            // The comparison will be based on "validation_queries".

            // Revised approach for "AFTER RUN":
            // We need to run the *optimized SQL* and get its data for comparison,
            // if the original SQL was a query.
            // If Gemini is also providing "validation_queries", how do they fit?
            // "validate that the output is identical" implies comparing output of OriginalSql vs OptimizedSql.

            // Sticking to "execute the validation_queries and store the result in DataTable afterData"
            // This means Gemini *must* provide validation queries that are SELECTs.
            // And these validation queries should fetch the *same kind of data* as OriginalSql.

            // If Gemini is expected to optimize a SELECT query, then `optimized_sql` is the optimized SELECT.
            // And `validation_queries` might be the *same* optimized SELECT, or specific checks.
            // The spec: "execute the validation_queries and store the result in DataTable afterData"
            // This is a bit ambiguous if `OriginalSql` is a SELECT.
            // Let's assume `validation_queries` are the primary source for `afterData`.
            // If `validation_queries` is null or empty, this step will be problematic.

            if (geminiResponse.ValidationQueries == null || geminiResponse.ValidationQueries.Count == 0)
            {
                StatusBarText = "Validation FAILED: Gemini did not provide validation queries.";
                OptimizedSql = geminiResponse.OptimizedSql ?? string.Empty; // Show the optimized SQL anyway
                ValidationDetails = "Gemini API response did not include 'validation_queries'. Cannot validate output.";
                IsSaveEnabled = false; // Cannot save if validation cannot be performed
                return;
            }

            // Execute optimized DMLs/DDLs first if any, then run validation queries
            // For now, let's assume optimized_sql itself might not return data for direct comparison,
            // and validation_queries are the ones that do.
            // If optimized_sql is indeed a SELECT, it should be one of the validation_queries.
            // The prompt to Gemini asked for "validation_queries".

            // Let's assume the `optimized_sql` itself might be run (e.g., if it's an anonymous block or DDL/DML)
            // and then `validation_queries` are used to fetch the results for comparison.
            // This implies that `OriginalSql` should also be compared against the results of `validation_queries`
            // run against the state *before* `optimized_sql` was executed.
            // This is getting complex. Let's simplify based on "validate that the output is identical".
            // This means `OriginalSql` (a query) vs `OptimizedSql` (the optimized version of that query).

            // Simplest interpretation:
            // 1. Run OriginalSQL -> beforeData
            // 2. Get OptimizedSQL from Gemini.
            // 3. Run OptimizedSQL -> afterData
            // 4. Compare beforeData and afterData.
            // The `validation_queries` from Gemini might be for more complex scenarios or additional checks.
            // The spec says "execute the validation_queries and store the result in DataTable afterData."
            // This means `afterData` comes from `validation_queries`, NOT `OptimizedSql`.

            // This setup implies `OriginalSql` is a SELECT, and `validation_queries` (also SELECTs)
            // are run after `OptimizedSql` (which could be DML/DDL or the optimized SELECT itself) is executed.
            // The comparison is between `beforeData` (from `OriginalSql`) and `afterData` (from `validation_queries`).

            // First, execute the main optimized script (could be DML, DDL, or the optimized query itself)
            // We'll assume ExecuteNonQuery for the main optimized script as per spec,
            // implying it might modify data or set up something.
            StatusBarText = "Executing optimized script (non-query part, if any)...";
            _databaseService.ExecuteNonQuery(ConnectionString, geminiResponse.OptimizedSql);
            // This might not be timed for the performance report if it's not the data-retrieving part.

            // Now, execute the validation queries to get `afterData`.
            // For simplicity, let's assume the first validation query is the one to compare.
            // A more robust solution would handle multiple validation queries.
            if (geminiResponse.ValidationQueries.Count > 0)
            {
                StatusBarText = "Executing validation queries...";
                stopwatch.Restart();
                // For now, using the first validation query. This might need refinement.
                afterData = _databaseService.ExecuteQuery(ConnectionString, geminiResponse.ValidationQueries[0]);
                stopwatch.Stop();
                optimizedSqlExecutionTime = stopwatch.Elapsed; // This is the time for the validation query
            }
            else
            {
                // This case should have been caught above, but as a fallback:
                ValidationDetails = "Validation FAILED: No validation queries provided by Gemini.";
                StatusBarText = "Validation FAILED. Optimized script rejected.";
                OptimizedSql = geminiResponse.OptimizedSql ?? string.Empty; // Show it but don't enable save
                IsSaveEnabled = false;
                return;
            }


            // VALIDATION
            StatusBarText = "Validating results...";
            bool areIdentical = DataTableComparator.AreIdentical(beforeData, afterData, out string comparisonDetails);
            ValidationDetails = comparisonDetails;

            // UPDATE UI
            if (areIdentical)
            {
                StatusBarText = "Validation PASSED.";
                OptimizedSql = geminiResponse.OptimizedSql ?? string.Empty; // Show the optimized SQL
                IsSaveEnabled = true;
            }
            else
            {
                StatusBarText = "Validation FAILED. Optimized script has been rejected.";
                // OptimizedSql = ""; // Clear it as per spec "Optimized script shall be rejected"
                // However, user might still want to see it. Let's show it but disable save.
                OptimizedSql = geminiResponse.OptimizedSql ?? string.Empty; // Show it, but IsSaveEnabled remains false.
                IsSaveEnabled = false;
            }

            // Performance Report
            StringBuilder perfReportBuilder = new StringBuilder();
            perfReportBuilder.AppendLine($"Original SQL Execution Time: {originalSqlExecutionTime.TotalMilliseconds:F2} ms");
            if (afterData != null) // afterData would be null if validation queries weren't run
            {
                perfReportBuilder.AppendLine($"Optimized SQL (Validation Query) Execution Time: {optimizedSqlExecutionTime.TotalMilliseconds:F2} ms");
                perfReportBuilder.AppendLine($"Delta: {(originalSqlExecutionTime - optimizedSqlExecutionTime).TotalMilliseconds:F2} ms");
            }
            else
            {
                perfReportBuilder.AppendLine($"Optimized SQL (Validation Query) Execution Time: Not run.");
            }
            PerformanceReport = perfReportBuilder.ToString();

        }
        catch (Oracle.ManagedDataAccess.Client.OracleException oraEx)
        {
            StatusBarText = $"Oracle Error: {oraEx.Message}";
            ValidationDetails = $"Oracle Exception: {oraEx.ToString()}";
            IsSaveEnabled = false;
        }
        catch (HttpRequestException httpEx)
        {
            StatusBarText = $"Gemini API Error: {httpEx.Message}";
            ValidationDetails = $"HTTP Request Exception: {httpEx.ToString()}";
            IsSaveEnabled = false;
        }
        catch (JsonException jsonEx)
        {
            StatusBarText = $"Gemini API Response Error: {jsonEx.Message}";
            ValidationDetails = $"JSON Deserialization Exception: {jsonEx.ToString()}";
            IsSaveEnabled = false;
        }
        catch (Exception ex)
        {
            StatusBarText = $"An error occurred: {ex.Message}";
            ValidationDetails = $"General Exception: {ex.ToString()}"; // Provide full details for debugging
            IsSaveEnabled = false;
            // Consider logging the full exception ex.ToString()
            // MessageBox.Show($"An unexpected error occurred: {ex.Message}\n\nDetails:\n{ex.ToString()}", "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            // Ensure stopwatch is stopped if it was started and an exception occurred mid-process
            if (stopwatch.IsRunning)
            {
                stopwatch.Stop();
            }
        }
    }

    // Placeholder for Save Script & Report command if needed later
    [RelayCommand(CanExecute = nameof(IsSaveEnabled))]
    private void SaveScriptAndReport()
    {
        // This is where you would implement logic to save:
        // - OptimizedSql
        // - GeminiReport
        // - ValidationDetails
        // - PerformanceReport
        // To a file or files.
        // For this exercise, the command is present, but implementation of saving is optional.

        StatusBarText = "Save functionality not implemented in this version.";
        // Example:
        // string combinedReport = $"--- Optimized SQL ---\n{OptimizedSql}\n\n--- Gemini Report ---\n{GeminiReport}\n\n--- Validation Details ---\n{ValidationDetails}\n\n--- Performance ---\n{PerformanceReport}";
        // System.IO.File.WriteAllText("OptimizedReport.txt", combinedReport);
        // StatusBarText = "Report saved to OptimizedReport.txt";
    }
}
