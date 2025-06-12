# Oracle SQL Optimizer WPF Application
xXxXxXxX
The Oracle SQL Optimizer is a .NET WPF desktop application designed to help users optimize Oracle SQL scripts using the Google Gemini API. It automates the process of sending a script to Gemini for optimization, running both the original and optimized versions (via validation queries), comparing their outputs, and presenting a comprehensive report.

## Features

*   **SQL Optimization via Gemini API**: Leverages Google's Gemini Pro model to receive suggestions for optimizing Oracle SQL scripts.
*   **Automated Script Execution**: Connects to an Oracle database to run the user's original SQL script.
*   **Execution of Optimized Script/Validation Queries**: Executes the Gemini-suggested optimized script (or specific validation queries provided by Gemini) against the same database.
*   **Output Validation**: Automatically compares the `DataTable` results from the original script and the validation queries. If the outputs are not identical, the optimized script is considered rejected for safety.
*   **Detailed Reporting**:
    *   **Optimized SQL Tab**: Displays the optimized SQL script received from Gemini.
    *   **Gemini Report Tab**: Shows the textual explanation and reasoning from Gemini regarding the optimizations.
    *   **Validation Details Tab**: Provides a summary of the data comparison, highlighting differences if any, or confirming success.
    *   **Performance Tab**: Presents basic execution time metrics for the original script and the validation queries, along with the time difference.
*   **User-Friendly Interface**: A single-window WPF application with clear sections for input, actions, and results.
*   **Status Updates**: Provides real-time feedback on the current operation via a status bar.
*   **Save Functionality**: Allows saving the optimized script and the various reports (Note: The actual file saving logic is a placeholder in the current version but the UI button is present).
*   **MVVM Architecture**: Built using the Model-View-ViewModel pattern for a clean separation of concerns, utilizing the CommunityToolkit.Mvvm library.
*   **Error Handling**: Provides feedback for common issues such as database connection errors, API communication problems, or script execution failures.


## Usage

To use the Oracle SQL Optimizer application, follow these steps:

1.  **Launch the Application**: Open the `OracleOptimizer.exe` application (after building it on a Windows machine with .NET 8 and WPF support).

2.  **Enter Connection Details**:
    *   **Oracle Connection String**: In the "Oracle Connection String" field, enter the full connection string for your Oracle database.
        *   Example: `Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=your-oracle-host)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=your-service-name)));User Id=your-user;Password=your-password;`
    *   **Gemini API Key**: In the "Gemini API Key" field, enter your Google Gemini API key. This is required to communicate with the optimization service.

3.  **Input Original SQL Script**:
    *   In the large text area labeled "Original SQL Script," paste or type the Oracle SQL query that you want to optimize. This script should typically be a `SELECT` statement if you expect its output to be directly compared.

4.  **Analyze and Optimize**:
    *   Click the "Analyze & Optimize" button.
    *   The application will perform the following actions, with status updates appearing in the bottom status bar:
        *   **Execute Original Script**: Connects to your Oracle database and runs the "Original SQL Script." The data returned (if any) and execution time are recorded.
        *   **Call Gemini API**: Sends the original SQL script and a specific prompt to the Gemini API, requesting an optimized version, an explanation, and validation queries.
        *   **Execute Optimized Script/Validation Queries**:
            *   The application first executes the `optimized_sql` part of the Gemini response (typically using an `ExecuteNonQuery` call, assuming it might contain DML or DDL, or be an Oracle PL/SQL block).
            *   Then, it executes the first `validation_query` provided by Gemini (using an `ExecuteQuery` call) to retrieve data from the database *after* the optimized script has run. The execution time for this validation query is recorded.
        *   **Validate Results**: The data table returned by your "Original SQL Script" is compared with the data table returned by the "validation query." The comparison checks for column count, names, types, row count, and individual cell values.

5.  **Review Results**:
    The results of the process are displayed in a set of tabs:
    *   **Optimized SQL Tab**:
        *   Displays the optimized SQL script exactly as received from the Gemini API.
        *   This field is read-only.
    *   **Gemini Report Tab**:
        *   Shows the textual explanation from Gemini. This usually includes why the suggested changes were made and how they might improve performance or structure.
        *   This field is read-only.
    *   **Validation Details Tab**:
        *   Provides a message indicating whether the validation passed or failed.
        *   If validation passed, it will confirm that row counts, column counts, names, types, and all cell values are identical.
        *   If validation failed, it will provide details about the first discrepancy found (e.g., different row/column counts, mismatched column names/types, or different cell values at a specific location).
        *   If an error occurred during the process (e.g., database error, API error), details of the error will be shown here.
    *   **Performance Tab**:
        *   Displays the execution time (in milliseconds) for the "Original SQL Script."
        *   Displays the execution time (in milliseconds) for the "Optimized SQL (Validation Query)."
        *   Calculates and shows the delta (difference) in execution times.

6.  **Outcome and Saving**:
    *   **Status Bar**: The status bar at the bottom will indicate the final outcome (e.g., "Validation PASSED," "Validation FAILED. Optimized script has been rejected," or error messages).
    *   **Save Script & Report Button**:
        *   This button (labeled "Save Script & Report") becomes enabled **only if** the validation step passes.
        *   If enabled, clicking it (in a future version) would save the optimized SQL, Gemini's report, validation details, and performance information. (Currently, it shows a "not implemented" message).
        *   If validation fails, the button remains disabled, signifying that the optimized script is rejected due to output differences.

7.  **Iterate or Refine**:
    *   Based on the results, you can choose to use the optimized script (if validation passed), further refine your original script, or try different approaches.
    *   If the Gemini API key is invalid or there are connection issues, the status bar and validation details tab will reflect these errors.


## Technical Details

*   **Framework**: .NET 8
*   **Application Type**: Windows Presentation Foundation (WPF)
*   **Language**: C#
*   **Architecture**: Model-View-ViewModel (MVVM)
    *   Utilizes `CommunityToolkit.Mvvm` for base classes (`ObservableObject`) and commands (`RelayCommand`).
*   **Key NuGet Packages**:
    *   `Oracle.ManagedDataAccess.Core`: For connecting to and interacting with Oracle databases.
    *   `CommunityToolkit.Mvvm`: For implementing the MVVM pattern.
*   **API Integration**: Uses standard `HttpClient` to communicate with the Google Gemini API (gemini-pro model) for SQL analysis and optimization.
*   **Data Comparison**: Implements a custom `DataTableComparator` to perform detailed comparisons between the datasets returned by the original and validation queries.
