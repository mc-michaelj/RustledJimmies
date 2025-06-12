using System.Data;
using System.Text;

namespace OracleOptimizer.Core;

public static class DataTableComparator
{
    public static bool AreIdentical(DataTable dt1, DataTable dt2, out string details)
    {
        details = string.Empty;
        StringBuilder sb = new StringBuilder();

        // 1. Check for nulls.
        if (dt1 == null && dt2 == null)
        {
            details = "Validation successful (both tables are null).";
            return true;
        }
        if (dt1 == null)
        {
            details = "Validation failed: Original data is null, but new data is not.";
            return false;
        }
        if (dt2 == null)
        {
            details = "Validation failed: New data is null, but original data is not.";
            return false;
        }

        // 2. Check column count.
        if (dt1.Columns.Count != dt2.Columns.Count)
        {
            sb.AppendLine($"Validation failed: Column count differs. Original: {dt1.Columns.Count}, New: {dt2.Columns.Count}.");
            // Optional: List column names if significantly different or for more detail
            details = sb.ToString();
            return false;
        }

        // Optional: Check column names and types (for stricter comparison)
        for (int i = 0; i < dt1.Columns.Count; i++)
        {
            if (dt1.Columns[i].ColumnName != dt2.Columns[i].ColumnName)
            {
                sb.AppendLine($"Validation failed: Column name mismatch at index {i}. Original: '{dt1.Columns[i].ColumnName}', New: '{dt2.Columns[i].ColumnName}'.");
                details = sb.ToString();
                return false;
            }
            // Basic type check (can be expanded)
            if (dt1.Columns[i].DataType != dt2.Columns[i].DataType)
            {
                 sb.AppendLine($"Validation failed: Column data type mismatch for column '{dt1.Columns[i].ColumnName}'. Original: {dt1.Columns[i].DataType}, New: {dt2.Columns[i].DataType}.");
                 details = sb.ToString();
                 return false;
            }
        }


        // 3. Check row count.
        if (dt1.Rows.Count != dt2.Rows.Count)
        {
            sb.AppendLine($"Validation failed: Row count differs. Original: {dt1.Rows.Count}, New: {dt2.Rows.Count}.");
            details = sb.ToString();
            return false;
        }

        // 4. Iterate through each row and each column, comparing the values.
        for (int i = 0; i < dt1.Rows.Count; i++)
        {
            for (int j = 0; j < dt1.Columns.Count; j++)
            {
                object value1 = dt1.Rows[i][j];
                object value2 = dt2.Rows[i][j];

                // Handle DBNull values
                string strValue1 = (value1 == DBNull.Value || value1 == null) ? "[NULL]" : value1.ToString();
                string strValue2 = (value2 == DBNull.Value || value2 == null) ? "[NULL]" : value2.ToString();

                if (strValue1 != strValue2)
                {
                    sb.AppendLine($"Validation failed: Mismatch at Row {i + 1}, Column '{dt1.Columns[j].ColumnName}' (Index {j}).");
                    sb.AppendLine($"  Original Value: '{strValue1}'");
                    sb.AppendLine($"  New Value     : '{strValue2}'");
                    details = sb.ToString();
                    return false;
                }
            }
        }

        // 5. If all checks pass, set 'details' and return true.
        details = "Validation successful: Row counts, column counts, column names, column types, and all cell values are identical.";
        return true;
    }
}
