using System.Data;
using System.Text;

namespace OracleOptimizer.Services
{
    /// <summary>
    /// Provides utility methods to compare two DataTable objects.
    /// </summary>
    public static class DataTableComparator
    {
        /// <summary>
        /// Compares two DataTable objects to determine if they are identical in structure and data.
        /// Checks for differences in row counts, column counts, column names, and cell values.
        /// </summary>
        /// <param name="dt1">The first DataTable to compare.</param>
        /// <param name="dt2">The second DataTable to compare.</param>
        /// <param name="details">An output string that provides details about any mismatches found.
        /// If the DataTables are identical, it will state "Data is identical."</param>
        /// <returns>True if the DataTables are identical in structure and content; otherwise, false.</returns>
        public static bool AreIdentical(DataTable dt1, DataTable dt2, out string details)
        {
            StringBuilder sb = new StringBuilder();
            bool identical = true;

            // Check 1: Row count
            if (dt1.Rows.Count != dt2.Rows.Count)
            {
                sb.AppendLine($"Row count mismatch: Before={dt1.Rows.Count}, After={dt2.Rows.Count}");
                identical = false;
            }

            // Check 2: Column count
            if (dt1.Columns.Count != dt2.Columns.Count)
            {
                sb.AppendLine($"Column count mismatch: Before={dt1.Columns.Count}, After={dt2.Columns.Count}");
                identical = false;
            }

            // If basic structure (row/column counts) is different, no need to check further details like column names or cell values.
            if (!identical)
            {
                details = sb.ToString();
                return false;
            }

            // Check 3: Column names (and implicitly, order).
            // Assuming column data types should also match if names do, but could be an explicit check if needed.
            for (int i = 0; i < dt1.Columns.Count; i++)
            {
                if (dt1.Columns[i].ColumnName != dt2.Columns[i].ColumnName)
                {
                    sb.AppendLine($"Column name mismatch at index {i}: Before='{dt1.Columns[i].ColumnName}', After='{dt2.Columns[i].ColumnName}'");
                    identical = false;
                }
                // Optionally, check dt1.Columns[i].DataType != dt2.Columns[i].DataType if strict type matching is required.
            }

            // If column names differ, further cell-by-cell comparison might be misleading or less useful.
            if (!identical)
            {
                details = sb.ToString();
                return false;
            }

            // Check 4: Cell values
            // Iterate through each row and then each cell in that row.
            for (int i = 0; i < dt1.Rows.Count; i++)
            {
                for (int j = 0; j < dt1.Columns.Count; j++)
                {
                    object val1 = dt1.Rows[i][j];
                    object val2 = dt2.Rows[i][j];

                    // Note: In DataTableComparator.cs, add a comment to address potential precision issues
                    // when comparing FLOAT or DOUBLE PRECISION Oracle types (mapping to C# float/double).
                    // Suggest the need for epsilon comparison if such types are common and require precise comparison.
                    if (!Equals(val1, val2))
                    {
                        sb.AppendLine($"Data mismatch at Row {i}, Column '{dt1.Columns[j].ColumnName}': Before='{val1}', After='{val2}'");
                        identical = false;
                    }
                }
            }

            details = identical ? "Data is identical." : sb.ToString();
            return identical;
        }
    }
}