# Intelligent Oracle SQL Optimizer

This is an advanced desktop tool for safely analyzing, optimizing, and validating entire Oracle PL/SQL procedures using Google's Gemini API. It accepts a full procedure body, intelligently determines how to test it, and uses a transaction-based workflow to guarantee data safety.

---

## How It Works: The Intelligent Workflow

1. **User Input**: The user pastes an Oracle `PROCEDURE` body into the application, along with their database connection details.
2. **AI Test Planning**: The application sends the procedure to the Gemini API, asking it to act as a Test Planner and provide:
   - `validation_query_before`: A `SELECT` statement to get the data state _before_ the logic runs.
   - `optimized_procedure_body`: The full, optimized PL/SQL code.
   - `validation_query_after`: A `SELECT` statement to get the data state _after_ the logic runs.
3. **"Before" Snapshot**: The application connects to Oracle and executes the `validation_query_before` to get the baseline data state.
4. **Safe Execution & "After" Snapshot**: The application starts a database **TRANSACTION**, executes the `optimized_procedure_body`, and immediately runs the `validation_query_after` to get the new data state.
5. **Automated Validation & Decision**:
   - **PASS**: If the data matches, the application issues a **COMMIT** and approves the script.
   - **FAIL**: If the data does not match, it issues a **ROLLBACK**, undoing all changes.
6. **Display Results**: The application displays the results, including a clear "PASS" or "FAIL (Rolled Back)" status.

---

## Installation & Usage

### Installation

No installation is required. Simply download the latest `.exe` file from the **Releases** page on the right-hand side of the GitHub repository.

### Usage Guide

1. Double-click the downloaded `.exe` file to run the application.
2. **Connection Details**: Fill in the three connection fields at the top of the window.
   - **Host**: `dev5-mer-db:1521/TCTN_MASTER`
   - **User**: `cisconvert`
   - **Password**: `cisconvert`
3. **Procedure Body**: Paste the full Oracle `PROCEDURE` into the large text area.
4. **Analyze**: Click the "Analyze & Optimize" button and wait for the results.
